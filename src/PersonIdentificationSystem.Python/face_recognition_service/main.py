"""
Person Identification System - Face Recognition Service
FastAPI application that delegates face recognition to CompreFace.
"""
import logging
from contextlib import asynccontextmanager

from fastapi import FastAPI
from fastapi.middleware.cors import CORSMiddleware

from routes import matching, health
from services.compreface_client import CompreFaceClient
from utils.config import settings
from utils.logger import get_logger

# Quiet noisy libraries - we only want our own CompreFace logs.
logging.getLogger("httpx").setLevel(logging.WARNING)
logging.getLogger("httpcore").setLevel(logging.WARNING)
logging.getLogger("uvicorn.access").setLevel(logging.WARNING)

logger = get_logger(__name__)


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

    yield

    logger.info("Shutting down face recognition service...")
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
    uvicorn.run("main:app", host="0.0.0.0", port=settings.port, reload=False)
