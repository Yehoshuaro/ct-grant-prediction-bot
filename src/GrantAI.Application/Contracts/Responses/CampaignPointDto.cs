using GrantAI.Domain.Enums;

namespace GrantAI.Application.Contracts.Responses;

/// <summary>One campaign data point for a group, projected for API/bot output.</summary>
public sealed record CampaignPointDto
{
    public int Year { get; init; }
    public Season Season { get; init; }
    public string Label { get; init; } = string.Empty;

    public int Applications { get; init; }
    public int Participants { get; init; }

    /// <summary>Participants ÷ applications, as a percentage (turn-out for the test).</summary>
    public double ParticipationRate { get; init; }

    public int PassedThreshold { get; init; }
    public int FailedThreshold { get; init; }

    /// <summary>Passed ÷ participants, as a percentage (threshold pass rate).</summary>
    public double PassRate { get; init; }
}
