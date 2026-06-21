using GrantAI.Application.Forecasting;
using Xunit;

namespace GrantAI.Tests.Forecasting;

public class SimpleLinearRegressionEdgeCasesTests
{
    [Fact]
    public void Fit_FlatSeries_HasZeroSlopeAndDegenerateR2()
    {
        // Constant ys: ssTot is zero, so the implementation treats R² as 1.0
        // and reports a flat line through the mean.
        double[] xs = [0, 1, 2, 3];
        double[] ys = [5, 5, 5, 5];

        var regression = SimpleLinearRegression.Fit(xs, ys);

        Assert.Equal(0.0, regression.Slope, precision: 9);
        Assert.Equal(5.0, regression.Intercept, precision: 9);
        Assert.Equal(1.0, regression.RSquared, precision: 9);
        Assert.Equal(0.0, regression.ResidualStdDev, precision: 9);
    }

    [Fact]
    public void PredictionMargin_GrowsAsXMovesAwayFromMean()
    {
        // Noisy data so the residual stddev is non-zero; otherwise PI is always 0.
        double[] xs = [0, 1, 2, 3];
        double[] ys = [1.0, 2.2, 2.8, 4.1];

        var regression = SimpleLinearRegression.Fit(xs, ys);

        var nearMean = regression.PredictionMargin(1.5, 2.0);
        var farFromMean = regression.PredictionMargin(20, 2.0);

        Assert.True(farFromMean > nearMean,
            "PI must widen as we extrapolate further from the centroid");
    }

    [Fact]
    public void PredictionMargin_WidensWithSmallerSample()
    {
        // 3-point series
        double[] xs3 = [0, 1, 2];
        double[] ys3 = [1, 2.2, 2.8];

        // 6-point series with the same slope/intercept and noise spread.
        double[] xs6 = [0, 1, 2, 3, 4, 5];
        double[] ys6 = [1, 2.2, 2.8, 4.1, 4.8, 6.1];

        var small = SimpleLinearRegression.Fit(xs3, ys3);
        var large = SimpleLinearRegression.Fit(xs6, ys6);

        var nextOrdinalSmall = xs3[^1] + 1;
        var nextOrdinalLarge = xs6[^1] + 1;

        var smallMargin = small.PredictionMargin(nextOrdinalSmall, 2.0);
        var largeMargin = large.PredictionMargin(nextOrdinalLarge, 2.0);

        Assert.True(smallMargin > largeMargin,
            $"Smaller sample should have a wider PI; got small={smallMargin}, large={largeMargin}");
    }
}
