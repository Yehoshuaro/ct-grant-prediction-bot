namespace GrantAI.Application.Contracts.Responses;

/// <summary>Summary returned by the Excel import engine.</summary>
public sealed record ImportResultDto
{
    public string FileName { get; init; } = string.Empty;
    public int TotalRows { get; init; }
    public int Inserted { get; init; }
    public int Updated { get; init; }
    public int Duplicates { get; init; }
    public int Failed { get; init; }
    public long DurationMs { get; init; }
    public IReadOnlyList<ImportRowErrorDto> Errors { get; init; } = [];
}

public sealed record ImportRowErrorDto
{
    public int RowNumber { get; init; }
    public string Reason { get; init; } = string.Empty;
}
