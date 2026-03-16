namespace PersonIdentificationSystem.API.Models.Entities;

public class RTSPStream
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string CameraName { get; set; } = string.Empty;
    public string? CameraLocation { get; set; }
    public string RtspUrl { get; set; } = string.Empty;
    public int FrameIntervalSeconds { get; set; } = 5;
    public bool IsActive { get; set; } = true;
    public string Status { get; set; } = "Unknown";
    public DateTime? LastChecked { get; set; }
    public DateTime DateAdded { get; set; } = DateTime.UtcNow;
    public DateTime DateUpdated { get; set; } = DateTime.UtcNow;

    // Navigation
    public ICollection<Detection> Detections { get; set; } = new List<Detection>();
}
