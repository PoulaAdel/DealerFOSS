// Money — an amount together with its ISO currency.
//
// Use:  new Money(24995.00m, "USD"). Add/Subtract refuse mixed currencies on
//       purpose; there is no implicit conversion anywhere in the system.
// Edit: only add operations that are true for every currency. Conversion needs
//       a rate and a date, so it belongs to the module that owns those.

using System.Globalization;

namespace OpenDealer360.Core;

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
