namespace GrantAI.Bot.Handlers;

/// <summary>
/// Serialises and parses the <c>callback_data</c> field used by inline buttons.
/// The encoded form is <c>action:argument</c> with no spaces. Telegram limits
/// callback_data to 64 bytes, so we keep the action short and reject anything
/// longer. Unknown actions and malformed input return <see langword="null"/>
/// from <see cref="ToCommand(string?)"/> so callers can fall back gracefully.
/// </summary>
public static class CallbackData
{
    private const int MaxBytes = 64;

    private static readonly HashSet<string> KnownActions = new(StringComparer.Ordinal)
    {
        "help", "start", "speciality", "history", "forecast", "chance", "compare", "grant"
    };

    /// <summary>Builds a callback payload, or <see langword="null"/> if it would not fit Telegram's 64-byte limit.</summary>
    public static string? Build(string action, string? argument = null)
    {
        if (string.IsNullOrEmpty(action)) return null;
        var payload = string.IsNullOrEmpty(argument) ? action : $"{action}:{argument}";
        return System.Text.Encoding.UTF8.GetByteCount(payload) <= MaxBytes ? payload : null;
    }

    /// <summary>
    /// Turns a callback payload back into a synthetic command string the
    /// router can run, e.g. <c>"forecast:M094"</c> becomes <c>"/forecast M094"</c>.
    /// Returns <see langword="null"/> if the data is missing, oversize or
    /// references an unknown action.
    /// </summary>
    public static string? ToCommand(string? data)
    {
        if (string.IsNullOrWhiteSpace(data)) return null;
        if (System.Text.Encoding.UTF8.GetByteCount(data) > MaxBytes) return null;

        var separator = data.IndexOf(':');
        var action = (separator < 0 ? data : data[..separator]).Trim();
        var argument = separator < 0 ? string.Empty : data[(separator + 1)..].Trim();

        if (!KnownActions.Contains(action)) return null;
        if (!IsSafeArgument(argument)) return null;

        return string.IsNullOrEmpty(argument) ? $"/{action}" : $"/{action} {argument}";
    }

    private static bool IsSafeArgument(string argument)
    {
        if (argument.Length == 0) return true;
        if (argument.Length > 32) return false;
        foreach (var ch in argument)
        {
            if (!(char.IsLetterOrDigit(ch) || ch == '-' || ch == '_'))
                return false;
        }
        return true;
    }
}
