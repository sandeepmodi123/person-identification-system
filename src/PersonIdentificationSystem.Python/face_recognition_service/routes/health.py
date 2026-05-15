"""Health check route."""
from fastapi import APIRouter
from pydantic import BaseModel

from services.compreface_client import CompreFaceClient

router = APIRouter()


class HealthResponse(BaseModel):
    status: str
    compreface_reachable: bool


@router.get("/health", response_model=HealthResponse)
async def health_check() -> HealthResponse:
    """Return service health status."""
    reachable = await CompreFaceClient.is_reachable()
    return HealthResponse(
        status="healthy" if reachable else "degraded",
        compreface_reachable=reachable,
    )
