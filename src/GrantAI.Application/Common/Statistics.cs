namespace GrantAI.Application.Common;

/// <summary>
/// Small, dependency-free statistics helpers shared by the forecasting and
/// probability engines. Kept pure (no state) so they are trivially unit-tested.
/// </summary>
public static class Statistics
{
    public static double Mean(IReadOnlyList<double> values)
    {
        if (values.Count == 0) return 0d;
        double sum = 0d;
        for (var i = 0; i < values.Count; i++) sum += values[i];
        return sum / values.Count;
    }

    /// <summary>Sample standard deviation (n-1). Returns 0 for fewer than 2 points.</summary>
    public static double SampleStdDev(IReadOnlyList<double> values)
    {
        if (values.Count < 2) return 0d;
        var mean = Mean(values);
        double sumSq = 0d;
        for (var i = 0; i < values.Count; i++)
        {
            var d = values[i] - mean;
            sumSq += d * d;
        }
        return Math.Sqrt(sumSq / (values.Count - 1));
    }

    /// <summary>
    /// Cumulative distribution function of the standard normal distribution,
    /// implemented via an Abramowitz &amp; Stegun erf approximation
    /// (max abs error ~1.5e-7). Used to turn a z-score into a probability.
    /// </summary>
    public static double NormalCdf(double z)
        => 0.5 * (1.0 + Erf(z / Math.Sqrt(2.0)));

    /// <summary>Clamps a value into the inclusive [min, max] range.</summary>
    public static double Clamp(double value, double min, double max)
        => value < min ? min : value > max ? max : value;

    private static double Erf(double x)
    {
        // Save the sign of x; erf is an odd function.
        var sign = x < 0 ? -1.0 : 1.0;
        x = Math.Abs(x);

        // Abramowitz & Stegun formula 7.1.26.
        const double a1 = 0.254829592;
        const double a2 = -0.284496736;
        const double a3 = 1.421413741;
        const double a4 = -1.453152027;
        const double a5 = 1.061405429;
        const double p = 0.3275911;

        var t = 1.0 / (1.0 + p * x);
        var y = 1.0 - (((((a5 * t + a4) * t) + a3) * t + a2) * t + a1) * t * Math.Exp(-x * x);
        return sign * y;
    }
}
