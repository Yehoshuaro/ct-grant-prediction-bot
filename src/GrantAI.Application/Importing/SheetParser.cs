using System.Text.RegularExpressions;
using GrantAI.Domain.Enums;

namespace GrantAI.Application.Importing;

/// <summary>A parsed data row together with its 1-based spreadsheet row number.</summary>
public sealed record ParsedRow(int RowNumber, RowParseResult Result);

/// <summary>Outcome of parsing one sheet: either a skip reason, or the campaign and rows.</summary>
public sealed record SheetParseOutcome(
    bool Ok, string? Error, int Year, Season Season, IReadOnlyList<ParsedRow> Rows)
{
    public static SheetParseOutcome Skip(string error) => new(false, error, 0, default, []);
    public static SheetParseOutcome Success(int year, Season season, IReadOnlyList<ParsedRow> rows)
        => new(true, null, year, season, rows);
}

/// <summary>
/// Turns one raw sheet of published КТ statistics into parsed rows. It handles
/// the real-world layout end to end:
///   * the campaign (year + season) is read from the sheet name, falling back to
///     the title row;
///   * the two-row merged header is located by content (the row that carries the
///     code, applications and participants headers);
///   * only genuine ГОП rows (codes like "M094") are treated as data, so the
///     title, blank rows, sub-header row, the special "ВЭ …" line and the
///     "ИТОГО"/"Всего" total rows are skipped automatically.
/// </summary>
public static partial class SheetParser
{
    private const int MaxHeaderScanRows = 30;

    public static SheetParseOutcome Parse(RawSheet sheet, string sourceFile, DateTime importedAtUtc)
    {
        var campaign = ValueParsers.TryParseCampaign(sheet.Name)
                       ?? ValueParsers.TryParseCampaign(FirstText(sheet.Rows));
        if (campaign is null)
            return SheetParseOutcome.Skip(
                $"Sheet '{sheet.Name}' skipped: could not determine year/season from the sheet name or title.");

        var headerIndex = FindHeaderRow(sheet.Rows);
        if (headerIndex < 0)
            return SheetParseOutcome.Skip(
                $"Sheet '{sheet.Name}' skipped: could not locate the column header row.");

        var subHeader = headerIndex + 1 < sheet.Rows.Count ? sheet.Rows[headerIndex + 1] : null;
        var map = ColumnMapper.Resolve(sheet.Rows[headerIndex], subHeader);

        var missing = ColumnMapper.MissingRequired(map);
        if (missing.Count > 0)
            return SheetParseOutcome.Skip(
                $"Sheet '{sheet.Name}' skipped: missing required columns {string.Join(", ", missing)}.");

        var codeIndex = map.IndexOf(AdmissionColumn.GroupCode)!.Value;
        var rows = new List<ParsedRow>();

        for (var r = headerIndex + 1; r < sheet.Rows.Count; r++)
        {
            var row = sheet.Rows[r];
            var code = codeIndex < row.Count ? row[codeIndex]?.Trim() : null;

            // Only real ГОП rows are data; everything else (sub-header, totals,
            // special non-ГОП lines, blanks) is silently skipped.
            if (string.IsNullOrWhiteSpace(code) || !GroupCodeRegex().IsMatch(code))
                continue;

            var rowNumber = r + 1; // 1-based, matching what a user sees in Excel
            var parsed = AdmissionRowParser.Parse(row, map, campaign.Value.Year, campaign.Value.Season, sourceFile, importedAtUtc);
            rows.Add(new ParsedRow(rowNumber, parsed));
        }

        return SheetParseOutcome.Success(campaign.Value.Year, campaign.Value.Season, rows);
    }

    /// <summary>Finds the header row: one carrying the code, applications and participants headers.</summary>
    private static int FindHeaderRow(IReadOnlyList<IReadOnlyList<string?>> rows)
    {
        var scanLimit = Math.Min(rows.Count, MaxHeaderScanRows);
        for (var r = 0; r < scanLimit; r++)
        {
            var normalized = rows[r].Select(ColumnMapper.Normalize).ToList();
            var hasCode = normalized.Any(n => n.Contains("kod", StringComparison.Ordinal) || n == "gop");
            var hasApplications = normalized.Any(n => n.Contains("zayavleni", StringComparison.Ordinal));
            var hasParticipants = normalized.Any(n => n.Contains("uchastnik", StringComparison.Ordinal));

            if (hasCode && hasApplications && hasParticipants)
                return r;
        }

        return -1;
    }

    private static string? FirstText(IReadOnlyList<IReadOnlyList<string?>> rows)
    {
        foreach (var row in rows.Take(3))
            foreach (var cell in row)
                if (!string.IsNullOrWhiteSpace(cell))
                    return cell;
        return null;
    }

    [GeneratedRegex(@"^M\d", RegexOptions.IgnoreCase)]
    private static partial Regex GroupCodeRegex();
}
