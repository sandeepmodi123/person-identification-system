"""Folder Face Dispatcher.

Reads every image in a local folder (e.g. the `detected_faces` folder produced
by the RTSP capture script) and forwards each one to the face_recognition_service
`/api/match` endpoint. Every file that is sent is recorded in a log file so we
have a clear audit trail of what was dispatched for recognition.
"""

import base64
import logging
import os
import sys
import time
from datetime import datetime
from logging.handlers import RotatingFileHandler

import requests


# ---------------------------------------------------------------------------
# Configuration
# ---------------------------------------------------------------------------

# Folder to scan for face crops. Must match the OUTPUT_DIR of the capture script.
INPUT_DIR = os.getenv("FACE_INPUT_DIR", "detected_faces")

# Where to move files once they've been processed (success or rejected).
PROCESSED_DIR = os.path.join(INPUT_DIR, "processed")
FAILED_DIR = os.path.join(INPUT_DIR, "failed")

# face_recognition_service base URL (FastAPI app from face_recognition_service/main.py).
FACE_RECOGNITION_URL = os.getenv("FACE_RECOGNITION_URL", "http://localhost:8000")
MATCH_ENDPOINT = f"{FACE_RECOGNITION_URL.rstrip('/')}/api/match"

# Log file (one entry per file dispatched).
LOG_FILE = os.getenv("DISPATCH_LOG_FILE", "face_dispatch.log")

# Scan behaviour
SUPPORTED_EXTS = (".jpg", ".jpeg", ".png", ".bmp")
WATCH_MODE = True          # True = keep polling for new files, False = run once and exit
POLL_INTERVAL_SECONDS = 2  # How often to re-scan in watch mode
REQUEST_TIMEOUT = 30       # Seconds


# ---------------------------------------------------------------------------
# Logger - writes to both console and rotating file
# ---------------------------------------------------------------------------

logger = logging.getLogger("face_dispatcher")
logger.setLevel(logging.INFO)

_fmt = logging.Formatter("%(asctime)s [%(levelname)s] %(message)s")

_file_handler = RotatingFileHandler(LOG_FILE, maxBytes=5 * 1024 * 1024, backupCount=3)
_file_handler.setFormatter(_fmt)
logger.addHandler(_file_handler)

_console_handler = logging.StreamHandler(sys.stdout)
_console_handler.setFormatter(_fmt)
logger.addHandler(_console_handler)


# ---------------------------------------------------------------------------
# Helpers
# ---------------------------------------------------------------------------

def _ensure_dirs() -> None:
    for d in (INPUT_DIR, PROCESSED_DIR, FAILED_DIR):
        os.makedirs(d, exist_ok=True)


def _list_pending_files():
    """Return a sorted list of image files waiting in INPUT_DIR (top-level only)."""
    try:
        entries = os.listdir(INPUT_DIR)
    except FileNotFoundError:
        return []

    files = []
    for name in entries:
        full = os.path.join(INPUT_DIR, name)
        if not os.path.isfile(full):
            continue
        if not name.lower().endswith(SUPPORTED_EXTS):
            continue
        files.append(full)
    files.sort()
    return files


def _encode_image(path: str) -> str:
    with open(path, "rb") as f:
        return base64.b64encode(f.read()).decode("utf-8")


def _move(src: str, dst_dir: str) -> str:
    os.makedirs(dst_dir, exist_ok=True)
    dst = os.path.join(dst_dir, os.path.basename(src))
    # Avoid clobbering on name collisions.
    if os.path.exists(dst):
        stem, ext = os.path.splitext(os.path.basename(src))
        ts = datetime.now().strftime("%Y%m%d_%H%M%S_%f")
        dst = os.path.join(dst_dir, f"{stem}_{ts}{ext}")
    os.replace(src, dst)
    return dst


def dispatch_file(path: str) -> bool:
    """Send a single image to /api/match. Returns True on a successful HTTP call."""
    filename = os.path.basename(path)

    try:
        image_b64 = _encode_image(path)
    except OSError as e:
        logger.error("READ_FAIL file=%s error=%s", filename, e)
        return False

    payload = {"image_base64": image_b64}

    # The log line the user asked for: which file is being sent for recognition.
    logger.info("SENT file=%s endpoint=%s bytes=%d",
                filename, MATCH_ENDPOINT, len(image_b64))

    try:
        resp = requests.post(MATCH_ENDPOINT, json=payload, timeout=REQUEST_TIMEOUT)
    except requests.RequestException as e:
        logger.error("HTTP_FAIL file=%s error=%s", filename, e)
        return False

    if resp.status_code != 200:
        logger.error("HTTP_%s file=%s body=%s",
                     resp.status_code, filename, resp.text[:200])
        return False

    try:
        data = resp.json()
    except ValueError:
        logger.error("BAD_JSON file=%s body=%s", filename, resp.text[:200])
        return False

    if data.get("match_found"):
        logger.info("MATCH file=%s person_face_id=%s confidence=%.3f",
                    filename, data.get("person_face_id"),
                    float(data.get("confidence", 0.0)))
    else:
        logger.info("NO_MATCH file=%s", filename)

    return True


def process_once() -> int:
    """Process every file currently pending. Returns the number of files handled."""
    pending = _list_pending_files()
    if not pending:
        return 0

    handled = 0
    for path in pending:
        ok = dispatch_file(path)
        try:
            _move(path, PROCESSED_DIR if ok else FAILED_DIR)
        except OSError as e:
            logger.error("MOVE_FAIL file=%s error=%s", os.path.basename(path), e)
        handled += 1
    return handled


# ---------------------------------------------------------------------------
# Entry point
# ---------------------------------------------------------------------------

def main() -> None:
    _ensure_dirs()
    logger.info("Folder dispatcher starting. input=%s endpoint=%s watch=%s",
                INPUT_DIR, MATCH_ENDPOINT, WATCH_MODE)

    if not WATCH_MODE:
        n = process_once()
        logger.info("Run complete. files_processed=%d", n)
        return

    try:
        while True:
            n = process_once()
            if n == 0:
                time.sleep(POLL_INTERVAL_SECONDS)
    except KeyboardInterrupt:
        logger.info("Stopped by user.")


if __name__ == "__main__":
    main()
