using GrantAI.Application.Common;
using GrantAI.Application.Contracts.Responses;
using GrantAI.Application.Forecasting;
using GrantAI.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace GrantAI.Application.Probability;

/// <summary>
/// Estimates the probability of clearing the entrance threshold (КТ порог) for
/// a group. This is the population pass rate carried straight through from the
/// forecast: it answers "что покажет группа", not "что покажу я". It is
/// deliberately distinct from a grant-cutoff estimate, which is a competitive
/// score and lives in <see cref="GrantForecastService"/>.
/// </summary>
public sealed class ProbabilityService : IProbabilityService
{
    private readonly IForecastService _forecast;
    private readonly ILogger<ProbabilityService> _logger;

    public ProbabilityService(IForecastService forecast, ILogger<ProbabilityService> logger)
    {
        _forecast = forecast;
        _logger = logger;
    }

    public ProbabilityDto Calculate(string code, IReadOnlyList<AdmissionRecord> records)
    {
        code = code.ToUpperInvariant();

        var forecast = _forecast.Forecast(code, records);

        if (records.Count == 0 || forecast.ConfidencePercent == 0)
        {
            return new ProbabilityDto
            {
                Code = code,
                PassProbabilityPercent = 0,
                DataPoints = 0,
                Explanation = $"Нет исторических данных для '{code}', оценка шанса не строится.",
                Factors = ["Кампании по этому коду не импортированы."]
            };
        }

        var probability = (int)Math.Round(Statistics.Clamp(forecast.PredictedPassRate, 0, 100));
        var lower = (int)Math.Round(Statistics.Clamp(forecast.LowerBound, 0, 100));
        var upper = (int)Math.Round(Statistics.Clamp(forecast.UpperBound, 0, 100));

        var factors = new List<string>();

        var latest = LatestCampaign(records);
        if (latest is not null)
        {
            var latestRate = latest.Participants > 0
                ? (double)latest.PassedThreshold / latest.Participants * 100.0
                : 0d;
            factors.Add(
                $"В последней кампании ({CampaignOrder.Label(latest.Year, latest.Season)}) порог " +
                $"набрали {latest.PassedThreshold} из {latest.Participants} участников ({latestRate:0.#}%).");
        }

        factors.Add($"Тренд по доле прохождения порога в группе: {RussianTrend(forecast.Trend)}.");
        factors.Add(forecast.DataPoints >= 5
            ? $"Оценка построена по {forecast.DataPoints} кампаниям истории."
            : $"Доступно только {forecast.DataPoints} кампании истории, поэтому диапазон широкий.");
        factors.Add("Это шанс пройти порог КТ в группе, не шанс получить грант. Для гранта используйте /grant.");

        var dto = new ProbabilityDto
        {
            Code = code,
            PassProbabilityPercent = probability,
            LowerBoundPercent = lower,
            UpperBoundPercent = upper,
            PredictedPassRate = forecast.PredictedPassRate,
            ConfidencePercent = forecast.ConfidencePercent,
            DataPoints = forecast.DataPoints,
            Factors = factors,
            Explanation =
                $"Для '{code}' оценка вероятности пройти порог КТ составляет около {probability}% " +
                $"(от {lower} до {upper}%) на основе прогноза доли прошедших порог. Это характеристика " +
                "группы, а не отдельного абитуриента, и это не шанс получить грант."
        };

        _logger.LogInformation(
            "Chance for {Code}: {Probability}% [{Lower}-{Upper}] over {Points} campaigns",
            code, dto.PassProbabilityPercent, dto.LowerBoundPercent, dto.UpperBoundPercent, dto.DataPoints);

        return dto;
    }

    private static AdmissionRecord? LatestCampaign(IReadOnlyList<AdmissionRecord> records)
    {
        AdmissionRecord? best = null;
        var bestOrdinal = int.MinValue;
        foreach (var r in records)
        {
            var ordinal = CampaignOrder.Ordinal(r.Year, r.Season);
            if (ordinal > bestOrdinal)
            {
                bestOrdinal = ordinal;
                best = r;
            }
        }
        return best;
    }

    private static string RussianTrend(Domain.Enums.TrendDirection trend) => trend switch
    {
        Domain.Enums.TrendDirection.Rising => "растёт",
        Domain.Enums.TrendDirection.Falling => "снижается",
        _ => "стабилен"
    };
}
