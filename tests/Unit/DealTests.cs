// Copyright (c) 2026 The DealerFOSS contributors.
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Overview: Purpose, File Design, and Engineering
//   DealTests — the money and approval rules, which are the ones that cost real
//   arguments when they are wrong.
//
// Usage:
//   Runs with the normal test suite; no infrastructure required.
//
// Coding Instructions:
//   The frozen-terms rule matters most. A price that can change after a
//   manager approved it makes the approval worthless, and that is a control
//   failure rather than a bug.

using FluentAssertions;
using DealerFOSS.Core;
using DealerFOSS.Deals;

namespace DealerFOSS.UnitTests;

public sealed class DealTests
{
    [Fact]
    public void A_new_deal_is_a_draft_with_that_moment_recorded()
    {
        var deal = Start();

        deal.Status.Should().Be(DealStatus.Draft);
        deal.TermsAreOpen.Should().BeTrue();
        deal.History.Should().ContainSingle().Which.ToStatus.Should().Be(DealStatus.Draft);
    }

    [Fact]
    public void A_deal_needs_a_customer_and_a_car()
    {
        var noCustomer = () => Deal.Start(
            Guid.NewGuid(), RooftopId.New(), Guid.Empty, Guid.NewGuid(), "USD", DateTimeOffset.UtcNow);
        var noCar = () => Deal.Start(
            Guid.NewGuid(), RooftopId.New(), Guid.NewGuid(), Guid.Empty, "USD", DateTimeOffset.UtcNow);

        noCustomer.Should().Throw<ArgumentException>();
        noCar.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void A_deal_refuses_a_currency_that_is_not_a_real_code()
    {
        var nonsense = () => Deal.Start(
            Guid.NewGuid(), RooftopId.New(), Guid.NewGuid(), Guid.NewGuid(), "DOLLARS", DateTimeOffset.UtcNow);

        nonsense.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void The_total_is_the_charges_less_the_trade_plus_what_is_owed_on_it()
    {
        var deal = Start();

        deal.SetTerms(
            [
                (ChargeKind.VehiclePrice, "2021 RAV4", 26995m),
                (ChargeKind.Fee, "Doc fee", 399m),
                (ChargeKind.Discount, "Manager discount", -500m),
            ],
            TradeIn.Create("2014 Civic", 4500m, 1200m));

        deal.Subtotal.Amount.Should().Be(26894m);
        deal.AmountDue.Amount.Should().Be(23594m, because: "26894 - 4500 allowance + 1200 still owed");
        deal.AmountDue.Currency.Should().Be("USD");
    }

    [Fact]
    public void A_trade_worth_less_than_is_owed_on_it_is_negative_equity()
    {
        // The single most common source of an argument at the desk, so it is
        // named rather than hidden inside a net figure.
        var trade = TradeIn.Create("2016 Sedan", 6000m, 9500m);

        trade.Equity.Should().Be(-3500m);
        trade.IsNegativeEquity.Should().BeTrue();
    }

    [Fact]
    public void A_discount_must_be_negative_and_a_fee_must_not_be()
    {
        var deal = Start();

        var positiveDiscount = () => deal.SetTerms([(ChargeKind.Discount, "Off", 500m)], null);
        var negativeFee = () => deal.SetTerms([(ChargeKind.Fee, "Doc", -100m)], null);

        positiveDiscount.Should().Throw<ArgumentException>();
        negativeFee.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void A_deal_has_one_vehicle_price()
    {
        var deal = Start();

        var twoCars = () => deal.SetTerms(
            [
                (ChargeKind.VehiclePrice, "One car", 20000m),
                (ChargeKind.VehiclePrice, "Another car", 30000m),
            ],
            null);

        twoCars.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void A_deal_cannot_be_submitted_without_a_price()
    {
        var deal = Start();

        var empty = () => deal.ChangeStatus(DealStatus.Submitted, DateTimeOffset.UtcNow);

        empty.Should().Throw<InvalidOperationException>().WithMessage("*price*");
    }

    [Fact]
    public void A_deal_that_pays_the_customer_more_than_they_pay_is_refused()
    {
        var deal = Start();
        deal.SetTerms(
            [(ChargeKind.VehiclePrice, "Car", 10000m)],
            TradeIn.Create("Trade worth more than the car", 15000m, 0m));

        var upsideDown = () => deal.ChangeStatus(DealStatus.Submitted, DateTimeOffset.UtcNow);

        upsideDown.Should().Throw<InvalidOperationException>().WithMessage("*discount*");
    }

    [Fact]
    public void The_numbers_freeze_once_the_deal_leaves_draft()
    {
        var deal = Submitted();

        var change = () => deal.SetTerms([(ChargeKind.VehiclePrice, "Cheaper now", 20000m)], null);

        change.Should().Throw<InvalidOperationException>().WithMessage("*Draft*");
        deal.TermsAreOpen.Should().BeFalse();
    }

    [Fact]
    public void A_deal_cannot_be_delivered_without_being_approved()
    {
        var deal = Submitted();

        var skip = () => deal.ChangeStatus(DealStatus.Delivered, DateTimeOffset.UtcNow);

        skip.Should().Throw<InvalidOperationException>().WithMessage("*Approved*");
    }

    [Fact]
    public void The_salesperson_cannot_approve_their_own_deal()
    {
        var salesperson = Guid.NewGuid();
        var deal = Deal.Start(
            Guid.NewGuid(), RooftopId.New(), Guid.NewGuid(), Guid.NewGuid(), "USD",
            DateTimeOffset.UtcNow, salespersonUserId: salesperson);

        deal.SetTerms([(ChargeKind.VehiclePrice, "2021 RAV4", 26995m)], null);
        deal.ChangeStatus(DealStatus.Submitted, DateTimeOffset.UtcNow, salesperson);

        var ownApproval = () => deal.ChangeStatus(DealStatus.Approved, DateTimeOffset.UtcNow, salesperson);

        ownApproval.Should().Throw<InvalidOperationException>()
            .WithMessage("*sales manager*", because: "the message should say who is supposed to do it");

        // Somebody else signs it off, and that works.
        deal.ChangeStatus(DealStatus.Approved, DateTimeOffset.UtcNow, Guid.NewGuid());
        deal.Status.Should().Be(DealStatus.Approved);
    }

    [Fact]
    public void Approving_records_who_approved_it_and_the_amount_they_saw()
    {
        var deal = Submitted();
        var manager = Guid.NewGuid();
        var approvedAt = DateTimeOffset.UtcNow;

        deal.ChangeStatus(DealStatus.Approved, approvedAt, manager, "Looks fine.");

        deal.ApprovedByUserId.Should().Be(manager);
        deal.ApprovedAt.Should().Be(approvedAt);
        deal.History[^1].AmountAtChange.Should().Be(deal.AmountDue.Amount,
            because: "an approval must record the number that was approved");
    }

    [Fact]
    public void Sending_a_deal_back_for_changes_withdraws_the_approval()
    {
        // Leaving the old approver on a reopened deal would be a lie about who
        // agreed to the new numbers.
        var deal = Submitted();
        deal.ChangeStatus(DealStatus.Approved, DateTimeOffset.UtcNow, Guid.NewGuid());
        deal.ChangeStatus(DealStatus.Cancelled, DateTimeOffset.UtcNow, Guid.NewGuid(), "Financing declined.");

        deal.Status.Should().Be(DealStatus.Cancelled);

        var reopened = Submitted();
        reopened.ChangeStatus(DealStatus.Draft, DateTimeOffset.UtcNow, Guid.NewGuid(), "Reprice it.");

        reopened.ApprovedByUserId.Should().BeNull();
        reopened.ApprovedAt.Should().BeNull();
        reopened.TermsAreOpen.Should().BeTrue();
    }

    [Fact]
    public void A_delivered_deal_is_finished()
    {
        var deal = Submitted();
        deal.ChangeStatus(DealStatus.Approved, DateTimeOffset.UtcNow, Guid.NewGuid());
        deal.ChangeStatus(DealStatus.Delivered, DateTimeOffset.UtcNow, Guid.NewGuid());

        var undo = () => deal.ChangeStatus(DealStatus.Cancelled, DateTimeOffset.UtcNow);

        undo.Should().Throw<InvalidOperationException>().WithMessage("*finished*");
    }

    private static Deal Start() => Deal.Start(
        Guid.NewGuid(), RooftopId.New(), Guid.NewGuid(), Guid.NewGuid(), "USD", DateTimeOffset.UtcNow);

    private static Deal Submitted()
    {
        var deal = Start();
        deal.SetTerms([(ChargeKind.VehiclePrice, "2021 RAV4", 26995m)], null);
        deal.ChangeStatus(DealStatus.Submitted, DateTimeOffset.UtcNow);
        return deal;
    }
}
