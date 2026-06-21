using System.Diagnostics.CodeAnalysis;

namespace GrantAI.Application.Common.Results;

/// <summary>
/// Either a successful value of <typeparamref name="T"/> or an <see cref="Error"/>.
/// Designed so callers handle both branches at the boundary (controller, bot
/// formatter) and never accidentally consume a default value as a real result.
/// </summary>
public readonly struct Result<T>
{
    private readonly T? _value;
    private readonly Error? _error;

    private Result(T value)
    {
        _value = value;
        _error = null;
        IsSuccess = true;
    }

    private Result(Error error)
    {
        _value = default;
        _error = error;
        IsSuccess = false;
    }

    public bool IsSuccess { get; }
    public bool IsError => !IsSuccess;

    /// <summary>The success value. Only call when <see cref="IsSuccess"/> is <see langword="true"/>.</summary>
    public T Value
    {
        get
        {
            if (!IsSuccess)
                throw new InvalidOperationException("Cannot access Value on a failed Result. Check IsSuccess first.");
            return _value!;
        }
    }

    /// <summary>The error. Only call when <see cref="IsError"/> is <see langword="true"/>.</summary>
    public Error Error
    {
        get
        {
            if (IsSuccess)
                throw new InvalidOperationException("Cannot access Error on a successful Result. Check IsError first.");
            return _error!;
        }
    }

    public static Result<T> Success(T value) => new(value);
    public static Result<T> Failure(Error error) => new(error);

    public static implicit operator Result<T>(T value) => Success(value);
    public static implicit operator Result<T>(Error error) => Failure(error);

    public bool TryGetValue([NotNullWhen(true)] out T? value, [NotNullWhen(false)] out Error? error)
    {
        if (IsSuccess)
        {
            value = _value!;
            error = null;
            return true;
        }
        value = default;
        error = _error;
        return false;
    }
}
