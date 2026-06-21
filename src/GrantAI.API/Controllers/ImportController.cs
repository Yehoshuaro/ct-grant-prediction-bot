using GrantAI.API.RateLimiting;
using GrantAI.Application.Contracts.Responses;
using GrantAI.Application.Importing;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace GrantAI.API.Controllers;

/// <summary>
/// Uploads admission-statistics Excel files. This endpoint is how data gets into
/// the system; everything else is read-only on top of what is imported here.
/// </summary>
[ApiController]
[Route("api/import")]
[Produces("application/json")]
[EnableRateLimiting(RateLimiterExtensions.StrictPolicy)]
public sealed class ImportController : ControllerBase
{
    private readonly IExcelImportService _import;
    private readonly ILogger<ImportController> _logger;

    public ImportController(IExcelImportService import, ILogger<ImportController> logger)
    {
        _import = import;
        _logger = logger;
    }

    /// <summary>Imports one or more <c>.xlsx</c> files; each is parsed, validated, de-duplicated and upserted.</summary>
    /// <param name="files">One or more Excel files (multipart/form-data).</param>
    /// <response code="200">Per-file import summaries (rows inserted/updated/duplicate/failed).</response>
    /// <response code="400">No files were supplied.</response>
    [HttpPost]
    [ProducesResponseType(typeof(IReadOnlyList<ImportResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [RequestSizeLimit(64 * 1024 * 1024)]
    public async Task<ActionResult<IReadOnlyList<ImportResultDto>>> Import(
        [FromForm] List<IFormFile> files,
        CancellationToken ct)
    {
        if (files is null || files.Count == 0)
        {
            return BadRequest(new { message = "Upload at least one .xlsx file in the 'files' field." });
        }

        var results = new List<ImportResultDto>(files.Count);
        foreach (var file in files)
        {
            await using var stream = file.OpenReadStream();
            var result = await _import.ImportAsync(stream, file.FileName, ct);
            _logger.LogInformation(
                "Imported {File}: {Inserted} inserted, {Updated} updated, {Duplicates} duplicate, {Failed} failed",
                result.FileName, result.Inserted, result.Updated, result.Duplicates, result.Failed);
            results.Add(result);
        }

        return Ok(results);
    }
}
