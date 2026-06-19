using System.Text.RegularExpressions;
using GrantAI.Domain.Entities;
using GrantAI.Domain.Enums;

namespace GrantAI.Application.Importing.Grants;

/// <summary>One winner row inside a ГОП block: the raw score, kept as an int.</summary>
public readonly record struct GrantWinnerRow(int Score);

/// <summary>
/// A ГОП block parsed out of a grant PDF: identifying metadata plus every
/// winner row found inside it. The block is the unit the import engine turns
/// into a <see cref="GrantCutoffRecord"/>.
/// </summary>
public sealed record GrantBlock(
    string GroupCode,
    string GroupName,
    MasterType MasterType,
    int ScoreScaleMax,
    IReadOnlyList<GrantWinnerRow> Winners);

/// <summary>Outcome of parsing one PDF: skip reason or the parsed blocks for an intake year.</summary>
public sealed record GrantPdfParseOutcome(
    bool Ok, string? Error, int Year, IReadOnlyList<GrantBlock> Blocks)
{
    public static GrantPdfParseOutcome Skip(string error) => new(false, error, 0, []);
    public static GrantPdfParseOutcome Success(int year, IReadOnlyList<GrantBlock> blocks)
        => new(true, null, year, blocks);
}

/// <summary>
/// Pure parser that turns a positioned-text view of a grant PDF into a list of
/// per-ГОП blocks, each carrying the master's track and every winner score.
/// Layout rules it handles end to end:
///
///   * the intake year is read from the title ("…НА 2025-2026 УЧЕБНЫЙ ГОД")
///     or, failing that, from the file name;
///   * sections are split by track: "ПРОФИЛЬНАЯ МАГИСТРАТУРА" (scale 0–70)
///     and "НАУЧНО-ПЕДАГОГИЧЕСКАЯ МАГИСТРАТУРА" (scale 0–150);
///   * blocks start at a line matching <c>M\d{3}</c>, with any bracketed extra
///     codes scrubbed from the name;
///   * the column header that follows tells us whether the block has an ОВПО
///     column — used to decide which numeric token on each row is the score
///     versus the OVPO code, instead of guessing by magnitude;
///   * winner rows are identified by their 8-digit ИКТ, so name lines that
///     wrap to a second line are ignored without losing the score.
/// </summary>
public static partial class GrantPdfParser
{
    private const int ProfileScale = 70;
    private const int ScientificPedagogicalScale = 150;

    public static GrantPdfParseOutcome Parse(RawGrantPdf pdf)
    {
        var year = ExtractYear(pdf);
        if (year is null)
            return GrantPdfParseOutcome.Skip(
                $"Could not determine the intake year from '{pdf.FileName}' or its title.");

        // Walk every page in document order, keeping track of the current
        // master's track and the open block. A block is closed and emitted
        // when a new block, a new section, or the end of the document arrives.
        var blocks = new List<GrantBlock>();
        MasterType? currentType = null;
        var currentScale = 0;
        BlockBuilder? open = null;

        void CloseOpenBlock()
        {
            if (open is null) return;
            if (open.Winners.Count > 0)
            {
                blocks.Add(open.Build());
            }
            open = null;
        }

        foreach (var page in pdf.Pages)
        {
            foreach (var line in page.Lines)
            {
                if (string.IsNullOrWhiteSpace(line.Text)) continue;

                // Section switch — закрываем текущий блок и обновляем track.
                var track = TryParseSection(line.Text);
                if (track is not null)
                {
                    CloseOpenBlock();
                    currentType = track.Value;
                    currentScale = track.Value == MasterType.Profile
                        ? ProfileScale
                        : ScientificPedagogicalScale;
                    continue;
                }

                // New ГОП block (e.g. "M094 (KZ-HKG-538) - Информационные технологии (...)").
                var blockHeader = TryParseBlockHeader(line.Text);
                if (blockHeader is not null && currentType is not null)
                {
                    CloseOpenBlock();
                    open = new BlockBuilder(
                        blockHeader.Value.Code,
                        blockHeader.Value.Name,
                        currentType.Value,
                        currentScale);
                    continue;
                }

                if (open is null || currentType is null) continue;

                // Column header row — note whether the block has an ОВПО column.
                if (LooksLikeColumnHeader(line.Text))
                {
                    open.HasOvpoColumn = line.Text.Contains("ОВПО", StringComparison.OrdinalIgnoreCase);
                    continue;
                }

                // Winner row — must carry an 8-digit ИКТ.
                var winner = TryParseWinnerRow(line, open.HasOvpoColumn);
                if (winner is not null)
                {
                    open.Winners.Add(winner.Value);
                }
            }
        }

        CloseOpenBlock();
        return GrantPdfParseOutcome.Success(year.Value, blocks);
    }

    /// <summary>
    /// Folds parsed blocks into the persistence-ready records. The grant cutoff
    /// is the minimum winner score in the block (i.e. the lowest score that
    /// still won a grant). Blocks with no winners are dropped — they are
    /// artefacts of parsing rather than real ГОПы.
    /// </summary>
    public static IReadOnlyList<GrantCutoffRecord> ToRecords(
        GrantPdfParseOutcome outcome, string sourceFile, DateTime importedAtUtc)
    {
        if (!outcome.Ok) return [];

        var records = new List<GrantCutoffRecord>(outcome.Blocks.Count);
        foreach (var block in outcome.Blocks)
        {
            if (block.Winners.Count == 0) continue;

            var scores = block.Winners.Select(w => w.Score).ToList();
            var cutoff = scores.Min();
            var max = scores.Max();
            var avg = scores.Average();

            var record = new GrantCutoffRecord
            {
                Id = GrantCutoffRecord.BuildId(outcome.Year, block.MasterType, block.GroupCode),
                Year = outcome.Year,
                MasterType = block.MasterType,
                ScoreScaleMax = block.ScoreScaleMax,
                GroupCode = block.GroupCode.ToUpperInvariant(),
                GroupName = block.GroupName,
                GrantCutoff = cutoff,
                GrantsAwarded = block.Winners.Count,
                MaxScore = max,
                AvgScore = Math.Round(avg, 2),
                SourceFile = sourceFile,
                ImportedAtUtc = importedAtUtc
            };
            records.Add(record);
        }

        return records;
    }

    /// <summary>
    /// Pulls the intake year from the document title ("…НА 2025-2026 УЧЕБНЫЙ
    /// ГОД") if present, otherwise from a <c>20\d{2}</c> in the file name. The
    /// first year of the two-year range is the intake year.
    /// </summary>
    private static int? ExtractYear(RawGrantPdf pdf)
    {
        // The title is in the first page or two; scan a small prefix.
        foreach (var page in pdf.Pages.Take(2))
        {
            foreach (var line in page.Lines)
            {
                var match = TitleYearRegex().Match(line.Text);
                if (match.Success && int.TryParse(match.Groups[1].Value, out var fromTitle))
                    return fromTitle;
            }
        }

        var fileMatch = YearRegex().Match(pdf.FileName);
        return fileMatch.Success && int.TryParse(fileMatch.Value, out var fromName)
            ? fromName
            : null;
    }

    private static MasterType? TryParseSection(string text)
    {
        var upper = text.ToUpperInvariant();
        if (upper.Contains("НАУЧНО") && upper.Contains("ПЕД"))
            return MasterType.ScientificPedagogical;
        if (upper.Contains("ПРОФИЛЬНАЯ МАГИСТРАТУРА"))
            return MasterType.Profile;
        return null;
    }

    /// <summary>
    /// Recognises a block header line such as
    /// <c>"M094 - Информационные технологии"</c> or its bracketed-code variant
    /// <c>"M094 (KZ-HKG-538) - Информационные технологии (KZ-HKG-538)"</c>.
    /// The trailing bracketed code is stripped from the returned name.
    /// </summary>
    private static (string Code, string Name)? TryParseBlockHeader(string text)
    {
        var match = BlockHeaderRegex().Match(text);
        if (!match.Success) return null;

        var code = match.Groups["code"].Value.ToUpperInvariant();
        var name = match.Groups["name"].Value.Trim();
        name = BracketedCodeRegex().Replace(name, string.Empty).Trim();
        // Collapse runs of whitespace left by the bracket removal.
        name = WhitespaceRegex().Replace(name, " ");
        return (code, name);
    }

    private static bool LooksLikeColumnHeader(string text)
    {
        return text.Contains("Сумма баллов", StringComparison.OrdinalIgnoreCase)
            || (text.Contains("ИКТ", StringComparison.OrdinalIgnoreCase)
                && text.Contains("ФИО", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// A winner row is identified by an 8-digit ИКТ token. With ОВПО present,
    /// the score is the rightmost numeric token <i>before</i> the ОВПО (a
    /// 1–3-digit number at the far right); without ОВПО, the score is simply
    /// the rightmost numeric token. We never compare magnitudes — a score like
    /// 153 (научно-пед, max 150) and an ОВПО like 013 are indistinguishable by
    /// value alone, which is exactly the trap the column rule avoids.
    /// </summary>
    private static GrantWinnerRow? TryParseWinnerRow(RawGrantLine line, bool hasOvpoColumn)
    {
        var tokens = line.Tokens;
        if (tokens.Count < 3) return null;

        var iktIndex = -1;
        for (var i = 0; i < tokens.Count; i++)
        {
            if (IktRegex().IsMatch(tokens[i].Text))
            {
                iktIndex = i;
                break;
            }
        }
        if (iktIndex < 0) return null;

        // Collect indexes of numeric tokens to the right of the ИКТ.
        var numericIndexes = new List<int>();
        for (var i = iktIndex + 1; i < tokens.Count; i++)
        {
            if (IntegerRegex().IsMatch(tokens[i].Text))
                numericIndexes.Add(i);
        }
        if (numericIndexes.Count == 0) return null;

        int scoreIndex;
        if (hasOvpoColumn && numericIndexes.Count >= 2)
        {
            // Last numeric = ОВПО; second-to-last = score.
            scoreIndex = numericIndexes[^2];
        }
        else
        {
            // Either no ОВПО column for this block, or the row has only one
            // number to the right of the ИКТ — that number is the score.
            scoreIndex = numericIndexes[^1];
        }

        if (!int.TryParse(tokens[scoreIndex].Text, out var score)) return null;
        return new GrantWinnerRow(score);
    }

    private sealed class BlockBuilder
    {
        public BlockBuilder(string code, string name, MasterType type, int scale)
        {
            Code = code;
            Name = name;
            Type = type;
            Scale = scale;
        }

        public string Code { get; }
        public string Name { get; }
        public MasterType Type { get; }
        public int Scale { get; }
        public bool HasOvpoColumn { get; set; }
        public List<GrantWinnerRow> Winners { get; } = new();

        public GrantBlock Build() => new(Code, Name, Type, Scale, Winners);
    }

    [GeneratedRegex(@"НА\s+(20\d{2})\s*[-–]\s*20\d{2}", RegexOptions.IgnoreCase)]
    private static partial Regex TitleYearRegex();

    [GeneratedRegex(@"20\d{2}")]
    private static partial Regex YearRegex();

    [GeneratedRegex(@"^(?<code>M\d{3})\s*(?:\([^)]*\))?\s*[-–]\s*(?<name>.+)$")]
    private static partial Regex BlockHeaderRegex();

    [GeneratedRegex(@"\([^)]*\)")]
    private static partial Regex BracketedCodeRegex();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();

    [GeneratedRegex(@"^0\d{7}$")]
    private static partial Regex IktRegex();

    [GeneratedRegex(@"^\d{1,3}$")]
    private static partial Regex IntegerRegex();
}
