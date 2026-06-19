using GrantAI.Application.Common;
using GrantAI.Application.Contracts.Responses;
using GrantAI.Domain.Entities;
using GrantAI.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace GrantAI.Application.Forecasting;

/// <summary>
/// Forecasts the next intake's grant cutoff for a ГОП. Built in the same
/// transparent style as <see cref="ForecastService"/> (OLS line + recency
/// weighted moving average + honest confidence and prediction interval), but
/// adapted for the realities of the grant data:
///
///   * only 2–3 yearly points exist per (group, track), so the confidence is
///     intentionally capped and the prediction interval widened — this is a
///     directional estimate, not a precise number;
///   * the two master's tracks (Profile 0–70 and Scientific-Pedagogical 0–150)
///     are forecasted independently because their scores live on different scales.
/// </summary>
public sealed class GrantForecastService : IGrantForecastService
{
    private const int WmaWindow = 3;

    private readonly ILogger<GrantForecastService> _logger;

    public GrantForecastService(ILogger<GrantForecastService> logger) => _logger = logger;

    public IReadOnlyList<GrantForecastDto> Forecast(string code, IReadOnlyList<GrantCutoffRecord> records)
    {
        code = (code ?? string.Empty).Trim().ToUpperInvariant();
        if (records.Count == 0) return [];

        var byTrack = records
            .GroupBy(r => r.MasterType)
            .OrderBy(g => (int)g.Key);

        var forecasts = new List<GrantForecastDto>();
        foreach (var trackGroup in byTrack)
        {
            var ordered = trackGroup.OrderBy(r => r.Year).ToList();
            forecasts.Add(ForecastOneTrack(code, trackGroup.Key, ordered));
        }

        return forecasts;
    }

    private GrantForecastDto ForecastOneTrack(string code, MasterType track, IReadOnlyList<GrantCutoffRecord> ordered)
    {
        var scaleMax = ordered[0].ScoreScaleMax;
        var name = ordered[^1].GroupName;

        if (ordered.Count == 1)
        {
            var only = ordered[0];
            var margin = Math.Max(2, (int)Math.Round(scaleMax * 0.05)); // ~5% of scale
            var lastCutoff = only.GrantCutoff;
            return new GrantForecastDto
            {
                Code = code,
                Name = name,
                MasterType = track,
                ScoreScaleMax = scaleMax,
                PredictedCutoff = ClampScore(lastCutoff, scaleMax),
                LowerBound = ClampScore(lastCutoff - margin, scaleMax),
                UpperBound = ClampScore(lastCutoff + margin, scaleMax),
                ConfidencePercent = 30,
                Trend = TrendDirection.Stable,
                DataPoints = 1,
                Method = "Last known value (insufficient history for regression)",
                Factors =
                [
                    $"Only one intake year ({only.Year}) is on record for this track, so the latest cutoff is reused as-is.",
                    "Import more historical PDFs to enable trend-based forecasting."
                ],
                Explanation =
                    $"Only one year is on record for '{code}' ({TrackLabel(track)}). " +
                    $"The forecast repeats the last grant cutoff ({only.GrantCutoff} out of {scaleMax}) with wide uncertainty."
            };
        }

        var xs = ordered.Select(r => (double)r.Year).ToArray();
        var ys = ordered.Select(r => (double)r.GrantCutoff).ToArray();

        var regression = SimpleLinearRegression.Fit(xs, ys);
        var nextYear = (int)xs[^1] + 1;
        var regressionForecast = regression.Predict(nextYear);
        var wma = WeightedMovingAverage.Compute(ys, WmaWindow);

        // With so few points we lean more on the moving average; the regression
        // only gets significant weight when the line genuinely fits.
        var regWeight = Statistics.Clamp(regression.RSquared, 0.20, 0.65);
        var basePrediction = regWeight * regressionForecast + (1 - regWeight) * wma;

        var observedMin = ys.Min();
        var observedMax = ys.Max();
        // Don't let the prediction wander far from the observed range; cutoff
        // movement between years is usually moderate compared to the scale.
        var predicted = Statistics.Clamp(basePrediction, observedMin - 0.1 * scaleMax, observedMax + 0.1 * scaleMax);
        predicted = Math.Clamp(predicted, 0, scaleMax);

        var slopePerYear = regression.Slope;
        var trendThreshold = Math.Max(1.0, scaleMax * 0.015);
        var trend = Classify(slopePerYear, trendThreshold);

        var factors = new List<string>
        {
            DescribeCutoffTrend(trend, slopePerYear, regression.RSquared, scaleMax),
            $"Forecast is based on {ordered.Count} intake year(s) of grant data on the {scaleMax}-point scale.",
            "Grant cutoffs swing year-to-year (quota effects, applicant pools); treat the figure as a guideline."
        };

        // Confidence is capped: with only 2–3 points the maths cannot honestly
        // promise anything close to certainty.
        var fitComponent = Statistics.Clamp(regression.RSquared, 0, 1);
        var dataComponent = Statistics.Clamp(ordered.Count / 5.0, 0, 1);
        var stabilityComponent = 1.0 - Statistics.Clamp(regression.ResidualStdDev / (scaleMax * 0.15), 0, 1);
        var confidence = 0.40 * fitComponent + 0.30 * dataComponent + 0.30 * stabilityComponent;
        confidence = Statistics.Clamp(confidence, 0.25, 0.70);

        // Use a generous t-multiplier when n is tiny; floor the margin to a
        // few points so the range stays honest even on perfectly fitted lines.
        var tMultiplier = ordered.Count >= 4 ? 2.0 : 2.8;
        var marginRaw = Math.Max(regression.PredictionMargin(nextYear, tMultiplier), scaleMax * 0.04);
        var lower = ClampScore((int)Math.Round(predicted - marginRaw), scaleMax);
        var upper = ClampScore((int)Math.Round(predicted + marginRaw), scaleMax);
        var predictedInt = ClampScore((int)Math.Round(predicted), scaleMax);

        var dto = new GrantForecastDto
        {
            Code = code,
            Name = name,
            MasterType = track,
            ScoreScaleMax = scaleMax,
            PredictedCutoff = predictedInt,
            LowerBound = lower,
            UpperBound = upper,
            ConfidencePercent = (int)Math.Round(confidence * 100),
            Trend = trend,
            DataPoints = ordered.Count,
            Method = "Linear regression blended with a recency-weighted moving average",
            Factors = factors,
            Explanation =
                $"To win a grant in '{code}' ({TrackLabel(track)}) next intake, you would " +
                $"likely need around {predictedInt} out of {scaleMax} points (range {lower}–{upper}, " +
                $"confidence {confidence * 100:0}%). Trend: {trend.ToString().ToLowerInvariant()}."
        };

        _logger.LogInformation(
            "Grant forecast for {Code}/{Track}: ~{Predicted}/{Scale} [{Lower}-{Upper}] conf {Confidence}% over {Points} years",
            code, track, dto.PredictedCutoff, scaleMax, dto.LowerBound, dto.UpperBound,
            dto.ConfidencePercent, dto.DataPoints);

        return dto;
    }

    private static TrendDirection Classify(double slopePerYear, double threshold)
    {
        if (Math.Abs(slopePerYear) < threshold) return TrendDirection.Stable;
        return slopePerYear > 0 ? TrendDirection.Rising : TrendDirection.Falling;
    }

    private static string DescribeCutoffTrend(TrendDirection trend, double slopePerYear, double rSquared, int scaleMax)
    {
        var fitNote = rSquared >= 0.6 ? "a clear" : "a weak";
        return trend switch
        {
            TrendDirection.Rising =>
                $"Grant cutoffs show {fitNote} upward trend (~{slopePerYear:0.#} points/year on a 0–{scaleMax} scale).",
            TrendDirection.Falling =>
                $"Grant cutoffs show {fitNote} downward trend (~{Math.Abs(slopePerYear):0.#} points/year on a 0–{scaleMax} scale).",
            _ => $"Grant cutoffs have been broadly flat across the available years (0–{scaleMax} scale)."
        };
    }

    private static int ClampScore(int value, int scaleMax)
        => value < 0 ? 0 : value > scaleMax ? scaleMax : value;

    private static string TrackLabel(MasterType track) => track switch
    {
        MasterType.Profile => "профильная",
        MasterType.ScientificPedagogical => "научно-педагогическая",
        _ => track.ToString()
    };
}
