namespace PersonIdentificationSystem.API.DTOs;

public record NotificationSettingsDto(
    Guid Id,
    string[] RecipientEmails,
    decimal MinimumConfidenceThreshold,
    string[] NotifyOnRiskLevels,
    int RateLimitMinutes,
    bool IsEnabled,
    string? SmtpHost,
    int? SmtpPort,
    string? FromEmail
);

public record UpdateNotificationSettingsRequest(
    string[]? RecipientEmails,
    decimal? MinimumConfidenceThreshold,
    string[]? NotifyOnRiskLevels,
    int? RateLimitMinutes,
    bool? IsEnabled,
    string? SmtpHost,
    int? SmtpPort,
    string? FromEmail
);

public record NotificationLogDto(
    Guid Id,
    Guid? DetectionId,
    string RecipientEmail,
    DateTime SentTimestamp,
    string Status,
    string? ErrorMessage
);
