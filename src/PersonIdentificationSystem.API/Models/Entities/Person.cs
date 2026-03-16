namespace PersonIdentificationSystem.API.Models.Entities;

public class Person
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string RiskLevel { get; set; } = "Medium";
    public bool IsActive { get; set; } = true;
    public DateTime DateAdded { get; set; } = DateTime.UtcNow;
    public DateTime DateUpdated { get; set; } = DateTime.UtcNow;
    public string? CreatedBy { get; set; }
    public string? UpdatedBy { get; set; }

    // Navigation
    public ICollection<PersonPhoto> Photos { get; set; } = new List<PersonPhoto>();
    public ICollection<Detection> Detections { get; set; } = new List<Detection>();
}
