using GrantAI.Application.Common.Results;
using GrantAI.Application.Contracts.Responses;
using GrantAI.Application.Specialties;
using GrantAI.Domain.Enums;

namespace GrantAI.Tests.TestSupport;

/// <summary>
/// Deterministic in-memory stand-in for <see cref="ISpecialtyQueryService"/>.
/// "M094" is the only known code; everything else returns Result.Failure(NotFound)
/// so 404 handling is exercised.
/// </summary>
internal sealed class FakeSpecialtyQueryService : ISpecialtyQueryService
{
    public const string KnownCode = "M094";

    private static bool IsKnown(string code) => string.Equals(code, KnownCode, StringComparison.OrdinalIgnoreCase);
    private static Error NotFound(string code)
        => Error.NotFound(code.ToUpperInvariant(), $"No admission data found for code '{code.ToUpperInvariant()}'.");

    public Task<IReadOnlyList<SpecialtySummaryDto>> GetSpecialtiesAsync(CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<SpecialtySummaryDto>>([Summary()]);

    public Task<Result<SpecialtySummaryDto>> GetSpecialtyAsync(string code, CancellationToken ct = default)
        => Task.FromResult(IsKnown(code)
            ? Result<SpecialtySummaryDto>.Success(Summary())
            : Result<SpecialtySummaryDto>.Failure(NotFound(code)));

    public Task<Result<AdmissionHistoryDto>> GetHistoryAsync(string code, CancellationToken ct = default)
        => Task.FromResult(IsKnown(code)
            ? Result<AdmissionHistoryDto>.Success(new AdmissionHistoryDto
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
            })
            : Result<AdmissionHistoryDto>.Failure(NotFound(code)));

    public Task<Result<ComparisonDto>> GetComparisonAsync(string code, CancellationToken ct = default)
        => Task.FromResult(IsKnown(code)
            ? Result<ComparisonDto>.Success(new ComparisonDto
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
            })
            : Result<ComparisonDto>.Failure(NotFound(code)));

    public Task<Result<ForecastDto>> GetForecastAsync(string code, CancellationToken ct = default)
        => Task.FromResult(IsKnown(code)
            ? Result<ForecastDto>.Success(new ForecastDto
            {
                Code = KnownCode,
                PredictedPassRate = 85,
                LowerBound = 80,
                UpperBound = 90,
                ConfidencePercent = 70,
                Trend = TrendDirection.Rising,
                DataPoints = 5,
                Method = "Test method"
            })
            : Result<ForecastDto>.Failure(NotFound(code)));

    public Task<Result<ProbabilityDto>> GetChanceAsync(string code, CancellationToken ct = default)
        => Task.FromResult(IsKnown(code)
            ? Result<ProbabilityDto>.Success(new ProbabilityDto
            {
                Code = KnownCode,
                PassProbabilityPercent = 85,
                LowerBoundPercent = 80,
                UpperBoundPercent = 90,
                PredictedPassRate = 85,
                ConfidencePercent = 70,
                DataPoints = 5
            })
            : Result<ProbabilityDto>.Failure(NotFound(code)));

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
/// known/unknown shape as the threshold fake.
/// </summary>
internal sealed class FakeGrantQueryService : IGrantQueryService
{
    public const string KnownCode = "M094";

    private static bool IsKnown(string code) => string.Equals(code, KnownCode, StringComparison.OrdinalIgnoreCase);
    private static Error NotFound(string code)
        => Error.NotFound(code.ToUpperInvariant(), $"No grant data found for code '{code.ToUpperInvariant()}'.");

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

    public Task<Result<GrantHistoryDto>> GetHistoryAsync(string code, CancellationToken ct = default)
        => Task.FromResult(IsKnown(code)
            ? Result<GrantHistoryDto>.Success(new GrantHistoryDto
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
            })
            : Result<GrantHistoryDto>.Failure(NotFound(code)));

    public Task<Result<IReadOnlyList<GrantForecastDto>>> GetForecastAsync(string code, CancellationToken ct = default)
        => Task.FromResult(IsKnown(code)
            ? Result<IReadOnlyList<GrantForecastDto>>.Success(
            [
                new GrantForecastDto
                {
                    Code = KnownCode, Name = "Test Group",
                    MasterType = MasterType.ScientificPedagogical, ScoreScaleMax = 150,
                    PredictedCutoff = 132, LowerBound = 124, UpperBound = 140,
                    ConfidencePercent = 55, Trend = TrendDirection.Rising,
                    DataPoints = 2, Method = "Test method"
                }
            ])
            : Result<IReadOnlyList<GrantForecastDto>>.Failure(NotFound(code)));
}
