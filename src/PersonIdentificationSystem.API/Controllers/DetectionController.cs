using Microsoft.AspNetCore.Mvc;
using PersonIdentificationSystem.API.DTOs;
using PersonIdentificationSystem.API.Services;

namespace PersonIdentificationSystem.API.Controllers;

/// <summary>
/// Manages detection events and face matching.
/// </summary>
[ApiController]
[Route("api")]
[Produces("application/json")]
public class DetectionController : ControllerBase
{
    private readonly IDetectionService _detectionService;
    private readonly IMatchingService _matchingService;

    public DetectionController(IDetectionService detectionService, IMatchingService matchingService)
    {
        _detectionService = detectionService;
        _matchingService = matchingService;
    }

    /// <summary>Get paginated list of detections.</summary>
    [HttpGet("detections")]
    [ProducesResponseType(typeof(PagedResult<DetectionDto>), 200)]
    public async Task<ActionResult<PagedResult<DetectionDto>>> GetDetections(
        [FromQuery] DetectionFilterRequest filter, CancellationToken ct = default)
        => Ok(await _detectionService.GetDetectionsAsync(filter, ct));

    /// <summary>Get detection details.</summary>
    [HttpGet("detections/{id:guid}")]
    [ProducesResponseType(typeof(DetectionDto), 200)]
    [ProducesResponseType(404)]
    public async Task<ActionResult<DetectionDto>> GetDetection(Guid id, CancellationToken ct = default)
    {
        var detection = await _detectionService.GetDetectionAsync(id, ct);
        return detection is null ? NotFound() : Ok(detection);
    }

    /// <summary>Manually verify a detection as true positive or false positive.</summary>
    [HttpPost("detections/{id:guid}/verify")]
    [ProducesResponseType(typeof(DetectionDto), 200)]
    [ProducesResponseType(404)]
    public async Task<ActionResult<DetectionDto>> VerifyDetection(
        Guid id, [FromBody] VerifyDetectionRequest request, CancellationToken ct = default)
    {
        var detection = await _detectionService.VerifyDetectionAsync(id, request, "system", ct);
        return detection is null ? NotFound() : Ok(detection);
    }

    /// <summary>Process a video frame for face matching (called by stream processor).</summary>
    [HttpPost("matching/process-frame")]
    [ProducesResponseType(typeof(ProcessFrameResult), 200)]
    public async Task<ActionResult<ProcessFrameResult>> ProcessFrame(
        [FromBody] ProcessFrameRequest request, CancellationToken ct = default)
        => Ok(await _matchingService.ProcessFrameAsync(request, ct));
}
