"""Health check route."""
from fastapi import APIRouter
from pydantic import BaseModel

from services.cache_service import CacheService
from services.model_loader import ModelLoader

router = APIRouter()


class HealthResponse(BaseModel):
    status: str
    model_loaded: bool
    cache_connected: bool


@router.get("/health", response_model=HealthResponse)
async def health_check() -> HealthResponse:
    """Return service health status."""
    return HealthResponse(
        status="healthy",
        model_loaded=ModelLoader.is_loaded(),
        cache_connected=await CacheService.is_connected(),
    )
