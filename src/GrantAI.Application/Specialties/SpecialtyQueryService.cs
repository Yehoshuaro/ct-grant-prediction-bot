using GrantAI.Application.Abstractions;
using GrantAI.Application.Analytics;
using GrantAI.Application.Common;
using GrantAI.Application.Common.Results;
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
                var all = await _repository.GetAllAsync(token).ConfigureAwait(false);
                return _analytics.BuildSpecialtyList(all);
            },
            _ttl.Specialties,
            ct);

    public async Task<Result<SpecialtySummaryDto>> GetSpecialtyAsync(string code, CancellationToken ct = default)
    {
        // The full list is already cached; reusing it avoids a separate query
        // and keeps memory pressure low. Compare on the upper-cased code once
        // rather than calling string.Equals(OrdinalIgnoreCase) on every element.
        var normalized = (code ?? string.Empty).Trim().ToUpperInvariant();
        var list = await GetSpecialtiesAsync(ct).ConfigureAwait(false);
        foreach (var summary in list)
        {
            if (summary.Code == normalized) return Result<SpecialtySummaryDto>.Success(summary);
        }
        return NotFound(code);
    }

    public async Task<Result<AdmissionHistoryDto>> GetHistoryAsync(string code, CancellationToken ct = default)
    {
        var history = await _cache.GetOrSetAsync(
            CacheKeys.History(code),
            async token =>
            {
                var records = await _repository.GetByCodeAsync(code, token).ConfigureAwait(false);
                return _analytics.BuildHistory(code, records);
            },
            _ttl.History,
            ct).ConfigureAwait(false);

        return history.Points.Count == 0 ? NotFound(code) : Result<AdmissionHistoryDto>.Success(history);
    }

    public async Task<Result<ComparisonDto>> GetComparisonAsync(string code, CancellationToken ct = default)
    {
        var comparison = await _cache.GetOrSetAsync(
            CacheKeys.Comparison(code),
            async token =>
            {
                var records = await _repository.GetByCodeAsync(code, token).ConfigureAwait(false);
                return _analytics.Compare(code, records);
            },
            _ttl.History,
            ct).ConfigureAwait(false);

        return comparison.BySeason.Count == 0 ? NotFound(code) : Result<ComparisonDto>.Success(comparison);
    }

    public async Task<Result<ForecastDto>> GetForecastAsync(string code, CancellationToken ct = default)
    {
        var forecast = await _cache.GetOrSetAsync(
            CacheKeys.Forecast(code),
            async token =>
            {
                var records = await _repository.GetByCodeAsync(code, token).ConfigureAwait(false);
                return _forecast.Forecast(code, records);
            },
            _ttl.Forecast,
            ct).ConfigureAwait(false);

        return forecast.DataPoints == 0 ? NotFound(code) : Result<ForecastDto>.Success(forecast);
    }

    public async Task<Result<ProbabilityDto>> GetChanceAsync(string code, CancellationToken ct = default)
    {
        var probability = await _cache.GetOrSetAsync(
            CacheKeys.Chance(code),
            async token =>
            {
                var records = await _repository.GetByCodeAsync(code, token).ConfigureAwait(false);
                return _probability.Calculate(code, records);
            },
            _ttl.Chance,
            ct).ConfigureAwait(false);

        return probability.DataPoints == 0 ? NotFound(code) : Result<ProbabilityDto>.Success(probability);
    }

    public Task<StatisticsOverviewDto> GetStatisticsAsync(CancellationToken ct = default)
        => _cache.GetOrSetAsync(
            CacheKeys.Statistics,
            async token =>
            {
                var all = await _repository.GetAllAsync(token).ConfigureAwait(false);
                return _analytics.BuildOverview(all);
            },
            _ttl.Statistics,
            ct);

    private static Error NotFound(string code)
        => Error.NotFound(code.ToUpperInvariant(), $"No admission data found for code '{code.ToUpperInvariant()}'.");
}
