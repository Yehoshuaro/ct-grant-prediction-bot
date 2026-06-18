using GrantAI.Application.Common;
using GrantAI.Application.Contracts.Responses;
using GrantAI.Domain.Entities;
using GrantAI.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace GrantAI.Application.Forecasting;

/// <summary>
/// Forecasts the next campaign's threshold pass rate (% набравших порог) for a
/// group.
///
/// The model is intentionally transparent (no black-box ML, no external AI):
///   1. Reduce history to one pass-rate point per campaign (year + season).
///   2. Fit an OLS regression of pass rate over the campaign time index.
///   3. Compute a recency-weighted moving average of recent pass rates.
///   4. Blend the two, weighting the regression by how well it fits (R²).
///   5. Bound the result to a plausible range and the 0–100 percentage scale.
///   6. Derive a confidence score and an approximate prediction interval.
///
/// Every step contributes a human-readable factor to the explanation, which is
/// what makes the output trustworthy for an applicant.
/// </summary>
public sealed class ForecastService : IForecastService
{
    private const double RateFloor = 0d;
    private const double RateCeiling = 100d; // pass rate is a percentage
    private const int WmaWindow = 4;

    private readonly ILogger<ForecastService> _logger;

    public ForecastService(ILogger<ForecastService> logger) => _logger = logger;

    public ForecastDto Forecast(string code, IReadOnlyList<AdmissionRecord> records)
    {
        code = code.ToUpperInvariant();

        if (records.Count == 0)
        {
            return new ForecastDto
            {
                Code = code,
                Method = "n/a",
                ConfidencePercent = 0,
                Trend = TrendDirection.Stable,
                Explanation = $"No historical data found for '{code}', so no forecast can be produced.",
                Factors = ["No imported campaigns for this code."]
            };
        }

        var series = BuildCampaignSeries(records);

        // One campaign: nothing to regress, so we report the last value with low confidence.
        if (series.Count == 1)
        {
            var only = series[0];
            return new ForecastDto
            {
                Code = code,
                PredictedPassRate = Math.Round(only.PassRate, 1),
                LowerBound = Math.Round(Clamp(only.PassRate - 8), 1),
                UpperBound = Math.Round(Clamp(only.PassRate + 8), 1),
                ConfidencePercent = 35,
                Trend = TrendDirection.Stable,
                DataPoints = 1,
                Method = "Last known value (insufficient history for regression)",
                Factors =
                [
                    "Only one campaign is on record, so the latest pass rate is used as-is.",
                    "Import more historical files to unlock trend-based forecasting."
                ],
                Explanation =
                    $"Only one campaign is available for '{code}'. The forecast simply repeats the last " +
                    $"threshold pass rate ({only.PassRate:0.#}%) with wide uncertainty."
            };
        }

        var xs = series.Select(p => (double)p.Ordinal).ToArray();
        var passRates = series.Select(p => p.PassRate).ToArray();
        var applications = series.Select(p => (double)p.Applications).ToArray();

        var regression = SimpleLinearRegression.Fit(xs, passRates);
        var nextOrdinal = series[^1].Ordinal + 1;

        var regressionForecast = regression.Predict(nextOrdinal);
        var wma = WeightedMovingAverage.Compute(passRates, WmaWindow);

        // Trust the regression more when it explains the data well.
        var regWeight = Statistics.Clamp(regression.RSquared, 0.25, 0.80);
        var wmaWeight = 1.0 - regWeight;
        var basePrediction = regWeight * regressionForecast + wmaWeight * wma;

        var factors = new List<string>();

        // --- Pass-rate trend (from the regression slope, expressed per year) ---
        var slopePerYear = regression.Slope * 2.0; // two campaigns per year
        var rateTrend = Classify(slopePerYear, 0.75);
        factors.Add(DescribeRateTrend(rateTrend, slopePerYear, regression.RSquared));

        var observedMin = passRates.Min();
        var observedMax = passRates.Max();
        var predicted = Statistics.Clamp(basePrediction, observedMin - 10, observedMax + 10);
        predicted = Clamp(predicted);

        // --- Context: applicant-volume trend (does not move the prediction) ---
        var applicationsTrend = TrendOfSeries(xs, applications);
        if (applicationsTrend == TrendDirection.Rising)
            factors.Add("Applications to this group have been rising over recent campaigns.");
        else if (applicationsTrend == TrendDirection.Falling)
            factors.Add("Applications to this group have been falling over recent campaigns.");

        // --- Confidence: fit quality + data volume + residual stability ---
        var fitComponent = Statistics.Clamp(regression.RSquared, 0, 1);
        var dataComponent = Statistics.Clamp(series.Count / 6.0, 0, 1);
        var stabilityComponent = 1.0 - Statistics.Clamp(regression.ResidualStdDev / 20.0, 0, 1);
        var confidence = 0.45 * fitComponent + 0.30 * dataComponent + 0.25 * stabilityComponent;
        confidence = Statistics.Clamp(confidence, 0.30, 0.95);

        // --- Prediction interval (wider for small samples) ---
        var tMultiplier = series.Count >= 5 ? 2.0 : 2.6;
        var margin = Math.Max(regression.PredictionMargin(nextOrdinal, tMultiplier), 2.0);
        var lower = Clamp(predicted - margin);
        var upper = Clamp(predicted + margin);

        factors.Add(series.Count >= 5
            ? $"Forecast is based on {series.Count} campaigns of history."
            : $"Only {series.Count} campaigns of history are available, so the range is widened.");

        var dto = new ForecastDto
        {
            Code = code,
            PredictedPassRate = Math.Round(predicted, 1),
            LowerBound = Math.Round(lower, 1),
            UpperBound = Math.Round(upper, 1),
            ConfidencePercent = (int)Math.Round(confidence * 100),
            Trend = rateTrend,
            DataPoints = series.Count,
            Method = "Linear regression blended with a recency-weighted moving average",
            Factors = factors,
            Explanation =
                $"Predicted threshold pass rate for the next '{code}' campaign is {predicted:0.#}% " +
                $"(range {lower:0.#}–{upper:0.#}%, confidence {confidence * 100:0}%). " +
                $"The pass-rate trend is {rateTrend.ToString().ToLowerInvariant()}."
        };

        _logger.LogInformation(
            "Forecast for {Code}: {Predicted}% [{Lower}-{Upper}] conf {Confidence}% over {Points} campaigns",
            code, dto.PredictedPassRate, dto.LowerBound, dto.UpperBound,
            dto.ConfidencePercent, dto.DataPoints);

        return dto;
    }

    /// <summary>
    /// Collapses raw records into one pass-rate point per campaign, summing the
    /// participant and pass counts (defensive against any same-campaign dupes)
    /// and ordering oldest to newest.
    /// </summary>
    private static List<CampaignSeriesPoint> BuildCampaignSeries(IReadOnlyList<AdmissionRecord> records)
        => records
            .GroupBy(r => CampaignOrder.Ordinal(r.Year, r.Season))
            .Select(g =>
            {
                var participants = g.Sum(r => r.Participants);
                var passed = g.Sum(r => r.PassedThreshold);
                var applications = g.Sum(r => r.Applications);
                var passRate = participants > 0 ? (double)passed / participants * 100.0 : 0d;
                return new CampaignSeriesPoint(g.Key, passRate, applications);
            })
            .OrderBy(p => p.Ordinal)
            .ToList();

    private static TrendDirection TrendOfSeries(IReadOnlyList<double> xs, IReadOnlyList<double> ys)
    {
        if (ys.Count < 2) return TrendDirection.Stable;
        var regression = SimpleLinearRegression.Fit(xs, ys);
        var slopePerYear = regression.Slope * 2.0;
        var mean = Statistics.Mean(ys);
        var threshold = Math.Max(0.5, Math.Abs(mean) * 0.05);
        return Classify(slopePerYear, threshold);
    }

    private static TrendDirection Classify(double slopePerYear, double threshold)
    {
        if (Math.Abs(slopePerYear) < threshold) return TrendDirection.Stable;
        return slopePerYear > 0 ? TrendDirection.Rising : TrendDirection.Falling;
    }

    private static string DescribeRateTrend(TrendDirection trend, double slopePerYear, double rSquared)
    {
        var fitNote = rSquared >= 0.6 ? "a clear" : "a weak";
        return trend switch
        {
            TrendDirection.Rising =>
                $"Pass rates show {fitNote} upward trend (~{slopePerYear:0.#} points/year).",
            TrendDirection.Falling =>
                $"Pass rates show {fitNote} downward trend (~{Math.Abs(slopePerYear):0.#} points/year).",
            _ => "Pass rates have been broadly flat over the available campaigns."
        };
    }

    private static double Clamp(double value) => Statistics.Clamp(value, RateFloor, RateCeiling);

    private readonly record struct CampaignSeriesPoint(int Ordinal, double PassRate, int Applications);
}
