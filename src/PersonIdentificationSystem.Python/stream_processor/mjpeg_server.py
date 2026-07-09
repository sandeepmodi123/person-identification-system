"""MJPEG streaming server - serves live camera frames over HTTP."""
import asyncio
import os
from typing import Dict, Optional

from aiohttp import web

try:
    from .logger import get_logger
except ImportError:
    from logger import get_logger

logger = get_logger(__name__)

# Shared state: stream_id -> latest encoded JPEG bytes
_latest_jpegs: Dict[str, bytes] = {}
_locks: Dict[str, asyncio.Lock] = {}
_runner: Optional[web.AppRunner] = None

MJPEG_FPS = int(os.getenv("MJPEG_FPS", "20"))
MJPEG_PORT = int(os.getenv("MJPEG_PORT", "8085"))
MJPEG_FIRST_FRAME_TIMEOUT_SECONDS = float(
    os.getenv("MJPEG_FIRST_FRAME_TIMEOUT_SECONDS", "3")
)


async def set_frame(stream_id: str, frame) -> None:
    """Called by FrameExtractor after each successful frame read.

    We encode once per incoming frame and reuse bytes for all clients.
    """
    if stream_id not in _locks:
        _locks[stream_id] = asyncio.Lock()

    import cv2
    ok, jpeg = cv2.imencode(".jpg", frame, [cv2.IMWRITE_JPEG_QUALITY, 70])
    if not ok:
        return

    data = jpeg.tobytes()
    async with _locks[stream_id]:
        _latest_jpegs[stream_id] = data


def remove_stream(stream_id: str) -> None:
    """Remove a stream from the shared frame dictionary."""
    _latest_jpegs.pop(stream_id, None)
    _locks.pop(stream_id, None)


def get_active_stream_ids() -> list:
    """Return list of stream IDs that have frames available."""
    return list(_latest_jpegs.keys())


async def mjpeg_handler(request: web.Request) -> web.StreamResponse:
    """Serve an MJPEG stream for the given stream_id."""
    stream_id = request.match_info["stream_id"]

    # Fail fast if there are no frames yet for this stream.
    # Without this, browsers keep waiting on an open HTTP response and
    # the UI appears as a blank/hung video tile.
    waited = 0.0
    while _latest_jpegs.get(stream_id) is None and waited < MJPEG_FIRST_FRAME_TIMEOUT_SECONDS:
        await asyncio.sleep(0.1)
        waited += 0.1

    if _latest_jpegs.get(stream_id) is None:
        return web.Response(
            status=503,
            text=f"No frames available for stream '{stream_id}'.",
            headers={
                "Access-Control-Allow-Origin": "*",
                "Cache-Control": "no-cache, no-store, must-revalidate",
            },
        )

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
        while True:
            lock = _locks.get(stream_id)
            data = _latest_jpegs.get(stream_id)

            if data is not None and lock is not None:
                async with lock:
                    current = _latest_jpegs.get(stream_id)
                    if current is not None:
                        data = current
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
    return web.json_response(
        get_active_stream_ids(),
        headers={
            "Access-Control-Allow-Origin": "*",
            "Cache-Control": "no-cache, no-store, must-revalidate",
        },
    )


async def start_mjpeg_server() -> None:
    """Start the MJPEG HTTP server."""
    global _runner

    if _runner is not None:
        logger.info("MJPEG server already running on port %d", MJPEG_PORT)
        return

    app = web.Application()
    app.router.add_get("/stream/{stream_id}/mjpeg", mjpeg_handler)
    app.router.add_get("/streams", streams_list_handler)

    runner = web.AppRunner(app, access_log=None)
    await runner.setup()
    site = web.TCPSite(runner, "0.0.0.0", MJPEG_PORT)
    await site.start()
    _runner = runner
    logger.info("MJPEG server listening on port %d (fps=%d)", MJPEG_PORT, MJPEG_FPS)


async def stop_mjpeg_server() -> None:
    """Stop the MJPEG HTTP server if running."""
    global _runner

    if _runner is None:
        return

    await _runner.cleanup()
    _runner = None
    logger.info("MJPEG server stopped")


def is_mjpeg_server_running() -> bool:
    """Return True if MJPEG server has been started and not yet stopped."""
    return _runner is not None
