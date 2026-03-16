namespace PersonIdentificationSystem.API.Models.Entities;

public class Detection
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid StreamId { get; set; }
    public Guid? PersonId { get; set; }
    public decimal ConfidenceScore { get; set; }
    public DateTime DetectionTimestamp { get; set; } = DateTime.UtcNow;
    public string? FrameImageUrl { get; set; }
    public bool IsVerified { get; set; }
    public string? VerificationStatus { get; set; }
    public string? VerifiedBy { get; set; }
    public DateTime? VerifiedAt { get; set; }
    public string? VerificationNotes { get; set; }
    public bool EmailSent { get; set; }
    public string? RawMatchData { get; set; }

    // Navigation
    public RTSPStream Stream { get; set; } = null!;
    public Person? Person { get; set; }
    public ICollection<NotificationLog> NotificationLogs { get; set; } = new List<NotificationLog>();
}
