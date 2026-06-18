using Serilog;
using Serilog.Events;

namespace GrantAI.Infrastructure.Logging;

/// <summary>
/// Shared Serilog configuration used by both the API and the bot host. Logs to
/// the console and to a daily rolling file under <c>logs/</c>. Centralising it
/// keeps logging consistent across the two entry points.
/// </summary>
public static class SerilogConfigurator
{
    public static void Configure(LoggerConfiguration configuration)
    {
        configuration
            .MinimumLevel.Information()
            .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
            .MinimumLevel.Override("Microsoft.Hosting.Lifetime", LogEventLevel.Information)
            .MinimumLevel.Override("System.Net.Http.HttpClient", LogEventLevel.Warning)
            .Enrich.FromLogContext()
            .WriteTo.Console(
                outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}")
            .WriteTo.File(
                path: "logs/grantai-.log",
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 14,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}");
    }
}
