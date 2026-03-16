namespace PersonIdentificationSystem.API.DTOs;

public record RTSPStreamDto(
    Guid Id,
    string CameraName,
    string? CameraLocation,
    string RtspUrl,
    int FrameIntervalSeconds,
    bool IsActive,
    string Status,
    DateTime? LastChecked
);

public record CreateRTSPStreamRequest(
    string CameraName,
    string? CameraLocation,
    string RtspUrl,
    int FrameIntervalSeconds = 5,
    bool IsActive = true
);

public record UpdateRTSPStreamRequest(
    string? CameraName,
    string? CameraLocation,
    string? RtspUrl,
    int? FrameIntervalSeconds,
    bool? IsActive
);

public record StreamConnectionTestResult(
    Guid StreamId,
    bool IsReachable,
    int? LatencyMs,
    DateTime TestedAt,
    string? ErrorMessage
);
