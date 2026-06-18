using GrantAI.Application.Importing;
using Xunit;

namespace GrantAI.Tests.Importing;

public class ColumnMapperTests
{
    // The real two-row merged header used by every bundled sample file. Row 3 is
    // the header ("Набрали порог"/"Не набрали порог" each span two columns) and
    // row 4 is the sub-header ("кол-во" / "%"). Keeping the exact strings here
    // guarantees the shipped sample data stays importable.
    private static readonly string?[] RealHeader =
    [
        "№ \nп/п", "Код \nГОП   ", "Наименование групп \nобразовательных программ",
        "Количество заявлений  КТ", "Количество участников КТ", "% ",
        "Набрали порог", null, "Не набрали порог"
    ];

    private static readonly string?[] RealSubHeader =
    [
        null, null, null, null, null, null, "кол-во", "%", "кол-во", "%"
    ];

    [Fact]
    public void Resolve_RealTwoRowHeader_MapsColumnsToTheCountFields()
    {
        var map = ColumnMapper.Resolve(RealHeader, RealSubHeader);

        Assert.Empty(ColumnMapper.MissingRequired(map));
        Assert.Equal(1, map.IndexOf(AdmissionColumn.GroupCode));
        Assert.Equal(2, map.IndexOf(AdmissionColumn.GroupName));
        Assert.Equal(3, map.IndexOf(AdmissionColumn.Applications));
        Assert.Equal(4, map.IndexOf(AdmissionColumn.Participants));
        // The count column wins over the adjacent percentage column.
        Assert.Equal(6, map.IndexOf(AdmissionColumn.PassedThreshold));
        Assert.Equal(8, map.IndexOf(AdmissionColumn.FailedThreshold));
    }

    [Fact]
    public void Resolve_SingleRowHeader_StillResolvesRequiredColumns()
    {
        string?[] header =
        [
            "Код ГОП", "Наименование", "Количество заявлений",
            "Количество участников", "Набрали порог кол-во", "Не набрали порог кол-во"
        ];

        var map = ColumnMapper.Resolve(header);

        Assert.Empty(ColumnMapper.MissingRequired(map));
        Assert.Equal(0, map.IndexOf(AdmissionColumn.GroupCode));
        Assert.Equal(2, map.IndexOf(AdmissionColumn.Applications));
        Assert.Equal(3, map.IndexOf(AdmissionColumn.Participants));
        Assert.Equal(4, map.IndexOf(AdmissionColumn.PassedThreshold));
        Assert.Equal(5, map.IndexOf(AdmissionColumn.FailedThreshold));
    }

    [Fact]
    public void Resolve_MissingThresholdColumn_IsReportedAsMissingRequired()
    {
        string?[] header = ["Код ГОП", "Количество заявлений", "Количество участников"];

        var map = ColumnMapper.Resolve(header);

        Assert.Contains(AdmissionColumn.PassedThreshold, ColumnMapper.MissingRequired(map));
    }

    [Fact]
    public void Classify_DistinguishesFailedFromPassedAndIgnoresPercent()
    {
        Assert.Equal(AdmissionColumn.PassedThreshold, ColumnMapper.Classify("nabrali porog kol vo"));
        Assert.Equal(AdmissionColumn.FailedThreshold, ColumnMapper.Classify("ne nabrali porog kol vo"));
        // Percentage sub-columns (no "кол-во") are intentionally not data columns.
        Assert.Null(ColumnMapper.Classify("nabrali porog"));
    }

    [Theory]
    [InlineData("Код \nГОП   ", "kod gop")]
    [InlineData("  Участников  ", "uchastnikov")]
    [InlineData("Количество заявлений  КТ", "kolichestvo zayavleniy kt")]
    public void Normalize_TransliteratesAndCleans(string input, string expected)
        => Assert.Equal(expected, ColumnMapper.Normalize(input));

    [Fact]
    public void Normalize_BlankInput_ReturnsEmpty()
        => Assert.Equal(string.Empty, ColumnMapper.Normalize("   "));
}
