using GrantAI.Application.Common.Results;
using GrantAI.Application.Contracts.Responses;

namespace GrantAI.Application.Specialties;

/// <summary>
/// Read-side facade used by both the API controllers and the Telegram bot.
/// Per-code lookups return <see cref="Result{T}"/> so a missing group is an
/// explicit <see cref="ErrorKind.NotFound"/> rather than a magic
/// "empty-collection" sentinel value.
/// </summary>
public interface ISpecialtyQueryService
{
    Task<IReadOnlyList<SpecialtySummaryDto>> GetSpecialtiesAsync(CancellationToken ct = default);

    Task<Result<SpecialtySummaryDto>> GetSpecialtyAsync(string code, CancellationToken ct = default);

    Task<Result<AdmissionHistoryDto>> GetHistoryAsync(string code, CancellationToken ct = default);

    Task<Result<ComparisonDto>> GetComparisonAsync(string code, CancellationToken ct = default);

    Task<Result<ForecastDto>> GetForecastAsync(string code, CancellationToken ct = default);

    Task<Result<ProbabilityDto>> GetChanceAsync(string code, CancellationToken ct = default);

    Task<StatisticsOverviewDto> GetStatisticsAsync(CancellationToken ct = default);
}
