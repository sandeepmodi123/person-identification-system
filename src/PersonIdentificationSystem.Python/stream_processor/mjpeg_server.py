"""MJPEG streaming server - serves live camera frames over HTTP."""
import asyncio
import os
from typing import Dict, Optional

import numpy as np
from aiohttp import web

from logger import get_logger

logger = get_logger(__name__)

# Shared state: stream_id -> latest raw numpy frame
_latest_frames: Dict[str, np.ndarray] = {}
_locks: Dict[str, asyncio.Lock] = {}

MJPEG_FPS = int(os.getenv("MJPEG_FPS", "10"))
MJPEG_PORT = int(os.getenv("MJPEG_PORT", "8085"))


async def set_frame(stream_id: str, frame: np.ndarray) -> None:
    """Called by FrameExtractor after each successful frame read."""
    if stream_id not in _locks:
        _locks[stream_id] = asyncio.Lock()
    async with _locks[stream_id]:
        _latest_frames[stream_id] = frame


def remove_stream(stream_id: str) -> None:
    """Remove a stream from the shared frame dictionary."""
    _latest_frames.pop(stream_id, None)
    _locks.pop(stream_id, None)


def get_active_stream_ids() -> list:
    """Return list of stream IDs that have frames available."""
    return list(_latest_frames.keys())


async def mjpeg_handler(request: web.Request) -> web.StreamResponse:
    """Serve an MJPEG stream for the given stream_id."""
    stream_id = request.match_info["stream_id"]

    response = web.StreamResponse(
        status=200,
        headers={
            "Content-Type": "multipart/x-mixed-replace; boundary=frame",
            "Cache-Control": "no-cache, no-store, must-revalidate",
            "Pragma": "no-cache",
            "Access-Control-Allow-Origin": "*",
        },
    )
    await response.prepare(request)

    interval = 1.0 / MJPEG_FPS

    try:
        import cv2

        while True:
            lock = _locks.get(stream_id)
            frame = _latest_frames.get(stream_id)

            if frame is not None and lock is not None:
                async with lock:
                    _, jpeg = cv2.imencode(
                        ".jpg", frame, [cv2.IMWRITE_JPEG_QUALITY, 70]
                    )
                data = jpeg.tobytes()
                await response.write(
                    b"--frame\r\n"
                    b"Content-Type: image/jpeg\r\n"
                    b"Content-Length: " + str(len(data)).encode() + b"\r\n\r\n"
                    + data
                    + b"\r\n"
                )

            await asyncio.sleep(interval)

    except (ConnectionResetError, asyncio.CancelledError):
        pass

    return response


async def streams_list_handler(request: web.Request) -> web.Response:
    """Return a JSON list of active stream IDs."""
    return web.json_response(get_active_stream_ids())


async def start_mjpeg_server() -> None:
    """Start the MJPEG HTTP server."""
    app = web.Application()
    app.router.add_get("/stream/{stream_id}/mjpeg", mjpeg_handler)
    app.router.add_get("/streams", streams_list_handler)

    runner = web.AppRunner(app)
    await runner.setup()
    site = web.TCPSite(runner, "0.0.0.0", MJPEG_PORT)
    await site.start()
    logger.info("MJPEG server listening on port %d (fps=%d)", MJPEG_PORT, MJPEG_FPS)
