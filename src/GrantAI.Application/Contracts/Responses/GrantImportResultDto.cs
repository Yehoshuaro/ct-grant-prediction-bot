namespace GrantAI.Application.Contracts.Responses;

/// <summary>Summary returned by the grant-PDF import engine.</summary>
public sealed record GrantImportResultDto
{
    public string FileName { get; init; } = string.Empty;

    /// <summary>Intake year extracted from the document (e.g. 2025 for 2025–2026).</summary>
    public int Year { get; init; }

    /// <summary>Number of ГОП blocks recognised in the PDF.</summary>
    public int Blocks { get; init; }

    /// <summary>How many cutoff records were inserted or updated.</summary>
    public int Inserted { get; init; }
    public int Updated { get; init; }

    public long DurationMs { get; init; }

    /// <summary>Reason this import was rejected, if any (e.g. unreadable PDF).</summary>
    public string? Error { get; init; }
}
