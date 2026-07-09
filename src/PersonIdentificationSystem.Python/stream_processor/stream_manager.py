"""
Stream Manager - manages multiple concurrent RTSP connections.
Fetches active streams from the .NET API, processes frames, and
dispatches face matches back to the API.
"""
import asyncio
import logging
import signal
import sys
from typing import Dict

import httpx

from config import StreamConfig
from exceptions import StreamConnectionError
from frame_extractor import FrameExtractor
from face_detector import FaceDetector
from logger import get_logger

logger = get_logger(__name__)


class StreamManager:
    """Manages all active RTSP stream processing tasks."""

    def __init__(self, config: StreamConfig):
        self.config = config
        self._tasks: Dict[str, asyncio.Task] = {}
        self._running = False
        self._dispatch_semaphore = asyncio.Semaphore(config.max_concurrent_streams)

    async def start(self) -> None:
        """Start stream manager: load streams from API and begin processing."""
        self._running = True
        logger.info("Stream manager starting...")

        while self._running:
            try:
                await self._sync_streams()
            except Exception as e:
                logger.error("Error syncing streams: %s", e)

            await asyncio.sleep(30)  # Re-sync every 30 seconds

    async def stop(self) -> None:
        """Gracefully stop all stream tasks."""
        self._running = False
        for stream_id, task in self._tasks.items():
            task.cancel()
            logger.info("Cancelled stream task: %s", stream_id)
        if self._tasks:
            await asyncio.gather(*self._tasks.values(), return_exceptions=True)
        self._tasks.clear()
        logger.info("Stream manager stopped.")

    async def _sync_streams(self) -> None:
        """Fetch active streams from API and start/stop tasks as needed."""
        async with httpx.AsyncClient(base_url=self.config.api_url, timeout=10) as client:
            response = await client.get("/api/rtsp-streams")
            response.raise_for_status()
            streams = response.json()

        active_ids = set()
        for stream in streams:
            if not stream.get("isActive", False):
                continue
            stream_id = stream["id"]
            active_ids.add(stream_id)

            if stream_id not in self._tasks or self._tasks[stream_id].done():
                logger.info("Starting stream task: %s - %s", stream_id, stream["cameraName"])
                task = asyncio.create_task(
                    self._process_stream(stream_id, stream["rtspUrl"], stream["frameIntervalSeconds"])
                )
                self._tasks[stream_id] = task

        # Cancel streams that are no longer active
        for stream_id in list(self._tasks.keys()):
            if stream_id not in active_ids:
                self._tasks[stream_id].cancel()
                del self._tasks[stream_id]
                logger.info("Stopped stream task: %s", stream_id)

    async def _process_stream(self, stream_id: str, rtsp_url: str, interval_seconds: int) -> None:
        """Process a single RTSP stream: extract frames and detect faces."""
        extractor = FrameExtractor(rtsp_url, interval_seconds, stream_id=stream_id)
        detector = FaceDetector()
        processing_task: asyncio.Task | None = None

        async def process_detection_frame(frame_b64: str, captured_at: str) -> None:
            """Run detection+dispatch off the frame-read hot path.

            Keep only one in-flight detection task per stream. This preserves
            smooth MJPEG updates when matching/API work is slower than capture.
            """
            try:
                loop = asyncio.get_running_loop()
                faces = await loop.run_in_executor(None, detector.detect, frame_b64)
                if not faces:
                    return

                for face_b64 in faces:
                    await self._dispatch_frame(stream_id, face_b64, captured_at)

            except Exception as e:
                logger.error("Frame processing error for stream %s: %s", stream_id, e)

        try:
            async for frame_b64, captured_at in extractor.extract_frames():
                if processing_task is None or processing_task.done():
                    processing_task = asyncio.create_task(
                        process_detection_frame(frame_b64, captured_at)
                    )

        except StreamConnectionError as e:
            logger.error("Stream %s connection failed: %s", stream_id, e)
        except asyncio.CancelledError:
            logger.info("Stream %s task cancelled.", stream_id)
        finally:
            if processing_task and not processing_task.done():
                processing_task.cancel()
                await asyncio.gather(processing_task, return_exceptions=True)
            extractor.release()

    async def _dispatch_frame(self, stream_id: str, face_b64: str, captured_at: str) -> None:
        """Send a face crop to the .NET API for matching."""
        async with self._dispatch_semaphore:
            async with httpx.AsyncClient(base_url=self.config.api_url, timeout=30) as client:
                payload = {
                    "streamId": stream_id,
                    "frameBase64": face_b64,
                    "capturedAt": captured_at,
                }
                response = await client.post("/api/matching/process-frame", json=payload)
                if response.status_code == 200:
                    result = response.json()
                    if result.get("matchFound"):
                        logger.warning(
                            "MATCH: person=%s confidence=%.2f stream=%s",
                            result.get("personName"),
                            result.get("confidenceScore", 0),
                            stream_id,
                        )


async def main():
    config = StreamConfig()
    manager = StreamManager(config)

    loop = asyncio.get_running_loop()

    def shutdown():
        asyncio.create_task(manager.stop())

    for sig in (signal.SIGTERM, signal.SIGINT):
        loop.add_signal_handler(sig, shutdown)

    await manager.start()


if __name__ == "__main__":
    asyncio.run(main())
