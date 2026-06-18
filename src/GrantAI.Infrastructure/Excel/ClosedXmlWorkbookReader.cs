using ClosedXML.Excel;
using GrantAI.Application.Importing;

namespace GrantAI.Infrastructure.Excel;

/// <summary>
/// Reads .xlsx workbooks with ClosedXML and reduces every sheet to its raw
/// string cells — every used row, with no row treated as a header. Locating the
/// title, the merged two-row header and the data is left to the pure Application
/// code, which keeps the spreadsheet library fully contained in Infrastructure.
/// </summary>
public sealed class ClosedXmlWorkbookReader : IWorkbookReader
{
    public RawWorkbook Read(Stream content, string fileName)
    {
        using var workbook = new XLWorkbook(content);
        var sheets = new List<RawSheet>();

        foreach (var worksheet in workbook.Worksheets)
        {
            var range = worksheet.RangeUsed();
            if (range is null)
                continue; // empty sheet

            var columnCount = range.ColumnCount();
            var rows = new List<IReadOnlyList<string?>>();

            foreach (var row in range.Rows())
            {
                var cells = new List<string?>(columnCount);
                for (var c = 1; c <= columnCount; c++)
                {
                    var cell = row.Cell(c);
                    cells.Add(cell.IsEmpty() ? null : cell.GetString());
                }

                rows.Add(cells);
            }

            sheets.Add(new RawSheet(worksheet.Name, rows));
        }

        return new RawWorkbook(fileName, sheets);
    }
}
