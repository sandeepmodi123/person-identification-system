namespace PersonIdentificationSystem.API.DTOs;

public record PersonPhotoDto(
    Guid Id,
    Guid PersonId,
    string PhotoUrl,
    decimal? QualityScore,
    bool IsPrimary,
    DateTime UploadDate,
    string? OriginalFilename
);
