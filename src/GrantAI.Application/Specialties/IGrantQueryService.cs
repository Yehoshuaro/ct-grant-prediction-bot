using GrantAI.Application.Common.Results;
using GrantAI.Application.Contracts.Responses;

namespace GrantAI.Application.Specialties;

/// <summary>
/// Read-side facade for the grant cutoff data. Mirrors
/// <see cref="ISpecialtyQueryService"/>; per-code lookups return
/// <see cref="Result{T}"/> so a missing group surfaces explicitly.
/// </summary>
public interface IGrantQueryService
{
    Task<IReadOnlyList<GrantSummaryDto>> GetAllAsync(CancellationToken ct = default);

    Task<Result<GrantHistoryDto>> GetHistoryAsync(string code, CancellationToken ct = default);

    Task<Result<IReadOnlyList<GrantForecastDto>>> GetForecastAsync(string code, CancellationToken ct = default);
}
