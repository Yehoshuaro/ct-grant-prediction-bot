namespace GrantAI.Application.Common;

/// <summary>
/// Central definition of Redis cache keys. A single shared prefix lets an
/// import invalidate every derived value (history, forecast, chance, stats)
/// with one prefix delete.
/// </summary>
public static class CacheKeys
{
    public const string Root = "grantai:";

    public static string History(string code) => $"{Root}history:{code.ToUpperInvariant()}";
    public static string Comparison(string code) => $"{Root}comparison:{code.ToUpperInvariant()}";
    public static string Forecast(string code) => $"{Root}forecast:{code.ToUpperInvariant()}";
    public static string Chance(string code) => $"{Root}chance:{code.ToUpperInvariant()}";
    public static string Specialties => $"{Root}specialties:list";
    public static string Statistics => $"{Root}statistics:overview";
}
