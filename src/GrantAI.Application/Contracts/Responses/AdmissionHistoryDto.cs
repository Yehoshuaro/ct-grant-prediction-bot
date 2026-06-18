using GrantAI.Domain.Enums;

namespace GrantAI.Application.Contracts.Responses;

/// <summary>Full campaign history for a group plus the direction of each series.</summary>
public sealed record AdmissionHistoryDto
{
    public string Code { get; init; } = string.Empty;
    public string GroupName { get; init; } = string.Empty;

    public IReadOnlyList<CampaignPointDto> Points { get; init; } = [];

    public TrendDirection ApplicationsTrend { get; init; }
    public TrendDirection ParticipantsTrend { get; init; }
    public TrendDirection PassRateTrend { get; init; }
}
