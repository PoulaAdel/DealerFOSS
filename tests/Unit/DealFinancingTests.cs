// Copyright (c) 2026 The DealerFOSS contributors.
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Overview: Purpose, File Design, and Engineering
//   DealFinancingTests — the payment arithmetic, and the four ways the figures
//   could disagree with each other.
//
// Usage:
//   Runs with the normal test suite; no infrastructure required.
//
// Coding Instructions:
//   THE SCHEDULE TEST IS THE ONE THAT MATTERS. Every other assertion here
//   checks one figure against another; The_schedule_pays_the_loan_off_exactly
//   re-amortises the plan month by month, in the test rather than in the code
//   under test, and asserts the balance lands on zero. A payment that is a cent
//   wrong is invisible to a spot check and obvious to that walk.
//
//   The worked example is $20,000 at 6% over 60 months. It is a textbook case
//   with a published answer — $386.66 a month — so a regression here is
//   checkable against any amortisation table rather than against this file.

using FluentAssertions;
using DealerFOSS.Core;
using DealerFOSS.Deals;

namespace DealerFOSS.UnitTests;

public sealed class DealFinancingTests
{
    [Fact]
    public void The_monthly_payment_is_what_an_amortisation_table_says_it_is()
    {
        // $20,000 over five years at 6%. The published answer is $386.66.
        var plan = Financing.Create(null, 0m, 0.06m, 60).PlanFor(20_000m);

        plan.Should().NotBeNull();
        plan!.MonthlyPayment.Should().Be(386.66m);

        // The last instalment clears the balance, so it is a few cents short of
        // the others rather than equal to them.
        plan.FinalPayment.Should().Be(386.41m);
        plan.TotalOfPayments.Should().Be(23_199.35m);
        plan.FinanceCharge.Should().Be(3_199.35m);
    }

    [Fact]
    public void The_schedule_pays_the_loan_off_exactly()
    {
        // The property, not an example: re-amortise the plan here, independently
        // of the code that produced it, and the balance has to reach zero. This
        // is what makes the payment provably right rather than approximately
        // right — and a cent of drift a month is a cent the customer and the
        // dealership would argue about sixty times.
        const decimal financed = 18_450.75m;
        const decimal rate = 0.0729m;
        const int term = 48;

        var plan = Financing.Create(null, 0m, rate, term).PlanFor(financed)!;

        var balance = financed;
        var monthlyRate = rate / 12m;
        var paid = 0m;

        for (var month = 1; month <= term; month++)
        {
            var interest = Math.Round(balance * monthlyRate, 2, MidpointRounding.AwayFromZero);
            var due = month == term ? plan.FinalPayment : plan.MonthlyPayment;

            balance = balance + interest - due;
            paid += due;
        }

        balance.Should().Be(0m,
            because: "the final instalment is what clears the balance, so nothing may be left over");

        paid.Should().Be(plan.TotalOfPayments,
            because: "the total of payments is the sum of the payments and nothing else");

        plan.FinanceCharge.Should().Be(paid - financed,
            because: "the finance charge is what the credit costs: paid out less advanced");
    }

    [Fact]
    public void Nothing_percent_is_a_real_offer_and_costs_nothing()
    {
        // A manufacturer 0% deal, which divides rather than amortises. Guarding
        // against a divide-by-zero would be the wrong reading of it: this is an
        // answer, not an edge case.
        var plan = Financing.Create("Toyota Financial Services", 0m, 0m, 24).PlanFor(12_000m)!;

        plan.MonthlyPayment.Should().Be(500m);
        plan.FinalPayment.Should().Be(500m);
        plan.TotalOfPayments.Should().Be(12_000m);
        plan.FinanceCharge.Should().Be(0m,
            because: "at nothing percent the customer pays back exactly what they borrowed");
    }

    [Fact]
    public void A_payment_that_does_not_divide_evenly_still_adds_up()
    {
        // 0% over a term the amount does not divide by. The rounded payment is a
        // cent high, so the final one has to absorb the difference — which is the
        // simplest case of the thing the schedule walk exists for.
        var plan = Financing.Create(null, 0m, 0m, 7).PlanFor(1_000m)!;

        plan.MonthlyPayment.Should().Be(142.86m);
        plan.TotalOfPayments.Should().Be(1_000m,
            because: "seven payments of 142.86 would be 1,000.02, and the customer owes 1,000.00");

        (plan.MonthlyPayment * 6m + plan.FinalPayment).Should().Be(plan.TotalOfPayments);
        plan.FinanceCharge.Should().Be(0m);
    }

    [Fact]
    public void The_amount_financed_is_what_is_owed_less_the_cash_down()
    {
        var deal = Draft(26_995m);
        deal.SetFinancing(Financing.Create("Ally", 4_000m, 0.0649m, 60));

        deal.AmountFinanced!.Value.Amount.Should().Be(22_995m);

        // The identity that makes storing the amount financed unnecessary. If a
        // column for it ever appears, this is the assertion that stops being
        // true by construction and starts being true by luck.
        (deal.Financing!.DownPayment + deal.AmountFinanced!.Value.Amount)
            .Should().Be(deal.AmountDue.Amount);
    }

    [Fact]
    public void The_down_payment_does_not_reduce_what_the_customer_owes()
    {
        // The single most important property here. A down payment is how the
        // customer pays, not money off: the receivable opens at the full amount
        // due and the down payment settles part of it. Netting it would make the
        // same money disappear twice, and would break both "the column adds up"
        // tests at the same time.
        var deal = Draft(26_995m);
        var before = deal.AmountDue.Amount;

        deal.SetFinancing(Financing.Create(null, 5_000m, 0.05m, 48));

        deal.AmountDue.Amount.Should().Be(before);
    }

    [Fact]
    public void A_deal_with_no_financing_has_no_payment_and_that_is_not_an_error()
    {
        var deal = Draft(26_995m);

        deal.Financing.Should().BeNull();
        deal.AmountFinanced.Should().BeNull();
        deal.Instalments.Should().BeNull();
    }

    [Fact]
    public void Putting_down_the_whole_price_is_a_cash_deal_rather_than_a_structure()
    {
        var deal = Draft(26_995m);

        var wholeThing = () => deal.SetFinancing(Financing.Create(null, 26_995m, 0.05m, 60));

        wholeThing.Should().Throw<ArgumentException>()
            .WithMessage("*nothing left to finance*");
    }

    [Fact]
    public void A_rate_typed_as_a_percentage_is_refused_rather_than_charged()
    {
        // The one data-entry slip this field will actually see, and the reason it
        // cannot be allowed through: 6.49 instead of 0.0649 is a 649% loan, and
        // the payment it produces looks like a number rather than like a mistake.
        var asPercent = () => Financing.Create(null, 0m, 6.49m, 60);

        asPercent.Should().Throw<ArgumentException>()
            .WithMessage("*fraction, not a percentage*");
    }

    [Theory]
    [InlineData(-0.01, 60)]
    [InlineData(0.05, 0)]
    [InlineData(0.05, -12)]
    [InlineData(0.05, 121)]
    public void A_structure_that_is_not_a_structure_is_refused(decimal rate, int termMonths)
    {
        var nonsense = () => Financing.Create(null, 0m, rate, termMonths);

        nonsense.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void A_negative_down_payment_is_refused()
    {
        var owing = () => Financing.Create(null, -500m, 0.05m, 60);

        owing.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void The_lender_is_a_name_and_an_empty_one_becomes_absent()
    {
        Financing.Create("  Ally Financial  ", 0m, 0.05m, 60).Lender.Should().Be("Ally Financial");
        Financing.Create("   ", 0m, 0.05m, 60).Lender.Should().BeNull();
        Financing.Create(null, 0m, 0.05m, 60).Lender.Should().BeNull();
    }

    [Fact]
    public void The_financing_freezes_with_the_rest_of_the_numbers()
    {
        // Same rule as the charges, the products and the tax, and for a reason of
        // its own: the payment derives from the amount due, which freezes on
        // submission. Financing that could move afterwards would change the
        // figure the customer was quoted without the approval moving with it.
        var deal = Draft(26_995m);
        deal.ChangeStatus(DealStatus.Submitted, DateTimeOffset.UtcNow);

        var afterTheFact = () => deal.SetFinancing(Financing.Create(null, 1_000m, 0.05m, 60));

        afterTheFact.Should().Throw<InvalidOperationException>()
            .WithMessage("*frozen*");
    }

    [Fact]
    public void Clearing_the_financing_turns_it_back_into_a_cash_deal()
    {
        var deal = Draft(26_995m);
        deal.SetFinancing(Financing.Create("Ally", 4_000m, 0.0649m, 60));

        deal.SetFinancing(null);

        deal.Financing.Should().BeNull();
        deal.Instalments.Should().BeNull();
    }

    [Fact]
    public void A_reprice_that_leaves_nothing_to_finance_is_visible_rather_than_thrown()
    {
        // The order-of-operations hazard: the financing was agreed against one
        // total and the car was then discounted below the cash down. A computed
        // property that threw here would turn a half-worked draft into a failed
        // request, so the plan comes back null and the deal can still be read.
        var deal = Draft(26_995m);
        deal.SetFinancing(Financing.Create(null, 20_000m, 0.05m, 60));
        deal.SetTerms(
            [(ChargeKind.VehiclePrice, "2021 RAV4", 26_995m), (ChargeKind.Discount, "Manager", -19_000m)],
            null);

        deal.AmountFinanced!.Value.Amount.Should().Be(-12_005m);
        deal.Instalments.Should().BeNull();
    }

    [Fact]
    public void A_financed_deal_with_no_payment_cannot_reach_a_manager()
    {
        // And the gate that catches what the null above allows through. A deal in
        // a manager's queue with the one figure the customer cares about missing
        // is worse than a refusal at the point of submitting it.
        var deal = Draft(26_995m);
        deal.SetFinancing(Financing.Create(null, 20_000m, 0.05m, 60));
        deal.SetTerms(
            [(ChargeKind.VehiclePrice, "2021 RAV4", 26_995m), (ChargeKind.Discount, "Manager", -19_000m)],
            null);

        var submitting = () => deal.ChangeStatus(DealStatus.Submitted, DateTimeOffset.UtcNow);

        submitting.Should().Throw<InvalidOperationException>()
            .WithMessage("*nothing to finance*");
    }

    [Fact]
    public void An_imported_deal_keeps_the_structure_it_was_signed_on()
    {
        // A migrated deal that arrived as a cash deal when it was financed over
        // sixty months has lost the contract — and the amount due check cannot
        // notice, because financing sits outside the total.
        var deal = Deal.Import(
            Guid.NewGuid(),
            RooftopId.New(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            "USD",
            DealStatus.Delivered,
            [(ChargeKind.VehiclePrice, "2019 Civic", 18_500m)],
            null,
            [],
            [],
            null,
            Financing.Create("Ally Financial", 2_500m, 0.0729m, 48),
            DateTimeOffset.UtcNow);

        deal.Financing!.Lender.Should().Be("Ally Financial");
        deal.Financing.TermMonths.Should().Be(48);
        deal.AmountFinanced!.Value.Amount.Should().Be(16_000m);
        deal.Instalments!.MonthlyPayment.Should().BeGreaterThan(0m);
    }

    [Fact]
    public void An_arriving_deal_is_not_refused_over_financing_that_does_not_fit_its_total()
    {
        // The import path deliberately does not check the down payment against
        // the total, and this is why. A package that has lost a charge leaves a
        // deal whose financing no longer fits it, and refusing THAT sends somebody
        // looking at the finance terms for a fault that is in the charges. The
        // total check in ImportAsync is the authority on an arriving deal's
        // arithmetic; this one arrives, and says it has no monthly payment.
        var deal = Deal.Import(
            Guid.NewGuid(),
            RooftopId.New(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            "USD",
            DealStatus.Delivered,
            [(ChargeKind.VehiclePrice, "2019 Civic", 900m)],
            null,
            [],
            [],
            null,
            Financing.Create("Ally Financial", 2_500m, 0.0729m, 48),
            DateTimeOffset.UtcNow);

        deal.Financing.Should().NotBeNull();
        deal.Instalments.Should().BeNull(
            because: "there is nothing left to finance, and that is visible rather than fatal");
    }

    private static Deal Draft(decimal price)
    {
        var deal = Deal.Start(
            Guid.NewGuid(), RooftopId.New(), Guid.NewGuid(), Guid.NewGuid(), "USD", DateTimeOffset.UtcNow);

        deal.SetTerms([(ChargeKind.VehiclePrice, "2021 RAV4", price)], null);
        return deal;
    }
}
