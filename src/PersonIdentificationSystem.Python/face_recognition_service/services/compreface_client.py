"""CompreFace HTTP client - thin async wrapper over the CompreFace Recognition API."""
from typing import Any, Dict, List, Optional

import httpx

from utils.config import settings
from utils.logger import get_logger

logger = get_logger(__name__)


class CompreFaceClient:
    """Async client for the CompreFace recognition service.

    Endpoints used:
      POST   /api/v1/recognition/recognize         - recognize faces in image
      POST   /api/v1/recognition/faces?subject=... - add an example face for a subject
      DELETE /api/v1/recognition/faces?subject=... - remove all examples for a subject
      GET    /api/v1/recognition/subjects          - list subjects
    """

    _client: Optional[httpx.AsyncClient] = None

    @classmethod
    async def connect(cls) -> None:
        cls._client = httpx.AsyncClient(
            base_url=settings.compreface_url.rstrip("/"),
            headers={"x-api-key": settings.compreface_api_key},
            timeout=httpx.Timeout(30.0, connect=10.0),
        )
        logger.info("CompreFace client initialized: %s", settings.compreface_url)

    @classmethod
    async def disconnect(cls) -> None:
        if cls._client:
            await cls._client.aclose()
            cls._client = None

    @classmethod
    def _require(cls) -> httpx.AsyncClient:
        if cls._client is None:
            raise RuntimeError("CompreFace client not initialized.")
        return cls._client

    @classmethod
    async def is_reachable(cls) -> bool:
        try:
            client = cls._require()
            # Listing subjects is the cheapest authenticated probe
            resp = await client.get("/api/v1/recognition/subjects")
            return resp.status_code < 500
        except Exception as e:
            logger.warning("CompreFace reachability check failed: %s", e)
            return False

    @classmethod
    async def recognize(cls, image_bytes: bytes) -> Dict[str, Any]:
        """Send an image to CompreFace and return the parsed recognize response."""
        client = cls._require()
        files = {"file": ("frame.jpg", image_bytes, "image/jpeg")}
        params = {
            "limit": 0,                    # 0 = return all detected faces
            "prediction_count": 5,         # top-5 candidates so we can see near-misses
            # Very permissive detector threshold - we want CompreFace to find faces
            # even at odd angles or partial occlusion. We gate matches on similarity later.
            "det_prob_threshold": 0.4,
            "status": "true",
            "face_plugins": "landmarks,gender,age",
        }
        resp = await client.post(
            "/api/v1/recognition/recognize",
            files=files,
            params=params,
        )
        if resp.status_code == 400:
            # CompreFace returns 400 with code 28 when it can't find a face -
            # that's an expected outcome, not an error.
            logger.debug("CompreFace recognize 400 (no face): %s", resp.text[:120])
            return {"result": []}
        if resp.status_code >= 400:
            logger.warning("CompreFace recognize %s: %s", resp.status_code, resp.text[:200])
            return {"result": []}
        return resp.json()

    @classmethod
    async def add_face(cls, subject: str, image_bytes: bytes) -> bool:
        """Add an example image for a given subject (person)."""
        client = cls._require()
        files = {"file": ("photo.jpg", image_bytes, "image/jpeg")}
        resp = await client.post(
            "/api/v1/recognition/faces",
            params={"subject": subject, "det_prob_threshold": 0.6},
            files=files,
        )
        if resp.status_code >= 400:
            logger.error(
                "CompreFace add_face failed for %s: %s %s",
                subject, resp.status_code, resp.text[:200],
            )
            return False
        return True

    @classmethod
    async def delete_subject(cls, subject: str) -> None:
        """Delete all face examples stored under a subject."""
        client = cls._require()
        try:
            await client.delete(
                "/api/v1/recognition/faces",
                params={"subject": subject},
            )
        except Exception as e:
            logger.warning("CompreFace delete_subject(%s) error: %s", subject, e)

    @classmethod
    async def delete_all(cls) -> int:
        """Delete every subject (and all their faces) from this CompreFace app.

        Returns the number of subjects deleted. Use to wipe orphaned subjects
        registered under old identifiers before a full re-sync.
        """
        client = cls._require()
        subjects = await cls.list_subjects()
        deleted = 0
        for subject in subjects:
            try:
                resp = await client.delete(
                    "/api/v1/recognition/subjects/" + subject,
                )
                if resp.status_code < 400:
                    deleted += 1
                else:
                    logger.warning(
                        "CompreFace delete subject %s -> %s %s",
                        subject, resp.status_code, resp.text[:120],
                    )
            except Exception as e:
                logger.warning("CompreFace delete subject %s error: %s", subject, e)
        logger.info("CompreFace wiped %d/%d subjects", deleted, len(subjects))
        return deleted

    @classmethod
    async def list_subjects(cls) -> List[str]:
        client = cls._require()
        try:
            resp = await client.get("/api/v1/recognition/subjects")
            if resp.status_code >= 400:
                return []
            data = resp.json()
            return list(data.get("subjects", []))
        except Exception as e:
            logger.warning("CompreFace list_subjects error: %s", e)
            return []
