"""
RTSP Stream Processor Service
Processes video streams from RTSP cameras and detects faces
"""

import logging
import os
import sys

# Silence ffmpeg/hevc stderr spam BEFORE OpenCV/ffmpeg gets imported anywhere.
os.environ.setdefault("OPENCV_LOG_LEVEL", "SILENT")
os.environ.setdefault("OPENCV_FFMPEG_LOGLEVEL", "-8")
os.environ.setdefault("OPENCV_VIDEOIO_DEBUG", "0")


def _silence_native_stderr() -> None:
    """Route fd-level stderr (used by ffmpeg/libavcodec) to NUL.

    Python logging is reattached to a fresh stream so our own logs stay visible.
    Done at Python level (os.dup2) AND C-runtime level (freopen via ctypes) -
    on Windows, ffmpeg writes through the C runtime's cached stderr FILE*
    which doesn't follow os.dup2 by itself.
    """
    try:
        # Save original stderr so our logger keeps a real terminal.
        saved = os.dup(2)
        devnull_fd = os.open(os.devnull, os.O_WRONLY)
        os.dup2(devnull_fd, 2)
        os.close(devnull_fd)
        # Reattach Python's sys.stderr to the saved terminal handle.
        sys.stderr = os.fdopen(saved, "w", buffering=1)

        # C-runtime level: ffmpeg uses fprintf(stderr, ...) which uses the
        # C runtime's FILE* cached at startup. freopen makes it point at NUL.
        if sys.platform == "win32":
            import ctypes
            try:
                ucrt = ctypes.CDLL("ucrtbase.dll")
            except OSError:
                ucrt = ctypes.CDLL("msvcrt.dll")
            try:
                # __acrt_iob_func(2) returns FILE* for stderr in UCRT.
                ucrt.__acrt_iob_func.restype = ctypes.c_void_p
                stderr_file = ucrt.__acrt_iob_func(2)
                ucrt.freopen(b"NUL", b"w", ctypes.c_void_p(stderr_file))
            except Exception:
                pass
    except Exception:
        pass


_silence_native_stderr()

import asyncio
from datetime import datetime
from typing import Dict
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

# Setup logging - WARNING for noisy libs, INFO only for our own dispatch lines.
logging.basicConfig(
    level=logging.INFO,
    stream=sys.stdout,
    format='%(asctime)s | %(levelname)s | %(message)s'
)
logging.getLogger("httpx").setLevel(logging.WARNING)
logging.getLogger("httpcore").setLevel(logging.WARNING)
logging.getLogger("aio_pika").setLevel(logging.WARNING)
logging.getLogger("aiormq").setLevel(logging.WARNING)
logging.getLogger("aiohttp.access").setLevel(logging.WARNING)
logging.getLogger("frame_extractor").setLevel(logging.ERROR)
logging.getLogger("mjpeg_server").setLevel(logging.WARNING)
logging.getLogger("face_detector").setLevel(logging.WARNING)
logger = logging.getLogger(__name__)

# Configuration
API_BASE_URL = os.getenv('API_BASE_URL', 'https://localhost:5001/api')
FACE_SERVICE_URL = os.getenv('FACE_SERVICE_URL', 'http://localhost:8000')
RABBITMQ_URL = os.getenv('RABBITMQ_URL', 'amqp://admin:RabbitMQPassword123@localhost:5672/')
LOG_LEVEL = os.getenv('LOG_LEVEL', 'INFO')
# Process up to TARGET_FPS frames per second from each stream. Every frame that
# passes the local face gate (Haar + sharpness) is sent to CompreFace.
TARGET_FPS = float(os.getenv('TARGET_FPS', '4'))
FRAME_INTERVAL = float(os.getenv('FRAME_INTERVAL', str(1.0 / max(TARGET_FPS, 0.1))))
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
                logger.debug(f"Found {len(active)} active streams")
                return active
            else:
                logger.error(f"API error: {response.status_code}")
                return []
        except Exception as e:
            logger.error(f"Failed to get streams: {e}")
            return []
    
    async def process_stream(self, stream_id: str, rtsp_url: str, camera_location: str):
        """Process a single RTSP stream.

        Reads frames at TARGET_FPS, runs a fast local Haar detection plus a
        sharpness gate, and dispatches every usable frame's face crop to the
        .NET API (which forwards to CompreFace). Frames with no face or that
        are too blurry are skipped - CompreFace is not called for them.
        """
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
                    # Local face gate: skip frames with no face / too blurry.
                    score = detector.score(frame_b64)
                    if score is None:
                        continue

                    # Send the upscaled, padded face crop. Only this dispatch is logged.
                    await self._dispatch_face(
                        stream_id, score.face_crop_b64, captured_at
                    )

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
                        "MATCH: person=%s confidence=%.2f stream=%s",
                        result.get('personName'),
                        result.get('confidenceScore', 0),
                        stream_id,
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
        logger.info(f"Target FPS: {TARGET_FPS} (interval {FRAME_INTERVAL:.3f}s)")
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
