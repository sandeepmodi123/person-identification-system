namespace PersonIdentificationSystem.API.DTOs;

public record PersonDto(
    Guid Id,
    string Name,
    string? Description,
    string RiskLevel,
    bool IsActive,
    DateTime DateAdded,
    List<PersonPhotoDto> Photos
);

public record CreatePersonRequest(
    string Name,
    string? Description,
    string RiskLevel = "Medium",
    bool IsActive = true
);

public record UpdatePersonRequest(
    string? Name,
    string? Description,
    string? RiskLevel,
    bool? IsActive
);

public record PagedResult<T>(
    List<T> Items,
    int TotalCount,
    int Page,
    int PageSize,
    int TotalPages
);
