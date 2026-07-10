using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;
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
    private static readonly Regex PhoneRegex = new(@"^\+?[1-9]\d{7,14}$", RegexOptions.Compiled);

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
        bool anySent = false;
        var stream = await _context.RTSPStreams
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == detection.StreamId, ct);

        string? cameraLocation = stream?.CameraLocation;
        decimal? cameraLatitude = stream?.CameraLatitude;
        decimal? cameraLongitude = stream?.CameraLongitude;
        string? mapUrl = null;
        if (cameraLatitude.HasValue && cameraLongitude.HasValue)
        {
            mapUrl = $"https://www.google.com/maps/search/?api=1&query={cameraLatitude.Value.ToString(CultureInfo.InvariantCulture)},{cameraLongitude.Value.ToString(CultureInfo.InvariantCulture)}";
        }
        else if (!string.IsNullOrWhiteSpace(cameraLocation))
        {
            mapUrl = $"https://www.google.com/maps/search/?api=1&query={HttpUtility.UrlEncode(cameraLocation)}";
        }

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
                        detection.Id.ToString(), stream?.CameraName, cameraLocation,
                        cameraLatitude, cameraLongitude, mapUrl)
                };

                using var client = new SmtpClient();
                await client.ConnectAsync(smtpHost, smtpPort, SecureSocketOptions.StartTls, ct);

                if (!string.IsNullOrWhiteSpace(username))
                    await client.AuthenticateAsync(username, password, ct);

                var response = await client.SendAsync(message, ct);
                await client.DisconnectAsync(true, ct);

                log.Status = "Sent";
                log.MessageId = message.MessageId;
                anySent = true;
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

        var smsResult = await TrySendSmsAlertAsync(detection, person, stream, mapUrl, ct);
        anySent = anySent || smsResult.Sent;
        allSent = allSent && !smsResult.Failed;

        return allSent && anySent;
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

    private async Task<(bool Sent, bool Failed)> TrySendSmsAlertAsync(
        Detection detection,
        Person person,
        RTSPStream? stream,
        string? mapUrl,
        CancellationToken ct)
    {
        if (!_config.GetValue("Sms:Enabled", true))
        {
            return (false, false);
        }

        var toNumberRaw = stream?.CameraMobileNumber;
        if (string.IsNullOrWhiteSpace(toNumberRaw))
        {
            return (false, false);
        }

        var toNumber = toNumberRaw.Trim();
        if (!PhoneRegex.IsMatch(toNumber))
        {
            _logger.LogWarning(
                "SMS not sent for detection {DetectionId}: invalid mobile number format {MobileNumber}",
                detection.Id,
                toNumber);
            return (false, true);
        }

        var accountSid = _config["Sms:Twilio:AccountSid"];
        var authToken = _config["Sms:Twilio:AuthToken"];
        var fromNumber = _config["Sms:Twilio:FromNumber"];
        var messagingServiceSid = _config["Sms:Twilio:MessagingServiceSid"];

        if (string.IsNullOrWhiteSpace(accountSid)
            || string.IsNullOrWhiteSpace(authToken)
            || (string.IsNullOrWhiteSpace(fromNumber) && string.IsNullOrWhiteSpace(messagingServiceSid)))
        {
            _logger.LogWarning(
                "SMS not sent for detection {DetectionId}: Twilio config missing (Sms:Twilio:AccountSid/AuthToken and either FromNumber or MessagingServiceSid).",
                detection.Id);
            return (false, false);
        }

        var location = stream?.CameraLocation;
        var camera = stream?.CameraName ?? "Unknown";
        var messageBody = new StringBuilder()
            .Append("ALERT: ")
            .Append(person.Name)
            .Append(" (")
            .Append(person.RiskLevel)
            .Append(") detected at ")
            .Append(camera);

        if (!string.IsNullOrWhiteSpace(location))
        {
            messageBody.Append(" - ").Append(location);
        }

        if (!string.IsNullOrWhiteSpace(mapUrl))
        {
            messageBody.Append(". Map: ").Append(mapUrl);
        }

        var log = new NotificationLog
        {
            DetectionId = detection.Id,
            RecipientEmail = $"sms:{toNumber}",
            Status = "Pending"
        };

        try
        {
            var endpoint = $"https://api.twilio.com/2010-04-01/Accounts/{accountSid}/Messages.json";
            using var httpClient = new HttpClient();
            var auth = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{accountSid}:{authToken}"));
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", auth);

            var payload = new Dictionary<string, string>
            {
                ["To"] = toNumber,
                ["Body"] = messageBody.ToString()
            };

            if (!string.IsNullOrWhiteSpace(messagingServiceSid))
            {
                payload["MessagingServiceSid"] = messagingServiceSid;
            }
            else
            {
                payload["From"] = fromNumber!;
            }

            using var content = new FormUrlEncodedContent(payload);

            using var response = await httpClient.PostAsync(endpoint, content, ct);
            if (response.IsSuccessStatusCode)
            {
                log.Status = "Sent";
                _logger.LogInformation(
                    "SMS alert sent to {Recipient} for detection {DetectionId}",
                    toNumber,
                    detection.Id);
                await _logRepo.AddAsync(log, ct);
                return (true, false);
            }

            var errorText = await response.Content.ReadAsStringAsync(ct);
            log.Status = "Failed";
            log.ErrorMessage = $"SMS API error ({(int)response.StatusCode}): {errorText}";
            await _logRepo.AddAsync(log, ct);
            _logger.LogError(
                "Failed to send SMS alert to {Recipient}. Status: {StatusCode}. Body: {Body}",
                toNumber,
                (int)response.StatusCode,
                errorText);
            return (false, true);
        }
        catch (Exception ex)
        {
            log.Status = "Failed";
            log.ErrorMessage = ex.Message;
            await _logRepo.AddAsync(log, ct);
            _logger.LogError(ex, "Failed to send SMS alert to {Recipient}", toNumber);
            return (false, true);
        }
    }
}
