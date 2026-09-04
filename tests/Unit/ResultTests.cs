// Copyright (c) 2026 The DealerFOSS contributors.
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Overview: Purpose, File Design, and Engineering
//   ResultTests — proves a Result cannot misrepresent an outcome.
//
// Usage:
//   Runs with the normal test suite; no infrastructure required.
//
// Coding Instructions:
//   The invariants here are what let calling code trust IsSuccess without
//   also null-checking. Loosening them would let a "successful failure"
//   through, which is worse than an exception.

using FluentAssertions;
using DealerFOSS.Core;

namespace DealerFOSS.UnitTests;

public sealed class ResultTests
{
    [Fact]
    public void A_success_carries_its_value()
    {
        var result = Result.Success(42);

        result.IsSuccess.Should().BeTrue();
        result.IsFailure.Should().BeFalse();
        result.Value.Should().Be(42);
        result.Error.Should().Be(Error.None);
    }

    [Fact]
    public void A_failure_carries_its_error()
    {
        var error = Error.NotFound("deal.not_found", "No such deal.");

        var result = Result.Failure<int>(error);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(error);
    }

    [Fact]
    public void Reading_the_value_of_a_failure_throws_rather_than_returning_a_default()
    {
        var result = Result.Failure<string>(Error.NotFound("x", "y"));

        var read = () => result.Value;

        read.Should().Throw<InvalidOperationException>(
            because: "returning null or 0 here would let a failure be used as data");
    }

    [Fact]
    public void A_success_cannot_carry_an_error()
    {
        var construct = () => Result.Failure(Error.None);

        construct.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void A_value_converts_implicitly_to_a_success()
    {
        Result<string> result = "ok";

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be("ok");
    }

    [Fact]
    public void An_error_converts_implicitly_to_a_failure()
    {
        Result<string> result = Error.Validation("bad", "Bad input.");

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("bad");
    }

    [Theory]
    [InlineData(ErrorType.Validation)]
    [InlineData(ErrorType.NotFound)]
    [InlineData(ErrorType.Conflict)]
    [InlineData(ErrorType.Forbidden)]
    public void Every_error_type_is_distinguishable_so_endpoints_can_map_it(ErrorType type)
    {
        // Endpoints translate ErrorType into a status code. A type that cannot
        // be told apart would silently become a 500.
        var error = type switch
        {
            ErrorType.Validation => Error.Validation("c", "m"),
            ErrorType.NotFound => Error.NotFound("c", "m"),
            ErrorType.Conflict => Error.Conflict("c", "m"),
            ErrorType.Forbidden => Error.Forbidden("c", "m"),
            _ => Error.None,
        };

        error.Type.Should().Be(type);
        error.Should().NotBe(Error.None);
    }

    [Fact]
    public void The_error_code_is_part_of_the_contract_and_survives_round_tripping()
    {
        var error = Error.Conflict("deal.version_stale", "The deal changed.");

        error.Code.Should().Be("deal.version_stale");
        error.Message.Should().Be("The deal changed.");
    }
}
