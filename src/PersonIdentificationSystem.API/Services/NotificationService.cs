using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using PersonIdentificationSystem.API.Infrastructure;
using PersonIdentificationSystem.API.Models.Entities;
using PersonIdentificationSystem.API.Repositories;
using Microsoft.EntityFrameworkCore;

namespace PersonIdentificationSystem.API.Services;

public interface INotificationService
{
    Task<bool> SendDetectionNotificationAsync(Detection detection, Person person, CancellationToken ct = default);
    Task<NotificationSettings?> GetSettingsAsync(CancellationToken ct = default);
    Task<NotificationSettings> UpdateSettingsAsync(NotificationSettings settings, CancellationToken ct = default);
}

public class NotificationService : INotificationService
{
    private readonly ApplicationDbContext _context;
    private readonly INotificationLogRepository _logRepo;
    private readonly IConfiguration _config;
    private readonly ILogger<NotificationService> _logger;

    public NotificationService(
        ApplicationDbContext context,
        INotificationLogRepository logRepo,
        IConfiguration config,
        ILogger<NotificationService> logger)
    {
        _context = context;
        _logRepo = logRepo;
        _config = config;
        _logger = logger;
    }

    public async Task<bool> SendDetectionNotificationAsync(
        Detection detection, Person person, CancellationToken ct = default)
    {
        var settings = await GetSettingsAsync(ct);
        if (settings is null || !settings.IsEnabled) return false;

        // Check risk level filter
        if (!settings.NotifyOnRiskLevels.Contains(person.RiskLevel)) return false;

        // Rate limiting: check if we already sent for this person recently
        var rateLimitCutoff = DateTime.UtcNow.AddMinutes(-settings.RateLimitMinutes);
        var recentLog = await _context.NotificationLogs
            .Where(n => n.Detection!.PersonId == person.Id
                     && n.Status == "Sent"
                     && n.SentTimestamp >= rateLimitCutoff)
            .FirstOrDefaultAsync(ct);

        if (recentLog is not null)
        {
            _logger.LogInformation("Rate limited: notification for person {PersonId} suppressed", person.Id);
            return false;
        }

        var smtpHost = settings.SmtpHost ?? _config["Email:SmtpHost"];
        var smtpPort = settings.SmtpPort ?? _config.GetValue<int>("Email:SmtpPort", 587);
        var fromEmail = settings.FromEmail ?? _config["Email:FromEmail"];
        var fromName = _config["Email:FromName"] ?? "Person Identification System";
        var username = _config["Email:Username"];
        var password = _config["Email:Password"];

        bool allSent = true;

        foreach (var recipient in settings.RecipientEmails)
        {
            // Guard against CRLF injection (CVE: GHSA-g7hc-96xr-gvvx)
            if (recipient.Contains('\r') || recipient.Contains('\n'))
            {
                _logger.LogWarning("Skipping recipient with CRLF characters: (sanitized)");
                continue;
            }
            var log = new NotificationLog
            {
                DetectionId = detection.Id,
                RecipientEmail = recipient,
                Status = "Pending"
            };

            try
            {
                var message = new MimeMessage();
                message.From.Add(new MailboxAddress(fromName, fromEmail));
                message.To.Add(MailboxAddress.Parse(recipient));
                message.Subject = $"[ALERT] Person Detected: {person.Name} ({person.RiskLevel} Risk)";

                message.Body = new TextPart("html")
                {
                    Text = EmailTemplateGenerator.GenerateDetectionAlert(
                        person.Name, person.RiskLevel, person.Description,
                        detection.DetectionTimestamp, (double)detection.ConfidenceScore,
                        detection.Id.ToString())
                };

                using var client = new SmtpClient();
                await client.ConnectAsync(smtpHost, smtpPort, SecureSocketOptions.StartTls, ct);

                if (!string.IsNullOrWhiteSpace(username))
                    await client.AuthenticateAsync(username, password, ct);

                var response = await client.SendAsync(message, ct);
                await client.DisconnectAsync(true, ct);

                log.Status = "Sent";
                log.MessageId = message.MessageId;
                _logger.LogInformation("Notification sent to {Recipient} for detection {DetectionId}", recipient, detection.Id);
            }
            catch (Exception ex)
            {
                log.Status = "Failed";
                log.ErrorMessage = ex.Message;
                allSent = false;
                _logger.LogError(ex, "Failed to send notification to {Recipient}", recipient);
            }

            await _logRepo.AddAsync(log, ct);
        }

        return allSent && settings.RecipientEmails.Length > 0;
    }

    public async Task<NotificationSettings?> GetSettingsAsync(CancellationToken ct = default)
        => await _context.NotificationSettings.FirstOrDefaultAsync(ct);

    public async Task<NotificationSettings> UpdateSettingsAsync(
        NotificationSettings settings, CancellationToken ct = default)
    {
        settings.UpdatedAt = DateTime.UtcNow;
        _context.NotificationSettings.Update(settings);
        await _context.SaveChangesAsync(ct);
        return settings;
    }
}
