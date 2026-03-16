namespace PersonIdentificationSystem.API.Models.Entities;

public class NotificationSettings
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string[] RecipientEmails { get; set; } = Array.Empty<string>();
    public decimal MinimumConfidence { get; set; } = 0.85m;
    public string[] NotifyOnRiskLevels { get; set; } = { "High", "Critical" };
    public int RateLimitMinutes { get; set; } = 5;
    public bool IsEnabled { get; set; } = true;
    public string? SmtpHost { get; set; }
    public int? SmtpPort { get; set; }
    public bool? SmtpUseTls { get; set; }
    public string? FromEmail { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
