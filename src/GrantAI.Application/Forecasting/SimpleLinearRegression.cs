namespace GrantAI.Application.Forecasting;

/// <summary>
/// Ordinary least squares fit of <c>y = intercept + slope * x</c>, plus the
/// goodness-of-fit (R²), the residual standard deviation, and an approximate
/// prediction interval. Dependency-free and immutable so it is easy to test.
/// </summary>
public sealed class SimpleLinearRegression
{
    public double Slope { get; }
    public double Intercept { get; }
    public double RSquared { get; }

    /// <summary>Standard deviation of residuals (s), using n-2 degrees of freedom.</summary>
    public double ResidualStdDev { get; }

    public int N { get; }

    private readonly double _meanX;
    private readonly double _sxx; // Σ(x − x̄)²

    private SimpleLinearRegression(
        double slope, double intercept, double rSquared,
        double residualStdDev, int n, double meanX, double sxx)
    {
        Slope = slope;
        Intercept = intercept;
        RSquared = rSquared;
        ResidualStdDev = residualStdDev;
        N = n;
        _meanX = meanX;
        _sxx = sxx;
    }

    public static SimpleLinearRegression Fit(IReadOnlyList<double> xs, IReadOnlyList<double> ys)
    {
        if (xs.Count != ys.Count)
            throw new ArgumentException("x and y must have the same length.");
        if (xs.Count < 2)
            throw new ArgumentException("At least two points are required for a regression.");

        var n = xs.Count;
        double meanX = 0, meanY = 0;
        for (var i = 0; i < n; i++) { meanX += xs[i]; meanY += ys[i]; }
        meanX /= n;
        meanY /= n;

        double sxx = 0, sxy = 0;
        for (var i = 0; i < n; i++)
        {
            var dx = xs[i] - meanX;
            sxx += dx * dx;
            sxy += dx * (ys[i] - meanY);
        }

        if (sxx <= double.Epsilon)
            throw new ArgumentException("x values have no variance; cannot fit a line.");

        var slope = sxy / sxx;
        var intercept = meanY - slope * meanX;

        double ssRes = 0, ssTot = 0;
        for (var i = 0; i < n; i++)
        {
            var predicted = intercept + slope * xs[i];
            var resid = ys[i] - predicted;
            ssRes += resid * resid;
            var totalDev = ys[i] - meanY;
            ssTot += totalDev * totalDev;
        }

        var rSquared = ssTot <= double.Epsilon ? 1.0 : 1.0 - ssRes / ssTot;
        var df = Math.Max(1, n - 2);
        var residualStdDev = Math.Sqrt(ssRes / df);

        return new SimpleLinearRegression(slope, intercept, rSquared, residualStdDev, n, meanX, sxx);
    }

    public double Predict(double x) => Intercept + Slope * x;

    /// <summary>
    /// Half-width of an approximate prediction interval for a single new
    /// observation at <paramref name="x"/>:
    /// <c>t · s · sqrt(1 + 1/n + (x − x̄)² / Σ(x − x̄)²)</c>.
    /// </summary>
    public double PredictionMargin(double x, double tMultiplier)
    {
        var dx = x - _meanX;
        var sePred = ResidualStdDev * Math.Sqrt(1.0 + 1.0 / N + dx * dx / _sxx);
        return tMultiplier * sePred;
    }
}
