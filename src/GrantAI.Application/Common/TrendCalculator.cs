using GrantAI.Application.Forecasting;
using GrantAI.Domain.Enums;

namespace GrantAI.Application.Common;

/// <summary>
/// Classifies the direction of a time series. The slope is converted from
/// "per ordinal step" to "per year" using the actual cadence observed in the
/// x values, so a series with uneven season coverage (e.g. only summer
/// intakes for some years) does not get its trend misread.
/// </summary>
public static class TrendCalculator
{
    public static TrendDirection FromSeries(IReadOnlyList<double> xs, IReadOnlyList<double> ys)
    {
        if (ys.Count < 2) return TrendDirection.Stable;

        var regression = SimpleLinearRegression.Fit(xs, ys);
        var slopePerYear = regression.Slope * InferCampaignsPerYear(xs);
        var mean = Statistics.Mean(ys);
        var threshold = Math.Max(0.5, Math.Abs(mean) * 0.02);
        return Classify(slopePerYear, threshold);
    }

    /// <summary>
    /// Given ordinals from <see cref="CampaignOrder.Ordinal(int, Season)"/>,
    /// infers the average number of campaigns per year that produced them.
    /// With uniform cadence (winter + summer every year) this is 2; with
    /// summer-only years it is closer to 1.
    /// </summary>
    public static double InferCampaignsPerYear(IReadOnlyList<double> xs)
    {
        if (xs.Count < 2) return 2.0;
        var yearsSpan = (xs[^1] - xs[0]) / 2.0;
        if (yearsSpan <= 0) return xs.Count;
        return (xs.Count - 1) / yearsSpan;
    }

    public static TrendDirection Classify(double slopePerYear, double threshold)
    {
        if (Math.Abs(slopePerYear) < threshold) return TrendDirection.Stable;
        return slopePerYear > 0 ? TrendDirection.Rising : TrendDirection.Falling;
    }
}
