namespace GrantAI.Application.Common.Results;

/// <summary>
/// Tagged error returned from Application read services. Carries a code so
/// callers can decide how to render it (e.g. HTTP 404 for <see cref="ErrorKind.NotFound"/>
/// or a Russian message in the bot), and a free-text message that already
/// names the resource. Errors are values, not exceptions.
/// </summary>
public sealed record Error(ErrorKind Kind, string Code, string Message)
{
    public static Error NotFound(string code, string message)
        => new(ErrorKind.NotFound, code, message);

    public static Error Validation(string code, string message)
        => new(ErrorKind.Validation, code, message);

    public static Error Unexpected(string message)
        => new(ErrorKind.Unexpected, "unexpected", message);
}

public enum ErrorKind
{
    NotFound,
    Validation,
    Unexpected
}
