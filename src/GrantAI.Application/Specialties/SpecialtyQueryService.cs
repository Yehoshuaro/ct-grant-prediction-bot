using GrantAI.Application.Abstractions;
using GrantAI.Application.Analytics;
using GrantAI.Application.Common;
using GrantAI.Application.Contracts.Responses;
using GrantAI.Application.Forecasting;
using GrantAI.Application.Probability;
using Microsoft.Extensions.Logging;

namespace GrantAI.Application.Specialties;

/// <summary>
/// Orchestrates the read side: fetch records from the repository, run the pure
/// engines, and memoise the result in the distributed cache. Cache keys all
/// share <see cref="CacheKeys.Root"/> so an import can drop everything at once.
/// </summary>
public sealed class SpecialtyQueryService : ISpecialtyQueryService
{
    private readonly IAdmissionRepository _repository;
    private readonly ICacheService _cache;
    private readonly IAnalyticsService _analytics;
    private readonly IForecastService _forecast;
    private readonly IProbabilityService _probability;
    private readonly CacheSettings _ttl;
    private readonly ILogger<SpecialtyQueryService> _logger;

    public SpecialtyQueryService(
        IAdmissionRepository repository,
        ICacheService cache,
        IAnalyticsService analytics,
        IForecastService forecast,
        IProbabilityService probability,
        CacheSettings ttl,
        ILogger<SpecialtyQueryService> logger)
    {
        _repository = repository;
        _cache = cache;
        _analytics = analytics;
        _forecast = forecast;
        _probability = probability;
        _ttl = ttl;
        _logger = logger;
    }

    public Task<IReadOnlyList<SpecialtySummaryDto>> GetSpecialtiesAsync(CancellationToken ct = default)
        => _cache.GetOrSetAsync(
            CacheKeys.Specialties,
            async token =>
            {
                var all = await _repository.GetAllAsync(token);
                return _analytics.BuildSpecialtyList(all);
            },
            _ttl.Specialties,
            ct);

    public async Task<SpecialtySummaryDto?> GetSpecialtyAsync(string code, CancellationToken ct = default)
    {
        var list = await GetSpecialtiesAsync(ct);
        return list.FirstOrDefault(s => string.Equals(s.Code, code, StringComparison.OrdinalIgnoreCase));
    }

    public Task<AdmissionHistoryDto> GetHistoryAsync(string code, CancellationToken ct = default)
        => _cache.GetOrSetAsync(
            CacheKeys.History(code),
            async token =>
            {
                var records = await _repository.GetByCodeAsync(code, token);
                return _analytics.BuildHistory(code, records);
            },
            _ttl.History,
            ct);

    public Task<ComparisonDto> GetComparisonAsync(string code, CancellationToken ct = default)
        => _cache.GetOrSetAsync(
            CacheKeys.Comparison(code),
            async token =>
            {
                var records = await _repository.GetByCodeAsync(code, token);
                return _analytics.Compare(code, records);
            },
            _ttl.History,
            ct);

    public Task<ForecastDto> GetForecastAsync(string code, CancellationToken ct = default)
        => _cache.GetOrSetAsync(
            CacheKeys.Forecast(code),
            async token =>
            {
                var records = await _repository.GetByCodeAsync(code, token);
                return _forecast.Forecast(code, records);
            },
            _ttl.Forecast,
            ct);

    public Task<ProbabilityDto> GetChanceAsync(string code, CancellationToken ct = default)
        => _cache.GetOrSetAsync(
            CacheKeys.Chance(code),
            async token =>
            {
                var records = await _repository.GetByCodeAsync(code, token);
                return _probability.Calculate(code, records);
            },
            _ttl.Chance,
            ct);

    public Task<StatisticsOverviewDto> GetStatisticsAsync(CancellationToken ct = default)
        => _cache.GetOrSetAsync(
            CacheKeys.Statistics,
            async token =>
            {
                var all = await _repository.GetAllAsync(token);
                return _analytics.BuildOverview(all);
            },
            _ttl.Statistics,
            ct);
}
