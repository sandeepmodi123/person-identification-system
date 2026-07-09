"""Face detector - detects, scores, and crops faces from video frames.

Uses Ultralytics YOLOv8-face (yolov8n-face.pt) for accurate CNN face detection.
The model file is auto-downloaded on first run if not already present.

Local detection picks the best face crop per frame; the actual recognition
happens downstream in CompreFace via the .NET API.
"""
import base64
import os
import urllib.request
from dataclasses import dataclass
from typing import List, Optional

try:
    from .logger import get_logger
except ImportError:
    from logger import get_logger

logger = get_logger(__name__)


# YOLOv8-face weights (lindevs/yolov8-face GitHub release).
YOLO_FACE_MODEL_URL = (
    "https://github.com/lindevs/yolov8-face/releases/latest/download/"
    "yolov8n-face-lindevs.pt"
)
YOLO_FACE_MODEL_FILENAME = "yolov8n-face.pt"


@dataclass
class FrameScore:
    """Quality metrics for a video frame that contains at least one face."""
    face_count: int
    largest_face_area: int       # in pixels (w*h)
    sharpness: float             # Laplacian variance of the face crop (higher = sharper)
    full_frame_b64: str          # full original frame, base64 JPEG
    face_only_b64: str           # padded crop of the largest face, base64 JPEG
    score: float                 # composite score (larger = better)

    # Backwards-compat alias used by older callers.
    @property
    def face_crop_b64(self) -> str:
        return self.face_only_b64


def _ensure_yolo_face_model() -> str:
    """Make sure the YOLOv8 face model is available on disk. Returns its path."""
    here = os.path.dirname(os.path.abspath(__file__))
    # Look for the file next to the script first, then in models/.
    for candidate in (
        os.path.join(here, YOLO_FACE_MODEL_FILENAME),
        os.path.join(here, "models", YOLO_FACE_MODEL_FILENAME),
    ):
        if os.path.exists(candidate) and os.path.getsize(candidate) > 1_000_000:
            return candidate

    model_dir = os.path.join(here, "models")
    os.makedirs(model_dir, exist_ok=True)
    model_path = os.path.join(model_dir, YOLO_FACE_MODEL_FILENAME)
    logger.info("Downloading YOLOv8 face model to %s ...", model_path)
    urllib.request.urlretrieve(YOLO_FACE_MODEL_URL, model_path)
    logger.info("YOLOv8 face model downloaded (%d bytes).",
                os.path.getsize(model_path))
    return model_path


class FaceDetector:
    """YOLOv8-face CNN detector with quality scoring.

    Defaults below mirror the tuned values from the operator's reference
    script so behavior here matches that snippet 1:1.
    """

    # Minimum face bounding-box edge in pixels.
    MIN_FACE_SIZE = 100
    # Padding around the detected face when cropping (fraction of each side).
    PADDING_RATIO = 0.50
    # Reject crops blurrier than this (Laplacian variance).
    BLUR_THRESHOLD = 60.0
    # YOLO detection confidence threshold.
    YOLO_CONF = 0.6
    # JPEG quality for saved / dispatched face crops (0-100).
    JPEG_QUALITY = 98

    def __init__(self):
        self._model = None
        self._load_model()

    def _load_model(self):
        try:
            from ultralytics import YOLO
        except ImportError:
            logger.error(
                "ultralytics is not installed - face detection disabled. "
                "Run: pip install ultralytics"
            )
            self._model = None
            return

        try:
            import cv2
            try:
                cv2.utils.logging.setLogLevel(cv2.utils.logging.LOG_LEVEL_SILENT)
            except Exception:
                pass
        except ImportError:
            logger.error("OpenCV not available - face detection disabled.")
            self._model = None
            return

        try:
            model_path = _ensure_yolo_face_model()
        except Exception as e:
            logger.error("Could not obtain YOLOv8 face model: %s", e)
            self._model = None
            return

        try:
            self._model = YOLO(model_path)
            logger.info("YOLOv8 face detector loaded (conf>=%.2f).", self.YOLO_CONF)
        except Exception as e:
            logger.error("Failed to load YOLOv8 face model: %s", e)
            self._model = None

    # ------------------------------------------------------------------ public

    def detect(self, frame_b64: str) -> List[str]:
        """Legacy helper: return list of base64-encoded face crops (one per face)."""
        s = self.score(frame_b64)
        return [s.face_only_b64] if s else []

    def score(self, frame_b64: str) -> Optional[FrameScore]:
        """Detect faces with YOLOv8 and return quality metrics + a padded crop.

        Returns None if no face passes size and sharpness gates.
        """
        if self._model is None:
            return None

        try:
            import cv2
            import numpy as np

            img_bytes = base64.b64decode(frame_b64)
            nparr = np.frombuffer(img_bytes, np.uint8)
            frame = cv2.imdecode(nparr, cv2.IMREAD_COLOR)
            if frame is None:
                return None

            h_frame, w_frame = frame.shape[:2]

            results = self._model(frame, conf=self.YOLO_CONF, verbose=False)
            if not results:
                return None

            # Collect every face box from every result, keep the largest one
            # that also passes size + sharpness gates.
            best = None  # (area, x1, y1, x2, y2, sharpness, conf)
            face_count = 0
            for result in results:
                boxes = getattr(result, "boxes", None)
                if boxes is None:
                    continue
                for box in boxes:
                    face_count += 1
                    x1, y1, x2, y2 = (int(v) for v in box.xyxy[0].tolist())
                    box_w = x2 - x1
                    box_h = y2 - y1
                    if box_w < self.MIN_FACE_SIZE or box_h < self.MIN_FACE_SIZE:
                        continue

                    pad_x = int(box_w * self.PADDING_RATIO)
                    pad_y = int(box_h * self.PADDING_RATIO)
                    px1 = max(0, x1 - pad_x)
                    py1 = max(0, y1 - pad_y)
                    px2 = min(w_frame, x2 + pad_x)
                    py2 = min(h_frame, y2 + pad_y)

                    cropped = frame[py1:py2, px1:px2]
                    if cropped.size == 0:
                        continue

                    gray = cv2.cvtColor(cropped, cv2.COLOR_BGR2GRAY)
                    sharpness = float(cv2.Laplacian(gray, cv2.CV_64F).var())
                    if sharpness < self.BLUR_THRESHOLD:
                        continue

                    area = box_w * box_h
                    conf = float(box.conf[0].item()) if hasattr(box, "conf") else self.YOLO_CONF
                    if best is None or area > best[0]:
                        best = (area, px1, py1, px2, py2, sharpness, conf)

            if best is None:
                return None

            area, px1, py1, px2, py2, sharpness, conf = best
            face_crop = frame[py1:py2, px1:px2]

            # Encode the padded face crop.
            _, fbuf = cv2.imencode(".jpg", face_crop,
                                   [int(cv2.IMWRITE_JPEG_QUALITY), self.JPEG_QUALITY])
            face_only_b64 = base64.b64encode(fbuf.tobytes()).decode("utf-8")

            # Encode the full frame too (kept available for callers that need it).
            _, buf = cv2.imencode(".jpg", frame,
                                  [int(cv2.IMWRITE_JPEG_QUALITY), self.JPEG_QUALITY])
            full_frame_b64 = base64.b64encode(buf.tobytes()).decode("utf-8")

            composite = (area ** 0.5) * (sharpness ** 0.5) * conf

            return FrameScore(
                face_count=face_count,
                largest_face_area=int(area),
                sharpness=sharpness,
                full_frame_b64=full_frame_b64,
                face_only_b64=face_only_b64,
                score=composite,
            )

        except Exception as e:
            logger.error("Face detection/scoring error: %s", e)
            return None
