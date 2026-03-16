using Microsoft.EntityFrameworkCore;
using PersonIdentificationSystem.API.Infrastructure;
using PersonIdentificationSystem.API.Models.Entities;
using PersonIdentificationSystem.API.Repositories;
using PersonIdentificationSystem.API.Services;

namespace PersonIdentificationSystem.API.Configuration;

public static class DependencyInjection
{
    public static IServiceCollection AddApplicationServices(
        this IServiceCollection services, IConfiguration configuration)
    {
        // ── Database ──────────────────────────────────────────────────────
        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("DefaultConnection")));

        // ── Repositories ──────────────────────────────────────────────────
        services.AddScoped<IPersonRepository, PersonRepository>();
        services.AddScoped<IStreamRepository, StreamRepository>();
        services.AddScoped<IDetectionRepository, DetectionRepository>();
        services.AddScoped<INotificationLogRepository, NotificationLogRepository>();
        services.AddScoped<IRepository<PersonPhoto>, BasePersonPhotoRepository>();

        // ── Services ──────────────────────────────────────────────────────
        services.AddScoped<IPersonService, PersonService>();
        services.AddScoped<IStreamService, StreamService>();
        services.AddScoped<IDetectionService, DetectionService>();
        services.AddScoped<IMatchingService, MatchingService>();
        services.AddScoped<INotificationService, NotificationService>();

        // ── Python Client ─────────────────────────────────────────────────
        services.AddHttpClient<IPythonFaceRecognitionClient, PythonFaceRecognitionClient>(client =>
        {
            var baseUrl = configuration["PythonService:BaseUrl"] ?? "http://localhost:8000";
            client.BaseAddress = new Uri(baseUrl);
            var timeout = configuration.GetValue<int>("PythonService:TimeoutSeconds", 30);
            client.Timeout = TimeSpan.FromSeconds(timeout);
        });

        // ── Redis ─────────────────────────────────────────────────────────
        services.AddStackExchangeRedisCache(options =>
            options.Configuration = configuration["Redis:ConnectionString"]);

        // ── Health Checks ─────────────────────────────────────────────────
        services.AddHealthChecks()
            .AddDbContextCheck<ApplicationDbContext>(name: "database")
            .AddRedis(configuration["Redis:ConnectionString"]!, name: "redis");

        // ── Static Files (for uploads) ────────────────────────────────────
        services.AddDirectoryBrowser();

        return services;
    }

    public static async Task ApplyMigrationsAsync(this IApplicationBuilder app)
    {
        using var scope = app.ApplicationServices.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<ApplicationDbContext>>();

        try
        {
            await context.Database.MigrateAsync();
            logger.LogInformation("Database migrations applied successfully.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to apply database migrations.");
            throw;
        }
    }
}

// Concrete repo for PersonPhoto (to avoid open generic DI issues)
public class BasePersonPhotoRepository : BaseRepository<PersonPhoto>
{
    public BasePersonPhotoRepository(ApplicationDbContext context) : base(context) { }
}
