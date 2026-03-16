namespace PersonIdentificationSystem.API.Models.Entities;

public class PersonPhoto
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PersonId { get; set; }
    public string PhotoUrl { get; set; } = string.Empty;
    public decimal? QualityScore { get; set; }
    public bool IsPrimary { get; set; }
    public DateTime UploadDate { get; set; } = DateTime.UtcNow;
    public long? FileSizeBytes { get; set; }
    public string? OriginalFilename { get; set; }

    // Navigation
    public Person Person { get; set; } = null!;
}
