using System.Diagnostics;
using FluentValidation;
using GrantAI.Application.Abstractions;
using GrantAI.Application.Common;
using GrantAI.Application.Contracts.Responses;
using GrantAI.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace GrantAI.Application.Importing;

/// <summary>
/// The Excel import engine. Steps:
///   1. Read the workbook into raw string rows (via <see cref="IWorkbookReader"/>).
///   2. Parse each sheet with <see cref="SheetParser"/>, which finds the campaign
///      and header, and yields one parse result per genuine ГОП row.
///   3. Validate every parsed row, collecting per-row errors.
///   4. De-duplicate within the file by natural key; classify the rest as
///      inserts or updates against what already exists in the database.
///   5. Bulk-upsert, persist an <see cref="ImportLog"/>, and invalidate caches.
///
/// Duplicate handling is twofold: rows sharing a natural key inside one file are
/// reported as duplicates, while re-importing rows already in the database is an
/// idempotent update (the key is the Mongo _id), so nothing is ever stored twice.
/// </summary>
public sealed class ExcelImportService : IExcelImportService
{
    private readonly IWorkbookReader _reader;
    private readonly IAdmissionRepository _repository;
    private readonly IImportLogRepository _importLogs;
    private readonly ICacheService _cache;
    private readonly IValidator<AdmissionRecord> _validator;
    private readonly ILogger<ExcelImportService> _logger;

    public ExcelImportService(
        IWorkbookReader reader,
        IAdmissionRepository repository,
        IImportLogRepository importLogs,
        ICacheService cache,
        IValidator<AdmissionRecord> validator,
        ILogger<ExcelImportService> logger)
    {
        _reader = reader;
        _repository = repository;
        _importLogs = importLogs;
        _cache = cache;
        _validator = validator;
        _logger = logger;
    }

    public async Task<ImportResultDto> ImportAsync(Stream content, string fileName, CancellationToken ct = default)
    {
        var startedAt = DateTime.UtcNow;
        var stopwatch = Stopwatch.StartNew();
        _logger.LogInformation("Import started for {File}", fileName);

        var errors = new List<ImportRowErrorDto>();
        var byKey = new Dictionary<string, AdmissionRecord>(StringComparer.Ordinal);
        var totalRows = 0;
        var duplicates = 0;
        var failed = 0;

        RawWorkbook workbook;
        try
        {
            workbook = _reader.Read(content, fileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to read workbook {File}", fileName);
            return Failure(fileName, $"Could not read workbook: {ex.Message}", stopwatch);
        }

        foreach (var sheet in workbook.Sheets)
        {
            var outcome = SheetParser.Parse(sheet, fileName, startedAt);
            if (!outcome.Ok)
            {
                // Report the skip against the top of the sheet so it surfaces in the result.
                errors.Add(new ImportRowErrorDto { RowNumber = 0, Reason = outcome.Error! });
                continue;
            }

            foreach (var (rowNumber, result) in outcome.Rows)
            {
                totalRows++;

                if (!result.Ok)
                {
                    failed++;
                    errors.Add(new ImportRowErrorDto { RowNumber = rowNumber, Reason = result.Error! });
                    continue;
                }

                var validation = _validator.Validate(result.Record!);
                if (!validation.IsValid)
                {
                    failed++;
                    errors.Add(new ImportRowErrorDto
                    {
                        RowNumber = rowNumber,
                        Reason = string.Join("; ", validation.Errors.Select(e => e.ErrorMessage))
                    });
                    continue;
                }

                if (!byKey.TryAdd(result.Record!.Id, result.Record!))
                    duplicates++; // same natural key seen earlier in this file
            }
        }

        var records = byKey.Values.ToList();
        var inserted = 0;
        var updated = 0;

        if (records.Count > 0)
        {
            var existing = await _repository.GetExistingIdsAsync(byKey.Keys.ToList(), ct);
            updated = existing.Count;
            inserted = records.Count - updated;

            await _repository.BulkUpsertAsync(records, ct);
            await _cache.RemoveByPrefixAsync(CacheKeys.Root, ct);
            _logger.LogInformation(
                "Cache invalidated after importing {File} ({Records} records)", fileName, records.Count);
        }

        stopwatch.Stop();

        var log = new ImportLog
        {
            FileName = fileName,
            StartedAtUtc = startedAt,
            CompletedAtUtc = DateTime.UtcNow,
            TotalRows = totalRows,
            Inserted = inserted,
            Updated = updated,
            Duplicates = duplicates,
            Failed = failed,
            Errors = errors.Select(e => new ImportRowError { RowNumber = e.RowNumber, Reason = e.Reason }).ToList()
        };
        await _importLogs.AddAsync(log, ct);

        _logger.LogInformation(
            "Import finished for {File}: {Rows} rows, {Inserted} inserted, {Updated} updated, {Dupes} duplicates, {Failed} failed in {Ms} ms",
            fileName, totalRows, inserted, updated, duplicates, failed, stopwatch.ElapsedMilliseconds);

        return new ImportResultDto
        {
            FileName = fileName,
            TotalRows = totalRows,
            Inserted = inserted,
            Updated = updated,
            Duplicates = duplicates,
            Failed = failed,
            DurationMs = stopwatch.ElapsedMilliseconds,
            Errors = errors
        };
    }

    private static ImportResultDto Failure(string fileName, string reason, Stopwatch stopwatch)
    {
        stopwatch.Stop();
        return new ImportResultDto
        {
            FileName = fileName,
            DurationMs = stopwatch.ElapsedMilliseconds,
            Failed = 1,
            Errors = [new ImportRowErrorDto { RowNumber = 0, Reason = reason }]
        };
    }
}
