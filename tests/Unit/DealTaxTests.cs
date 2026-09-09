// Copyright (c) 2026 The DealerFOSS contributors.
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Overview: Purpose, File Design, and Engineering
//   DealTaxTests — what a vehicle sale is taxed on, and what a tax line has to
//   be able to say about itself.
//
// Usage:
//   Runs with the normal test suite; no infrastructure required.
//
// Coding Instructions:
//   THE TRADE-IN PAIR IS THE POINT OF THIS FILE. The same deal, the same
//   figures, two jurisdictions, two different taxable amounts — and the
//   difference is $10,000 of basis. It is the case a general retail tax engine
//   has no way to express, which is why ADR-024 keeps the basis on our side and
//   buys only the rate.
//
//   Do not soften these into "the basis is not zero". The defect being guarded
//   against is a plausible number, not a missing one.

using FluentAssertions;
using DealerFOSS.Core;
using DealerFOSS.Deals;

namespace DealerFOSS.UnitTests;

public sealed class DealTaxTests
{
    /// <summary>Most US states: the trade comes off the taxable amount.</summary>
    private static readonly TaxBasisRules MostStates = TaxBasisRules.CommonUnitedStates;

    /// <summary>California: CDTFA Publication 34 taxes the full price.</summary>
    private static readonly TaxBasisRules California = new(
        TradeInReducesBasis: false,
        DocumentationFeeIsTaxable: true,
        OtherFeesAreTaxable: false);

    private static readonly TaxAddress Springfield =
        TaxAddress.Create("IL", "Sangamon", "62704", "US");

    private static Deal Priced()
    {
        var deal = Deal.Start(
            Guid.NewGuid(), RooftopId.New(), Guid.NewGuid(), Guid.NewGuid(), "USD", DateTimeOffset.UtcNow);

        deal.SetTerms(
            [
                (ChargeKind.VehiclePrice, "2021 RAV4", 30000m),
                (ChargeKind.DocumentationFee, "Documentation", 500m),
                (ChargeKind.Fee, "Registration", 200m),
                (ChargeKind.Accessory, "Tow bar", 1000m),
                (ChargeKind.Discount, "Sale price", -2000m),
            ],
            TradeIn.Create("2014 Civic", 10000m, 3000m));

        return deal;
    }

    [Fact]
    public void Most_states_tax_the_price_after_the_trade_comes_off()
    {
        // 30000 + 500 doc + 1000 accessory - 2000 discount = 29500, less the
        // 10000 allowance. Registration is a government fee and is not taxed.
        Priced().TaxableBasis(MostStates).Amount.Should().Be(19500m);
    }

    [Fact]
    public void California_taxes_the_same_deal_on_the_full_price()
    {
        // The same car, the same trade, ten thousand dollars more of basis. This
        // is the difference no general retail tax engine can express.
        Priced().TaxableBasis(California).Amount.Should().Be(29500m);
    }

    [Fact]
    public void A_documentation_fee_is_taxed_where_the_rules_say_so_and_not_where_they_do_not()
    {
        var untaxedDocFee = MostStates with { DocumentationFeeIsTaxable = false };

        Priced().TaxableBasis(MostStates).Amount
            .Should().Be(Priced().TaxableBasis(untaxedDocFee).Amount + 500m,
                because: "the doc fee is the only difference between those two rule sets");
    }

    [Fact]
    public void A_registration_fee_stays_out_unless_the_rules_pull_it_in()
    {
        var taxedFees = MostStates with { OtherFeesAreTaxable = true };

        Priced().TaxableBasis(taxedFees).Amount
            .Should().Be(Priced().TaxableBasis(MostStates).Amount + 200m);
    }

    [Fact]
    public void A_trade_worth_more_than_the_car_does_not_produce_a_negative_tax()
    {
        var deal = Deal.Start(
            Guid.NewGuid(), RooftopId.New(), Guid.NewGuid(), Guid.NewGuid(), "USD", DateTimeOffset.UtcNow);

        deal.SetTerms(
            [(ChargeKind.VehiclePrice, "2016 Fiesta", 6000m)],
            TradeIn.Create("2022 Ranger", 18000m, 0m));

        // A real deal, and a real cheque to the customer. Not a real tax.
        deal.TaxableBasis(MostStates).Amount.Should().Be(0m);
    }

    [Fact]
    public void Finance_products_are_left_out_of_the_basis_on_purpose()
    {
        var deal = Priced();
        var before = deal.TaxableBasis(MostStates).Amount;

        deal.SetProducts([(Guid.NewGuid(), "Service plan", 1200m, 700m, 36, null)]);

        deal.TaxableBasis(MostStates).Amount.Should().Be(before,
            because: "whether a service contract is taxable is its own question per state, "
                + "and a guess here would be a wrong number that looks considered");
    }

    // --- what a line has to be able to say about itself ----------------------

    [Fact]
    public void A_person_may_enter_the_tax_and_the_record_says_a_person_did()
    {
        var deal = Priced();

        deal.SetTax(
            [("Sales tax", "US-IL", 19500m, 0m, 1657.50m, TaxProvenance.EnteredByPerson, null, null)],
            Springfield);

        var line = deal.TaxLines.Single();
        line.Provenance.Should().Be(TaxProvenance.EnteredByPerson);
        line.PackId.Should().BeNull();
        deal.TaxTotal.Amount.Should().Be(1657.50m);
        deal.TaxedAt!.County.Should().Be("Sangamon");
    }

    [Fact]
    public void Tax_from_a_pack_has_to_name_the_pack_and_its_version()
    {
        var deal = Priced();

        var unattributed = () => deal.SetTax(
            [("Sales tax", "US-IL", 19500m, 0.085m, 1657.50m, TaxProvenance.Pack, null, null)],
            Springfield);

        unattributed.Should().Throw<ArgumentException>(
            because: "a figure from a rate table that cannot name the table cannot be audited");
    }

    [Fact]
    public void A_figure_a_person_typed_may_not_claim_a_pack_produced_it()
    {
        var deal = Priced();

        var borrowedAuthority = () => deal.SetTax(
            [("Sales tax", "US-IL", 19500m, 0m, 1657.50m, TaxProvenance.EnteredByPerson, "sst-il", 3)],
            Springfield);

        borrowedAuthority.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Tax_needs_the_address_it_was_worked_out_from()
    {
        var deal = Priced();

        var nowhere = () => deal.SetTax(
            [("Sales tax", "US-IL", 19500m, 0m, 1657.50m, TaxProvenance.EnteredByPerson, null, null)],
            null);

        nowhere.Should().Throw<ArgumentException>(
            because: "an address is how a rate is defended; without one the figure cannot be checked");
    }

    [Fact]
    public void Clearing_the_tax_clears_the_address_with_it()
    {
        var deal = Priced();
        deal.SetTax(
            [("Sales tax", "US-IL", 19500m, 0m, 1657.50m, TaxProvenance.EnteredByPerson, null, null)],
            Springfield);

        deal.SetTax([], null);

        deal.TaxLines.Should().BeEmpty();
        deal.TaxedAt.Should().BeNull(because: "an address with no tax on it is a leftover, not a record");
    }

    [Fact]
    public void Tax_off_is_a_discount_and_is_refused_as_a_tax()
    {
        var deal = Priced();

        var negative = () => deal.SetTax(
            [("Rebate", "US-IL", 19500m, 0m, -100m, TaxProvenance.EnteredByPerson, null, null)],
            Springfield);

        negative.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void The_amount_due_includes_the_tax()
    {
        var deal = Priced();
        var before = deal.AmountDue.Amount;

        deal.SetTax(
            [("Sales tax", "US-IL", 19500m, 0m, 1657.50m, TaxProvenance.EnteredByPerson, null, null)],
            Springfield);

        deal.AmountDue.Amount.Should().Be(before + 1657.50m,
            because: "a total that leaves the tax out is the number the customer disputes at delivery");
    }

    [Fact]
    public void A_deal_that_has_left_draft_will_not_have_its_tax_changed()
    {
        var deal = Priced();
        deal.SetTax(
            [("Sales tax", "US-IL", 19500m, 0m, 1657.50m, TaxProvenance.EnteredByPerson, null, null)],
            Springfield);
        deal.ChangeStatus(DealStatus.Submitted, DateTimeOffset.UtcNow);

        var late = () => deal.SetTax(
            [("Sales tax", "US-IL", 19500m, 0m, 1m, TaxProvenance.EnteredByPerson, null, null)],
            Springfield);

        late.Should().Throw<InvalidOperationException>(
            because: "the figures a manager approved are what the customer was told");
    }
}
