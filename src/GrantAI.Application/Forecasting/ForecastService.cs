using GrantAI.Application.Common;
using GrantAI.Application.Common.Telemetry;
using GrantAI.Application.Contracts.Responses;
using GrantAI.Domain.Entities;
using GrantAI.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace GrantAI.Application.Forecasting;

/// <summary>
/// Forecasts the next campaign's threshold pass rate for a program group.
/// Pipeline: collapse to one point per campaign, fit an OLS line over the
/// campaign ordinal axis, take a recency-weighted moving average, blend the
/// two by R^2, clamp to [0, 100] and a plausible range around the observed
/// data, and report a confidence and prediction interval.
/// </summary>
public sealed class ForecastService : IForecastService
{
    private const double RateFloor = 0d;
    private const double RateCeiling = 100d;
    private const int WmaWindow = 4;

    private readonly ILogger<ForecastService> _logger;

    public ForecastService(ILogger<ForecastService> logger) => _logger = logger;

    public ForecastDto Forecast(string code, IReadOnlyList<AdmissionRecord> records)
    {
        code = code.ToUpperInvariant();

        using var activity = GrantAiTelemetry.Activity.StartActivity("forecast.threshold");
        activity?.SetTag("grantai.code", code);
        activity?.SetTag("grantai.points", records.Count);

        try
        {
            return ForecastCore(code, records);
        }
        finally
        {
            GrantAiTelemetry.ForecastsServed.Add(1, new KeyValuePair<string, object?>("kind", "threshold"));
        }
    }

    private ForecastDto ForecastCore(string code, IReadOnlyList<AdmissionRecord> records)
    {
        if (records.Count == 0)
        {
            return new ForecastDto
            {
                Code = code,
                Method = "нет данных",
                ConfidencePercent = 0,
                Trend = TrendDirection.Stable,
                Explanation = $"Нет исторических данных для '{code}', прогноз не строится.",
                Factors = ["Кампании по этому коду не импортированы."]
            };
        }

        var series = BuildCampaignSeries(records);

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
                Method = "последнее наблюдение (для регрессии недостаточно истории)",
                Factors =
                [
                    "На счету только одна кампания, поэтому прогноз повторяет её результат.",
                    "Импортируйте больше истории, чтобы включить трендовый прогноз."
                ],
                Explanation =
                    $"Для '{code}' на счету только одна кампания. Прогноз повторяет последнюю долю " +
                    $"прошедших порог ({only.PassRate:0.#}%) с широкой неопределённостью."
            };
        }

        var xs = new double[series.Count];
        var passRates = new double[series.Count];
        var applications = new double[series.Count];
        for (var i = 0; i < series.Count; i++)
        {
            xs[i] = series[i].Ordinal;
            passRates[i] = series[i].PassRate;
            applications[i] = series[i].Applications;
        }

        var regression = SimpleLinearRegression.Fit(xs, passRates);
        var nextOrdinal = series[^1].Ordinal + 1;

        var regressionForecast = regression.Predict(nextOrdinal);
        var wma = WeightedMovingAverage.Compute(passRates, WmaWindow);

        var regWeight = Statistics.Clamp(regression.RSquared, 0.25, 0.80);
        var wmaWeight = 1.0 - regWeight;
        var basePrediction = regWeight * regressionForecast + wmaWeight * wma;

        var factors = new List<string>();

        // Slope is per campaign (Ordinal step = 1 campaign). To express it per
        // year, divide by the average campaigns-per-year observed in the series.
        var campaignsPerYear = TrendCalculator.InferCampaignsPerYear(xs);
        var slopePerYear = regression.Slope * campaignsPerYear;
        var rateTrend = Classify(slopePerYear, 0.75);
        factors.Add(DescribeRateTrend(rateTrend, slopePerYear, regression.RSquared));

        var observedMin = passRates.Min();
        var observedMax = passRates.Max();
        var predicted = Statistics.Clamp(basePrediction, observedMin - 10, observedMax + 10);
        predicted = Clamp(predicted);

        var applicationsTrend = TrendOfSeries(xs, applications, campaignsPerYear);
        if (applicationsTrend == TrendDirection.Rising)
            factors.Add("Заявок в этой группе становится больше от кампании к кампании.");
        else if (applicationsTrend == TrendDirection.Falling)
            factors.Add("Заявок в этой группе становится меньше от кампании к кампании.");

        var fitComponent = Statistics.Clamp(regression.RSquared, 0, 1);
        var dataComponent = Statistics.Clamp(series.Count / 6.0, 0, 1);
        var stabilityComponent = 1.0 - Statistics.Clamp(regression.ResidualStdDev / 20.0, 0, 1);
        var confidence = 0.45 * fitComponent + 0.30 * dataComponent + 0.25 * stabilityComponent;
        confidence = Statistics.Clamp(confidence, 0.30, 0.95);

        var tMultiplier = series.Count >= 5 ? 2.0 : 2.6;
        var margin = Math.Max(regression.PredictionMargin(nextOrdinal, tMultiplier), 2.0);
        var lower = Clamp(predicted - margin);
        var upper = Clamp(predicted + margin);

        factors.Add(series.Count >= 5
            ? $"Прогноз построен по {series.Count} кампаниям истории."
            : $"Доступно только {series.Count} кампании истории, поэтому диапазон шире обычного.");

        var dto = new ForecastDto
        {
            Code = code,
            PredictedPassRate = Math.Round(predicted, 1),
            LowerBound = Math.Round(lower, 1),
            UpperBound = Math.Round(upper, 1),
            ConfidencePercent = (int)Math.Round(confidence * 100),
            Trend = rateTrend,
            DataPoints = series.Count,
            Method = "линейная регрессия со взвешенным скользящим средним",
            Factors = factors,
            Explanation =
                $"Прогноз доли участников, набравших порог, для '{code}' в следующей кампании: " +
                $"{predicted:0.#}% (от {lower:0.#} до {upper:0.#}%, уверенность {confidence * 100:0}%). " +
                $"Тренд по доле порога: {RussianTrend(rateTrend)}."
        };

        _logger.LogInformation(
            "Forecast for {Code}: {Predicted}% [{Lower}-{Upper}] conf {Confidence}% over {Points} campaigns",
            code, dto.PredictedPassRate, dto.LowerBound, dto.UpperBound,
            dto.ConfidencePercent, dto.DataPoints);

        return dto;
    }

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

    private static TrendDirection TrendOfSeries(IReadOnlyList<double> xs, IReadOnlyList<double> ys, double campaignsPerYear)
    {
        if (ys.Count < 2) return TrendDirection.Stable;
        var regression = SimpleLinearRegression.Fit(xs, ys);
        var slopePerYear = regression.Slope * campaignsPerYear;
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
        var fitNote = rSquared >= 0.6 ? "явный" : "слабый";
        return trend switch
        {
            TrendDirection.Rising =>
                $"Доля прошедших порог показывает {fitNote} рост (около {slopePerYear:0.#} п.п. в год).",
            TrendDirection.Falling =>
                $"Доля прошедших порог показывает {fitNote} спад (около {Math.Abs(slopePerYear):0.#} п.п. в год).",
            _ => "Доля прошедших порог в среднем стабильна по доступным кампаниям."
        };
    }

    private static string RussianTrend(TrendDirection trend) => trend switch
    {
        TrendDirection.Rising => "растёт",
        TrendDirection.Falling => "снижается",
        _ => "стабилен"
    };

    private static double Clamp(double value) => Statistics.Clamp(value, RateFloor, RateCeiling);

    private readonly record struct CampaignSeriesPoint(int Ordinal, double PassRate, int Applications);
}
