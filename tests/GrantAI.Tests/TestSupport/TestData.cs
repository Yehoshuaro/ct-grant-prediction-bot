using AutoMapper;
using GrantAI.Application.Mapping;
using GrantAI.Domain.Entities;
using GrantAI.Domain.Enums;

namespace GrantAI.Tests.TestSupport;

/// <summary>
/// Builders and helpers shared across the test suite so individual tests stay
/// focused on the behaviour under test rather than on record construction.
/// </summary>
internal static class TestData
{
    /// <summary>Builds a single admission record (one group, one campaign) with sensible defaults.</summary>
    public static AdmissionRecord Record(
        int year,
        Season season = Season.Summer,
        string group = "M094",
        string name = "Test Group",
        int applications = 100,
        int participants = 90,
        int passed = 45,
        int? failed = null)
    {
        return new AdmissionRecord
        {
            Id = AdmissionRecord.BuildId(year, season, group),
            Year = year,
            Season = season,
            GroupCode = group.ToUpperInvariant(),
            GroupName = name,
            Applications = applications,
            Participants = participants,
            PassedThreshold = passed,
            FailedThreshold = failed ?? Math.Max(participants - passed, 0),
            SourceFile = "test.xlsx",
            ImportedAtUtc = DateTime.UtcNow
        };
    }

    /// <summary>
    /// A summer-only series with a clearly rising threshold pass rate
    /// (60 → 66 → 72 → 78 → 84 %) and rising applications, for one group.
    /// </summary>
    public static List<AdmissionRecord> RisingSeries(string group = "M094")
        =>
        [
            Record(2021, group: group, applications: 120, participants: 100, passed: 60),
            Record(2022, group: group, applications: 124, participants: 100, passed: 66),
            Record(2023, group: group, applications: 130, participants: 100, passed: 72),
            Record(2024, group: group, applications: 140, participants: 100, passed: 78),
            Record(2025, group: group, applications: 150, participants: 100, passed: 84),
        ];

    /// <summary>A real AutoMapper instance built from the production profile.</summary>
    public static IMapper Mapper()
    {
        var configuration = new MapperConfiguration(cfg => cfg.AddProfile<MappingProfile>());
        return configuration.CreateMapper();
    }
}
