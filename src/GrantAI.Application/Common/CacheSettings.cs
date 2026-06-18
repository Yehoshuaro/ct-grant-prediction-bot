namespace GrantAI.Application.Common;

/// <summary>
/// Time-to-live configuration for the derived values we cache. Bound from the
/// "Cache" configuration section at startup; the defaults below are used when
/// no configuration is supplied (e.g. in unit tests).
/// </summary>
public sealed class CacheSettings
{
    public int HistoryMinutes { get; set; } = 60;
    public int ForecastMinutes { get; set; } = 120;
    public int ChanceMinutes { get; set; } = 30;
    public int SpecialtiesMinutes { get; set; } = 60;
    public int StatisticsMinutes { get; set; } = 30;

    public TimeSpan History => TimeSpan.FromMinutes(HistoryMinutes);
    public TimeSpan Forecast => TimeSpan.FromMinutes(ForecastMinutes);
    public TimeSpan Chance => TimeSpan.FromMinutes(ChanceMinutes);
    public TimeSpan Specialties => TimeSpan.FromMinutes(SpecialtiesMinutes);
    public TimeSpan Statistics => TimeSpan.FromMinutes(StatisticsMinutes);
}
