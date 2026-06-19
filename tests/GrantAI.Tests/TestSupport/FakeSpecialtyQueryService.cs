using GrantAI.Application.Contracts.Responses;
using GrantAI.Application.Specialties;
using GrantAI.Domain.Enums;

namespace GrantAI.Tests.TestSupport;

/// <summary>
/// A deterministic, in-memory stand-in for <see cref="ISpecialtyQueryService"/>
/// used by the API tests so they exercise the HTTP/controller layer without a
/// running MongoDB or Redis. "M094" is the only known code; everything else
/// returns the documented no-data shapes so 404 handling can be verified.
/// </summary>
internal sealed class FakeSpecialtyQueryService : ISpecialtyQueryService
{
    public const string KnownCode = "M094";

    private static bool IsKnown(string code) => string.Equals(code, KnownCode, StringComparison.OrdinalIgnoreCase);

    public Task<IReadOnlyList<SpecialtySummaryDto>> GetSpecialtiesAsync(CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<SpecialtySummaryDto>>([Summary()]);

    public Task<SpecialtySummaryDto?> GetSpecialtyAsync(string code, CancellationToken ct = default)
        => Task.FromResult(IsKnown(code) ? Summary() : null);

    public Task<AdmissionHistoryDto> GetHistoryAsync(string code, CancellationToken ct = default)
        => Task.FromResult(IsKnown(code)
            ? new AdmissionHistoryDto
            {
                Code = KnownCode,
                GroupName = "Test Group",
                Points =
                [
                    new CampaignPointDto
                    {
                        Year = 2025, Season = Season.Summer, Label = "2025 Summer",
                        Applications = 150, Participants = 120, ParticipationRate = 80,
                        PassedThreshold = 96, FailedThreshold = 24, PassRate = 80
                    }
                ],
                ApplicationsTrend = TrendDirection.Rising,
                ParticipantsTrend = TrendDirection.Rising,
                PassRateTrend = TrendDirection.Rising
            }
            : new AdmissionHistoryDto { Code = code });

    public Task<ComparisonDto> GetComparisonAsync(string code, CancellationToken ct = default)
        => Task.FromResult(IsKnown(code)
            ? new ComparisonDto
            {
                Code = KnownCode,
                BySeason =
                [
                    new SeasonStatsDto
                    {
                        Season = Season.Summer, CampaignCount = 3,
                        AverageApplications = 140, AverageParticipationRate = 82, AveragePassRate = 78
                    }
                ],
                Summary = "Test comparison."
            }
            : new ComparisonDto { Code = code });

    public Task<ForecastDto> GetForecastAsync(string code, CancellationToken ct = default)
        => Task.FromResult(IsKnown(code)
            ? new ForecastDto
            {
                Code = KnownCode,
                PredictedPassRate = 85,
                LowerBound = 80,
                UpperBound = 90,
                ConfidencePercent = 70,
                Trend = TrendDirection.Rising,
                DataPoints = 5,
                Method = "Test method"
            }
            : new ForecastDto { Code = code, DataPoints = 0, ConfidencePercent = 0 });

    public Task<ProbabilityDto> GetChanceAsync(string code, CancellationToken ct = default)
        => Task.FromResult(IsKnown(code)
            ? new ProbabilityDto
            {
                Code = KnownCode,
                PassProbabilityPercent = 85,
                LowerBoundPercent = 80,
                UpperBoundPercent = 90,
                PredictedPassRate = 85,
                ConfidencePercent = 70,
                DataPoints = 5
            }
            : new ProbabilityDto { Code = code, DataPoints = 0 });

    public Task<StatisticsOverviewDto> GetStatisticsAsync(CancellationToken ct = default)
        => Task.FromResult(new StatisticsOverviewDto
        {
            TotalRecords = 42,
            TotalGroups = 4,
            EarliestYear = 2023,
            LatestYear = 2025,
            TotalApplications = 5000,
            TotalParticipants = 4200,
            TotalPassed = 1800,
            OverallParticipationRate = 84,
            OverallPassRate = 42.86
        });

    private static SpecialtySummaryDto Summary() => new()
    {
        Code = KnownCode,
        Name = "Test Group",
        CampaignCount = 5,
        LatestYear = 2025,
        LatestSeason = Season.Summer,
        LatestApplications = 150,
        LatestParticipants = 120,
        LatestPassRate = 80
    };
}

/// <summary>
/// Deterministic in-memory stand-in for <see cref="IGrantQueryService"/>. Same
/// known/unknown shape as the entrance-threshold fake so the HTTP-layer tests
/// can verify 200/404 handling without a database.
/// </summary>
internal sealed class FakeGrantQueryService : IGrantQueryService
{
    public const string KnownCode = "M094";

    private static bool IsKnown(string code) => string.Equals(code, KnownCode, StringComparison.OrdinalIgnoreCase);

    public Task<IReadOnlyList<GrantSummaryDto>> GetAllAsync(CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<GrantSummaryDto>>(
        [
            new GrantSummaryDto
            {
                Code = KnownCode, Name = "Test Group",
                MasterType = MasterType.ScientificPedagogical, ScoreScaleMax = 150,
                LatestYear = 2025, LatestCutoff = 130, LatestGrantsAwarded = 12, YearsOnRecord = 3
            }
        ]);

    public Task<GrantHistoryDto> GetHistoryAsync(string code, CancellationToken ct = default)
        => Task.FromResult(IsKnown(code)
            ? new GrantHistoryDto
            {
                Code = KnownCode, GroupName = "Test Group",
                Points =
                [
                    new GrantCutoffPointDto
                    {
                        Year = 2024, MasterType = MasterType.ScientificPedagogical, ScoreScaleMax = 150,
                        GrantCutoff = 125, GrantsAwarded = 10, MaxScore = 148, AvgScore = 135
                    },
                    new GrantCutoffPointDto
                    {
                        Year = 2025, MasterType = MasterType.ScientificPedagogical, ScoreScaleMax = 150,
                        GrantCutoff = 130, GrantsAwarded = 12, MaxScore = 149, AvgScore = 138
                    }
                ]
            }
            : new GrantHistoryDto { Code = code });

    public Task<IReadOnlyList<GrantForecastDto>> GetForecastAsync(string code, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<GrantForecastDto>>(IsKnown(code)
            ?
            [
                new GrantForecastDto
                {
                    Code = KnownCode, Name = "Test Group",
                    MasterType = MasterType.ScientificPedagogical, ScoreScaleMax = 150,
                    PredictedCutoff = 132, LowerBound = 124, UpperBound = 140,
                    ConfidencePercent = 55, Trend = TrendDirection.Rising,
                    DataPoints = 2, Method = "Test method"
                }
            ]
            : []);
}
