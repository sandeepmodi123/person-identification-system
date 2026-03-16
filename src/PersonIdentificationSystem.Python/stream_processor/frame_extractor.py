"""Frame extractor - reads frames from RTSP stream at configurable intervals."""
import asyncio
import base64
import time
from datetime import datetime, timezone
from typing import AsyncGenerator, Tuple

from exceptions import StreamConnectionError
from logger import get_logger

logger = get_logger(__name__)


class FrameExtractor:
    """Connects to an RTSP stream and yields base64-encoded frames."""

    def __init__(self, rtsp_url: str, interval_seconds: int = 5):
        self.rtsp_url = rtsp_url
        self.interval_seconds = interval_seconds
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

    async def extract_frames(self) -> AsyncGenerator[Tuple[str, str], None]:
        """
        Asynchronously yield (frame_base64, iso_timestamp) tuples at the
        configured interval.
        """
        self._open()

        try:
            import cv2
            while True:
                ret, frame = self._cap.read()
                if not ret:
                    logger.warning("Stream %s returned no frame; retrying in 5s.", self.rtsp_url)
                    await asyncio.sleep(5)
                    # Try to reconnect
                    self.release()
                    self._open()
                    continue

                # Encode frame as JPEG, then base64
                _, buffer = cv2.imencode(".jpg", frame, [cv2.IMWRITE_JPEG_QUALITY, 85])
                frame_b64 = base64.b64encode(buffer.tobytes()).decode("utf-8")
                captured_at = datetime.now(timezone.utc).isoformat()

                yield frame_b64, captured_at

                await asyncio.sleep(self.interval_seconds)

        except Exception as e:
            raise StreamConnectionError(f"Stream error: {e}") from e
