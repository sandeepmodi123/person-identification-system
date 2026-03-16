using System.Diagnostics;

namespace PersonIdentificationSystem.API.Middleware;

public class PerformanceMonitoringMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<PerformanceMonitoringMiddleware> _logger;
    private const int SlowRequestThresholdMs = 1000;

    public PerformanceMonitoringMiddleware(RequestDelegate next, ILogger<PerformanceMonitoringMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var sw = Stopwatch.StartNew();
        await _next(context);
        sw.Stop();

        if (sw.ElapsedMilliseconds > SlowRequestThresholdMs)
        {
            _logger.LogWarning("Slow request: {Method} {Path} completed in {ElapsedMs}ms",
                context.Request.Method, context.Request.Path, sw.ElapsedMilliseconds);
        }
    }
}
