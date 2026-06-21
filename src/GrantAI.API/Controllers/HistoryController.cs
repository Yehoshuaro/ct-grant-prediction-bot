using GrantAI.Application.Contracts.Responses;
using GrantAI.Application.Specialties;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;

namespace GrantAI.API.Controllers;

/// <summary>Full historical campaign series for an educational program group.</summary>
[ApiController]
[Route("api/history")]
[Produces("application/json")]
public sealed class HistoryController : ControllerBase
{
    private readonly ISpecialtyQueryService _specialties;
    private readonly ProblemDetailsFactory _problemFactory;

    public HistoryController(ISpecialtyQueryService specialties, ProblemDetailsFactory problemFactory)
    {
        _specialties = specialties;
        _problemFactory = problemFactory;
    }

    /// <summary>Returns every imported campaign for a group plus applications, participants and pass-rate trends.</summary>
    /// <param name="code">Group or specialty code, e.g. <c>M094</c> (case-insensitive).</param>
    /// <response code="200">The historical series.</response>
    /// <response code="404">No data exists for the given code.</response>
    [HttpGet("{code}")]
    [ProducesResponseType(typeof(AdmissionHistoryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AdmissionHistoryDto>> Get(string code, CancellationToken ct)
    {
        var result = await _specialties.GetHistoryAsync(code, ct);
        return result.ToActionResult(this, _problemFactory);
    }
}
