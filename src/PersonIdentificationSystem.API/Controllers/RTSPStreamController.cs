using Microsoft.AspNetCore.Mvc;
using PersonIdentificationSystem.API.DTOs;
using PersonIdentificationSystem.API.Services;

namespace PersonIdentificationSystem.API.Controllers;

/// <summary>
/// Manages RTSP camera stream configurations.
/// </summary>
[ApiController]
[Route("api/rtsp-streams")]
[Produces("application/json")]
public class RTSPStreamController : ControllerBase
{
    private readonly IStreamService _streamService;

    public RTSPStreamController(IStreamService streamService)
    {
        _streamService = streamService;
    }

    /// <summary>Get all RTSP streams.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(List<RTSPStreamDto>), 200)]
    public async Task<ActionResult<List<RTSPStreamDto>>> GetStreams(CancellationToken ct = default)
        => Ok(await _streamService.GetAllAsync(ct));

    /// <summary>Get a stream by ID.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(RTSPStreamDto), 200)]
    [ProducesResponseType(404)]
    public async Task<ActionResult<RTSPStreamDto>> GetStream(Guid id, CancellationToken ct = default)
    {
        var stream = await _streamService.GetAsync(id, ct);
        return stream is null ? NotFound() : Ok(stream);
    }

    /// <summary>Add a new RTSP stream.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(RTSPStreamDto), 201)]
    public async Task<ActionResult<RTSPStreamDto>> CreateStream(
        [FromBody] CreateRTSPStreamRequest request, CancellationToken ct = default)
    {
        var stream = await _streamService.CreateAsync(request, ct);
        return CreatedAtAction(nameof(GetStream), new { id = stream.Id }, stream);
    }

    /// <summary>Update an RTSP stream.</summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(RTSPStreamDto), 200)]
    [ProducesResponseType(404)]
    public async Task<ActionResult<RTSPStreamDto>> UpdateStream(
        Guid id, [FromBody] UpdateRTSPStreamRequest request, CancellationToken ct = default)
    {
        var stream = await _streamService.UpdateAsync(id, request, ct);
        return stream is null ? NotFound() : Ok(stream);
    }

    /// <summary>Delete an RTSP stream.</summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(204)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> DeleteStream(Guid id, CancellationToken ct = default)
        => await _streamService.DeleteAsync(id, ct) ? NoContent() : NotFound();

    /// <summary>Test connectivity to an RTSP stream.</summary>
    [HttpPost("{id:guid}/test-connection")]
    [ProducesResponseType(typeof(StreamConnectionTestResult), 200)]
    [ProducesResponseType(404)]
    public async Task<ActionResult<StreamConnectionTestResult>> TestConnection(Guid id, CancellationToken ct = default)
        => Ok(await _streamService.TestConnectionAsync(id, ct));
}
