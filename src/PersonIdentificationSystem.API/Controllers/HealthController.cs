using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace PersonIdentificationSystem.API.Controllers;

/// <summary>
/// System health check endpoints.
/// </summary>
[ApiController]
[Route("[controller]")]
public class HealthController : ControllerBase
{
    private readonly HealthCheckService _healthCheckService;
    private readonly IConfiguration _config;

    public HealthController(HealthCheckService healthCheckService, IConfiguration config)
    {
        _healthCheckService = healthCheckService;
        _config = config;
    }

    /// <summary>Get overall system health status.</summary>
    [HttpGet]
    [ProducesResponseType(200)]
    [ProducesResponseType(503)]
    public async Task<IActionResult> Get(CancellationToken ct = default)
    {
        var report = await _healthCheckService.CheckHealthAsync(ct);

        var result = new
        {
            status = report.Status.ToString(),
            version = "1.0.0",
            timestamp = DateTime.UtcNow,
            checks = report.Entries.ToDictionary(
                e => e.Key,
                e => e.Value.Status.ToString())
        };

        return report.Status == HealthStatus.Healthy ? Ok(result) : StatusCode(503, result);
    }
}
