using GrantAI.Application.Forecasting;
using GrantAI.Domain.Entities;
using GrantAI.Domain.Enums;
using GrantAI.Tests.TestSupport;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace GrantAI.Tests.Forecasting;

public class ForecastServiceTests
{
    private static ForecastService NewService() => new(NullLogger<ForecastService>.Instance);

    [Fact]
    public void Forecast_NoData_ReturnsZeroConfidence()
    {
        var forecast = NewService().Forecast("M094", []);

        Assert.Equal(0, forecast.DataPoints);
        Assert.Equal(0, forecast.ConfidencePercent);
        Assert.Equal(TrendDirection.Stable, forecast.Trend);
    }

    [Fact]
    public void Forecast_SingleCampaign_RepeatsLastPassRate()
    {
        // 45 of 90 participants cleared → 50% pass rate.
        var records = new List<AdmissionRecord> { TestData.Record(2025, participants: 90, passed: 45) };

        var forecast = NewService().Forecast("M094", records);

        Assert.Equal(1, forecast.DataPoints);
        Assert.Equal(35, forecast.ConfidencePercent);
        Assert.Equal(50, forecast.PredictedPassRate, precision: 1);
        Assert.Contains("последнее наблюдение", forecast.Method);
    }

    [Fact]
    public void Forecast_RisingSeries_PredictsAboveTheMeanAndReportsRising()
    {
        var forecast = NewService().Forecast("M094", TestData.RisingSeries());

        Assert.Equal(5, forecast.DataPoints);
        Assert.Equal(TrendDirection.Rising, forecast.Trend);
        // Mean of the rising series is 72%; the forecast should sit above it.
        Assert.True(forecast.PredictedPassRate > 72, $"Expected > 72, got {forecast.PredictedPassRate}");
        Assert.True(forecast.ConfidencePercent > 0);
    }

    [Fact]
    public void Forecast_StaysWithinZeroToHundred()
    {
        // Pass rates already near the ceiling must not be predicted above 100%.
        var records = new List<AdmissionRecord>
        {
            TestData.Record(2022, participants: 100, passed: 94),
            TestData.Record(2023, participants: 100, passed: 96),
            TestData.Record(2024, participants: 100, passed: 98),
            TestData.Record(2025, participants: 100, passed: 99),
        };

        var forecast = NewService().Forecast("M094", records);

        Assert.InRange(forecast.PredictedPassRate, 0, 100);
        Assert.InRange(forecast.UpperBound, 0, 100);
        Assert.InRange(forecast.LowerBound, 0, 100);
    }

    [Fact]
    public void Forecast_FallingSeries_ReportsFallingTrend()
    {
        var records = new List<AdmissionRecord>
        {
            TestData.Record(2022, participants: 100, passed: 90),
            TestData.Record(2023, participants: 100, passed: 84),
            TestData.Record(2024, participants: 100, passed: 78),
            TestData.Record(2025, participants: 100, passed: 72),
        };

        var forecast = NewService().Forecast("M094", records);

        Assert.Equal(TrendDirection.Falling, forecast.Trend);
    }
}
