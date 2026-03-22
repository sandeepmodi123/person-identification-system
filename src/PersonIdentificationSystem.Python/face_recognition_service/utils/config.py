"""Configuration settings for the face recognition service."""
import os
from pydantic_settings import BaseSettings


class Settings(BaseSettings):
    # Server
    port: int = 8000

    # Redis
    redis_host: str = "localhost"
    redis_port: int = 6379
    redis_password: str = ""

    # Model
    model_name: str = "buffalo_l"
    model_cache_dir: str = os.path.join(os.path.expanduser("~"), ".insightface", "models")
    confidence_threshold: float = 0.10

    class Config:
        env_file = ".env"
        case_sensitive = False


settings = Settings()
