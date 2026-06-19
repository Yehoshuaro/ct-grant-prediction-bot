using GrantAI.Domain.Enums;

namespace GrantAI.Application.Contracts.Responses;

/// <summary>Compact latest-year grant-cutoff summary for one (code, track) pair.</summary>
public sealed record GrantSummaryDto
{
    public string Code { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public MasterType MasterType { get; init; }
    public int ScoreScaleMax { get; init; }
    public int LatestYear { get; init; }
    public int LatestCutoff { get; init; }
    public int LatestGrantsAwarded { get; init; }

    /// <summary>How many intake years are on record for this (code, track).</summary>
    public int YearsOnRecord { get; init; }
}
