using System.Text;

namespace GrantAI.Application.Importing;

/// <summary>The canonical columns every admission row is mapped onto.</summary>
public enum AdmissionColumn
{
    GroupCode,
    GroupName,
    Applications,
    Participants,
    PassedThreshold,
    FailedThreshold
}

/// <summary>Resolved mapping from canonical column to its zero-based index in a sheet.</summary>
public sealed class ColumnMap
{
    private readonly IReadOnlyDictionary<AdmissionColumn, int> _indexByColumn;

    public ColumnMap(IReadOnlyDictionary<AdmissionColumn, int> indexByColumn)
        => _indexByColumn = indexByColumn;

    public bool Has(AdmissionColumn column) => _indexByColumn.ContainsKey(column);

    public int? IndexOf(AdmissionColumn column)
        => _indexByColumn.TryGetValue(column, out var index) ? index : null;
}

/// <summary>
/// Resolves spreadsheet headers onto <see cref="AdmissionColumn"/> values.
///
/// The published statistics use a two-row merged header — for example a "Набрали
/// порог" cell spanning a "кол-во" and a "%" sub-column. The resolver therefore
/// works on a <em>combined</em> header (the header cell joined with the cell in
/// the row beneath it), normalized to a transliterated, punctuation-free form,
/// and classifies it with explicit keyword rules. Explicit rules (rather than a
/// loose substring dictionary) keep "не набрали" cleanly distinct from "набрали"
/// and pick the count column over the percentage column.
/// </summary>
public static class ColumnMapper
{
    /// <summary>Columns a sheet must provide for its rows to be importable.</summary>
    public static readonly IReadOnlyList<AdmissionColumn> Required =
    [
        AdmissionColumn.GroupCode,
        AdmissionColumn.Applications,
        AdmissionColumn.Participants,
        AdmissionColumn.PassedThreshold
    ];

    /// <summary>
    /// Builds the column map from a header row, optionally disambiguated by the
    /// sub-header row directly beneath it. The first column matching a canonical
    /// kind wins, which makes the result stable and predictable.
    /// </summary>
    public static ColumnMap Resolve(IReadOnlyList<string?> header, IReadOnlyList<string?>? subHeader = null)
    {
        var result = new Dictionary<AdmissionColumn, int>();
        var width = header.Count;

        for (var i = 0; i < width; i++)
        {
            var top = Normalize(header[i]);
            var sub = subHeader is not null && i < subHeader.Count ? Normalize(subHeader[i]) : string.Empty;
            var combined = (top + " " + sub).Trim();
            if (combined.Length == 0)
                continue;

            var column = Classify(combined);
            if (column is not null)
                result.TryAdd(column.Value, i);
        }

        return new ColumnMap(result);
    }

    public static IReadOnlyList<AdmissionColumn> MissingRequired(ColumnMap map)
        => Required.Where(c => !map.Has(c)).ToList();

    /// <summary>
    /// Classifies a normalized combined header into a canonical column, or null.
    /// Order matters: name before code (so "наименование" is not caught by a code
    /// rule), and "не набрали" before "набрали".
    /// </summary>
    public static AdmissionColumn? Classify(string combinedNormalized)
    {
        var n = combinedNormalized;

        if (Contains(n, "naimenovanie") || Contains(n, "specialnost"))
            return AdmissionColumn.GroupName;

        if (Contains(n, "kod") || n == "gop" || Contains(n, "shifr"))
            return AdmissionColumn.GroupCode;

        if (Contains(n, "zayavleni"))
            return AdmissionColumn.Applications;

        if (Contains(n, "uchastnik"))
            return AdmissionColumn.Participants;

        // Threshold columns: prefer the count ("кол-во") over the percentage.
        if (Contains(n, "ne nabrali") || Contains(n, "ne preodol"))
            return Contains(n, "kol") ? AdmissionColumn.FailedThreshold : null;

        if (Contains(n, "nabrali") || Contains(n, "preodol") || Contains(n, "porog"))
            return Contains(n, "kol") ? AdmissionColumn.PassedThreshold : null;

        return null;
    }

    private static bool Contains(string haystack, string needle)
        => haystack.Contains(needle, StringComparison.Ordinal);

    /// <summary>
    /// Normalizes a header: lower-cased Latin letters and digits kept as-is,
    /// Cyrillic transliterated to a Latin key, everything else collapsed to a
    /// single space. The Cyrillic mapping lets the rules stay ASCII and readable
    /// while still matching Russian headers.
    /// </summary>
    public static string Normalize(string? header)
    {
        if (string.IsNullOrWhiteSpace(header))
            return string.Empty;

        var sb = new StringBuilder(header.Length);
        var lastWasSpace = false;

        foreach (var raw in header.Trim().ToLowerInvariant())
        {
            if (raw >= 'a' && raw <= 'z' || raw >= '0' && raw <= '9')
            {
                sb.Append(raw);
                lastWasSpace = false;
            }
            else if (Cyrillic.TryGetValue(raw, out var latin))
            {
                if (latin.Length > 0)
                {
                    sb.Append(latin);
                    lastWasSpace = false;
                }
            }
            else if (!lastWasSpace)
            {
                sb.Append(' ');
                lastWasSpace = true;
            }
        }

        return sb.ToString().Trim();
    }

    // Lightweight Russian -> Latin transliteration sufficient for header keys.
    private static readonly IReadOnlyDictionary<char, string> Cyrillic = new Dictionary<char, string>
    {
        ['а'] = "a", ['б'] = "b", ['в'] = "v", ['г'] = "g", ['д'] = "d", ['е'] = "e",
        ['ё'] = "e", ['ж'] = "zh", ['з'] = "z", ['и'] = "i", ['й'] = "y", ['к'] = "k",
        ['л'] = "l", ['м'] = "m", ['н'] = "n", ['о'] = "o", ['п'] = "p", ['р'] = "r",
        ['с'] = "s", ['т'] = "t", ['у'] = "u", ['ф'] = "f", ['х'] = "h", ['ц'] = "c",
        ['ч'] = "ch", ['ш'] = "sh", ['щ'] = "sch", ['ъ'] = "", ['ы'] = "y", ['ь'] = "",
        ['э'] = "e", ['ю'] = "yu", ['я'] = "ya"
    };
}
