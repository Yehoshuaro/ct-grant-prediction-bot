using GrantAI.Application.Forecasting;
using GrantAI.Domain.Entities;
using GrantAI.Domain.Enums;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace GrantAI.Tests.Forecasting;

/// <summary>
/// Synthetic-point checks for <see cref="GrantForecastService"/>: trend,
/// clamping to the per-track score scale, and reasonable prediction-interval
/// behaviour with only a handful of years on record.
/// </summary>
public class GrantForecastServiceTests
{
    private static GrantForecastService NewService()
        => new(NullLogger<GrantForecastService>.Instance);

    private static GrantCutoffRecord Record(int year, int cutoff,
        MasterType type = MasterType.ScientificPedagogical, int scale = 150, string code = "M094")
        => new()
        {
            Id = GrantCutoffRecord.BuildId(year, type, code),
            Year = year,
            MasterType = type,
            ScoreScaleMax = scale,
            GroupCode = code,
            GroupName = "Test",
            GrantCutoff = cutoff,
            GrantsAwarded = 10,
            MaxScore = cutoff + 20,
            AvgScore = cutoff + 5,
            SourceFile = "test.pdf",
            ImportedAtUtc = DateTime.UtcNow
        };

    [Fact]
    public void Forecast_NoData_ReturnsEmpty()
    {
        var forecast = NewService().Forecast("M094", []);
        Assert.Empty(forecast);
    }

    [Fact]
    public void Forecast_SingleYear_RepeatsLastCutoffWithLowConfidence()
    {
        var records = new[] { Record(2025, cutoff: 120) };
        var forecasts = NewService().Forecast("M094", records);

        var f = Assert.Single(forecasts);
        Assert.Equal(1, f.DataPoints);
        Assert.Equal(120, f.PredictedCutoff);
        Assert.True(f.ConfidencePercent <= 35);
        Assert.Equal(150, f.ScoreScaleMax);
        Assert.True(f.LowerBound < f.PredictedCutoff);
        Assert.True(f.UpperBound > f.PredictedCutoff);
    }

    [Fact]
    public void Forecast_RisingSeries_DetectsRisingTrend_AndStaysOnScale()
    {
        var records = new[]
        {
            Record(2023, cutoff: 100),
            Record(2024, cutoff: 110),
            Record(2025, cutoff: 120),
        };

        var forecasts = NewService().Forecast("M094", records);

        var f = Assert.Single(forecasts);
        Assert.Equal(TrendDirection.Rising, f.Trend);
        Assert.InRange(f.PredictedCutoff, 0, 150);
        Assert.InRange(f.LowerBound, 0, 150);
        Assert.InRange(f.UpperBound, 0, 150);
        // With three points of perfect trend, the prediction should sit at or above
        // the most recent observation, but never beyond the scale ceiling.
        Assert.True(f.PredictedCutoff >= 115);
        Assert.True(f.UpperBound >= f.PredictedCutoff);
    }

    [Fact]
    public void Forecast_FallingProfileSeries_ClampedAtZero()
    {
        var records = new[]
        {
            Record(2023, cutoff: 30, type: MasterType.Profile, scale: 70),
            Record(2024, cutoff: 20, type: MasterType.Profile, scale: 70),
            Record(2025, cutoff: 10, type: MasterType.Profile, scale: 70),
        };

        var forecasts = NewService().Forecast("M094", records);

        var f = Assert.Single(forecasts);
        Assert.Equal(MasterType.Profile, f.MasterType);
        Assert.Equal(70, f.ScoreScaleMax);
        Assert.Equal(TrendDirection.Falling, f.Trend);
        // Even a steep negative trend can't push the bounds below zero.
        Assert.InRange(f.PredictedCutoff, 0, 70);
        Assert.InRange(f.LowerBound, 0, 70);
        Assert.InRange(f.UpperBound, 0, 70);
    }

    [Fact]
    public void Forecast_TwoTracks_AreReportedIndependently()
    {
        // Same code, both tracks — must be forecasted separately because the
        // 70- and 150-point scales are not comparable.
        var records = new[]
        {
            Record(2023, cutoff: 50, type: MasterType.Profile, scale: 70),
            Record(2024, cutoff: 55, type: MasterType.Profile, scale: 70),
            Record(2025, cutoff: 60, type: MasterType.Profile, scale: 70),
            Record(2023, cutoff: 130, type: MasterType.ScientificPedagogical, scale: 150),
            Record(2024, cutoff: 135, type: MasterType.ScientificPedagogical, scale: 150),
            Record(2025, cutoff: 140, type: MasterType.ScientificPedagogical, scale: 150),
        };

        var forecasts = NewService().Forecast("M094", records);

        Assert.Equal(2, forecasts.Count);
        var profile = forecasts.Single(f => f.MasterType == MasterType.Profile);
        var sciPed = forecasts.Single(f => f.MasterType == MasterType.ScientificPedagogical);
        Assert.Equal(70, profile.ScoreScaleMax);
        Assert.Equal(150, sciPed.ScoreScaleMax);
        Assert.True(profile.PredictedCutoff <= 70);
        Assert.True(sciPed.PredictedCutoff <= 150);
    }

    [Fact]
    public void Forecast_ConfidenceCappedHonestlyForTinySample()
    {
        // Even on a perfectly fitted line, 2 points should not yield certainty.
        var records = new[]
        {
            Record(2024, cutoff: 100),
            Record(2025, cutoff: 110),
        };

        var f = Assert.Single(NewService().Forecast("M094", records));
        Assert.InRange(f.ConfidencePercent, 25, 70);
    }
}
