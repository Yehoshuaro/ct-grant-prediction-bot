using GrantAI.API.RateLimiting;
using GrantAI.Application.Contracts.Responses;
using GrantAI.Application.Specialties;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace GrantAI.API.Controllers;

/// <summary>Next-campaign threshold pass-rate forecast for an educational program group.</summary>
[ApiController]
[Route("api/forecast")]
[Produces("application/json")]
[EnableRateLimiting(RateLimiterExtensions.StrictPolicy)]
public sealed class ForecastController : ControllerBase
{
    private readonly ISpecialtyQueryService _specialties;

    public ForecastController(ISpecialtyQueryService specialties) => _specialties = specialties;

    /// <summary>
    /// Forecasts the next campaign's threshold pass rate using linear regression
    /// blended with a weighted moving average, with a confidence interval and the driving factors.
    /// </summary>
    /// <param name="code">Group or specialty code, e.g. <c>M094</c> (case-insensitive).</param>
    /// <response code="200">The forecast.</response>
    /// <response code="404">No data exists for the given code.</response>
    [HttpGet("{code}")]
    [ProducesResponseType(typeof(ForecastDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ForecastDto>> Get(string code, CancellationToken ct)
    {
        var forecast = await _specialties.GetForecastAsync(code, ct);
        return forecast.DataPoints == 0
            ? NotFound(SpecialitiesController.NotFoundPayload(code))
            : Ok(forecast);
    }
}
