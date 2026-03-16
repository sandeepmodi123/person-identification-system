namespace PersonIdentificationSystem.API.Models.Entities;

public class NotificationLog
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? DetectionId { get; set; }
    public string RecipientEmail { get; set; } = string.Empty;
    public DateTime SentTimestamp { get; set; } = DateTime.UtcNow;
    public string Status { get; set; } = "Pending";
    public string? ErrorMessage { get; set; }
    public int RetryCount { get; set; }
    public string? MessageId { get; set; }

    // Navigation
    public Detection? Detection { get; set; }
}
