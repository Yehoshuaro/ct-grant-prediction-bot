using GrantAI.Application.Forecasting;
using GrantAI.Domain.Entities;
using GrantAI.Domain.Enums;
using GrantAI.Tests.TestSupport;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace GrantAI.Tests.Forecasting;

/// <summary>
/// Edge-case behaviour of the forecast pipeline that protects against the kind
/// of regressions that the audit caught: uneven season cadence, two-point fits,
/// honesty of confidence on tiny samples, and clamping at the percentage rails.
/// </summary>
public class ForecastServiceCornerCasesTests
{
    private static ForecastService NewService() => new(NullLogger<ForecastService>.Instance);

    [Fact]
    public void Forecast_FlatSeries_ReportsStableTrend()
    {
        var records = new List<AdmissionRecord>
        {
            TestData.Record(2022, participants: 100, passed: 70),
            TestData.Record(2023, participants: 100, passed: 70),
            TestData.Record(2024, participants: 100, passed: 70),
            TestData.Record(2025, participants: 100, passed: 70),
        };

        var forecast = NewService().Forecast("M094", records);

        Assert.Equal(TrendDirection.Stable, forecast.Trend);
        Assert.Equal(70, forecast.PredictedPassRate, precision: 1);
    }

    [Fact]
    public void Forecast_SummerOnlyYears_DoesNotInflateSlopePerYear()
    {
        // Three summer-only intakes, +5 p.p. each year. Per-year slope is +5,
        // not +10 (which it would be under the old fixed *2 multiplier).
        var records = new List<AdmissionRecord>
        {
            TestData.Record(2023, season: Season.Summer, participants: 100, passed: 60),
            TestData.Record(2024, season: Season.Summer, participants: 100, passed: 65),
            TestData.Record(2025, season: Season.Summer, participants: 100, passed: 70),
        };

        var forecast = NewService().Forecast("M094", records);

        Assert.Equal(TrendDirection.Rising, forecast.Trend);
        // Predicted next-summer point should be near 75, not catapulted toward 80.
        Assert.InRange(forecast.PredictedPassRate, 70, 80);

        var slopeFactor = forecast.Factors.First(f => f.Contains("год"));
        Assert.Contains("5", slopeFactor); // ~5 points/year
    }

    [Fact]
    public void Forecast_TwoCampaigns_WidensIntervalAndCapsConfidence()
    {
        var records = new List<AdmissionRecord>
        {
            TestData.Record(2024, participants: 100, passed: 70),
            TestData.Record(2025, participants: 100, passed: 80),
        };

        var forecast = NewService().Forecast("M094", records);

        Assert.True(forecast.UpperBound - forecast.LowerBound >= 4,
            $"PI should be at least a few p.p. wide on a 2-point fit (was {forecast.LowerBound}-{forecast.UpperBound})");
        Assert.InRange(forecast.ConfidencePercent, 30, 95);
        Assert.Contains(forecast.Factors, f => f.Contains("2 кампании"));
    }

    [Fact]
    public void Forecast_NearCeiling_StaysAtOrBelow100()
    {
        var records = new List<AdmissionRecord>
        {
            TestData.Record(2022, participants: 100, passed: 97),
            TestData.Record(2023, participants: 100, passed: 98),
            TestData.Record(2024, participants: 100, passed: 99),
            TestData.Record(2025, participants: 100, passed: 100),
        };

        var forecast = NewService().Forecast("M094", records);

        Assert.InRange(forecast.PredictedPassRate, 0, 100);
        Assert.InRange(forecast.UpperBound, 0, 100);
        Assert.InRange(forecast.LowerBound, 0, 100);
    }

    [Fact]
    public void Forecast_NearFloor_StaysAtOrAbove0()
    {
        var records = new List<AdmissionRecord>
        {
            TestData.Record(2022, participants: 100, passed: 12),
            TestData.Record(2023, participants: 100, passed: 9),
            TestData.Record(2024, participants: 100, passed: 6),
            TestData.Record(2025, participants: 100, passed: 3),
        };

        var forecast = NewService().Forecast("M094", records);

        Assert.InRange(forecast.PredictedPassRate, 0, 100);
        Assert.True(forecast.LowerBound >= 0);
        Assert.True(forecast.UpperBound >= 0);
        Assert.Equal(TrendDirection.Falling, forecast.Trend);
    }

    [Fact]
    public void Forecast_SingleCampaign_TextHonestlyAcknowledgesScarcity()
    {
        var records = new List<AdmissionRecord> { TestData.Record(2025, participants: 90, passed: 45) };

        var forecast = NewService().Forecast("M094", records);

        Assert.Contains(forecast.Factors, f => f.Contains("только одна"));
        Assert.Contains("одна кампания", forecast.Explanation);
    }

    [Fact]
    public void Forecast_NoData_TextIsExplicit()
    {
        var forecast = NewService().Forecast("ZZZ", []);

        Assert.Equal(0, forecast.DataPoints);
        Assert.Equal(0, forecast.ConfidencePercent);
        Assert.Contains("Нет исторических данных", forecast.Explanation);
    }
}
