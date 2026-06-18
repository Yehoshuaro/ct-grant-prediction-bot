namespace GrantAI.Application.Contracts.Responses;

/// <summary>
/// Estimated probability that a participant clears the entrance threshold (КТ
/// порог) for a group, derived from the forecasted pass rate.
/// </summary>
public sealed record ProbabilityDto
{
    public string Code { get; init; } = string.Empty;

    /// <summary>Probability of clearing the threshold, as a percentage (0–100).</summary>
    public int PassProbabilityPercent { get; init; }

    /// <summary>Lower/upper bounds of that probability, as percentages.</summary>
    public int LowerBoundPercent { get; init; }
    public int UpperBoundPercent { get; init; }

    /// <summary>The forecasted pass rate this probability is based on.</summary>
    public double PredictedPassRate { get; init; }

    public int ConfidencePercent { get; init; }

    /// <summary>Number of historical campaigns behind the estimate (0 = no data).</summary>
    public int DataPoints { get; init; }

    public IReadOnlyList<string> Factors { get; init; } = [];

    public string Explanation { get; init; } = string.Empty;
}
