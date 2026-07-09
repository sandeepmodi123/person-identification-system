"""Face matching API route - backed by CompreFace."""
import base64
from typing import Optional

from fastapi import APIRouter, HTTPException
from pydantic import BaseModel

from face_recognition_service.services.compreface_client import CompreFaceClient
from face_recognition_service.services.dedup_service import DedupService
from face_recognition_service.utils.config import settings
from face_recognition_service.utils.logger import get_logger

logger = get_logger(__name__)
router = APIRouter()


class MatchRequest(BaseModel):
    image_base64: str


class MatchResponse(BaseModel):
    match_found: bool
    person_face_id: Optional[str] = None
    confidence: float = 0.0


class RegisterRequest(BaseModel):
    person_face_id: str
    image_base64: str


class RegisterResponse(BaseModel):
    success: bool
    person_face_id: str
    message: str


def _decode_image(image_base64: str) -> bytes:
    try:
        return base64.b64decode(image_base64)
    except Exception:
        raise HTTPException(status_code=400, detail="Invalid base64 image data.")


def _face_meets_min_size(box: dict) -> bool:
    """Return True if the detected face box satisfies the min size threshold."""
    try:
        w = int(box.get("x_max", 0)) - int(box.get("x_min", 0))
        h = int(box.get("y_max", 0)) - int(box.get("y_min", 0))
        return w >= settings.min_face_size_px and h >= settings.min_face_size_px
    except Exception:
        return True  # if box info missing, don't drop the result


@router.post("/register", response_model=RegisterResponse)
async def register_face(request: RegisterRequest) -> RegisterResponse:
    """Register a photo as a CompreFace example under subject = person_face_id."""
    image_bytes = _decode_image(request.image_base64)

    ok = await CompreFaceClient.add_face(request.person_face_id, image_bytes)
    if not ok:
        raise HTTPException(
            status_code=422,
            detail="CompreFace rejected the image (no face detected or invalid).",
        )

    return RegisterResponse(
        success=True,
        person_face_id=request.person_face_id,
        message="Face registered with CompreFace.",
    )


class UnregisterRequest(BaseModel):
    person_face_id: str


@router.post("/unregister")
async def unregister_face(request: UnregisterRequest) -> dict:
    """Delete all CompreFace examples for the given subject."""
    await CompreFaceClient.delete_subject(request.person_face_id)
    return {"success": True, "person_face_id": request.person_face_id}


@router.post("/wipe-all")
async def wipe_all() -> dict:
    """Delete every subject in this CompreFace app. Used before a full re-sync."""
    deleted = await CompreFaceClient.delete_all()
    return {"success": True, "deleted": deleted}


@router.get("/subjects")
async def list_subjects() -> dict:
    """List all enrolled subjects in CompreFace. Diagnostic endpoint."""
    subjects = await CompreFaceClient.list_subjects()
    return {"count": len(subjects), "subjects": subjects}


@router.post("/match", response_model=MatchResponse)
async def match_face(request: MatchRequest) -> MatchResponse:
    """Recognize a face via CompreFace and apply threshold + min size + dedup cooldown."""
    image_bytes = _decode_image(request.image_base64)

    data = await CompreFaceClient.recognize(image_bytes)
    results = data.get("result") or []
    if not results:
        logger.debug("CompreFace returned no faces in the image.")
        return MatchResponse(match_found=False)

    # Walk every detected face + every candidate subject, keeping the best similarity.
    best: Optional[dict] = None
    best_similarity = -1.0
    for face in results:
        box = face.get("box") or {}
        try:
            w = int(box.get("x_max", 0)) - int(box.get("x_min", 0))
            h = int(box.get("y_max", 0)) - int(box.get("y_min", 0))
        except Exception:
            w = h = 0

        if w < settings.min_face_size_px or h < settings.min_face_size_px:
            continue

        for subj in (face.get("subjects") or []):
            similarity = float(subj.get("similarity", 0.0))
            if similarity > best_similarity:
                best_similarity = similarity
                best = subj

    if best is None or best_similarity < settings.confidence_threshold:
        logger.info(
            "CompreFace -> no match (best=%.3f, threshold=%.2f)",
            max(best_similarity, 0.0), settings.confidence_threshold,
        )
        return MatchResponse(match_found=False)

    person_face_id = str(best.get("subject", "")).strip()
    if not person_face_id:
        return MatchResponse(match_found=False)

    # 30s cooldown so we don't spam the same person
    if not DedupService.should_emit(person_face_id):
        return MatchResponse(match_found=False)

    logger.info(
        "CompreFace -> MATCH face_id=%s confidence=%.3f",
        person_face_id, best_similarity,
    )
    return MatchResponse(
        match_found=True,
        person_face_id=person_face_id,
        confidence=best_similarity,
    )
