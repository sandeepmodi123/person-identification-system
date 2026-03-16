"""
Person Identification System - Face Recognition Service
FastAPI application for face embedding generation and matching.
"""
from contextlib import asynccontextmanager

from fastapi import FastAPI
from fastapi.middleware.cors import CORSMiddleware

from routes import matching, health
from services.model_loader import ModelLoader
from services.cache_service import CacheService
from utils.config import settings
from utils.logger import get_logger

logger = get_logger(__name__)


@asynccontextmanager
async def lifespan(app: FastAPI):
    """Application lifespan: load models on startup, cleanup on shutdown."""
    logger.info("Loading face recognition models...")
    ModelLoader.load()
    logger.info("Model loaded successfully: %s", settings.model_name)

    logger.info("Connecting to Redis cache...")
    await CacheService.connect()
    logger.info("Redis connected.")

    yield

    logger.info("Shutting down face recognition service...")
    await CacheService.disconnect()


app = FastAPI(
    title="Face Recognition Service",
    description="Generates face embeddings and performs similarity matching.",
    version="1.0.0",
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
