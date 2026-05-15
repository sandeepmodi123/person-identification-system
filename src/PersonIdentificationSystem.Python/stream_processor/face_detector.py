"""Face detector - detects, scores, and crops faces from video frames.

Uses OpenCV Haar Cascade for fast local detection. Local detection is only a
gate; the actual recognition happens in CompreFace. Scoring lets the stream
processor pick the single best frame per 1-second window before paying the
network/CPU cost of a CompreFace call.
"""
import base64
from dataclasses import dataclass
from typing import List, Optional

from logger import get_logger

logger = get_logger(__name__)


@dataclass
class FrameScore:
    """Quality metrics for a video frame that contains at least one face."""
    face_count: int
    largest_face_area: int       # in pixels (w*h)
    sharpness: float             # Laplacian variance of the face crop (higher = sharper)
    full_frame_b64: str          # full original frame, base64 JPEG - sent to CompreFace
    score: float                 # composite score (larger = better)

    # Backwards-compat alias used by older callers.
    @property
    def face_crop_b64(self) -> str:
        return self.full_frame_b64


class FaceDetector:
    """Detects faces in a base64-encoded image and returns cropped face images."""

    # Minimum face bounding-box edge in pixels. Below this Haar produces many
    # false positives at this camera distance.
    MIN_FACE_SIZE = 60
    # Padding around the detected face when cropping (fraction of max(w,h)).
    CROP_PADDING = 0.45
    # Minimum Laplacian-variance sharpness for a candidate to be considered.
    # Anything below this is almost certainly a Haar false positive on a blurry patch.
    MIN_SHARPNESS = 80.0
    # Upscale crops so the face is large enough for CompreFace's detector.
    TARGET_CROP_MIN_EDGE = 320

    def __init__(self):
        self._detector = None
        self._load_model()

    def _load_model(self):
        try:
            import cv2
            try:
                # Silence OpenCV's own log channel (separate from ffmpeg).
                cv2.utils.logging.setLogLevel(cv2.utils.logging.LOG_LEVEL_SILENT)
            except Exception:
                pass
            self._detector = cv2.CascadeClassifier(
                cv2.data.haarcascades + "haarcascade_frontalface_default.xml"
            )
            logger.info("OpenCV Haar Cascade face detector loaded.")
        except ImportError:
            logger.warning("OpenCV not available - face detection disabled.")
            self._detector = None

    # ------------------------------------------------------------------ public

    def detect(self, frame_b64: str) -> List[str]:
        """Legacy helper: return list of base64-encoded face crops (one per face)."""
        s = self.score(frame_b64)
        return [s.face_crop_b64] if s else []

    def score(self, frame_b64: str) -> Optional[FrameScore]:
        """Detect faces and return quality metrics + a padded crop of the largest face.

        Returns None if no face meeting the minimum size is detected.
        """
        if self._detector is None:
            return None

        try:
            import cv2
            import numpy as np

            img_bytes = base64.b64decode(frame_b64)
            nparr = np.frombuffer(img_bytes, np.uint8)
            frame = cv2.imdecode(nparr, cv2.IMREAD_COLOR)
            if frame is None:
                return None

            gray = cv2.cvtColor(frame, cv2.COLOR_BGR2GRAY)
            faces = self._detector.detectMultiScale(
                gray,
                scaleFactor=1.1,
                minNeighbors=5,
                minSize=(self.MIN_FACE_SIZE, self.MIN_FACE_SIZE),
            )
            if len(faces) == 0:
                return None

            # Pick the LARGEST face in the frame as the primary subject.
            x, y, w, h = max(faces, key=lambda f: f[2] * f[3])
            area = int(w * h)

            # Sharpness via variance of Laplacian on the grayscale face region only.
            face_gray = gray[y:y + h, x:x + w]
            sharpness = float(cv2.Laplacian(face_gray, cv2.CV_64F).var())

            # Reject obvious Haar false positives (blurry patches that look face-like).
            if sharpness < self.MIN_SHARPNESS:
                return None

            # Send the FULL frame to CompreFace. Its CNN detector is far better
            # than Haar at finding the face and produces stronger embeddings on
            # uncropped, un-resampled imagery than on a tiny upscaled crop.
            _, buf = cv2.imencode(".jpg", frame, [cv2.IMWRITE_JPEG_QUALITY, 90])
            full_frame_b64 = base64.b64encode(buf.tobytes()).decode("utf-8")

            # Composite score: large + sharp wins. sqrt() keeps the units sane.
            composite = (area ** 0.5) * (sharpness ** 0.5)

            return FrameScore(
                face_count=len(faces),
                largest_face_area=area,
                sharpness=sharpness,
                full_frame_b64=full_frame_b64,
                score=composite,
            )

        except Exception as e:
            logger.error("Face detection/scoring error: %s", e)
            return None
