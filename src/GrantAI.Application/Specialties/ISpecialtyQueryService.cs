using GrantAI.Application.Contracts.Responses;

namespace GrantAI.Application.Specialties;

/// <summary>
/// Read-side facade used by both the API controllers and the Telegram bot.
/// Each method applies a cache-aside strategy over the repository and the pure
/// analytics / forecasting / probability engines, so callers never touch the
/// cache or the database directly.
/// </summary>
public interface ISpecialtyQueryService
{
    Task<IReadOnlyList<SpecialtySummaryDto>> GetSpecialtiesAsync(CancellationToken ct = default);

    Task<SpecialtySummaryDto?> GetSpecialtyAsync(string code, CancellationToken ct = default);

    Task<AdmissionHistoryDto> GetHistoryAsync(string code, CancellationToken ct = default);

    Task<ComparisonDto> GetComparisonAsync(string code, CancellationToken ct = default);

    Task<ForecastDto> GetForecastAsync(string code, CancellationToken ct = default);

    Task<ProbabilityDto> GetChanceAsync(string code, CancellationToken ct = default);

    Task<StatisticsOverviewDto> GetStatisticsAsync(CancellationToken ct = default);
}
