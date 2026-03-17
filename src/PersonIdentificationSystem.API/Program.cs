using PersonIdentificationSystem.API.Configuration;
using PersonIdentificationSystem.API.Middleware;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// ── Logging ───────────────────────────────────────────────────────────────
builder.Host.UseSerilog((ctx, lc) => lc.ConfigureLogging(ctx.Configuration));

// ── Services ──────────────────────────────────────────────────────────────
builder.Services.AddApplicationServices(builder.Configuration);
builder.Services.AddCorsConfiguration(builder.Configuration);
builder.Services.AddSwaggerConfiguration();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

var app = builder.Build();

// ── Middleware Pipeline ────────────────────────────────────────────────────
app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseMiddleware<PerformanceMonitoringMiddleware>();
app.UseMiddleware<LoggingMiddleware>();
string virtualPath = "/"; // Your desired base path
if (!string.IsNullOrEmpty(virtualPath))
{
    // Must be called before UseRouting and UseSwaggerUI
    app.UsePathBase(new PathString(virtualPath));
}
app.UseRouting();
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Person Identification API v1");
        c.RoutePrefix = string.Empty;
    });
}

app.UseCors("AllowAngular");
app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();
app.MapHealthChecks("/health");
// ── Apply Pending Migrations on Startup ───────────────────────────────────
await app.ApplyMigrationsAsync();

app.Run();
