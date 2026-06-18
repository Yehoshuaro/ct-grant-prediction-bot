namespace GrantAI.Domain.Enums;

/// <summary>
/// Direction of a time series (pass rate, applications, etc.) as classified
/// by the analytics and forecasting engines.
/// </summary>
public enum TrendDirection
{
    Falling = -1,
    Stable = 0,
    Rising = 1
}
