using GrantAI.Domain.Enums;

namespace GrantAI.Application.Contracts.Responses;

/// <summary>
/// Forecast of the grant cutoff score (минимальный балл обладателя гранта)
/// for the next intake of one ГОП, in one master's track.
/// </summary>
public sealed record GrantForecastDto
{
    public string Code { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public MasterType MasterType { get; init; }
    public int ScoreScaleMax { get; init; }

    /// <summary>Predicted minimum score required to claim a grant in the next intake.</summary>
    public int PredictedCutoff { get; init; }

    /// <summary>Lower/upper bounds of the prediction interval, on the same score scale.</summary>
    public int LowerBound { get; init; }
    public int UpperBound { get; init; }

    /// <summary>Confidence as a percentage (0–100). Honestly low when only 2–3 years are on record.</summary>
    public int ConfidencePercent { get; init; }

    public TrendDirection Trend { get; init; }

    /// <summary>Number of historical intake years the forecast is based on.</summary>
    public int DataPoints { get; init; }

    public string Method { get; init; } = string.Empty;
    public IReadOnlyList<string> Factors { get; init; } = [];
    public string Explanation { get; init; } = string.Empty;
}
