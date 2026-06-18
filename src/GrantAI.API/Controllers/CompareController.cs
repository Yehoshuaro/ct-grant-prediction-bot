using GrantAI.Application.Contracts.Responses;
using GrantAI.Application.Specialties;
using Microsoft.AspNetCore.Mvc;

namespace GrantAI.API.Controllers;

/// <summary>Season-vs-season comparison for an educational program group.</summary>
[ApiController]
[Route("api/compare")]
[Produces("application/json")]
public sealed class CompareController : ControllerBase
{
    private readonly ISpecialtyQueryService _specialties;

    public CompareController(ISpecialtyQueryService specialties) => _specialties = specialties;

    /// <summary>
    /// Compares summer vs winter intakes for a group (average applications,
    /// participation rate and threshold pass rate), with a short textual summary.
    /// </summary>
    /// <param name="code">Group code, e.g. <c>M094</c> (case-insensitive).</param>
    /// <response code="200">The comparison.</response>
    /// <response code="404">No data exists for the given code.</response>
    [HttpGet("{code}")]
    [ProducesResponseType(typeof(ComparisonDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ComparisonDto>> Get(string code, CancellationToken ct)
    {
        var comparison = await _specialties.GetComparisonAsync(code, ct);
        return comparison.BySeason.Count == 0
            ? NotFound(SpecialitiesController.NotFoundPayload(code))
            : Ok(comparison);
    }
}
