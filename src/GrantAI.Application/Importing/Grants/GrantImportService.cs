using System.Diagnostics;
using GrantAI.Application.Abstractions;
using GrantAI.Application.Common;
using GrantAI.Application.Contracts.Responses;
using Microsoft.Extensions.Logging;

namespace GrantAI.Application.Importing.Grants;

/// <summary>
/// The grant-PDF import engine. Steps:
///   1. Read the PDF into positioned lines (via <see cref="IGrantPdfReader"/>).
///   2. Parse blocks with <see cref="GrantPdfParser"/>, which detects sections
///      (профильная / научно-педагогическая), block headers and winner rows.
///   3. Fold each block into a single <see cref="Domain.Entities.GrantCutoffRecord"/>
///      whose cutoff is the minimum winner score.
///   4. Bulk-upsert the records and invalidate the grant-side caches.
///
/// Idempotency is provided by the natural-key <c>_id</c>: re-importing the same
/// PDF updates the same documents in place rather than producing duplicates.
/// </summary>
public sealed class GrantImportService : IGrantImportService
{
    private readonly IGrantPdfReader _reader;
    private readonly IGrantCutoffRepository _repository;
    private readonly ICacheService _cache;
    private readonly ILogger<GrantImportService> _logger;

    public GrantImportService(
        IGrantPdfReader reader,
        IGrantCutoffRepository repository,
        ICacheService cache,
        ILogger<GrantImportService> logger)
    {
        _reader = reader;
        _repository = repository;
        _cache = cache;
        _logger = logger;
    }

    public async Task<GrantImportResultDto> ImportAsync(Stream content, string fileName, CancellationToken ct = default)
    {
        var startedAt = DateTime.UtcNow;
        var stopwatch = Stopwatch.StartNew();
        _logger.LogInformation("Grant import started for {File}", fileName);

        RawGrantPdf raw;
        try
        {
            raw = _reader.Read(content, fileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to read grant PDF {File}", fileName);
            stopwatch.Stop();
            return new GrantImportResultDto
            {
                FileName = fileName,
                DurationMs = stopwatch.ElapsedMilliseconds,
                Error = $"Could not read PDF: {ex.Message}"
            };
        }

        var outcome = GrantPdfParser.Parse(raw);
        if (!outcome.Ok)
        {
            stopwatch.Stop();
            _logger.LogWarning("Grant import skipped {File}: {Reason}", fileName, outcome.Error);
            return new GrantImportResultDto
            {
                FileName = fileName,
                DurationMs = stopwatch.ElapsedMilliseconds,
                Error = outcome.Error
            };
        }

        var records = GrantPdfParser.ToRecords(outcome, fileName, startedAt);

        var inserted = 0;
        var updated = 0;
        if (records.Count > 0)
        {
            // The Mongo upsert reports back whether each id existed before.
            var existing = await GetExistingIdsAsync(records.Select(r => r.Id).ToList(), ct);
            updated = existing.Count;
            inserted = records.Count - updated;

            await _repository.BulkUpsertAsync(records, ct);
            await _cache.RemoveByPrefixAsync(GrantCacheKeys.Root, ct);
            _logger.LogInformation(
                "Grant cache invalidated after importing {File} ({Records} records)", fileName, records.Count);
        }

        stopwatch.Stop();
        _logger.LogInformation(
            "Grant import finished for {File}: year {Year}, {Blocks} blocks, {Inserted} inserted, {Updated} updated in {Ms} ms",
            fileName, outcome.Year, outcome.Blocks.Count, inserted, updated, stopwatch.ElapsedMilliseconds);

        return new GrantImportResultDto
        {
            FileName = fileName,
            Year = outcome.Year,
            Blocks = outcome.Blocks.Count,
            Inserted = inserted,
            Updated = updated,
            DurationMs = stopwatch.ElapsedMilliseconds
        };
    }

    /// <summary>
    /// Existing-id detection mirrors the Excel import: a single lookup, no
    /// per-record round trips. The grant repository does not expose this on the
    /// port (kept smaller than its admission counterpart) so we read by code
    /// once and intersect; the volume is at most a few hundred records per PDF.
    /// </summary>
    private async Task<HashSet<string>> GetExistingIdsAsync(IReadOnlyCollection<string> ids, CancellationToken ct)
    {
        if (ids.Count == 0) return new HashSet<string>(StringComparer.Ordinal);

        var all = await _repository.GetAllAsync(ct);
        var known = new HashSet<string>(all.Select(r => r.Id), StringComparer.Ordinal);
        known.IntersectWith(ids);
        return known;
    }
}
