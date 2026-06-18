using GrantAI.Application.Contracts.Responses;
using GrantAI.Domain.Entities;

namespace GrantAI.Application.Forecasting;

/// <summary>
/// Forecasts the next campaign's threshold pass rate from historical records.
/// Pure (no I/O): the caller supplies the records, this returns the result.
/// </summary>
public interface IForecastService
{
    ForecastDto Forecast(string code, IReadOnlyList<AdmissionRecord> records);
}
