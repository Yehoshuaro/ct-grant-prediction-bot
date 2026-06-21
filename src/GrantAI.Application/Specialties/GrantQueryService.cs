using GrantAI.Application.Abstractions;
using GrantAI.Application.Common;
using GrantAI.Application.Common.Results;
using GrantAI.Application.Contracts.Responses;
using GrantAI.Application.Forecasting;
using GrantAI.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace GrantAI.Application.Specialties;

/// <summary>
/// Orchestrates the grant-side reads: fetch from the grant repository, run the
/// pure forecast engine, and memoise the result in the distributed cache. Cache
/// keys share <see cref="GrantCacheKeys.Root"/> so a grant import can wipe
/// every grant-derived value in one prefix delete without touching the
/// admission cache.
/// </summary>
public sealed class GrantQueryService : IGrantQueryService
{
    private readonly IGrantCutoffRepository _repository;
    private readonly ICacheService _cache;
    private readonly IGrantForecastService _forecast;
    private readonly CacheSettings _ttl;
    private readonly ILogger<GrantQueryService> _logger;

    public GrantQueryService(
        IGrantCutoffRepository repository,
        ICacheService cache,
        IGrantForecastService forecast,
        CacheSettings ttl,
        ILogger<GrantQueryService> logger)
    {
        _repository = repository;
        _cache = cache;
        _forecast = forecast;
        _ttl = ttl;
        _logger = logger;
    }

    public Task<IReadOnlyList<GrantSummaryDto>> GetAllAsync(CancellationToken ct = default)
        => _cache.GetOrSetAsync(
            GrantCacheKeys.List,
            async token =>
            {
                var all = await _repository.GetAllAsync(token).ConfigureAwait(false);
                return BuildSummaries(all);
            },
            _ttl.Specialties,
            ct);

    public async Task<Result<GrantHistoryDto>> GetHistoryAsync(string code, CancellationToken ct = default)
    {
        var history = await _cache.GetOrSetAsync(
            GrantCacheKeys.History(code),
            async token =>
            {
                var records = await _repository.GetByCodeAsync(code, token).ConfigureAwait(false);
                return BuildHistory(code, records);
            },
            _ttl.History,
            ct).ConfigureAwait(false);

        return history.Points.Count == 0 ? NotFound(code) : Result<GrantHistoryDto>.Success(history);
    }

    public async Task<Result<IReadOnlyList<GrantForecastDto>>> GetForecastAsync(string code, CancellationToken ct = default)
    {
        var forecasts = await _cache.GetOrSetAsync(
            GrantCacheKeys.Forecast(code),
            async token =>
            {
                var records = await _repository.GetByCodeAsync(code, token).ConfigureAwait(false);
                if (records.Count == 0)
                {
                    _logger.LogInformation("Grant forecast requested for unknown code {Code}", code);
                }
                return _forecast.Forecast(code, records);
            },
            _ttl.Forecast,
            ct).ConfigureAwait(false);

        return forecasts.Count == 0 ? NotFound(code) : Result<IReadOnlyList<GrantForecastDto>>.Success(forecasts);
    }

    private static IReadOnlyList<GrantSummaryDto> BuildSummaries(IReadOnlyList<GrantCutoffRecord> records)
        => records
            .GroupBy(r => (Code: r.GroupCode, Track: r.MasterType))
            .Select(g =>
            {
                var ordered = g.OrderByDescending(r => r.Year).ToList();
                var latest = ordered[0];
                return new GrantSummaryDto
                {
                    Code = latest.GroupCode,
                    Name = latest.GroupName,
                    MasterType = latest.MasterType,
                    ScoreScaleMax = latest.ScoreScaleMax,
                    LatestYear = latest.Year,
                    LatestCutoff = latest.GrantCutoff,
                    LatestGrantsAwarded = latest.GrantsAwarded,
                    YearsOnRecord = ordered.Count
                };
            })
            .OrderBy(s => s.Code, StringComparer.Ordinal)
            .ThenBy(s => (int)s.MasterType)
            .ToList();

    private static GrantHistoryDto BuildHistory(string code, IReadOnlyList<GrantCutoffRecord> records)
    {
        if (records.Count == 0)
        {
            return new GrantHistoryDto { Code = code.ToUpperInvariant() };
        }

        var groupName = records
            .OrderByDescending(r => r.Year)
            .Select(r => r.GroupName)
            .FirstOrDefault(n => !string.IsNullOrWhiteSpace(n)) ?? string.Empty;

        var points = records
            .OrderBy(r => (int)r.MasterType)
            .ThenBy(r => r.Year)
            .Select(r => new GrantCutoffPointDto
            {
                Year = r.Year,
                MasterType = r.MasterType,
                ScoreScaleMax = r.ScoreScaleMax,
                GrantCutoff = r.GrantCutoff,
                GrantsAwarded = r.GrantsAwarded,
                MaxScore = r.MaxScore,
                AvgScore = r.AvgScore
            })
            .ToList();

        return new GrantHistoryDto
        {
            Code = records[0].GroupCode,
            GroupName = groupName,
            Points = points
        };
    }

    private static Error NotFound(string code)
        => Error.NotFound(code.ToUpperInvariant(), $"No grant data found for code '{code.ToUpperInvariant()}'.");
}
