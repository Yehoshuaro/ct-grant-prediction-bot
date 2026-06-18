using GrantAI.Application.Contracts.Responses;
using GrantAI.Domain.Entities;

namespace GrantAI.Application.Analytics;

/// <summary>
/// Reusable analytics over admission records: history series with trends,
/// season / track comparisons, specialty summaries and the global overview.
/// All methods are pure and operate on records supplied by the caller.
/// </summary>
public interface IAnalyticsService
{
    AdmissionHistoryDto BuildHistory(string code, IReadOnlyList<AdmissionRecord> records);

    ComparisonDto Compare(string code, IReadOnlyList<AdmissionRecord> records);

    IReadOnlyList<SpecialtySummaryDto> BuildSpecialtyList(IReadOnlyList<AdmissionRecord> records);

    StatisticsOverviewDto BuildOverview(IReadOnlyList<AdmissionRecord> records);
}
