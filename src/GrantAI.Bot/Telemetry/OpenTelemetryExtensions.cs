using GrantAI.Application.Common.Telemetry;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace GrantAI.Bot.Telemetry;

/// <summary>
/// Configures OpenTelemetry tracing + metrics for the bot. No ASP.NET Core
/// instrumentation: the bot has no HTTP surface. Telegram client traffic is
/// covered by the HttpClient instrumentation. The OTLP exporter only attaches
/// when <c>OTEL_EXPORTER_OTLP_ENDPOINT</c> is set.
/// </summary>
public static class OpenTelemetryExtensions
{
    public static IServiceCollection AddGrantAiObservability(
        this IServiceCollection services, IConfiguration configuration)
    {
        var otlpEndpoint = configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]
                           ?? Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT");

        services.AddOpenTelemetry()
            .ConfigureResource(_ => _.AddService(GrantAiTelemetry.ServiceName, serviceInstanceId: "bot"))
            .WithTracing(tracing =>
            {
                tracing
                    .AddSource(GrantAiTelemetry.SourceName)
                    .AddHttpClientInstrumentation();

                if (!string.IsNullOrWhiteSpace(otlpEndpoint))
                    tracing.AddOtlpExporter(opt => opt.Endpoint = new Uri(otlpEndpoint));
            })
            .WithMetrics(metrics =>
            {
                metrics
                    .AddMeter(GrantAiTelemetry.SourceName)
                    .AddHttpClientInstrumentation()
                    .AddRuntimeInstrumentation();

                if (!string.IsNullOrWhiteSpace(otlpEndpoint))
                    metrics.AddOtlpExporter(opt => opt.Endpoint = new Uri(otlpEndpoint));
            });

        return services;
    }
}
