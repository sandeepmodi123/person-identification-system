using Microsoft.OpenApi.Models;

namespace PersonIdentificationSystem.API.Configuration;

public static class SwaggerConfiguration
{
    public static IServiceCollection AddSwaggerConfiguration(this IServiceCollection services)
    {
        services.AddSwaggerGen(c =>
        {
            c.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "Person Identification System API",
                Version = "v1",
                Description = "REST API for managing persons, RTSP streams, detections, and notifications in the Person Identification System.",
                Contact = new OpenApiContact { Name = "Admin", Email = "admin@yoursystem.com" }
            });

            // Include XML comments
            var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
            var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
            if (File.Exists(xmlPath))
                c.IncludeXmlComments(xmlPath);
        });

        return services;
    }
}
