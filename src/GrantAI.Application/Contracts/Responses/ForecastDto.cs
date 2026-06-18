using GrantAI.Domain.Enums;

namespace GrantAI.Application.Contracts.Responses;

/// <summary>Output of the forecasting engine: the next campaign's threshold pass rate.</summary>
public sealed record ForecastDto
{
    public string Code { get; init; } = string.Empty;

    /// <summary>Predicted threshold pass rate for the next campaign, as a percentage.</summary>
    public double PredictedPassRate { get; init; }

    /// <summary>Lower/upper bounds of the prediction interval, as percentages.</summary>
    public double LowerBound { get; init; }
    public double UpperBound { get; init; }

    /// <summary>Confidence as a percentage (0–100).</summary>
    public int ConfidencePercent { get; init; }

    public TrendDirection Trend { get; init; }

    /// <summary>Number of historical campaigns the forecast is based on.</summary>
    public int DataPoints { get; init; }

    public string Method { get; init; } = string.Empty;

    /// <summary>Human readable factors that drove the prediction.</summary>
    public IReadOnlyList<string> Factors { get; init; } = [];

    public string Explanation { get; init; } = string.Empty;
}
