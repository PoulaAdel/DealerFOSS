// Copyright (c) 2026 The DealerFOSS contributors.
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Overview: Purpose, File Design, and Engineering
//   The return type for operations that can fail for ordinary reasons. It draws
//   the line this system depends on: an EXPECTED outcome — not found, not
//   permitted, already submitted — is a value, and only a bug is an exception.
//
//   That split is what makes the HTTP surface thin. An endpoint maps a failed
//   Result to a status code and a Problem Details body without knowing anything
//   about the rule that failed, and a service can refuse something without
//   deciding how the refusal will be transported.
//
// Usage:
//   return Result.Failure<Deal>(Error.NotFound("deal.not_found", "..."));
//   return Result.Success(deal);
//   if (result.IsFailure) return result.Error.ToProblem();
//
// Coding Instructions:
//   Combinators (Map, Bind) belong here if they earn their keep across several
//   capabilities. A domain-specific helper never does: Core must stay free of
//   dealership concepts, and one Deal-shaped method here would be the first
//   crack in the wall that keeps this project testable without a database.

namespace DealerFOSS.Core;

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
