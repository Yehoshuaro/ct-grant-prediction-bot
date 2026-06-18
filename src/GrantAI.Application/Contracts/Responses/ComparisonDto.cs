using GrantAI.Domain.Enums;

namespace GrantAI.Application.Contracts.Responses;

/// <summary>Season-vs-season comparison for one group.</summary>
public sealed record ComparisonDto
{
    public string Code { get; init; } = string.Empty;
    public IReadOnlyList<SeasonStatsDto> BySeason { get; init; } = [];
    public string Summary { get; init; } = string.Empty;
}

/// <summary>Aggregated statistics for one season (summer or winter).</summary>
public sealed record SeasonStatsDto
{
    public Season Season { get; init; }
    public int CampaignCount { get; init; }
    public double AverageApplications { get; init; }
    public double AverageParticipationRate { get; init; }
    public double AveragePassRate { get; init; }
}
