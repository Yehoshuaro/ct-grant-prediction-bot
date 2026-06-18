using GrantAI.Application.Forecasting;
using Xunit;

namespace GrantAI.Tests.Forecasting;

public class SimpleLinearRegressionTests
{
    [Fact]
    public void Fit_PerfectLine_RecoversSlopeInterceptAndR2()
    {
        // y = 2x + 1
        double[] xs = [0, 1, 2, 3, 4];
        double[] ys = [1, 3, 5, 7, 9];

        var regression = SimpleLinearRegression.Fit(xs, ys);

        Assert.Equal(2.0, regression.Slope, precision: 9);
        Assert.Equal(1.0, regression.Intercept, precision: 9);
        Assert.Equal(1.0, regression.RSquared, precision: 9);
        Assert.Equal(0.0, regression.ResidualStdDev, precision: 9);
        Assert.Equal(9.0, regression.Predict(4), precision: 9);
        Assert.Equal(11.0, regression.Predict(5), precision: 9);
    }

    [Fact]
    public void Fit_NoisyData_HasPositiveSlopeAndReasonableFit()
    {
        double[] xs = [0, 1, 2, 3, 4];
        double[] ys = [10, 11, 13, 14, 16];

        var regression = SimpleLinearRegression.Fit(xs, ys);

        Assert.True(regression.Slope > 0);
        Assert.InRange(regression.RSquared, 0.9, 1.0);
        Assert.True(regression.PredictionMargin(5, 2.0) > 0);
    }

    [Fact]
    public void Fit_TooFewPoints_Throws()
        => Assert.Throws<ArgumentException>(() => SimpleLinearRegression.Fit([1.0], [2.0]));

    [Fact]
    public void Fit_MismatchedLengths_Throws()
        => Assert.Throws<ArgumentException>(() => SimpleLinearRegression.Fit([1.0, 2.0], [1.0]));

    [Fact]
    public void Fit_NoVarianceInX_Throws()
        => Assert.Throws<ArgumentException>(() => SimpleLinearRegression.Fit([2.0, 2.0, 2.0], [1.0, 2.0, 3.0]));
}
