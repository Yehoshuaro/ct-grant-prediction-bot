using System.Globalization;
using System.Text.RegularExpressions;
using GrantAI.Domain.Enums;

namespace GrantAI.Application.Importing;

/// <summary>
/// Tolerant parsers for the messy reality of published spreadsheets: numbers
/// written with spaces or commas, percentage cells that are sometimes blank or
/// even the literal "%", and a campaign (year + season) that lives in the sheet
/// name or title rather than in a column. All number parsers return null on
/// failure so the caller can report a clean error.
/// </summary>
public static partial class ValueParsers
{
    public static Season? ParseSeason(string? value)
    {
        var n = ColumnMapper.Normalize(value);
        if (n.Length == 0) return null;

        if (n.Contains("zim", StringComparison.Ordinal) || n.Contains("winter", StringComparison.Ordinal))
            return Season.Winter;
        if (n.Contains("let", StringComparison.Ordinal) || n.Contains("summer", StringComparison.Ordinal))
            return Season.Summer;

        return null;
    }

    /// <summary>
    /// Extracts a campaign (year + season) from free text such as a sheet name
    /// ("2024-зима-рус") or a title ("… в магистратуру 2024 г. (зима)").
    /// </summary>
    public static (int Year, Season Season)? TryParseCampaign(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;

        var yearMatch = YearRegex().Match(text);
        if (!yearMatch.Success) return null;
        if (!int.TryParse(yearMatch.Value, out var year)) return null;

        var season = ParseSeason(text);
        if (season is null) return null;

        return (year, season.Value);
    }

    public static int? ParseInt(string? value)
    {
        var cleaned = Clean(value);
        if (cleaned.Length == 0) return null;

        if (double.TryParse(cleaned, NumberStyles.Any, CultureInfo.InvariantCulture, out var d))
            return (int)Math.Round(d);

        return null;
    }

    public static double? ParseDouble(string? value)
    {
        var cleaned = Clean(value);
        if (cleaned.Length == 0) return null;

        return double.TryParse(cleaned, NumberStyles.Any, CultureInfo.InvariantCulture, out var d)
            ? d
            : null;
    }

    /// <summary>Trims, drops thousands spaces, and normalizes the decimal comma to a dot.</summary>
    private static string Clean(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;

        var trimmed = value.Trim().Replace(",", ".");
        return trimmed.Replace(" ", string.Empty).Replace("\u00A0", string.Empty);
    }

    [GeneratedRegex(@"20\d{2}")]
    private static partial Regex YearRegex();
}
