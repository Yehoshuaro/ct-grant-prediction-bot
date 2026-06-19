using GrantAI.Application.Contracts.Responses;
using GrantAI.Domain.Entities;

namespace GrantAI.Application.Forecasting;

/// <summary>
/// Forecasts the next intake's grant cutoff score (минимум среди обладателей
/// гранта) from historical records. Pure (no I/O): the caller supplies the
/// records, this returns the result.
///
/// The two master's tracks have non-comparable scales (Profile 0–70 vs
/// Scientific-Pedagogical 0–150), so the implementation forecasts each track
/// independently from the records belonging to it.
/// </summary>
public interface IGrantForecastService
{
    /// <summary>
    /// Returns one forecast per master's track present in <paramref name="records"/>.
    /// An empty list means no records were supplied for the given code.
    /// </summary>
    IReadOnlyList<GrantForecastDto> Forecast(string code, IReadOnlyList<GrantCutoffRecord> records);
}
