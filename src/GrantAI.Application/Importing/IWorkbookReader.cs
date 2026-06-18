namespace GrantAI.Application.Importing;

/// <summary>
/// Reads a spreadsheet into a string-only, layout-agnostic shape. The concrete
/// reader (ClosedXML) lives in Infrastructure; keeping the port here means the
/// import use-case and its tests never depend on a spreadsheet library.
///
/// No row is treated as "the header" here: the published files carry a title
/// row, a blank row and a two-row merged header before the data, so locating the
/// header is the job of the pure <see cref="SheetParser"/>.
/// </summary>
public interface IWorkbookReader
{
    RawWorkbook Read(Stream content, string fileName);
}

/// <summary>A workbook reduced to its sheets and raw string cells.</summary>
public sealed record RawWorkbook(string FileName, IReadOnlyList<RawSheet> Sheets);

/// <summary>
/// One sheet: its name (which often encodes the campaign, e.g. "2024-зима-рус")
/// and every used row as a list of (possibly null) string cells. Parsing and
/// interpretation happen later in pure code.
/// </summary>
public sealed record RawSheet(string Name, IReadOnlyList<IReadOnlyList<string?>> Rows);
