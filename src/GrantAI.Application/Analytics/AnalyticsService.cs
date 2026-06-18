using AutoMapper;
using GrantAI.Application.Common;
using GrantAI.Application.Contracts.Responses;
using GrantAI.Domain.Entities;
using GrantAI.Domain.Enums;

namespace GrantAI.Application.Analytics;

/// <summary>
/// Reusable analytics over admission records. Everything here is pure: it takes
/// the records the caller already fetched and turns them into projection DTOs.
/// Trends are computed on a per-campaign aggregate so that any duplicate rows in
/// the same campaign don't distort the direction of a series.
/// </summary>
public sealed class AnalyticsService : IAnalyticsService
{
    private readonly IMapper _mapper;

    public AnalyticsService(IMapper mapper) => _mapper = mapper;

    public AdmissionHistoryDto BuildHistory(string code, IReadOnlyList<AdmissionRecord> records)
    {
        code = code.ToUpperInvariant();
        if (records.Count == 0)
            return new AdmissionHistoryDto { Code = code };

        var points = records
            .OrderBy(r => CampaignOrder.Ordinal(r.Year, r.Season))
            .Select(_mapper.Map<CampaignPointDto>)
            .ToList();

        var campaigns = AggregateByCampaign(records);
        var xs = campaigns.Select(c => (double)c.Ordinal).ToArray();

        var name = records
            .OrderByDescending(r => CampaignOrder.Ordinal(r.Year, r.Season))
            .Select(r => r.GroupName)
            .FirstOrDefault(n => !string.IsNullOrWhiteSpace(n)) ?? string.Empty;

        return new AdmissionHistoryDto
        {
            Code = code,
            GroupName = name,
            Points = points,
            ApplicationsTrend = TrendCalculator.FromSeries(xs, campaigns.Select(c => (double)c.Applications).ToArray()),
            ParticipantsTrend = TrendCalculator.FromSeries(xs, campaigns.Select(c => (double)c.Participants).ToArray()),
            PassRateTrend = TrendCalculator.FromSeries(xs, campaigns.Select(c => c.PassRate).ToArray())
        };
    }

    public ComparisonDto Compare(string code, IReadOnlyList<AdmissionRecord> records)
    {
        code = code.ToUpperInvariant();
        if (records.Count == 0)
            return new ComparisonDto { Code = code, Summary = $"No data found for '{code}'." };

        var bySeason = records
            .GroupBy(r => r.Season)
            .OrderBy(g => g.Key)
            .Select(g => new SeasonStatsDto
            {
                Season = g.Key,
                CampaignCount = DistinctCampaigns(g),
                AverageApplications = Round(g.Average(r => (double)r.Applications)),
                AverageParticipationRate = Round(g.Average(ParticipationRate)),
                AveragePassRate = Round(g.Average(PassRate))
            })
            .ToList();

        return new ComparisonDto
        {
            Code = code,
            BySeason = bySeason,
            Summary = BuildComparisonSummary(code, bySeason)
        };
    }

    public IReadOnlyList<SpecialtySummaryDto> BuildSpecialtyList(IReadOnlyList<AdmissionRecord> records)
    {
        if (records.Count == 0)
            return [];

        return records
            .GroupBy(r => r.GroupCode.ToUpperInvariant())
            .Select(BuildSummary)
            .OrderBy(s => s.Code, StringComparer.Ordinal)
            .ToList();
    }

    public StatisticsOverviewDto BuildOverview(IReadOnlyList<AdmissionRecord> records)
    {
        if (records.Count == 0)
            return new StatisticsOverviewDto();

        long applications = records.Sum(r => (long)r.Applications);
        long participants = records.Sum(r => (long)r.Participants);
        long passed = records.Sum(r => (long)r.PassedThreshold);

        return new StatisticsOverviewDto
        {
            TotalRecords = records.Count,
            TotalGroups = records.Select(r => r.GroupCode.ToUpperInvariant()).Distinct().Count(),
            EarliestYear = records.Min(r => r.Year),
            LatestYear = records.Max(r => r.Year),
            TotalApplications = applications,
            TotalParticipants = participants,
            TotalPassed = passed,
            OverallParticipationRate = applications > 0 ? Round((double)participants / applications * 100.0) : 0d,
            OverallPassRate = participants > 0 ? Round((double)passed / participants * 100.0) : 0d
        };
    }

    private SpecialtySummaryDto BuildSummary(IGrouping<string, AdmissionRecord> group)
    {
        var latestOrdinal = group.Max(r => CampaignOrder.Ordinal(r.Year, r.Season));
        var latest = group.Where(r => CampaignOrder.Ordinal(r.Year, r.Season) == latestOrdinal).ToList();
        var head = latest[0];

        var applications = latest.Sum(r => r.Applications);
        var participants = latest.Sum(r => r.Participants);
        var passed = latest.Sum(r => r.PassedThreshold);

        return new SpecialtySummaryDto
        {
            Code = group.Key,
            Name = latest.Select(r => r.GroupName).FirstOrDefault(n => !string.IsNullOrWhiteSpace(n)) ?? string.Empty,
            CampaignCount = DistinctCampaigns(group),
            LatestYear = head.Year,
            LatestSeason = head.Season,
            LatestApplications = applications,
            LatestParticipants = participants,
            LatestPassRate = participants > 0 ? Round((double)passed / participants * 100.0) : 0d
        };
    }

    private static string BuildComparisonSummary(string code, IReadOnlyList<SeasonStatsDto> seasons)
    {
        if (seasons.Count == 2)
        {
            var summer = seasons.FirstOrDefault(s => s.Season == Season.Summer);
            var winter = seasons.FirstOrDefault(s => s.Season == Season.Winter);
            if (summer is not null && winter is not null)
            {
                var diff = summer.AveragePassRate - winter.AveragePassRate;
                return Math.Abs(diff) < 0.5
                    ? "Summer and winter pass rates are broadly comparable."
                    : $"Summer pass rates run about {Math.Abs(diff):0.#} point(s) {(diff > 0 ? "higher" : "lower")} than winter on average.";
            }
        }

        return $"Comparison for '{code}' is based on the available campaigns.";
    }

    private static List<CampaignAggregate> AggregateByCampaign(IReadOnlyList<AdmissionRecord> records)
        => records
            .GroupBy(r => CampaignOrder.Ordinal(r.Year, r.Season))
            .OrderBy(g => g.Key)
            .Select(g =>
            {
                var participants = g.Sum(r => r.Participants);
                var passed = g.Sum(r => r.PassedThreshold);
                var passRate = participants > 0 ? (double)passed / participants * 100.0 : 0d;
                return new CampaignAggregate(g.Key, g.Sum(r => r.Applications), participants, passRate);
            })
            .ToList();

    private static int DistinctCampaigns(IEnumerable<AdmissionRecord> records)
        => records.Select(r => CampaignOrder.Ordinal(r.Year, r.Season)).Distinct().Count();

    private static double ParticipationRate(AdmissionRecord r)
        => r.Applications > 0 ? (double)r.Participants / r.Applications * 100.0 : 0d;

    private static double PassRate(AdmissionRecord r)
        => r.Participants > 0 ? (double)r.PassedThreshold / r.Participants * 100.0 : 0d;

    private static double Round(double value) => Math.Round(value, 2);

    private readonly record struct CampaignAggregate(int Ordinal, int Applications, int Participants, double PassRate);
}
