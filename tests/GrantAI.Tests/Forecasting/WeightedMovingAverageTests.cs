using GrantAI.Application.Forecasting;
using Xunit;

namespace GrantAI.Tests.Forecasting;

public class WeightedMovingAverageTests
{
    [Fact]
    public void Compute_FullWindow_WeightsRecentValuesMore()
    {
        // (10*1 + 20*2 + 30*3 + 40*4) / (1+2+3+4) = 300 / 10 = 30
        double[] series = [10, 20, 30, 40];

        var wma = WeightedMovingAverage.Compute(series, window: 4);

        Assert.Equal(30.0, wma, precision: 9);
    }

    [Fact]
    public void Compute_WindowLargerThanSeries_UsesWholeSeries()
    {
        // (10*1 + 20*2) / 3 = 50 / 3
        double[] series = [10, 20];

        var wma = WeightedMovingAverage.Compute(series, window: 4);

        Assert.Equal(50.0 / 3.0, wma, precision: 9);
    }

    [Fact]
    public void Compute_OnlyUsesMostRecentWindow()
    {
        // Window 2 over [..., 30, 40] => (30*1 + 40*2)/3 = 110/3
        double[] series = [10, 20, 30, 40];

        var wma = WeightedMovingAverage.Compute(series, window: 2);

        Assert.Equal(110.0 / 3.0, wma, precision: 9);
    }

    [Fact]
    public void Compute_EmptySeries_ReturnsZero()
        => Assert.Equal(0.0, WeightedMovingAverage.Compute([], window: 3));

    [Fact]
    public void Compute_FavoursRecentOverSimpleMean_ForRisingSeries()
    {
        double[] series = [10, 20, 30, 40];
        var simpleMean = 25.0;

        var wma = WeightedMovingAverage.Compute(series, window: 4);

        Assert.True(wma > simpleMean);
    }
}
