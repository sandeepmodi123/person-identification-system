"""
RTSP Stream Processor Service
Processes video streams from RTSP cameras and detects faces
"""

import logging
import os
import asyncio
from datetime import datetime
from typing import Dict, Optional
import requests
import urllib3
urllib3.disable_warnings(urllib3.exceptions.InsecureRequestWarning)
import httpx
import aio_pika
from dotenv import load_dotenv

from frame_extractor import FrameExtractor
from face_detector import FaceDetector
from exceptions import StreamConnectionError
from mjpeg_server import start_mjpeg_server

# Load environment variables
load_dotenv()

# Setup logging
logging.basicConfig(
    level=logging.INFO,
    format='%(asctime)s - %(name)s - %(levelname)s - %(message)s'
)
logger = logging.getLogger(__name__)

# Configuration
API_BASE_URL = os.getenv('API_BASE_URL', 'https://localhost:5001/api')
FACE_SERVICE_URL = os.getenv('FACE_SERVICE_URL', 'http://localhost:8000')
RABBITMQ_URL = os.getenv('RABBITMQ_URL', 'amqp://admin:RabbitMQPassword123@localhost:5672/')
LOG_LEVEL = os.getenv('LOG_LEVEL', 'INFO')
FRAME_INTERVAL = int(os.getenv('FRAME_INTERVAL', '5'))
MAX_WORKERS = int(os.getenv('MAX_WORKERS', '4'))


class StreamProcessor:
    """Main Stream Processor Service"""
    
    def __init__(self):
        self.api_url = API_BASE_URL
        self.face_service_url = FACE_SERVICE_URL
        self.rabbitmq_url = RABBITMQ_URL
        self.connection = None
        self.channel = None
        self.exchange = None
        self._stream_tasks: Dict[str, asyncio.Task] = {}
        
    async def connect_rabbitmq(self):
        """Connect to RabbitMQ"""
        try:
            logger.info("Connecting to RabbitMQ...")
            self.connection = await aio_pika.connect_robust(self.rabbitmq_url)
            self.channel = await self.connection.channel()
            
            # Declare exchange
            self.exchange = await self.channel.declare_exchange(
                'person_identification',
                aio_pika.ExchangeType.DIRECT,
                durable=True
            )
            
            logger.info("✅ Connected to RabbitMQ")
            return True
        except Exception as e:
            logger.error(f"❌ Failed to connect to RabbitMQ: {e}")
            return False
    
    async def get_active_streams(self):
        """Get list of active RTSP streams from API"""
        try:
            response = requests.get(
                f"{self.api_url}/rtsp-streams",
                timeout=5,
                verify=False
            )
            if response.status_code == 200:
                streams = response.json()
                active = [s for s in streams if s.get('isActive', False)]
                logger.info(f"Found {len(active)} active streams")
                return active
            else:
                logger.error(f"API error: {response.status_code}")
                return []
        except Exception as e:
            logger.error(f"Failed to get streams: {e}")
            return []
    
    async def process_stream(self, stream_id: str, rtsp_url: str, camera_location: str):
        """Process a single RTSP stream: extract frames, detect faces, dispatch to API."""
        logger.info(f"Starting stream processing: {stream_id} ({camera_location}) - {rtsp_url}")

        extractor = FrameExtractor(rtsp_url, FRAME_INTERVAL, stream_id=stream_id)
        detector = FaceDetector()

        try:
            # Publish stream status to RabbitMQ
            if self.exchange:
                message = aio_pika.Message(
                    body=f'{{"stream_id": "{stream_id}", "status": "active"}}'.encode(),
                    content_type='application/json'
                )
                await self.exchange.publish(message, routing_key='stream.status')

            async for frame_b64, captured_at in extractor.extract_frames():
                try:
                    faces = detector.detect(frame_b64)
                    if not faces:
                        continue

                    logger.info(f"Detected {len(faces)} face(s) in stream {stream_id}")

                    for face_b64 in faces:
                        await self._dispatch_face(stream_id, face_b64, captured_at)

                except Exception as e:
                    logger.error(f"Frame processing error for stream {stream_id}: {e}")

        except StreamConnectionError as e:
            logger.error(f"Stream {stream_id} connection failed: {e}")
        except asyncio.CancelledError:
            logger.info(f"Stream {stream_id} task cancelled.")
        except Exception as e:
            logger.error(f"Error processing stream {stream_id}: {e}")
        finally:
            extractor.release()
            logger.info(f"Stream {stream_id} processing stopped.")

    async def _dispatch_face(self, stream_id: str, face_b64: str, captured_at: str):
        """Send a detected face crop to the .NET API for matching."""
        async with httpx.AsyncClient(base_url=self.api_url, verify=False, timeout=30) as client:
            payload = {
                "streamId": stream_id,
                "frameBase64": face_b64,
                "capturedAt": captured_at,
            }
            response = await client.post("/matching/process-frame", json=payload)
            if response.status_code == 200:
                result = response.json()
                if result.get("matchFound"):
                    logger.warning(
                        f"MATCH FOUND: person={result.get('personName')} "
                        f"confidence={result.get('confidenceScore', 0):.2f} "
                        f"stream={stream_id}"
                    )
    
    async def monitor_streams(self):
        """Monitor active streams continuously, managing long-running tasks."""
        logger.info("Starting stream monitoring...")

        while True:
            try:
                streams = await self.get_active_streams()

                active_ids = set()
                for stream in streams:
                    sid = stream.get('id')
                    active_ids.add(sid)

                    # Only start a new task if one isn't already running
                    if sid not in self._stream_tasks or self._stream_tasks[sid].done():
                        task = asyncio.create_task(
                            self.process_stream(
                                sid,
                                stream.get('rtspUrl'),
                                stream.get('cameraLocation', 'Unknown')
                            )
                        )
                        self._stream_tasks[sid] = task

                # Cancel tasks for streams that are no longer active
                for sid in list(self._stream_tasks.keys()):
                    if sid not in active_ids:
                        self._stream_tasks[sid].cancel()
                        del self._stream_tasks[sid]
                        logger.info(f"Stopped stream task: {sid}")

                # Re-sync every 30 seconds
                await asyncio.sleep(30)

            except Exception as e:
                logger.error(f"Error in monitoring loop: {e}")
                await asyncio.sleep(5)
    
    async def start(self):
        """Start the Stream Processor service"""
        logger.info("=" * 60)
        logger.info("🎬 Stream Processor Service Starting")
        logger.info("=" * 60)
        logger.info(f"API URL: {self.api_url}")
        logger.info(f"Face Service: {self.face_service_url}")
        logger.info(f"RabbitMQ URL: {self.rabbitmq_url}")
        logger.info(f"Frame Interval: {FRAME_INTERVAL}s")
        logger.info(f"Max Workers: {MAX_WORKERS}")
        logger.info("=" * 60)
        
        # Start MJPEG server for live stream viewing
        asyncio.create_task(start_mjpeg_server())

        # Connect to RabbitMQ
        rabbitmq_ready = await self.connect_rabbitmq()
        if not rabbitmq_ready:
            logger.warning("⚠️  RabbitMQ not available, continuing without queue")
        
        # Start monitoring streams
        try:
            await self.monitor_streams()
        except KeyboardInterrupt:
            logger.info("Stream Processor shutting down...")
        finally:
            if self.connection:
                await self.connection.close()
            logger.info("Stream Processor stopped")


async def main():
    """Main entry point"""
    processor = StreamProcessor()
    await processor.start()


if __name__ == '__main__':
    logger.info("Stream Processor Service")
    logger.info(f"Started at {datetime.now()}")
    
    try:
        asyncio.run(main())
    except KeyboardInterrupt:
        logger.info("Interrupted by user")
    except Exception as e:
        logger.error(f"Fatal error: {e}", exc_info=True)
