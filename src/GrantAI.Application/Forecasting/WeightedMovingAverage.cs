namespace GrantAI.Application.Forecasting;

/// <summary>
/// Linearly weighted moving average that favours recent campaigns. The newest
/// value in the window gets the highest weight, the oldest the lowest. Used to
/// damp the regression line with the most recent reality.
/// </summary>
public static class WeightedMovingAverage
{
    /// <param name="orderedOldToNew">Series sorted from oldest to newest.</param>
    /// <param name="window">How many of the most recent points to consider.</param>
    public static double Compute(IReadOnlyList<double> orderedOldToNew, int window)
    {
        if (orderedOldToNew.Count == 0) return 0d;

        var take = Math.Min(window, orderedOldToNew.Count);
        var start = orderedOldToNew.Count - take;

        double weightedSum = 0d, weightTotal = 0d;
        for (var i = 0; i < take; i++)
        {
            var weight = i + 1; // oldest in window = 1, newest = take
            weightedSum += orderedOldToNew[start + i] * weight;
            weightTotal += weight;
        }

        return weightedSum / weightTotal;
    }
}
