"""In-memory dedup / cooldown service.

Suppresses repeat detections of the same person within a configurable
cooldown window so the system doesn't fire 100 alerts for someone
standing in front of a camera.
"""
import time
from threading import Lock
from typing import Dict

from utils.config import settings
from utils.logger import get_logger

logger = get_logger(__name__)


class DedupService:
    _last_seen: Dict[str, float] = {}
    _lock = Lock()

    @classmethod
    def should_emit(cls, person_id: str) -> bool:
        """Return True if this person hasn't been reported within the cooldown window."""
        now = time.monotonic()
        cooldown = settings.dedup_cooldown_seconds
        with cls._lock:
            last = cls._last_seen.get(person_id, 0.0)
            if now - last < cooldown:
                logger.debug(
                    "Suppressing duplicate for %s (%.1fs since last)",
                    person_id, now - last,
                )
                return False
            cls._last_seen[person_id] = now
            return True

    @classmethod
    def reset(cls) -> None:
        with cls._lock:
            cls._last_seen.clear()
