using GrantAI.Domain.Enums;

namespace GrantAI.Domain.Entities;

/// <summary>
/// One normalized row of <i>grant</i> cutoff statistics for a single educational
/// program group (ГОП), in a single intake year, within a single master's track
/// (профильная / научно-педагогическая). This is the canonical shape every
/// published "СПИСОК ОБЛАДАТЕЛЕЙ ОБРАЗОВАТЕЛЬНЫХ ГРАНТОВ" PDF is mapped into.
///
/// Each source PDF contains one block per ГОП, listing every grant winner
/// sorted by descending score. The <i>grant cutoff</i> we care about is the
/// minimum score among the winners — the threshold an applicant had to clear
/// in order to receive a grant.
///
/// This is deliberately a separate entity from <see cref="AdmissionRecord"/>:
/// the latter describes the <b>entrance-test threshold</b> (порог) and is
/// reported by season twice a year; this one describes the <b>grant</b>
/// competition and is reported once per intake year. The two streams are
/// independent and must not be mixed.
/// </summary>
public sealed class GrantCutoffRecord
{
    /// <summary>
    /// Deterministic natural key (see <see cref="BuildId"/>), used as the Mongo
    /// <c>_id</c> so re-importing the same year/track/group is idempotent.
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>Intake year, e.g. 2025 for the 2025–2026 academic year.</summary>
    public int Year { get; set; }

    /// <summary>Master's track. Profile and Scientific-Pedagogical have different score scales.</summary>
    public MasterType MasterType { get; set; }

    /// <summary>Maximum score on the scale used by this track (70 for Profile, 150 for Scientific-Pedagogical).</summary>
    public int ScoreScaleMax { get; set; }

    /// <summary>Educational program group code, e.g. "M094".</summary>
    public string GroupCode { get; set; } = string.Empty;

    /// <summary>Group name, e.g. "Информационные технологии".</summary>
    public string GroupName { get; set; } = string.Empty;

    /// <summary>
    /// Minimum score among grant winners in this block — i.e. the score an
    /// applicant had to reach to win a grant. This is the headline metric.
    /// </summary>
    public int GrantCutoff { get; set; }

    /// <summary>Number of grants awarded in this block (count of winners listed).</summary>
    public int GrantsAwarded { get; set; }

    /// <summary>Highest score among winners (context only).</summary>
    public int MaxScore { get; set; }

    /// <summary>Average score among winners (context only).</summary>
    public double AvgScore { get; set; }

    /// <summary>Name of the PDF this record was imported from.</summary>
    public string SourceFile { get; set; } = string.Empty;

    public DateTime ImportedAtUtc { get; set; }

    /// <summary>
    /// Builds the deterministic identity of a record from its business key.
    /// Master's track is part of the key because the same code can legitimately
    /// appear in both tracks of the same year with non-comparable scores.
    /// </summary>
    public static string BuildId(int year, MasterType masterType, string groupCode)
        => $"{year}|{(int)masterType}|{Normalize(groupCode)}";

    private static string Normalize(string value)
        => (value ?? string.Empty).Trim().ToUpperInvariant();
}
