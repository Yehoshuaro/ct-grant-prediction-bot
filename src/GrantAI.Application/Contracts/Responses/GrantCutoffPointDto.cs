using GrantAI.Domain.Enums;

namespace GrantAI.Application.Contracts.Responses;

/// <summary>One historical (year, track) grant-cutoff point for a ГОП.</summary>
public sealed record GrantCutoffPointDto
{
    public int Year { get; init; }
    public MasterType MasterType { get; init; }
    public int ScoreScaleMax { get; init; }
    public int GrantCutoff { get; init; }
    public int GrantsAwarded { get; init; }
    public int MaxScore { get; init; }
    public double AvgScore { get; init; }
}
