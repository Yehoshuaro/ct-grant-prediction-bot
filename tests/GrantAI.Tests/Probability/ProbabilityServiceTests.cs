using GrantAI.Application.Forecasting;
using GrantAI.Application.Probability;
using GrantAI.Domain.Entities;
using GrantAI.Tests.TestSupport;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace GrantAI.Tests.Probability;

public class ProbabilityServiceTests
{
    private static (ProbabilityService Probability, ForecastService Forecast) NewServices()
    {
        var forecast = new ForecastService(NullLogger<ForecastService>.Instance);
        var probability = new ProbabilityService(forecast, NullLogger<ProbabilityService>.Instance);
        return (probability, forecast);
    }

    [Fact]
    public void Calculate_NoData_ReturnsZero()
    {
        var (probability, _) = NewServices();

        var result = probability.Calculate("M094", []);

        Assert.Equal(0, result.PassProbabilityPercent);
        Assert.Equal(0, result.DataPoints);
    }

    [Fact]
    public void Calculate_UsesForecastedPassRateAsTheProbability()
    {
        var (probability, forecast) = NewServices();
        var records = TestData.RisingSeries();

        var expected = forecast.Forecast("M094", records);
        var result = probability.Calculate("M094", records);

        Assert.Equal((int)System.Math.Round(expected.PredictedPassRate), result.PassProbabilityPercent);
        Assert.Equal(expected.PredictedPassRate, result.PredictedPassRate, precision: 3);
        Assert.Equal(expected.DataPoints, result.DataPoints);
    }

    [Fact]
    public void Calculate_ProbabilityWithinItsOwnBounds()
    {
        var (probability, _) = NewServices();

        var result = probability.Calculate("M094", TestData.RisingSeries());

        Assert.InRange(result.PassProbabilityPercent, 0, 100);
        Assert.True(result.LowerBoundPercent <= result.PassProbabilityPercent);
        Assert.True(result.PassProbabilityPercent <= result.UpperBoundPercent);
    }

    [Fact]
    public void Calculate_PopulatesExplanatoryFactors()
    {
        var (probability, _) = NewServices();

        var result = probability.Calculate("M094", TestData.RisingSeries());

        Assert.NotEmpty(result.Factors);
        Assert.False(string.IsNullOrWhiteSpace(result.Explanation));
    }
}
