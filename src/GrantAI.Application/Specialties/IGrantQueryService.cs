using GrantAI.Application.Contracts.Responses;

namespace GrantAI.Application.Specialties;

/// <summary>
/// Read-side facade for the <i>grant</i> cutoff data. Mirrors the structure of
/// <see cref="ISpecialtyQueryService"/> for the entrance-threshold data: each
/// method applies a cache-aside strategy over the grant repository and the pure
/// grant forecast engine, so callers (controllers, bot) never touch the cache
/// or the database directly.
/// </summary>
public interface IGrantQueryService
{
    /// <summary>All ГОПы with grant data, summarized for their latest year.</summary>
    Task<IReadOnlyList<GrantSummaryDto>> GetAllAsync(CancellationToken ct = default);

    /// <summary>Full grant-cutoff history (every year) for a single ГОП.</summary>
    Task<GrantHistoryDto> GetHistoryAsync(string code, CancellationToken ct = default);

    /// <summary>
    /// Next-intake grant forecast for a ГОП — one entry per master's track on
    /// record for that code (Profile and/or Scientific-Pedagogical).
    /// </summary>
    Task<IReadOnlyList<GrantForecastDto>> GetForecastAsync(string code, CancellationToken ct = default);
}
