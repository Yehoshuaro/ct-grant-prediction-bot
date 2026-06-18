using System.Globalization;
using System.Net;
using GrantAI.Domain.Enums;

namespace GrantAI.Bot.Formatting;

/// <summary>
/// Small presentation helpers shared by the message formatters: enum-to-text,
/// trend arrows, percentages and HTML escaping for Telegram's HTML parse mode.
/// </summary>
internal static class Display
{
    public static string Trend(TrendDirection trend) => trend switch
    {
        TrendDirection.Rising => "📈 rising",
        TrendDirection.Falling => "📉 falling",
        _ => "➡️ stable"
    };

    public static string Season(Season season) => season switch
    {
        Domain.Enums.Season.Summer => "Summer",
        Domain.Enums.Season.Winter => "Winter",
        _ => season.ToString()
    };

    /// <summary>Escapes the characters that matter for Telegram HTML mode.</summary>
    public static string Escape(string? value) => WebUtility.HtmlEncode(value ?? string.Empty);

    public static string Percent(double value) =>
        value % 1 == 0
            ? value.ToString("0", CultureInfo.InvariantCulture) + "%"
            : value.ToString("0.0", CultureInfo.InvariantCulture) + "%";

    public static string Num(double value) =>
        value % 1 == 0
            ? value.ToString("0", CultureInfo.InvariantCulture)
            : value.ToString("0.0", CultureInfo.InvariantCulture);
}
