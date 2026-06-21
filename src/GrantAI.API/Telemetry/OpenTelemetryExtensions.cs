using GrantAI.Application.Common.Telemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace GrantAI.API.Telemetry;

/// <summary>
/// Configures OpenTelemetry tracing + metrics for the API. The OTLP exporter
/// is only attached when <c>OTEL_EXPORTER_OTLP_ENDPOINT</c> is set; otherwise
/// the pipelines are configured but stay silent, so local runs work without a
/// collector. Custom <see cref="GrantAiTelemetry.SourceName"/> source emits
/// the Application-layer activities and counters.
/// </summary>
public static class OpenTelemetryExtensions
{
    public static IServiceCollection AddGrantAiObservability(
        this IServiceCollection services, IConfiguration configuration, string roleName)
    {
        var otlpEndpoint = configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]
                           ?? Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT");

        var resource = ResourceBuilder.CreateDefault()
            .AddService(serviceName: GrantAiTelemetry.ServiceName, serviceInstanceId: roleName);

        services.AddOpenTelemetry()
            .ConfigureResource(_ => _.AddService(GrantAiTelemetry.ServiceName, serviceInstanceId: roleName))
            .WithTracing(tracing =>
            {
                tracing
                    .AddSource(GrantAiTelemetry.SourceName)
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation();

                if (!string.IsNullOrWhiteSpace(otlpEndpoint))
                    tracing.AddOtlpExporter(opt => opt.Endpoint = new Uri(otlpEndpoint));
            })
            .WithMetrics(metrics =>
            {
                metrics
                    .AddMeter(GrantAiTelemetry.SourceName)
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddRuntimeInstrumentation();

                if (!string.IsNullOrWhiteSpace(otlpEndpoint))
                    metrics.AddOtlpExporter(opt => opt.Endpoint = new Uri(otlpEndpoint));
            });

        return services;
    }
}
