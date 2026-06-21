using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace GrantAI.Application.Common.Telemetry;

/// <summary>
/// Shared OpenTelemetry sources for the Application layer. The names are
/// consumed by the host's OTel registration; if no exporter is configured the
/// sources are simply silent.
/// </summary>
public static class GrantAiTelemetry
{
    public const string ServiceName = "GrantAI";
    public const string SourceName = "GrantAI.Application";

    public static readonly ActivitySource Activity = new(SourceName);
    public static readonly Meter Meter = new(SourceName);

    public static readonly Counter<long> ForecastsServed =
        Meter.CreateCounter<long>("grantai.forecasts.served", description: "Forecast operations served");

    public static readonly Counter<long> ImportsCompleted =
        Meter.CreateCounter<long>("grantai.imports.completed", description: "Excel/PDF import operations completed");
}
