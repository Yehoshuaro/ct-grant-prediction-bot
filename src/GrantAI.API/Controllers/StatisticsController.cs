using GrantAI.Application.Contracts.Responses;
using GrantAI.Application.Specialties;
using Microsoft.AspNetCore.Mvc;

namespace GrantAI.API.Controllers;

/// <summary>Aggregate snapshot across everything currently imported.</summary>
[ApiController]
[Route("api/statistics")]
[Produces("application/json")]
public sealed class StatisticsController : ControllerBase
{
    private readonly ISpecialtyQueryService _specialties;

    public StatisticsController(ISpecialtyQueryService specialties) => _specialties = specialties;

    /// <summary>Returns totals, year span and averages over all imported campaigns.</summary>
    /// <response code="200">The overview (zeroed when nothing is imported yet).</response>
    [HttpGet]
    [ProducesResponseType(typeof(StatisticsOverviewDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<StatisticsOverviewDto>> Get(CancellationToken ct)
        => Ok(await _specialties.GetStatisticsAsync(ct));
}
