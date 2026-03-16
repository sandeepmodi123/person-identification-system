"""Face detector - detects and crops faces from video frames."""
import base64
from typing import List

from logger import get_logger

logger = get_logger(__name__)


class FaceDetector:
    """Detects faces in a base64-encoded image and returns cropped face images."""

    def __init__(self):
        self._net = None
        self._load_model()

    def _load_model(self):
        """Load YOLO or OpenCV DNN face detector."""
        try:
            import cv2
            # Use OpenCV's built-in face detector as fallback
            self._detector = cv2.CascadeClassifier(
                cv2.data.haarcascades + "haarcascade_frontalface_default.xml"
            )
            logger.info("OpenCV Haar Cascade face detector loaded.")
        except ImportError:
            logger.warning("OpenCV not available - face detection disabled.")
            self._detector = None

    def detect(self, frame_b64: str) -> List[str]:
        """
        Detect faces in the frame and return list of base64-encoded face crops.

        Args:
            frame_b64: Base64-encoded JPEG frame.

        Returns:
            List of base64-encoded face crop images.
        """
        if self._detector is None:
            return []

        try:
            import cv2
            import numpy as np

            # Decode frame
            img_bytes = base64.b64decode(frame_b64)
            nparr = np.frombuffer(img_bytes, np.uint8)
            frame = cv2.imdecode(nparr, cv2.IMREAD_COLOR)

            if frame is None:
                return []

            gray = cv2.cvtColor(frame, cv2.COLOR_BGR2GRAY)
            faces = self._detector.detectMultiScale(gray, scaleFactor=1.1, minNeighbors=5, minSize=(80, 80))

            crops = []
            for (x, y, w, h) in faces:
                # Add padding
                pad = int(0.2 * max(w, h))
                x1 = max(0, x - pad)
                y1 = max(0, y - pad)
                x2 = min(frame.shape[1], x + w + pad)
                y2 = min(frame.shape[0], y + h + pad)

                face_crop = frame[y1:y2, x1:x2]
                _, buf = cv2.imencode(".jpg", face_crop)
                crops.append(base64.b64encode(buf.tobytes()).decode("utf-8"))

            return crops

        except Exception as e:
            logger.error("Face detection error: %s", e)
            return []
