using GrantAI.Application.Abstractions;
using GrantAI.Application.Common;
using GrantAI.Application.Contracts.Responses;
using GrantAI.Application.Forecasting;
using GrantAI.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace GrantAI.Application.Specialties;

/// <summary>
/// Orchestrates the grant-side reads: fetch from the grant repository, run the
/// pure forecast engine, and memoise the result in the distributed cache. Cache
/// keys all share <see cref="GrantCacheKeys.Root"/> so a grant import can wipe
/// every grant-derived value in one prefix delete (without touching the
/// admission/threshold cache).
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
                var all = await _repository.GetAllAsync(token);
                return BuildSummaries(all);
            },
            _ttl.Specialties,
            ct);

    public Task<GrantHistoryDto> GetHistoryAsync(string code, CancellationToken ct = default)
        => _cache.GetOrSetAsync(
            GrantCacheKeys.History(code),
            async token =>
            {
                var records = await _repository.GetByCodeAsync(code, token);
                return BuildHistory(code, records);
            },
            _ttl.History,
            ct);

    public Task<IReadOnlyList<GrantForecastDto>> GetForecastAsync(string code, CancellationToken ct = default)
        => _cache.GetOrSetAsync(
            GrantCacheKeys.Forecast(code),
            async token =>
            {
                var records = await _repository.GetByCodeAsync(code, token);
                if (records.Count == 0)
                {
                    _logger.LogInformation("Grant forecast requested for unknown code {Code}", code);
                }
                return _forecast.Forecast(code, records);
            },
            _ttl.Forecast,
            ct);

    /// <summary>
    /// Builds one <see cref="GrantSummaryDto"/> per (code, track) pair so that
    /// the same group appearing in both Profile and Scientific-Pedagogical
    /// shows up as two summaries — their scales are not comparable.
    /// </summary>
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
}
