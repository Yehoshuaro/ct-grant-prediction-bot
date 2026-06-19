namespace GrantAI.Application.Contracts.Responses;

/// <summary>Grant cutoff history for a single educational program group.</summary>
public sealed record GrantHistoryDto
{
    public string Code { get; init; } = string.Empty;
    public string GroupName { get; init; } = string.Empty;
    public IReadOnlyList<GrantCutoffPointDto> Points { get; init; } = [];
}
