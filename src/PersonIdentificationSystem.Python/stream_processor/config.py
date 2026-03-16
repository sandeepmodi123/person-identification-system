"""Stream processor configuration."""
import os
from dataclasses import dataclass


@dataclass
class StreamConfig:
    api_url: str = os.getenv("DOTNET_API_URL", "http://localhost:5000")
    face_recognition_url: str = os.getenv("FACE_RECOGNITION_URL", "http://localhost:8000")
    frame_interval_seconds: int = int(os.getenv("FRAME_INTERVAL_SECONDS", "5"))
    max_concurrent_streams: int = int(os.getenv("MAX_CONCURRENT_STREAMS", "10"))
