using GrantAI.Application.Importing.Grants;
using GrantAI.Domain.Enums;
using Xunit;

namespace GrantAI.Tests.Importing;

/// <summary>
/// Exercises the pure grant-PDF parser against synthetic fixtures that capture
/// the real document's quirks: a section split by master's track, blocks with
/// and without the ОВПО column, names with bracketed extra codes, and rows
/// whose ФИО wraps to a second line.
/// </summary>
public class GrantPdfParserTests
{
    /// <summary>
    /// Builds a token line with monotonically increasing X coordinates, so the
    /// parser's left-to-right ordering is preserved regardless of input order.
    /// </summary>
    private static RawGrantLine Line(double y, params string[] tokens)
    {
        var pieces = new List<RawGrantToken>(tokens.Length);
        for (var i = 0; i < tokens.Length; i++)
        {
            pieces.Add(new RawGrantToken(50 + i * 60, tokens[i]));
        }
        return new RawGrantLine(y, string.Join(' ', tokens), pieces);
    }

    [Fact]
    public void Parse_ExtractsYear_Sections_Codes_AndCutoffMinimum()
    {
        // A tiny but realistic document: a title, a profile section with two ГОПы,
        // and a научно-педагогическая section with one block. M056 has 5 winners
        // including a low quota score (30), so its cutoff must be 30 — exactly
        // the case the spec calls out for the 2025 file.
        var pdf = new RawGrantPdf("grants-2025.pdf",
        [
            new RawGrantPage(1,
            [
                Line(800, "СПИСОК", "ОБЛАДАТЕЛЕЙ", "ОБРАЗОВАТЕЛЬНЫХ", "ГРАНТОВ", "НА", "2025-2026", "УЧЕБНЫЙ", "ГОД"),
                Line(780, "ПРОФИЛЬНАЯ", "МАГИСТРАТУРА"),
                Line(760, "M056", "-", "Электротехника", "и", "энергетика"),
                Line(740, "№", "ИКТ", "ФИО", "Сумма", "баллов", "ОВПО"),
                Line(720, "1", "00012345", "Иванов", "И.", "И.", "36", "153"),
                Line(700, "2", "00012346", "Петров", "П.", "П.", "34", "013"),
                Line(680, "3", "00012347", "Сидоров", "С.", "С.", "31", "153"),
                Line(660, "4", "00012348", "Смирнов", "С.", "С.", "31", "013"),
                Line(640, "5", "00012349", "Васильев", "В.", "В.", "30", "153"),

                // Block 2 in the same section, without the ОВПО column.
                Line(600, "M094", "(KZ-HKG-538)", "-", "Информационные", "технологии", "(KZ-HKG-538)"),
                Line(580, "№", "ИКТ", "ФИО", "Сумма", "баллов"),
                Line(560, "1", "00022001", "Тестов", "Т.", "Т.", "65"),
                // Row with the ФИО wrapping onto a second line — the score line
                // still has its 8-digit ИКТ so the parser must find it.
                Line(540, "2", "00022002", "Очень-длинная-фамилия", "60"),
                Line(525, "Имя", "Отчество"), // continuation, no ИКТ → ignored
                Line(510, "3", "00022003", "Краткий", "К.", "К.", "55"),
            ]),
            new RawGrantPage(2,
            [
                Line(800, "НАУЧНО-ПЕДАГОГИЧЕСКАЯ", "МАГИСТРАТУРА"),
                Line(780, "M001", "-", "Педагогика", "и", "психология"),
                Line(760, "№", "ИКТ", "ФИО", "Сумма", "баллов", "ОВПО"),
                Line(740, "1", "00099001", "А", "А", "А", "148", "153"),
                Line(720, "2", "00099002", "Б", "Б", "Б", "140", "013"),
                Line(700, "3", "00099003", "В", "В", "В", "132", "153"),
            ])
        ]);

        var outcome = GrantPdfParser.Parse(pdf);

        Assert.True(outcome.Ok);
        Assert.Equal(2025, outcome.Year);
        Assert.Equal(3, outcome.Blocks.Count);

        // Profile M056 — minimum among 36/34/31/31/30 is 30 (the spec example).
        var m056 = outcome.Blocks.Single(b => b.GroupCode == "M056");
        Assert.Equal(MasterType.Profile, m056.MasterType);
        Assert.Equal(70, m056.ScoreScaleMax);
        Assert.Equal(5, m056.Winners.Count);
        Assert.Equal(30, m056.Winners.Min(w => w.Score));
        Assert.Equal("Электротехника и энергетика", m056.GroupName);

        // Profile M094 — block without ОВПО, plus a ФИО-continuation row.
        // The continuation line carries no ИКТ, so the 3 real winners stand.
        var m094 = outcome.Blocks.Single(b => b.GroupCode == "M094");
        Assert.Equal(MasterType.Profile, m094.MasterType);
        Assert.Equal(70, m094.ScoreScaleMax);
        Assert.Equal(3, m094.Winners.Count);
        Assert.Equal(55, m094.Winners.Min(w => w.Score));
        Assert.Equal("Информационные технологии", m094.GroupName); // bracketed code scrubbed

        // Scientific-Pedagogical M001 — different scale, scores on 0–150.
        var m001 = outcome.Blocks.Single(b => b.GroupCode == "M001");
        Assert.Equal(MasterType.ScientificPedagogical, m001.MasterType);
        Assert.Equal(150, m001.ScoreScaleMax);
        Assert.Equal(132, m001.Winners.Min(w => w.Score));
    }

    [Fact]
    public void Parse_FallsBackToYearInFileName_WhenTitleMissing()
    {
        var pdf = new RawGrantPdf("grants-2024.pdf",
        [
            new RawGrantPage(1,
            [
                Line(800, "(some", "preamble", "without", "title)"),
                Line(780, "ПРОФИЛЬНАЯ", "МАГИСТРАТУРА"),
                Line(760, "M094", "-", "Информационные", "технологии"),
                Line(740, "№", "ИКТ", "ФИО", "Сумма", "баллов", "ОВПО"),
                Line(720, "1", "00099999", "Один", "О.", "О.", "62", "153")
            ])
        ]);

        var outcome = GrantPdfParser.Parse(pdf);

        Assert.True(outcome.Ok);
        Assert.Equal(2024, outcome.Year);
        Assert.Single(outcome.Blocks);
    }

    [Fact]
    public void Parse_NoYear_SkipsDocument()
    {
        var pdf = new RawGrantPdf("misc.pdf",
        [
            new RawGrantPage(1,
            [
                Line(800, "Random", "header"),
                Line(780, "M094", "-", "Stuff")
            ])
        ]);

        var outcome = GrantPdfParser.Parse(pdf);

        Assert.False(outcome.Ok);
        Assert.NotNull(outcome.Error);
    }

    [Fact]
    public void ToRecords_BuildsCutoffMinAndKeepsAggregates()
    {
        var pdf = new RawGrantPdf("grants-2025.pdf",
        [
            new RawGrantPage(1,
            [
                Line(800, "СПИСОК", "ОБЛАДАТЕЛЕЙ", "ОБРАЗОВАТЕЛЬНЫХ", "ГРАНТОВ", "НА", "2025-2026", "УЧЕБНЫЙ", "ГОД"),
                Line(780, "ПРОФИЛЬНАЯ", "МАГИСТРАТУРА"),
                Line(760, "M056", "-", "Электротехника"),
                Line(740, "№", "ИКТ", "ФИО", "Сумма", "баллов", "ОВПО"),
                Line(720, "1", "00012345", "А", "А", "А", "60", "153"),
                Line(700, "2", "00012346", "Б", "Б", "Б", "40", "013"),
                Line(680, "3", "00012347", "В", "В", "В", "30", "153")
            ])
        ]);

        var outcome = GrantPdfParser.Parse(pdf);
        var records = GrantPdfParser.ToRecords(outcome, "grants-2025.pdf", DateTime.UtcNow);

        var record = Assert.Single(records);
        Assert.Equal("M056", record.GroupCode);
        Assert.Equal(2025, record.Year);
        Assert.Equal(30, record.GrantCutoff); // min(60, 40, 30)
        Assert.Equal(60, record.MaxScore);
        Assert.Equal(3, record.GrantsAwarded);
        Assert.Equal(70, record.ScoreScaleMax);
    }
}
