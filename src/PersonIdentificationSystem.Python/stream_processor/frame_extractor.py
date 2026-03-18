"""Frame extractor - reads frames from RTSP stream at configurable intervals."""
import asyncio
import base64
import time
from datetime import datetime, timezone
from typing import AsyncGenerator, Tuple

from exceptions import StreamConnectionError
from logger import get_logger
import mjpeg_server

logger = get_logger(__name__)


class FrameExtractor:
    """Connects to an RTSP stream and yields base64-encoded frames."""

    def __init__(self, rtsp_url: str, interval_seconds: int = 5, stream_id: str = ""):
        self.rtsp_url = rtsp_url
        self.interval_seconds = interval_seconds
        self.stream_id = stream_id
        self._cap = None

    def _open(self):
        try:
            import cv2
            cap = cv2.VideoCapture(self.rtsp_url)
            if not cap.isOpened():
                raise StreamConnectionError(f"Cannot open RTSP stream: {self.rtsp_url}")
            self._cap = cap
        except ImportError:
            raise StreamConnectionError("OpenCV not installed.")

    def release(self):
        if self._cap is not None:
            self._cap.release()
            self._cap = None
        if self.stream_id:
            mjpeg_server.remove_stream(self.stream_id)

    async def extract_frames(self) -> AsyncGenerator[Tuple[str, str], None]:
        """
        Asynchronously yield (frame_base64, iso_timestamp) tuples at the
        configured interval. Reads frames continuously (~10fps) for smooth
        MJPEG streaming, but only yields for face detection at the configured
        interval.
        """
        self._open()

        try:
            import cv2
            last_yield = 0.0
            read_interval = 0.1  # ~10fps for MJPEG smoothness

            while True:
                ret, frame = self._cap.read()
                if not ret:
                    logger.warning("Stream %s returned no frame; retrying in 5s.", self.rtsp_url)
                    await asyncio.sleep(5)
                    self.release()
                    self._open()
                    continue

                # Always publish to MJPEG server for smooth live viewing
                if self.stream_id:
                    await mjpeg_server.set_frame(self.stream_id, frame)

                # Only yield for face detection at the configured interval
                now = time.monotonic()
                if now - last_yield >= self.interval_seconds:
                    last_yield = now
                    _, buffer = cv2.imencode(".jpg", frame, [cv2.IMWRITE_JPEG_QUALITY, 85])
                    frame_b64 = base64.b64encode(buffer.tobytes()).decode("utf-8")
                    captured_at = datetime.now(timezone.utc).isoformat()
                    yield frame_b64, captured_at

                await asyncio.sleep(read_interval)

        except Exception as e:
            raise StreamConnectionError(f"Stream error: {e}") from e
