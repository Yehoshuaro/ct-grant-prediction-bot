using GrantAI.Application.Contracts.Responses;

namespace GrantAI.Application.Importing.Grants;

/// <summary>
/// Application use-case that imports one grant-list PDF: read, parse blocks,
/// fold winners into per-ГОП cutoff records, upsert and invalidate caches.
/// </summary>
public interface IGrantImportService
{
    Task<GrantImportResultDto> ImportAsync(Stream content, string fileName, CancellationToken ct = default);
}
