namespace GrantAI.Application.Common;

/// <summary>
/// Cache keys for the grant-side reads. Kept in their own namespace prefix so
/// a grant-PDF import only invalidates grant-derived values and leaves the
/// existing admission-threshold cache untouched.
/// </summary>
public static class GrantCacheKeys
{
    public const string Root = "grantai:grant:";

    public static string History(string code) => $"{Root}history:{code.ToUpperInvariant()}";
    public static string Forecast(string code) => $"{Root}forecast:{code.ToUpperInvariant()}";
    public static string List => $"{Root}list";
}
