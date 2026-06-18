using GrantAI.Application.Forecasting;
using GrantAI.Domain.Enums;

namespace GrantAI.Application.Common;

/// <summary>
/// Classifies the direction of a time series. Shared by the analytics and
/// forecasting engines so "rising / falling / stable" means the same thing
/// everywhere. A move is only "rising"/"falling" if its yearly slope exceeds a
/// small relative threshold, which suppresses noise on flat series.
/// </summary>
public static class TrendCalculator
{
    public static TrendDirection FromSeries(IReadOnlyList<double> xs, IReadOnlyList<double> ys)
    {
        if (ys.Count < 2) return TrendDirection.Stable;

        var regression = SimpleLinearRegression.Fit(xs, ys);
        var slopePerYear = regression.Slope * 2.0; // two campaigns per year
        var mean = Statistics.Mean(ys);
        var threshold = Math.Max(0.5, Math.Abs(mean) * 0.02);
        return Classify(slopePerYear, threshold);
    }

    public static TrendDirection Classify(double slopePerYear, double threshold)
    {
        if (Math.Abs(slopePerYear) < threshold) return TrendDirection.Stable;
        return slopePerYear > 0 ? TrendDirection.Rising : TrendDirection.Falling;
    }
}
