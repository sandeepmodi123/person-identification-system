using Microsoft.AspNetCore.Mvc;
using PersonIdentificationSystem.API.DTOs;
using PersonIdentificationSystem.API.Models.Entities;
using PersonIdentificationSystem.API.Repositories;
using PersonIdentificationSystem.API.Services;

namespace PersonIdentificationSystem.API.Controllers;

/// <summary>
/// Manages notification settings and delivery logs.
/// </summary>
[ApiController]
[Route("api/notifications")]
[Produces("application/json")]
public class NotificationController : ControllerBase
{
    private readonly INotificationService _notificationService;
    private readonly INotificationLogRepository _logRepo;

    public NotificationController(INotificationService notificationService, INotificationLogRepository logRepo)
    {
        _notificationService = notificationService;
        _logRepo = logRepo;
    }

    /// <summary>Get notification settings.</summary>
    [HttpGet("settings")]
    [ProducesResponseType(typeof(NotificationSettingsDto), 200)]
    public async Task<ActionResult<NotificationSettingsDto>> GetSettings(CancellationToken ct = default)
    {
        var settings = await _notificationService.GetSettingsAsync(ct);
        if (settings is null) return NotFound("Notification settings not configured.");
        return Ok(MapToDto(settings));
    }

    /// <summary>Update notification settings.</summary>
    [HttpPut("settings")]
    [ProducesResponseType(typeof(NotificationSettingsDto), 200)]
    public async Task<ActionResult<NotificationSettingsDto>> UpdateSettings(
        [FromBody] UpdateNotificationSettingsRequest request, CancellationToken ct = default)
    {
        var existing = await _notificationService.GetSettingsAsync(ct)
            ?? new NotificationSettings();

        if (request.RecipientEmails is not null) existing.RecipientEmails = request.RecipientEmails;
        if (request.MinimumConfidenceThreshold.HasValue) existing.MinimumConfidence = request.MinimumConfidenceThreshold.Value;
        if (request.NotifyOnRiskLevels is not null) existing.NotifyOnRiskLevels = request.NotifyOnRiskLevels;
        if (request.RateLimitMinutes.HasValue) existing.RateLimitMinutes = request.RateLimitMinutes.Value;
        if (request.IsEnabled.HasValue) existing.IsEnabled = request.IsEnabled.Value;
        if (request.SmtpHost is not null) existing.SmtpHost = request.SmtpHost;
        if (request.SmtpPort.HasValue) existing.SmtpPort = request.SmtpPort.Value;
        if (request.FromEmail is not null) existing.FromEmail = request.FromEmail;

        var updated = await _notificationService.UpdateSettingsAsync(existing, ct);
        return Ok(MapToDto(updated));
    }

    /// <summary>Get notification delivery logs.</summary>
    [HttpGet("logs")]
    [ProducesResponseType(typeof(PagedResult<NotificationLogDto>), 200)]
    public async Task<ActionResult<PagedResult<NotificationLogDto>>> GetLogs(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? status = null,
        [FromQuery] Guid? detectionId = null,
        CancellationToken ct = default)
    {
        var (items, total) = await _logRepo.GetPagedAsync(page, pageSize, status, detectionId, ct);
        var dtos = items.Select(l => new NotificationLogDto(
            l.Id, l.DetectionId, l.RecipientEmail, l.SentTimestamp, l.Status, l.ErrorMessage)).ToList();
        return Ok(new PagedResult<NotificationLogDto>(
            dtos, total, page, pageSize, (int)Math.Ceiling((double)total / pageSize)));
    }

    private static NotificationSettingsDto MapToDto(NotificationSettings s) => new(
        s.Id, s.RecipientEmails, s.MinimumConfidence,
        s.NotifyOnRiskLevels, s.RateLimitMinutes, s.IsEnabled,
        s.SmtpHost, s.SmtpPort, s.FromEmail);
}
