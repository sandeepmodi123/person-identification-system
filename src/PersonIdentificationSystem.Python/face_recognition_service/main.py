"""
Person Identification System - Face Recognition Service
FastAPI application that delegates face recognition to CompreFace.
"""
import asyncio
import os
import sys
import logging
from contextlib import asynccontextmanager

if __package__ in (None, ""):
    from pathlib import Path

    sys.path.insert(0, str(Path(__file__).resolve().parent.parent))

from fastapi import FastAPI
from fastapi.middleware.cors import CORSMiddleware

from face_recognition_service.routes import matching, health
from face_recognition_service.services.compreface_client import CompreFaceClient
from face_recognition_service.utils.config import settings
from face_recognition_service.utils.logger import get_logger

# Quiet noisy libraries - we only want our own CompreFace logs.
logging.getLogger("httpx").setLevel(logging.WARNING)
logging.getLogger("httpcore").setLevel(logging.WARNING)
logging.getLogger("uvicorn.access").setLevel(logging.WARNING)

logger = get_logger(__name__)


async def _start_stream_runtime(app: FastAPI) -> None:
    """Start stream manager + MJPEG server in the same process."""
    if os.getenv("STREAM_PROCESSOR_ENABLED", "true").lower() not in ("1", "true", "yes", "on"):
        logger.info("Stream processor runtime disabled by STREAM_PROCESSOR_ENABLED.")
        return

    try:
        from stream_processor.config import StreamConfig
        from stream_processor.stream_manager import StreamManager
        from stream_processor.mjpeg_server import start_mjpeg_server

        manager = StreamManager(StreamConfig())
        app.state.stream_manager = manager
        app.state.stream_manager_task = asyncio.create_task(manager.start())
        await start_mjpeg_server()
        logger.info("Merged runtime: stream manager and MJPEG server started.")
    except Exception as e:
        logger.exception("Failed to start merged stream runtime: %s", e)


async def _stop_stream_runtime(app: FastAPI) -> None:
    """Stop stream manager + MJPEG server background tasks."""
    manager = getattr(app.state, "stream_manager", None)
    manager_task = getattr(app.state, "stream_manager_task", None)
    if manager is not None:
        try:
            await manager.stop()
        except Exception as e:
            logger.warning("Error while stopping stream manager: %s", e)

    if manager_task is not None and not manager_task.done():
        manager_task.cancel()
        await asyncio.gather(manager_task, return_exceptions=True)

    try:
        from stream_processor.mjpeg_server import stop_mjpeg_server

        await stop_mjpeg_server()
    except Exception as e:
        logger.warning("Error while stopping MJPEG server: %s", e)

    logger.info("Merged runtime: stream manager and MJPEG server stopped.")


@asynccontextmanager
async def lifespan(app: FastAPI):
    """Application lifespan: init CompreFace client on startup, dispose on shutdown."""
    logger.info("Initializing CompreFace client: %s", settings.compreface_url)
    await CompreFaceClient.connect()

    reachable = await CompreFaceClient.is_reachable()
    if reachable:
        logger.info("CompreFace is reachable.")
    else:
        logger.warning("CompreFace is NOT reachable at startup - service will run degraded.")

    await _start_stream_runtime(app)

    yield

    logger.info("Shutting down face recognition service...")
    await _stop_stream_runtime(app)
    await CompreFaceClient.disconnect()


app = FastAPI(
    title="Face Recognition Service",
    description="Thin gateway over CompreFace for face registration and matching.",
    version="2.0.0",
    lifespan=lifespan,
)

app.add_middleware(
    CORSMiddleware,
    allow_origins=["*"],
    allow_methods=["*"],
    allow_headers=["*"],
)

app.include_router(matching.router, prefix="/api")
app.include_router(health.router)


if __name__ == "__main__":
    import uvicorn
    uvicorn.run("face_recognition_service.main:app", host="0.0.0.0", port=settings.port, reload=False)
