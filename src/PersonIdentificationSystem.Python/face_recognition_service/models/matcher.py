"""Face similarity matching logic."""
from dataclasses import dataclass
from typing import List, Optional

import numpy as np

from services.cache_service import PersonEmbeddingEntry
from utils.config import settings
from utils.logger import get_logger

logger = get_logger(__name__)


@dataclass
class MatchResult:
    person_id: str
    person_name: str
    confidence: float


class FaceMatcher:
    """Computes cosine similarity between query and stored embeddings."""

    @staticmethod
    def find_best_match(
        query_embedding: np.ndarray,
        candidates: List[PersonEmbeddingEntry],
    ) -> Optional[MatchResult]:
        """
        Find the best matching person for the given query embedding.

        Args:
            query_embedding: Normalized face embedding vector.
            candidates: List of person embedding entries from cache.

        Returns:
            MatchResult if a match above threshold is found, else None.
        """
        best_score = -1.0
        best_entry: Optional[PersonEmbeddingEntry] = None

        q_norm = query_embedding / (np.linalg.norm(query_embedding) + 1e-8)

        for entry in candidates:
            candidate_vec = np.array(entry.embedding, dtype=np.float32)
            c_norm = candidate_vec / (np.linalg.norm(candidate_vec) + 1e-8)

            # Cosine similarity
            score = float(np.dot(q_norm, c_norm))

            if score > best_score:
                best_score = score
                best_entry = entry

        if best_entry is None or best_score < settings.confidence_threshold:
            logger.info(
                "No match above threshold %.3f (best=%.4f, candidate=%s)",
                settings.confidence_threshold,
                best_score,
                best_entry.person_name if best_entry else "none",
            )
            return None

        return MatchResult(
            person_id=best_entry.person_id,
            person_name=best_entry.person_name,
            confidence=best_score,
        )
