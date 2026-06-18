using GrantAI.Domain.Enums;

namespace GrantAI.Application.Contracts.Responses;

/// <summary>Compact latest-campaign summary for one educational program group.</summary>
public sealed record SpecialtySummaryDto
{
    public string Code { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;

    public int CampaignCount { get; init; }

    public int LatestYear { get; init; }
    public Season LatestSeason { get; init; }

    public int LatestApplications { get; init; }
    public int LatestParticipants { get; init; }

    /// <summary>Latest campaign's threshold pass rate, as a percentage.</summary>
    public double LatestPassRate { get; init; }
}
