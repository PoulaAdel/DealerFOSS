// Copyright (c) 2026 The DealerFOSS contributors.
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Overview: Purpose, File Design, and Engineering
//   An amount together with the currency it is denominated in, as one value
//   rather than a decimal that happens to have a currency written down beside
//   it somewhere. It lives in Core because every capability needs it and none
//   of them owns it.
//
//   The engineering decision is that arithmetic REFUSES mixed currencies
//   instead of converting them. A conversion needs a rate and a date, and this
//   type can know neither; silently picking one would produce a number that
//   looks right and is wrong, in a system whose whole job is money.
//
// Usage:
//   new Money(24995.00m, "USD")
//   a.Add(b)        → throws when a and b are different currencies
//   a.Subtract(b)   → the same
//
// Coding Instructions:
//   Only add operations that are true for every currency. Conversion belongs to
//   whichever capability owns rates and effective dates, not here.
//
//   Never add an implicit conversion to decimal. It would let an amount lose
//   its currency at a call site nobody reviews, which is exactly the class of
//   bug this type exists to make impossible.

using System.Globalization;

namespace DealerFOSS.Core;

/// <summary>
/// A monetary amount with an explicit ISO 4217 currency. Money never collapses
/// to a bare <see cref="decimal"/> (doc 04 §4, doc 08 §5). Arithmetic across
/// different currencies is rejected — conversion is an explicit domain action.
/// </summary>
public readonly record struct Money
{
    public Money(decimal amount, string currency)
    {
        if (string.IsNullOrWhiteSpace(currency) || currency.Length != 3)
        {
            throw new ArgumentException("Currency must be a 3-letter ISO 4217 code.", nameof(currency));
        }

        Amount = amount;
        Currency = currency.ToUpperInvariant();
    }

    public decimal Amount { get; }

    public string Currency { get; }

    public static Money Zero(string currency) => new(0m, currency);

    public Money Add(Money other)
    {
        EnsureSameCurrency(other);
        return new Money(Amount + other.Amount, Currency);
    }

    public Money Subtract(Money other)
    {
        EnsureSameCurrency(other);
        return new Money(Amount - other.Amount, Currency);
    }

    private void EnsureSameCurrency(Money other)
    {
        if (Currency != other.Currency)
        {
            throw new InvalidOperationException(
                $"Cannot operate on {Currency} and {other.Currency} without an explicit conversion.");
        }
    }

    public override string ToString() =>
        $"{Amount.ToString("0.00##", CultureInfo.InvariantCulture)} {Currency}";
}
