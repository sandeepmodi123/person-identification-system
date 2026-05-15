"""Configuration settings for the face recognition service (CompreFace-backed)."""
import os
from pydantic_settings import BaseSettings


class Settings(BaseSettings):
    # Server
    port: int = 8000

    # CompreFace
    compreface_url: str = os.getenv("COMPREFACE_URL", "http://20.219.170.37:8000/")
    compreface_api_key: str = os.getenv(
        "COMPREFACE_API_KEY", "76e8fde7-a78a-46c2-a343-ad6e24bf9a9a"
    )

    # Matching
    # CompreFace similarity is 0..1. Real CCTV faces rarely exceed 0.85, so we use
    # 0.75 as the default operating point - tune via CONFIDENCE_THRESHOLD env var.
    confidence_threshold: float = float(os.getenv("CONFIDENCE_THRESHOLD", "0.75"))
    # Minimum face bounding box dimension (in pixels) to accept. Tiny faces are skipped.
    min_face_size_px: int = int(os.getenv("MIN_FACE_SIZE_PX", "40"))
    # Cooldown window in seconds for deduplicating repeat detections of the same person.
    dedup_cooldown_seconds: int = int(os.getenv("DEDUP_COOLDOWN_SECONDS", "30"))

    class Config:
        env_file = ".env"
        case_sensitive = False


settings = Settings()
