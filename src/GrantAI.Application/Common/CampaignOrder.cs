using GrantAI.Domain.Enums;

namespace GrantAI.Application.Common;

/// <summary>
/// Helpers for ordering admission campaigns on a single chronological axis.
/// Kazakhstan's winter intake (January) precedes the summer intake (August)
/// of the same calendar year, which is why Winter sorts before Summer here.
/// </summary>
public static class CampaignOrder
{
    /// <summary>
    /// A monotonically increasing integer used as the time index for regression
    /// and trend analysis. Two campaigns per year => year * 2 + season offset.
    /// </summary>
    public static int Ordinal(int year, Season season)
        => year * 2 + (season == Season.Winter ? 0 : 1);

    /// <summary>Human friendly label such as "2024 Summer".</summary>
    public static string Label(int year, Season season) => $"{year} {season}";
}
