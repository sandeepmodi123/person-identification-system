"""Model loader - loads the face recognition model once at startup."""
from typing import Any, Optional

from utils.config import settings
from utils.logger import get_logger

logger = get_logger(__name__)


class ModelLoader:
    _model: Optional[Any] = None
    _loaded: bool = False

    @classmethod
    def load(cls) -> None:
        """Load the face recognition model into memory."""
        try:
            import insightface
            from insightface.app import FaceAnalysis

            model = FaceAnalysis(
                name=settings.model_name,
                root=settings.model_cache_dir,
                providers=["CPUExecutionProvider"],
            )
            model.prepare(ctx_id=0, det_size=(640, 640))
            cls._model = model
            cls._loaded = True
            logger.info("InsightFace model '%s' loaded.", settings.model_name)

        except ImportError:
            logger.warning(
                "InsightFace not installed. Falling back to stub model for testing."
            )
            cls._model = None
            cls._loaded = True  # Mark as loaded (stub mode)

    @classmethod
    def get_model(cls) -> Any:
        if not cls._loaded:
            raise RuntimeError("Model not loaded. Call ModelLoader.load() first.")
        return cls._model

    @classmethod
    def is_loaded(cls) -> bool:
        return cls._loaded
