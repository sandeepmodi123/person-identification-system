"""Redis cache service for person embeddings."""
import json
from typing import Dict, List, Optional

import redis.asyncio as aioredis

from utils.config import settings
from utils.logger import get_logger

logger = get_logger(__name__)

EMBEDDINGS_KEY_PREFIX = "person:embedding:"


class PersonEmbeddingEntry:
    def __init__(self, person_id: str, person_name: str, embedding: list):
        self.person_id = person_id
        self.person_name = person_name
        self.embedding = embedding


class CacheService:
    _client: Optional[aioredis.Redis] = None

    @classmethod
    async def connect(cls) -> None:
        cls._client = await aioredis.from_url(
            f"redis://:{settings.redis_password}@{settings.redis_host}:{settings.redis_port}",
            encoding="utf-8",
            decode_responses=True,
        )

    @classmethod
    async def disconnect(cls) -> None:
        if cls._client:
            await cls._client.aclose()

    @classmethod
    async def is_connected(cls) -> bool:
        if not cls._client:
            return False
        try:
            return await cls._client.ping()
        except Exception:
            return False

    @classmethod
    async def store_embedding(cls, person_id: str, person_name: str, embedding: list) -> None:
        """Store a person's face embedding in Redis."""
        if not cls._client:
            return
        key = f"{EMBEDDINGS_KEY_PREFIX}{person_id}"
        value = json.dumps({"person_id": person_id, "person_name": person_name, "embedding": embedding})
        await cls._client.set(key, value)

    @classmethod
    async def get_all_embeddings(cls) -> List[PersonEmbeddingEntry]:
        """Retrieve all person embeddings from Redis."""
        if not cls._client:
            return []

        keys = await cls._client.keys(f"{EMBEDDINGS_KEY_PREFIX}*")
        if not keys:
            return []

        values = await cls._client.mget(*keys)
        entries = []
        for val in values:
            if val:
                try:
                    data = json.loads(val)
                    entries.append(PersonEmbeddingEntry(
                        data["person_id"],
                        data["person_name"],
                        data["embedding"],
                    ))
                except (json.JSONDecodeError, KeyError) as e:
                    logger.warning("Invalid embedding cache entry: %s", e)
        return entries

    @classmethod
    async def delete_embedding(cls, person_id: str) -> None:
        """Remove a person's embedding from cache."""
        if cls._client:
            await cls._client.delete(f"{EMBEDDINGS_KEY_PREFIX}{person_id}")
