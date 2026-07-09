"""Health check route."""
import os

from fastapi import APIRouter, Request
from pydantic import BaseModel

from face_recognition_service.services.compreface_client import CompreFaceClient

router = APIRouter()


class HealthResponse(BaseModel):
    status: str
    compreface_reachable: bool
    stream_runtime_enabled: bool
    stream_manager_running: bool
    mjpeg_server_running: bool
    active_stream_buffers: int


@router.get("/health", response_model=HealthResponse)
async def health_check(request: Request) -> HealthResponse:
    """Return merged service health (CompreFace + stream runtime)."""
    reachable = await CompreFaceClient.is_reachable()

    stream_enabled = os.getenv("STREAM_PROCESSOR_ENABLED", "true").lower() in (
        "1", "true", "yes", "on"
    )
    stream_manager_task = getattr(request.app.state, "stream_manager_task", None)

    stream_manager_running = bool(
        stream_manager_task is not None and not stream_manager_task.done()
    )
    mjpeg_server_running = False

    active_stream_buffers = 0
    try:
        from stream_processor.mjpeg_server import (
            get_active_stream_ids,
            is_mjpeg_server_running,
        )

        mjpeg_server_running = is_mjpeg_server_running()
        active_stream_buffers = len(get_active_stream_ids())
    except Exception:
        mjpeg_server_running = False
        active_stream_buffers = 0

    if stream_enabled:
        overall_healthy = reachable and stream_manager_running and mjpeg_server_running
    else:
        overall_healthy = reachable

    return HealthResponse(
        status="healthy" if overall_healthy else "degraded",
        compreface_reachable=reachable,
        stream_runtime_enabled=stream_enabled,
        stream_manager_running=stream_manager_running,
        mjpeg_server_running=mjpeg_server_running,
        active_stream_buffers=active_stream_buffers,
    )
