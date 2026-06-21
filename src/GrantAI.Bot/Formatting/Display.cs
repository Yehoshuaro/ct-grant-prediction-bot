using System.Globalization;
using System.Net;
using GrantAI.Domain.Enums;

namespace GrantAI.Bot.Formatting;

internal static class Display
{
    public static string Trend(TrendDirection trend) => trend switch
    {
        TrendDirection.Rising => "рост",
        TrendDirection.Falling => "снижение",
        _ => "стабильно"
    };

    public static string Season(Season season) => season switch
    {
        Domain.Enums.Season.Summer => "Лето",
        Domain.Enums.Season.Winter => "Зима",
        _ => season.ToString()
    };

    public static string MasterTrack(MasterType track) => track switch
    {
        MasterType.Profile => "Профильная",
        MasterType.ScientificPedagogical => "Научно-педагогическая",
        _ => track.ToString()
    };

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
