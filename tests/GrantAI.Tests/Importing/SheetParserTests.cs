using GrantAI.Application.Importing;
using GrantAI.Domain.Enums;
using Xunit;

namespace GrantAI.Tests.Importing;

public class SheetParserTests
{
    // A synthetic sheet that mirrors the real published layout: a title row, a
    // blank row, a two-row merged header, two genuine ГОП rows, one special
    // non-ГОП line ("ВЭ …") and an "ИТОГО" total row.
    private static RawSheet RealisticSheet(string name) => new(
        name,
        [
            ["Статистические данные по итогам комплексного тестирования в магистратуру 2024 г. (зима)",
                null, null, null, null, null, null, null, null, null],
            [null, null, null, null, null, null, null, null, null, null],
            ["№", "Код ГОП", "Наименование групп образовательных программ",
                "Количество заявлений", "Количество участников", "%",
                "Набрали порог", null, "Не набрали порог", null],
            [null, null, null, null, null, null, "кол-во", "%", "кол-во", "%"],
            ["1", "M001", "Педагогика и психология", "582", "537", "92.27", "223", "41.53", "314", "58.47"],
            ["2", "M002", "Дошкольное обучение", "157", "137", "87.26", "29", "21.17", "108", "78.83"],
            ["148", "ВЭ по иностранному языку (для негражданских ОВПО)", null,
                "196", "179", "91.33", "17", "9.5", "162", "90.5"],
            [null, null, "ИТОГО", "10625", "9722", "91.5", "3077", "31.6", "6645", "68.4"]
        ]);

    [Fact]
    public void Parse_ReadsCampaignFromSheetName_AndSkipsTotalsAndNonGopRows()
    {
        var outcome = SheetParser.Parse(RealisticSheet("2024-зима-рус"), "file.xlsx", DateTime.UtcNow);

        Assert.True(outcome.Ok);
        Assert.Equal(2024, outcome.Year);
        Assert.Equal(Season.Winter, outcome.Season);

        // Only the two real ГОП rows survive; ВЭ and ИТОГО are skipped.
        Assert.Equal(2, outcome.Rows.Count);
        Assert.All(outcome.Rows, r => Assert.True(r.Result.Ok));

        var first = outcome.Rows[0].Result.Record!;
        Assert.Equal("M001", first.GroupCode);
        Assert.Equal(582, first.Applications);
        Assert.Equal(537, first.Participants);
        Assert.Equal(223, first.PassedThreshold);
    }

    [Fact]
    public void Parse_FallsBackToTitleRowWhenSheetNameLacksCampaign()
    {
        var outcome = SheetParser.Parse(RealisticSheet("Лист1"), "file.xlsx", DateTime.UtcNow);

        Assert.True(outcome.Ok);
        Assert.Equal(2024, outcome.Year);
        Assert.Equal(Season.Winter, outcome.Season);
    }

    [Fact]
    public void Parse_NoCampaignAnywhere_SkipsSheet()
    {
        var sheet = new RawSheet("Sheet1", new List<IReadOnlyList<string?>>
        {
            new List<string?> { "Какой-то заголовок без даты" },
            new List<string?> { "Код ГОП", "Количество заявлений", "Количество участников", "Набрали порог кол-во" },
            new List<string?> { "M001", "10", "9", "5" }
        });

        var outcome = SheetParser.Parse(sheet, "file.xlsx", DateTime.UtcNow);

        Assert.False(outcome.Ok);
        Assert.Contains("year/season", outcome.Error!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Parse_NoHeaderRow_SkipsSheet()
    {
        var sheet = new RawSheet("2024-зима-рус", new List<IReadOnlyList<string?>>
        {
            new List<string?> { "Статистика 2024 (зима)" },
            new List<string?> { "just", "some", "values" },
            new List<string?> { "1", "2", "3" }
        });

        var outcome = SheetParser.Parse(sheet, "file.xlsx", DateTime.UtcNow);

        Assert.False(outcome.Ok);
    }
}
