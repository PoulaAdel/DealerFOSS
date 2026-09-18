// Copyright (c) 2026 The DealerFOSS contributors.
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Overview: Purpose, File Design, and Engineering
//   WarrantyClaimTests — the status rules a claim moves through, and the two
//   places that need more than a status: a denial needs a reason and a
//   payment needs an amount.
//
// Usage:
//   Runs with the normal test suite; no infrastructure required.
//
// Coding Instructions:
//   The rule worth protecting is that a claim cannot skip a step — Open
//   straight to Paid would be a claim nobody actually sent anywhere.

using FluentAssertions;
using DealerFOSS.Core;
using DealerFOSS.RepairOrders;

namespace DealerFOSS.UnitTests;

public sealed class WarrantyClaimTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 18, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void A_new_claim_is_open_with_that_moment_recorded()
    {
        var claim = Open();

        claim.Status.Should().Be(WarrantyClaimStatus.Open);
        claim.History.Should().ContainSingle().Which.ToStatus.Should().Be(WarrantyClaimStatus.Open);
    }

    [Fact]
    public void A_claim_needs_an_amount_above_zero()
    {
        var zero = () => WarrantyClaim.Open(
            Guid.NewGuid(), RooftopId.New(), Guid.NewGuid(), Money.Zero("USD"), Now, null);

        zero.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void A_claim_moves_from_open_through_submitted_and_approved_to_paid()
    {
        var claim = Open();

        claim.Submit(Now, null, "Sent by the portal");
        claim.Status.Should().Be(WarrantyClaimStatus.Submitted);

        claim.Approve(Now, null, "Manufacturer confirmed");
        claim.Status.Should().Be(WarrantyClaimStatus.Approved);

        claim.RecordPaid(200m, Now, null, "Cheque received");
        claim.Status.Should().Be(WarrantyClaimStatus.Paid);
        claim.AmountPaid.Should().Be(200m);

        claim.History.Should().HaveCount(4);
    }

    [Fact]
    public void A_claim_can_be_denied_from_open_submitted_or_approved()
    {
        var fromOpen = Open();
        fromOpen.Deny(Now, null, "Out of warranty");
        fromOpen.Status.Should().Be(WarrantyClaimStatus.Denied);

        var fromSubmitted = Open();
        fromSubmitted.Submit(Now, null, null);
        fromSubmitted.Deny(Now, null, "Pre-existing condition");
        fromSubmitted.Status.Should().Be(WarrantyClaimStatus.Denied);

        var fromApproved = Open();
        fromApproved.Submit(Now, null, null);
        fromApproved.Approve(Now, null, null);
        fromApproved.Deny(Now, null, "Reversed on review");
        fromApproved.Status.Should().Be(WarrantyClaimStatus.Denied);
    }

    [Fact]
    public void Denying_a_claim_needs_a_reason()
    {
        var claim = Open();

        var noReason = () => claim.Deny(Now, null, "");
        var blank = () => claim.Deny(Now, null, "   ");

        noReason.Should().Throw<ArgumentException>();
        blank.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Recording_a_claim_paid_needs_an_amount_above_zero()
    {
        var claim = Open();
        claim.Submit(Now, null, null);
        claim.Approve(Now, null, null);

        var zero = () => claim.RecordPaid(0m, Now, null, null);
        var negative = () => claim.RecordPaid(-50m, Now, null, null);

        zero.Should().Throw<ArgumentOutOfRangeException>();
        negative.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void What_the_manufacturer_paid_can_differ_from_what_was_billed()
    {
        // The manufacturer disputing one line and paying less than billed is
        // the ordinary case this figure exists to record honestly.
        var claim = WarrantyClaim.Open(
            Guid.NewGuid(), RooftopId.New(), Guid.NewGuid(), new Money(500m, "USD"), Now, null);
        claim.Submit(Now, null, null);
        claim.Approve(Now, null, null);

        claim.RecordPaid(460m, Now, null, "Disputed one line");

        claim.Amount.Should().Be(500m, because: "what was billed never moves");
        claim.AmountPaid.Should().Be(460m);
    }

    [Fact]
    public void Denied_and_paid_are_both_terminal()
    {
        var denied = Open();
        denied.Deny(Now, null, "Not covered");
        var afterDenied = () => denied.Submit(Now, null, null);
        afterDenied.Should().Throw<InvalidOperationException>().WithMessage("*finished*");

        var paid = Open();
        paid.Submit(Now, null, null);
        paid.Approve(Now, null, null);
        paid.RecordPaid(100m, Now, null, null);
        var afterPaid = () => paid.Deny(Now, null, "Too late");
        afterPaid.Should().Throw<InvalidOperationException>().WithMessage("*finished*");
    }

    [Fact]
    public void A_claim_cannot_skip_from_open_to_paid()
    {
        var claim = Open();

        var skip = () => claim.RecordPaid(100m, Now, null, null);

        skip.Should().Throw<InvalidOperationException>();
    }

    private static WarrantyClaim Open() =>
        WarrantyClaim.Open(Guid.NewGuid(), RooftopId.New(), Guid.NewGuid(), new Money(300m, "USD"), Now, null);
}
