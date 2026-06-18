using GrantAI.Application.Analytics;
using GrantAI.Domain.Entities;
using GrantAI.Domain.Enums;
using GrantAI.Tests.TestSupport;
using Xunit;

namespace GrantAI.Tests.Analytics;

public class AnalyticsServiceTests
{
    private static AnalyticsService NewService() => new(TestData.Mapper());

    [Fact]
    public void BuildOverview_AggregatesTotalsAndRates()
    {
        var records = new List<AdmissionRecord>
        {
            TestData.Record(2025, group: "M094", applications: 100, participants: 90, passed: 45),
            TestData.Record(2024, group: "M095", applications: 200, participants: 150, passed: 90),
        };

        var overview = NewService().BuildOverview(records);

        Assert.Equal(2, overview.TotalRecords);
        Assert.Equal(2, overview.TotalGroups);
        Assert.Equal(2024, overview.EarliestYear);
        Assert.Equal(2025, overview.LatestYear);
        Assert.Equal(300, overview.TotalApplications);
        Assert.Equal(240, overview.TotalParticipants);
        Assert.Equal(135, overview.TotalPassed);
        Assert.Equal(80, overview.OverallParticipationRate, precision: 2);   // 240/300
        Assert.Equal(56.25, overview.OverallPassRate, precision: 2);          // 135/240
    }

    [Fact]
    public void BuildOverview_NoData_ReturnsZeroedOverview()
    {
        var overview = NewService().BuildOverview([]);

        Assert.Equal(0, overview.TotalRecords);
        Assert.Null(overview.EarliestYear);
    }

    [Fact]
    public void BuildHistory_ComputesParticipationAndPassRateAndLabel()
    {
        var records = new List<AdmissionRecord>
        {
            TestData.Record(2025, Season.Summer, applications: 100, participants: 90, passed: 45)
        };

        var history = NewService().BuildHistory("m094", records);

        var point = Assert.Single(history.Points);
        Assert.Equal("2025 Summer", point.Label);
        Assert.Equal(90, point.ParticipationRate, precision: 2); // 90/100
        Assert.Equal(50, point.PassRate, precision: 2);          // 45/90
    }

    [Fact]
    public void BuildSpecialtyList_SummarisesLatestCampaign()
    {
        var records = new List<AdmissionRecord>
        {
            TestData.Record(2024, group: "M094", participants: 100, passed: 60),
            TestData.Record(2025, group: "M094", participants: 100, passed: 80),
        };

        var summary = Assert.Single(NewService().BuildSpecialtyList(records));

        Assert.Equal("M094", summary.Code);
        Assert.Equal(2, summary.CampaignCount);
        Assert.Equal(2025, summary.LatestYear);
        Assert.Equal(80, summary.LatestPassRate, precision: 2);
    }

    [Fact]
    public void Compare_ProducesSeasonBreakdown()
    {
        var records = new List<AdmissionRecord>
        {
            TestData.Record(2024, Season.Summer, participants: 100, passed: 80),
            TestData.Record(2024, Season.Winter, participants: 100, passed: 60),
        };

        var comparison = NewService().Compare("M094", records);

        Assert.Equal(2, comparison.BySeason.Count);
        Assert.False(string.IsNullOrWhiteSpace(comparison.Summary));
    }
}
