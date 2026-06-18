using GrantAI.Domain.Enums;

namespace GrantAI.Domain.Entities;

/// <summary>
/// One normalized row of complex-testing (КТ) admission statistics for a single
/// educational program group (ГОП) in one campaign (year + season). This is the
/// canonical shape every published statistics workbook is mapped into.
///
/// The source files report, per group: how many people applied, how many sat the
/// test, and how many cleared the entrance threshold ("порог") versus not. There
/// is deliberately no per-applicant score here — the published data is counts.
/// </summary>
public sealed class AdmissionRecord
{
    /// <summary>
    /// Deterministic natural key (see <see cref="BuildId"/>), used as the Mongo
    /// <c>_id</c> so re-importing the same group/campaign is idempotent and
    /// duplicates collapse to one document.
    /// </summary>
    public string Id { get; set; } = string.Empty;

    public int Year { get; set; }
    public Season Season { get; set; }

    /// <summary>Educational program group code, e.g. "M094".</summary>
    public string GroupCode { get; set; } = string.Empty;

    /// <summary>Group name, e.g. "Математика и статистика".</summary>
    public string GroupName { get; set; } = string.Empty;

    /// <summary>Number of applications for the complex test (Количество заявлений).</summary>
    public int Applications { get; set; }

    /// <summary>Number who actually sat the test (Количество участников).</summary>
    public int Participants { get; set; }

    /// <summary>Number who cleared the entrance threshold (Набрали порог).</summary>
    public int PassedThreshold { get; set; }

    /// <summary>Number who did not clear the threshold (Не набрали порог).</summary>
    public int FailedThreshold { get; set; }

    /// <summary>Name of the Excel file this record was imported from.</summary>
    public string SourceFile { get; set; } = string.Empty;

    public DateTime ImportedAtUtc { get; set; }

    /// <summary>
    /// Builds the deterministic identity of a record from its business key:
    /// one group, one campaign. Two rows describing the same group/campaign
    /// collapse to a single document.
    /// </summary>
    public static string BuildId(int year, Season season, string groupCode)
        => $"{year}|{(int)season}|{Normalize(groupCode)}";

    private static string Normalize(string value)
        => (value ?? string.Empty).Trim().ToUpperInvariant();
}
