using GrantAI.Domain.Entities;
using GrantAI.Domain.Enums;

namespace GrantAI.Application.Importing;

/// <summary>Outcome of parsing a single spreadsheet row.</summary>
public sealed record RowParseResult(AdmissionRecord? Record, string? Error)
{
    public bool Ok => Record is not null;

    public static RowParseResult Success(AdmissionRecord record) => new(record, null);
    public static RowParseResult Failure(string reason) => new(null, reason);
}

/// <summary>
/// Pure mapping of one raw data row onto a normalized <see cref="AdmissionRecord"/>.
/// The campaign (year + season) is supplied by the caller because it comes from
/// the sheet, not the row. Required count fields that are missing or unparseable
/// produce a descriptive error instead of throwing, so a single bad row never
/// aborts an import. The failed-threshold count falls back to participants minus
/// passed when the column is absent.
/// </summary>
public static class AdmissionRowParser
{
    public static bool IsBlank(IReadOnlyList<string?> row)
        => row.All(c => string.IsNullOrWhiteSpace(c));

    public static RowParseResult Parse(
        IReadOnlyList<string?> row, ColumnMap map, int year, Season season, string sourceFile, DateTime importedAtUtc)
    {
        var groupCode = Cell(row, map, AdmissionColumn.GroupCode)?.Trim();
        if (string.IsNullOrWhiteSpace(groupCode))
            return RowParseResult.Failure("Group code is missing.");

        var applications = ParseRequiredInt(row, map, AdmissionColumn.Applications, "applications");
        if (applications.Error is not null) return RowParseResult.Failure(applications.Error);

        var participants = ParseRequiredInt(row, map, AdmissionColumn.Participants, "participants");
        if (participants.Error is not null) return RowParseResult.Failure(participants.Error);

        var passed = ParseRequiredInt(row, map, AdmissionColumn.PassedThreshold, "passed-threshold count");
        if (passed.Error is not null) return RowParseResult.Failure(passed.Error);

        var failedParsed = ValueParsers.ParseInt(Cell(row, map, AdmissionColumn.FailedThreshold));
        var failed = failedParsed ?? Math.Max(participants.Value - passed.Value, 0);

        var name = Cell(row, map, AdmissionColumn.GroupName)?.Trim() ?? string.Empty;

        var record = new AdmissionRecord
        {
            Id = AdmissionRecord.BuildId(year, season, groupCode!),
            Year = year,
            Season = season,
            GroupCode = groupCode!.ToUpperInvariant(),
            GroupName = name,
            Applications = applications.Value,
            Participants = participants.Value,
            PassedThreshold = passed.Value,
            FailedThreshold = failed,
            SourceFile = sourceFile,
            ImportedAtUtc = importedAtUtc
        };

        return RowParseResult.Success(record);
    }

    private static (int Value, string? Error) ParseRequiredInt(
        IReadOnlyList<string?> row, ColumnMap map, AdmissionColumn column, string label)
    {
        var parsed = ValueParsers.ParseInt(Cell(row, map, column));
        return parsed is null
            ? (0, $"Invalid or missing {label}.")
            : (parsed.Value, null);
    }

    private static string? Cell(IReadOnlyList<string?> row, ColumnMap map, AdmissionColumn column)
    {
        var index = map.IndexOf(column);
        if (index is null || index.Value >= row.Count) return null;
        return row[index.Value];
    }
}
