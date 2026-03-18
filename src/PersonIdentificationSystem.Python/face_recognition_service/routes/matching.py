"""Face matching API route."""
import base64
from typing import Optional

import numpy as np
from fastapi import APIRouter, HTTPException
from pydantic import BaseModel

from models.matcher import FaceMatcher
from services.cache_service import CacheService
from services.embedding_model import EmbeddingModel
from utils.logger import get_logger

logger = get_logger(__name__)
router = APIRouter()


class MatchRequest(BaseModel):
    image_base64: str


class MatchResponse(BaseModel):
    match_found: bool
    person_id: Optional[str] = None
    person_name: Optional[str] = None
    confidence: float = 0.0


class RegisterRequest(BaseModel):
    person_id: str
    person_name: str
    image_base64: str


class RegisterResponse(BaseModel):
    success: bool
    person_id: str
    message: str


@router.post("/register", response_model=RegisterResponse)
async def register_face(request: RegisterRequest) -> RegisterResponse:
    """
    Accept a base64-encoded face image, generate its embedding, and store
    it in the Redis cache for future matching.
    """
    try:
        image_bytes = base64.b64decode(request.image_base64)
    except Exception:
        raise HTTPException(status_code=400, detail="Invalid base64 image data.")

    try:
        embedding = EmbeddingModel.generate(image_bytes)
    except Exception as e:
        logger.error("Embedding generation failed for person %s: %s", request.person_id, e)
        raise HTTPException(status_code=422, detail="Failed to extract face embedding from image.")

    await CacheService.store_embedding(
        request.person_id,
        request.person_name,
        embedding.tolist(),
    )

    logger.info("Registered face embedding for person %s (%s)", request.person_id, request.person_name)
    return RegisterResponse(
        success=True,
        person_id=request.person_id,
        message=f"Face embedding registered for {request.person_name}.",
    )


@router.post("/match", response_model=MatchResponse)
async def match_face(request: MatchRequest) -> MatchResponse:
    """
    Accept a base64-encoded face image, generate embedding, and find the
    best matching person from the Redis embedding cache.
    """
    # Decode image
    try:
        image_bytes = base64.b64decode(request.image_base64)
    except Exception:
        raise HTTPException(status_code=400, detail="Invalid base64 image data.")

    # Generate embedding
    try:
        query_embedding = EmbeddingModel.generate(image_bytes)
    except Exception as e:
        logger.error("Embedding generation failed: %s", e)
        raise HTTPException(status_code=422, detail="Failed to extract face embedding from image.")

    # Load all person embeddings from cache
    person_embeddings = await CacheService.get_all_embeddings()

    if not person_embeddings:
        logger.warning("No person embeddings in cache - cannot match.")
        return MatchResponse(match_found=False)

    # Find best match
    match = FaceMatcher.find_best_match(query_embedding, person_embeddings)

    if match is None:
        return MatchResponse(match_found=False)

    logger.info(
        "Match found: person_id=%s confidence=%.4f",
        match.person_id,
        match.confidence,
    )

    return MatchResponse(
        match_found=True,
        person_id=match.person_id,
        person_name=match.person_name,
        confidence=match.confidence,
    )
