using GrantAI.API.RateLimiting;
using GrantAI.Application.Contracts.Responses;
using GrantAI.Application.Importing.Grants;
using GrantAI.Application.Specialties;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace GrantAI.API.Controllers;

/// <summary>
/// Grant-cutoff endpoints: import published "СПИСОК ОБЛАДАТЕЛЕЙ ОБРАЗОВАТЕЛЬНЫХ
/// ГРАНТОВ" PDFs, query the resulting history by ГОП, and forecast next intake's
/// grant cutoff. This sits alongside (and is intentionally independent of) the
/// existing entrance-threshold endpoints.
/// </summary>
[ApiController]
[Route("api/grants")]
[Produces("application/json")]
public sealed class GrantsController : ControllerBase
{
    private readonly IGrantImportService _import;
    private readonly IGrantQueryService _grants;
    private readonly ILogger<GrantsController> _logger;

    public GrantsController(
        IGrantImportService import,
        IGrantQueryService grants,
        ILogger<GrantsController> logger)
    {
        _import = import;
        _grants = grants;
        _logger = logger;
    }

    /// <summary>Lists every ГОП with a latest-year grant-cutoff summary, one row per master's track.</summary>
    /// <response code="200">The list of summaries (empty if no PDFs have been imported yet).</response>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<GrantSummaryDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<GrantSummaryDto>>> GetAll(CancellationToken ct)
        => Ok(await _grants.GetAllAsync(ct));

    /// <summary>Returns the full grant-cutoff history by year for one ГОП.</summary>
    /// <param name="code">Group code, e.g. <c>M094</c> (case-insensitive).</param>
    /// <response code="200">The history.</response>
    /// <response code="404">No grant data exists for the given code.</response>
    [HttpGet("{code}")]
    [ProducesResponseType(typeof(GrantHistoryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<GrantHistoryDto>> GetHistory(string code, CancellationToken ct)
    {
        var history = await _grants.GetHistoryAsync(code, ct);
        return history.Points.Count == 0
            ? NotFound(SpecialitiesController.NotFoundPayload(code))
            : Ok(history);
    }

    /// <summary>
    /// Forecasts the next intake's grant cutoff for a ГОП: one entry per master's
    /// track on record. The forecast is intentionally an order-of-magnitude
    /// estimate (only 2-3 years of data are usually available).
    /// </summary>
    /// <param name="code">Group code, e.g. <c>M094</c> (case-insensitive).</param>
    /// <response code="200">The forecast(s): one entry per master's track.</response>
    /// <response code="404">No grant data exists for the given code.</response>
    [HttpGet("{code}/forecast")]
    [EnableRateLimiting(RateLimiterExtensions.StrictPolicy)]
    [ProducesResponseType(typeof(IReadOnlyList<GrantForecastDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<GrantForecastDto>>> GetForecast(string code, CancellationToken ct)
    {
        var forecasts = await _grants.GetForecastAsync(code, ct);
        return forecasts.Count == 0
            ? NotFound(SpecialitiesController.NotFoundPayload(code))
            : Ok(forecasts);
    }

    /// <summary>Imports one or more grant-list PDFs; each is parsed, folded and upserted.</summary>
    /// <param name="files">One or more PDF files (multipart/form-data, field <c>files</c>).</param>
    /// <response code="200">Per-file import summaries (year, blocks, inserted/updated).</response>
    /// <response code="400">No files were supplied.</response>
    [HttpPost("import")]
    [EnableRateLimiting(RateLimiterExtensions.StrictPolicy)]
    [ProducesResponseType(typeof(IReadOnlyList<GrantImportResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [RequestSizeLimit(128 * 1024 * 1024)]
    public async Task<ActionResult<IReadOnlyList<GrantImportResultDto>>> Import(
        [FromForm] List<IFormFile> files,
        CancellationToken ct)
    {
        if (files is null || files.Count == 0)
        {
            return BadRequest(new { message = "Upload at least one PDF file in the 'files' field." });
        }

        var results = new List<GrantImportResultDto>(files.Count);
        foreach (var file in files)
        {
            await using var stream = file.OpenReadStream();
            var result = await _import.ImportAsync(stream, file.FileName, ct);
            _logger.LogInformation(
                "Imported grants {File}: year {Year}, {Blocks} blocks, {Inserted} inserted, {Updated} updated",
                result.FileName, result.Year, result.Blocks, result.Inserted, result.Updated);
            results.Add(result);
        }

        return Ok(results);
    }
}
