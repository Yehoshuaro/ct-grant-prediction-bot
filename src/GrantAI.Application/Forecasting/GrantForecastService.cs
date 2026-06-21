using GrantAI.Application.Common;
using GrantAI.Application.Contracts.Responses;
using GrantAI.Domain.Entities;
using GrantAI.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace GrantAI.Application.Forecasting;

/// <summary>
/// Forecasts the next intake's grant cutoff (проходной балл на грант) per ГОП
/// and per master's track. Mirrors <see cref="ForecastService"/> but the
/// confidence is capped and the prediction interval is wider because only 2-3
/// yearly points typically exist per (group, track). The two scales (Profile
/// 0-70 and Scientific-Pedagogical 0-150) are forecasted independently.
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
            // Margin ~5% of the scale, floor of 2 points so the range stays informative.
            var margin = Math.Max(2, (int)Math.Round(scaleMax * 0.05));
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
                Method = "последнее наблюдение (для регрессии недостаточно истории)",
                Factors =
                [
                    $"Доступен только один год ({only.Year}) для этого трека, прогноз повторяет его проходной балл.",
                    "Импортируйте больше PDF, чтобы включить трендовый прогноз."
                ],
                Explanation =
                    $"Для '{code}' ({TrackLabel(track)}) доступен только один год. " +
                    $"Прогноз повторяет последний проходной балл на грант ({only.GrantCutoff} из {scaleMax}) с широкой неопределённостью."
            };
        }

        var xs = new double[ordered.Count];
        var ys = new double[ordered.Count];
        for (var i = 0; i < ordered.Count; i++)
        {
            xs[i] = ordered[i].Year;
            ys[i] = ordered[i].GrantCutoff;
        }

        var regression = SimpleLinearRegression.Fit(xs, ys);
        var nextYear = (int)xs[^1] + 1;
        var regressionForecast = regression.Predict(nextYear);
        var wma = WeightedMovingAverage.Compute(ys, WmaWindow);

        var regWeight = Statistics.Clamp(regression.RSquared, 0.20, 0.65);
        var basePrediction = regWeight * regressionForecast + (1 - regWeight) * wma;

        var observedMin = ys.Min();
        var observedMax = ys.Max();
        var predicted = Statistics.Clamp(basePrediction, observedMin - 0.1 * scaleMax, observedMax + 0.1 * scaleMax);
        predicted = Math.Clamp(predicted, 0, scaleMax);

        var slopePerYear = regression.Slope;
        var trendThreshold = Math.Max(1.0, scaleMax * 0.015);
        var trend = Classify(slopePerYear, trendThreshold);

        var factors = new List<string>
        {
            DescribeCutoffTrend(trend, slopePerYear, regression.RSquared, scaleMax),
            $"Прогноз построен по {ordered.Count} годам данных на шкале 0-{scaleMax}.",
            "Проходной балл на грант сильно колеблется год к году (квоты, конкурс); относитесь к цифре как к ориентиру."
        };

        // Confidence is capped: 2-3 points cannot honestly support a high number.
        var fitComponent = Statistics.Clamp(regression.RSquared, 0, 1);
        var dataComponent = Statistics.Clamp(ordered.Count / 5.0, 0, 1);
        var stabilityComponent = 1.0 - Statistics.Clamp(regression.ResidualStdDev / (scaleMax * 0.15), 0, 1);
        var confidence = 0.40 * fitComponent + 0.30 * dataComponent + 0.30 * stabilityComponent;
        confidence = Statistics.Clamp(confidence, 0.25, 0.70);

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
            Method = "линейная регрессия со взвешенным скользящим средним",
            Factors = factors,
            Explanation =
                $"Чтобы получить грант по '{code}' ({TrackLabel(track)}) в следующем наборе, " +
                $"вероятно понадобится около {predictedInt} из {scaleMax} баллов (диапазон от {lower} до {upper}, " +
                $"уверенность {confidence * 100:0}%). Тренд: {RussianTrend(trend)}."
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
        var fitNote = rSquared >= 0.6 ? "явный" : "слабый";
        return trend switch
        {
            TrendDirection.Rising =>
                $"Проходной балл на грант показывает {fitNote} рост (около {slopePerYear:0.#} балла в год на шкале 0-{scaleMax}).",
            TrendDirection.Falling =>
                $"Проходной балл на грант показывает {fitNote} спад (около {Math.Abs(slopePerYear):0.#} балла в год на шкале 0-{scaleMax}).",
            _ => $"Проходной балл на грант стабилен по доступным годам (шкала 0-{scaleMax})."
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

    private static string RussianTrend(TrendDirection trend) => trend switch
    {
        TrendDirection.Rising => "растёт",
        TrendDirection.Falling => "снижается",
        _ => "стабилен"
    };
}
