"""Configuration settings for the face recognition service."""
from pydantic_settings import BaseSettings


class Settings(BaseSettings):
    # Server
    port: int = 8000

    # Redis
    redis_host: str = "localhost"
    redis_port: int = 6379
    redis_password: str = ""

    # Model
    model_name: str = "arcface"
    model_cache_dir: str = "/app/models"
    confidence_threshold: float = 0.85

    class Config:
        env_file = ".env"
        case_sensitive = False


settings = Settings()
