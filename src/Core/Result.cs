// Result — the return type for operations that can fail for ordinary reasons.
//
// Use:  return Result.Failure<T>(Error.NotFound(...)) for an expected outcome;
//       throw only for bugs. Endpoints map a failed Result to a status code.
// Edit: add combinators (Map, Bind) here if they earn their keep. Never add a
//       domain-specific helper — Core must stay free of dealership concepts.

namespace OpenDealer360.Core;

/// <summary>
/// The outcome of an operation that is expected to fail for ordinary business
/// reasons. Expected failures return a <see cref="Result"/>; they are never
/// signalled by throwing (doc 08 §5). Unexpected failures still throw.
/// </summary>
public class Result
{
    protected Result(bool isSuccess, Error error)
    {
        if (isSuccess && error != Error.None)
        {
            throw new InvalidOperationException("A successful result cannot carry an error.");
        }

        if (!isSuccess && error == Error.None)
        {
            throw new InvalidOperationException("A failed result must carry an error.");
        }

        IsSuccess = isSuccess;
        Error = error;
    }

    public bool IsSuccess { get; }

    public bool IsFailure => !IsSuccess;

    public Error Error { get; }

    public static Result Success() => new(true, Error.None);

    public static Result Failure(Error error) => new(false, error);

    public static Result<TValue> Success<TValue>(TValue value) => new(value, true, Error.None);

    public static Result<TValue> Failure<TValue>(Error error) => new(default, false, error);
}

/// <summary>A result that carries a value on success.</summary>
public sealed class Result<TValue> : Result
{
    private readonly TValue? _value;

    internal Result(TValue? value, bool isSuccess, Error error)
        : base(isSuccess, error)
    {
        _value = value;
    }

    /// <summary>The success value. Throws if accessed on a failed result.</summary>
    public TValue Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException("The value of a failed result cannot be accessed.");

    public static implicit operator Result<TValue>(TValue value) => Success(value);

    public static implicit operator Result<TValue>(Error error) => Failure<TValue>(error);
}
