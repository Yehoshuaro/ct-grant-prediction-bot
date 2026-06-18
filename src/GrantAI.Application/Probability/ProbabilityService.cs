using GrantAI.Application.Common;
using GrantAI.Application.Contracts.Responses;
using GrantAI.Domain.Entities;
using Microsoft.Extensions.Logging;
using GrantAI.Application.Forecasting;
namespace GrantAI.Application.Probability;

/// <summary>
/// Threshold-clearing probability model.
///
/// The published data gives, per group and campaign, the share of participants
/// who cleared the entrance threshold. The honest estimate of "what is my chance
/// of clearing the threshold for this group?" is therefore the forecasted pass
/// rate, carried straight through with its prediction interval and confidence.
///
/// This reuses the forecast (single source of truth) and stays fully
/// explainable: the latest pass rate, its trend, and the amount of data are the
/// only inputs. It models the population base rate, not an individual's ability.
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
                Explanation = $"No historical data for '{code}', so a probability cannot be estimated.",
                Factors = ["No imported campaigns for this code."]
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
                $"In the latest campaign ({CampaignOrder.Label(latest.Year, latest.Season)}), " +
                $"{latest.PassedThreshold} of {latest.Participants} participants cleared the threshold ({latestRate:0.#}%).");
        }

        factors.Add($"Pass-rate trend for this group is {forecast.Trend.ToString().ToLowerInvariant()}.");
        factors.Add(forecast.DataPoints >= 5
            ? $"Estimate uses {forecast.DataPoints} campaigns of history."
            : $"Only {forecast.DataPoints} campaigns are available, so the range is wide.");

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
                $"For '{code}', the estimated probability of clearing the entrance threshold is " +
                $"about {probability}% (range {lower}–{upper}%), based on the forecasted pass rate. " +
                "This reflects the group's historical pass rate, not an individual candidate's ability."
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
}
