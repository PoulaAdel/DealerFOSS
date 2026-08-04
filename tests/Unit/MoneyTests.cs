// MoneyTests — proves money carries its currency and refuses to lose it.
//
// Use:  runs with the normal test suite; no infrastructure required.
// Edit: the cross-currency refusals are the point. If a change makes mixed
//       arithmetic "just work", that is a silent financial bug, not a
//       convenience — these tests exist to stop it.

using FluentAssertions;
using DealerFOSS.Core;

namespace DealerFOSS.UnitTests;

public sealed class MoneyTests
{
    [Fact]
    public void Amount_and_currency_are_preserved()
    {
        var price = new Money(24995.00m, "USD");

        price.Amount.Should().Be(24995.00m);
        price.Currency.Should().Be("USD");
    }

    [Theory]
    [InlineData("usd")]
    [InlineData("Usd")]
    public void Currency_is_normalized_to_upper_case(string input)
    {
        new Money(1m, input).Currency.Should().Be("USD");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("US")]
    [InlineData("USDX")]
    public void A_currency_that_is_not_a_three_letter_code_is_rejected(string currency)
    {
        var construct = () => new Money(1m, currency);

        construct.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Adding_the_same_currency_sums_the_amounts()
    {
        var total = new Money(1000m, "USD").Add(new Money(250.50m, "USD"));

        total.Amount.Should().Be(1250.50m);
        total.Currency.Should().Be("USD");
    }

    [Fact]
    public void Subtracting_can_produce_a_negative_amount()
    {
        // Negative money is legitimate — a refund, a credit, a trade payoff
        // exceeding allowance. Money must not silently clamp at zero.
        var balance = new Money(100m, "USD").Subtract(new Money(150m, "USD"));

        balance.Amount.Should().Be(-50m);
    }

    [Fact]
    public void Adding_different_currencies_is_refused()
    {
        var add = () => new Money(1000m, "USD").Add(new Money(1000m, "CAD"));

        add.Should().Throw<InvalidOperationException>()
            .WithMessage("*USD*CAD*",
                because: "conversion needs a rate and a date, so it cannot happen implicitly");
    }

    [Fact]
    public void Subtracting_different_currencies_is_refused()
    {
        var subtract = () => new Money(1000m, "USD").Subtract(new Money(1m, "EUR"));

        subtract.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Zero_carries_a_currency_like_any_other_amount()
    {
        var zero = Money.Zero("EUR");

        zero.Amount.Should().Be(0m);
        zero.Currency.Should().Be("EUR");
        // Even zero must not be added across currencies: a currency-less zero
        // is how mixed-currency arithmetic sneaks in.
        var add = () => zero.Add(new Money(0m, "USD"));
        add.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Equality_is_by_amount_and_currency_together()
    {
        new Money(10m, "USD").Should().Be(new Money(10m, "USD"));
        new Money(10m, "USD").Should().NotBe(new Money(10m, "CAD"));
    }
}
