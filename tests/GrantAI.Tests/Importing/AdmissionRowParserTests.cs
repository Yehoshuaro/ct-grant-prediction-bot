using GrantAI.Application.Importing;
using GrantAI.Domain.Enums;
using Xunit;

namespace GrantAI.Tests.Importing;

public class AdmissionRowParserTests
{
    // Real two-row header → maps code=1, name=2, applications=3, participants=4,
    // passed=6, failed=8.
    private static readonly string?[] Header =
    [
        "№ \nп/п", "Код \nГОП   ", "Наименование групп \nобразовательных программ",
        "Количество заявлений  КТ", "Количество участников КТ", "% ",
        "Набрали порог", null, "Не набрали порог"
    ];

    private static readonly string?[] SubHeader =
    [
        null, null, null, null, null, null, "кол-во", "%", "кол-во", "%"
    ];

    private static ColumnMap RealMap() => ColumnMapper.Resolve(Header, SubHeader);

    [Fact]
    public void Parse_ValidRow_ProducesNormalizedRecord()
    {
        // № Код Наименование Заявл. Участн. % Набрали-кол Набрали-% Ненабрали-кол Ненабрали-%
        string?[] row =
        [
            "1", "M001", "Педагогика и психология", "582", "537", "92.27",
            "223", "41.53", "314", "58.47"
        ];

        var result = AdmissionRowParser.Parse(row, RealMap(), 2024, Season.Winter, "file1.xlsx", DateTime.UtcNow);

        Assert.True(result.Ok);
        var record = result.Record!;
        Assert.Equal("2024|2|M001", record.Id);
        Assert.Equal(2024, record.Year);
        Assert.Equal(Season.Winter, record.Season);
        Assert.Equal("M001", record.GroupCode);
        Assert.Equal("Педагогика и психология", record.GroupName);
        Assert.Equal(582, record.Applications);
        Assert.Equal(537, record.Participants);
        Assert.Equal(223, record.PassedThreshold);
        Assert.Equal(314, record.FailedThreshold);
    }

    [Fact]
    public void Parse_NonNumericParticipants_ReturnsError()
    {
        string?[] row =
        [
            "1", "M001", "Педагогика", "582", "abc", "92.27", "223", "41.53", "314", "58.47"
        ];

        var result = AdmissionRowParser.Parse(row, RealMap(), 2024, Season.Winter, "file1.xlsx", DateTime.UtcNow);

        Assert.False(result.Ok);
        Assert.Contains("participants", result.Error!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Parse_MissingFailedColumn_DerivesFailedFromParticipantsMinusPassed()
    {
        // Single-row header without a "не набрали" column.
        string?[] header =
        [
            "Код ГОП", "Количество заявлений", "Количество участников", "Набрали порог кол-во"
        ];
        var map = ColumnMapper.Resolve(header);

        string?[] row = ["M005", "644", "589", "92"];

        var result = AdmissionRowParser.Parse(row, map, 2024, Season.Summer, "file.xlsx", DateTime.UtcNow);

        Assert.True(result.Ok);
        Assert.Equal(589 - 92, result.Record!.FailedThreshold);
    }

    [Fact]
    public void Parse_BlankCode_ReturnsError()
    {
        string?[] row = ["1", "  ", "Имя", "10", "9", "90", "5", "55", "4", "45"];

        var result = AdmissionRowParser.Parse(row, RealMap(), 2024, Season.Summer, "file.xlsx", DateTime.UtcNow);

        Assert.False(result.Ok);
    }
}
