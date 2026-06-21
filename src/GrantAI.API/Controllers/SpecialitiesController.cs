using GrantAI.Application.Contracts.Responses;
using GrantAI.Application.Specialties;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;

namespace GrantAI.API.Controllers;

/// <summary>
/// Educational program groups ("specialities") that have been imported, e.g. M094.
/// </summary>
[ApiController]
[Route("api/specialities")]
[Produces("application/json")]
public sealed class SpecialitiesController : ControllerBase
{
    private readonly ISpecialtyQueryService _specialties;
    private readonly ProblemDetailsFactory _problemFactory;

    public SpecialitiesController(ISpecialtyQueryService specialties, ProblemDetailsFactory problemFactory)
    {
        _specialties = specialties;
        _problemFactory = problemFactory;
    }

    /// <summary>Lists every educational program group with a compact latest-campaign summary.</summary>
    /// <response code="200">The list of groups (empty if nothing has been imported yet).</response>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<SpecialtySummaryDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<SpecialtySummaryDto>>> GetAll(CancellationToken ct)
        => Ok(await _specialties.GetSpecialtiesAsync(ct));

    /// <summary>Returns the latest-campaign summary for a single group.</summary>
    /// <param name="code">Group or specialty code, e.g. <c>M094</c> (case-insensitive).</param>
    /// <response code="200">The group summary.</response>
    /// <response code="404">No data exists for the given code.</response>
    [HttpGet("{code}")]
    [ProducesResponseType(typeof(SpecialtySummaryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SpecialtySummaryDto>> GetByCode(string code, CancellationToken ct)
    {
        var result = await _specialties.GetSpecialtyAsync(code, ct);
        return result.ToActionResult(this, _problemFactory);
    }
}
