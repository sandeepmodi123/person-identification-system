"""Custom exceptions for the stream processor."""


class StreamConnectionError(Exception):
    """Raised when an RTSP stream cannot be connected to or fails during processing."""
    pass


class FrameExtractionError(Exception):
    """Raised when frame extraction from a stream fails."""
    pass


class FaceDetectionError(Exception):
    """Raised when face detection processing fails."""
    pass
