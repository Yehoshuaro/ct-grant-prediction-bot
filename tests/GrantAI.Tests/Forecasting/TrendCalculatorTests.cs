using GrantAI.Application.Common;
using GrantAI.Domain.Enums;
using Xunit;

namespace GrantAI.Tests.Forecasting;

public class TrendCalculatorTests
{
    [Theory]
    [InlineData(new double[] { 2 * 2022, 2 * 2022 + 1, 2 * 2023, 2 * 2023 + 1 }, 2.0)]
    [InlineData(new double[] { 2 * 2023 + 1, 2 * 2024 + 1, 2 * 2025 + 1 }, 1.0)]
    public void InferCampaignsPerYear_MatchesObservedCadence(double[] xs, double expected)
    {
        Assert.Equal(expected, TrendCalculator.InferCampaignsPerYear(xs), precision: 3);
    }

    [Fact]
    public void InferCampaignsPerYear_TooFewPoints_DefaultsToTwo()
    {
        Assert.Equal(2.0, TrendCalculator.InferCampaignsPerYear([5.0]));
    }

    [Fact]
    public void FromSeries_FlatValues_ClassifiesAsStable()
    {
        double[] xs = [2 * 2022 + 1, 2 * 2023 + 1, 2 * 2024 + 1];
        double[] ys = [50, 50, 50];

        Assert.Equal(TrendDirection.Stable, TrendCalculator.FromSeries(xs, ys));
    }

    [Fact]
    public void FromSeries_SmallNoise_StaysStable()
    {
        double[] xs = [2 * 2022 + 1, 2 * 2023 + 1, 2 * 2024 + 1, 2 * 2025 + 1];
        double[] ys = [50.0, 50.1, 49.9, 50.2];

        Assert.Equal(TrendDirection.Stable, TrendCalculator.FromSeries(xs, ys));
    }

    [Fact]
    public void FromSeries_ClearGrowth_ClassifiesAsRising()
    {
        double[] xs = [2 * 2022 + 1, 2 * 2023 + 1, 2 * 2024 + 1, 2 * 2025 + 1];
        double[] ys = [50, 55, 60, 65];

        Assert.Equal(TrendDirection.Rising, TrendCalculator.FromSeries(xs, ys));
    }

    [Fact]
    public void Classify_BorderlineSlope_BelowThresholdIsStable()
    {
        Assert.Equal(TrendDirection.Stable, TrendCalculator.Classify(0.4, threshold: 0.5));
        Assert.Equal(TrendDirection.Rising, TrendCalculator.Classify(0.6, threshold: 0.5));
        Assert.Equal(TrendDirection.Falling, TrendCalculator.Classify(-0.6, threshold: 0.5));
    }
}
