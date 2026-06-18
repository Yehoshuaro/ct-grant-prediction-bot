namespace GrantAI.Domain.Entities;

/// <summary>
/// Audit record describing the outcome of one Excel import operation.
/// Persisted so the import history (a required feature) is queryable.
/// </summary>
public sealed class ImportLog
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    public string FileName { get; set; } = string.Empty;
    public DateTime StartedAtUtc { get; set; }
    public DateTime CompletedAtUtc { get; set; }

    public int TotalRows { get; set; }
    public int Inserted { get; set; }
    public int Updated { get; set; }
    public int Duplicates { get; set; }
    public int Failed { get; set; }

    /// <summary>Per-row problems (row number + reason) captured during parsing.</summary>
    public List<ImportRowError> Errors { get; set; } = new();

    public bool Succeeded => Failed == 0;
}

/// <summary>A single row that could not be parsed or validated during import.</summary>
public sealed class ImportRowError
{
    public int RowNumber { get; set; }
    public string Reason { get; set; } = string.Empty;
}
