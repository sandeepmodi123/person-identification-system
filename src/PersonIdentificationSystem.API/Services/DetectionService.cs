using PersonIdentificationSystem.API.DTOs;
using PersonIdentificationSystem.API.Models.Entities;
using PersonIdentificationSystem.API.Repositories;

namespace PersonIdentificationSystem.API.Services;

public interface IDetectionService
{
    Task<PagedResult<DetectionDto>> GetDetectionsAsync(DetectionFilterRequest filter, CancellationToken ct = default);
    Task<DetectionDto?> GetDetectionAsync(Guid id, CancellationToken ct = default);
    Task<DetectionDto?> VerifyDetectionAsync(Guid id, VerifyDetectionRequest request, string verifiedBy, CancellationToken ct = default);
    Task<Detection> CreateDetectionAsync(Guid streamId, Guid? personId, decimal confidence, string? frameImageUrl, string? rawData, CancellationToken ct = default);
}

public class DetectionService : IDetectionService
{
    private readonly IDetectionRepository _detectionRepo;
    private readonly ILogger<DetectionService> _logger;

    public DetectionService(IDetectionRepository detectionRepo, ILogger<DetectionService> logger)
    {
        _detectionRepo = detectionRepo;
        _logger = logger;
    }

    public async Task<PagedResult<DetectionDto>> GetDetectionsAsync(
        DetectionFilterRequest filter, CancellationToken ct = default)
    {
        filter = filter with { Page = Math.Max(1, filter.Page), PageSize = Math.Clamp(filter.PageSize, 1, 100) };
        var (items, total) = await _detectionRepo.GetPagedAsync(filter, ct);
        var dtos = items.Select(MapToDto).ToList();
        return new PagedResult<DetectionDto>(dtos, total, filter.Page, filter.PageSize,
            (int)Math.Ceiling((double)total / filter.PageSize));
    }

    public async Task<DetectionDto?> GetDetectionAsync(Guid id, CancellationToken ct = default)
    {
        var detection = await _detectionRepo.GetWithDetailsAsync(id, ct);
        return detection is null ? null : MapToDto(detection);
    }

    public async Task<DetectionDto?> VerifyDetectionAsync(
        Guid id, VerifyDetectionRequest request, string verifiedBy, CancellationToken ct = default)
    {
        var detection = await _detectionRepo.GetWithDetailsAsync(id, ct);
        if (detection is null) return null;

        detection.IsVerified = true;
        detection.VerificationStatus = request.Status;
        detection.VerifiedBy = verifiedBy;
        detection.VerifiedAt = DateTime.UtcNow;
        detection.VerificationNotes = request.Notes;

        await _detectionRepo.UpdateAsync(detection, ct);
        _logger.LogInformation("Detection {DetectionId} verified as {Status}", id, request.Status);
        return MapToDto(detection);
    }

    public async Task<Detection> CreateDetectionAsync(
        Guid streamId, Guid? personId, decimal confidence,
        string? frameImageUrl, string? rawData, CancellationToken ct = default)
    {
        var detection = new Detection
        {
            StreamId = streamId,
            PersonId = personId,
            ConfidenceScore = confidence,
            FrameImageUrl = frameImageUrl,
            RawMatchData = rawData
        };
        await _detectionRepo.AddAsync(detection, ct);
        _logger.LogInformation("Created detection {DetectionId} for person {PersonId} with confidence {Confidence:P0}",
            detection.Id, personId, confidence);
        return detection;
    }

    private static DetectionDto MapToDto(Detection d) => new(
        d.Id,
        d.StreamId,
        d.Stream?.CameraName ?? string.Empty,
        d.PersonId,
        d.Person?.Name,
        d.Person?.RiskLevel,
        d.ConfidenceScore,
        d.DetectionTimestamp,
        d.FrameImageUrl,
        d.IsVerified,
        d.VerificationStatus,
        d.EmailSent);
}
