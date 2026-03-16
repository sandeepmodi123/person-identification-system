using Serilog;

namespace PersonIdentificationSystem.API.Configuration;

public static class LoggingConfiguration
{
    public static LoggerConfiguration ConfigureLogging(
        this LoggerConfiguration lc, IConfiguration configuration)
    {
        return lc
            .ReadFrom.Configuration(configuration)
            .Enrich.FromLogContext();
    }
}
