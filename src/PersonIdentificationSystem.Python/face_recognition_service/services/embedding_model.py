"""Face embedding generation service."""
import io
from typing import Optional

import numpy as np

from services.model_loader import ModelLoader
from utils.logger import get_logger

logger = get_logger(__name__)


class EmbeddingModel:
    """Generates face embeddings from raw image bytes using the loaded model."""

    @staticmethod
    def generate(image_bytes: bytes) -> np.ndarray:
        """
        Generate a normalized face embedding from image bytes.

        Args:
            image_bytes: Raw image bytes (JPEG/PNG).

        Returns:
            1-D numpy array (embedding vector).

        Raises:
            ValueError: If no face is detected in the image.
        """
        model = ModelLoader.get_model()

        if model is None:
            # Stub mode: return random normalized vector for testing
            logger.warning("InsightFace not available - using random embedding (test mode).")
            vec = np.random.randn(512).astype(np.float32)
            vec /= np.linalg.norm(vec)
            return vec

        try:
            import cv2
            nparr = np.frombuffer(image_bytes, np.uint8)
            img = cv2.imdecode(nparr, cv2.IMREAD_COLOR)

            if img is None:
                raise ValueError("Could not decode image bytes.")

            faces = model.get(img)
            if not faces:
                raise ValueError("No face detected in image.")

            # Use first detected face embedding
            embedding = faces[0].normed_embedding
            return embedding.astype(np.float32)

        except ImportError:
            logger.warning("OpenCV not available - using random embedding (test mode).")
            vec = np.random.randn(512).astype(np.float32)
            vec /= np.linalg.norm(vec)
            return vec
