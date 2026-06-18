using GrantAI.Application.Contracts.Responses;

namespace GrantAI.Application.Importing;

/// <summary>
/// Application use-case that imports one Excel file: read, map columns, parse,
/// validate, de-duplicate, upsert, log and invalidate caches.
/// </summary>
public interface IExcelImportService
{
    Task<ImportResultDto> ImportAsync(Stream content, string fileName, CancellationToken ct = default);
}
