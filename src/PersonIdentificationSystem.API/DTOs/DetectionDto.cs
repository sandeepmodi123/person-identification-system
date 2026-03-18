namespace PersonIdentificationSystem.API.DTOs;

public record DetectionDto(
    Guid Id,
    Guid StreamId,
    string CameraName,
    Guid? PersonId,
    string? PersonName,
    string? RiskLevel,
    decimal ConfidenceScore,
    DateTime DetectionTimestamp,
    string? FrameImageUrl,
    bool IsVerified,
    string? VerificationStatus,
    bool EmailSent
);

public record ProcessFrameRequest(
    Guid StreamId,
    string FrameBase64,
    DateTime CapturedAt
);

public record ProcessFrameResult(
    bool MatchFound,
    Guid? DetectionId,
    Guid? PersonId,
    string? PersonName,
    decimal? ConfidenceScore,
    bool NotificationSent
);

public record DetectionEventDto(
    Guid DetectionId,
    Guid StreamId,
    string CameraName,
    Guid PersonId,
    string PersonName,
    string RiskLevel,
    decimal ConfidenceScore,
    DateTime DetectionTimestamp,
    bool NotificationSent
);

public record VerifyDetectionRequest(
    string Status,  // TruePositive | FalsePositive
    string? Notes
);

public record DetectionFilterRequest
{
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
    public Guid? StreamId { get; init; }
    public Guid? PersonId { get; init; }
    public DateTime? FromDate { get; init; }
    public DateTime? ToDate { get; init; }
    public decimal? MinConfidence { get; init; }
    public bool? IsVerified { get; init; }
}
