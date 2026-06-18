namespace GrantAI.Application.Contracts.Responses;

/// <summary>Aggregate snapshot of everything currently imported.</summary>
public sealed record StatisticsOverviewDto
{
    public long TotalRecords { get; init; }
    public int TotalGroups { get; init; }
    public int? EarliestYear { get; init; }
    public int? LatestYear { get; init; }

    public long TotalApplications { get; init; }
    public long TotalParticipants { get; init; }
    public long TotalPassed { get; init; }

    /// <summary>Participants ÷ applications across everything, as a percentage.</summary>
    public double OverallParticipationRate { get; init; }

    /// <summary>Passed ÷ participants across everything, as a percentage.</summary>
    public double OverallPassRate { get; init; }
}
