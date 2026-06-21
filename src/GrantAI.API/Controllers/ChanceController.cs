using GrantAI.API.RateLimiting;
using GrantAI.Application.Contracts.Responses;
using GrantAI.Application.Specialties;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.RateLimiting;

namespace GrantAI.API.Controllers;

/// <summary>Probability of clearing the entrance threshold (КТ порог) for a group.</summary>
[ApiController]
[Route("api/chance")]
[Produces("application/json")]
[EnableRateLimiting(RateLimiterExtensions.StrictPolicy)]
public sealed class ChanceController : ControllerBase
{
    private readonly ISpecialtyQueryService _specialties;
    private readonly ProblemDetailsFactory _problemFactory;

    public ChanceController(ISpecialtyQueryService specialties, ProblemDetailsFactory problemFactory)
    {
        _specialties = specialties;
        _problemFactory = problemFactory;
    }

    /// <summary>
    /// Estimates the probability that a participant clears the entrance threshold
    /// for the group, derived from the forecasted pass rate. This is the group's
    /// historical pass rate, not a grant cutoff. Returns the figure with a range
    /// and the factors behind it.
    /// </summary>
    /// <param name="code">Group code, e.g. <c>M094</c> (case-insensitive).</param>
    /// <response code="200">The probability estimate.</response>
    /// <response code="404">No data exists for the given code.</response>
    [HttpGet("{code}")]
    [ProducesResponseType(typeof(ProbabilityDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProbabilityDto>> Get(string code, CancellationToken ct)
    {
        var result = await _specialties.GetChanceAsync(code, ct);
        return result.ToActionResult(this, _problemFactory);
    }
}
