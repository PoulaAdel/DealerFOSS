// Copyright (c) 2026 The DealerFOSS contributors.
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Overview: Purpose, File Design, and Engineering
//   TaxBasisRules — the three flags that decide what a vehicle sale is taxed
//   ON, before any rate is applied to it.
//
// Usage:
//   var basis = deal.TaxableBasis(rules);
//
// Coding Instructions:
//   THIS IS THE PART A GENERAL TAX ENGINE GETS WRONG, and it is the reason
//   ADR-024 keeps the basis on our side of the seam while the RATE is bought or
//   fetched. Avalara and its peers price general retail; none of them knows
//   that a trade-in changes the taxable amount, and getting that wrong is not a
//   rounding error — on a $30,000 car against a $10,000 trade it is the tax on
//   ten thousand dollars, every deal, in one direction.
//
//   FLAGS AND NUMBERS, NEVER LOGIC (ADR-024 R1). A pack supplies these three
//   booleans; the arithmetic that reads them lives in Deal.TaxableBasis, in our
//   code, under our tests. The moment a pack wants to express something these
//   flags cannot, that is the signal to add a flag — not to let the pack carry
//   a rule.
//
//   The two worked examples below are real and are opposites, which is why they
//   are stated here rather than left to a pack author to rediscover:
//
//   - MOST US STATES tax the price less the trade-in allowance.
//   - CALIFORNIA DOES NOT. CDTFA Publication 34 taxes the full price, and uses
//     a $20,000 car with a $4,000 trade as its own worked example: tax is based
//     on $20,000. A pack for California sets TradeInReducesBasis to false.
//
//   Fees are split in two because they behave differently and lumping them
//   together is how the doc fee ends up untaxed in a state that taxes it. The
//   documentation fee is a dealer charge and is part of the taxable price in
//   most states; registration and title fees are government pass-throughs and
//   usually are not. Both are flags because "usually" is not a rule.

namespace DealerFOSS.Deals;

/// <summary>
/// What a jurisdiction taxes a vehicle sale on. Supplied by a pack; read by
/// <see cref="Deal.TaxableBasis"/>.
/// </summary>
/// <param name="TradeInReducesBasis">
/// True where the trade-in allowance comes off the taxable amount, which is most
/// US states. False in California and the handful like it.
/// </param>
/// <param name="DocumentationFeeIsTaxable">
/// True where the dealer's documentation fee is part of the taxable price, which
/// is most states.
/// </param>
/// <param name="OtherFeesAreTaxable">
/// Registration, title and delivery. Usually false — they are collected on
/// somebody else's behalf.
/// </param>
public sealed record TaxBasisRules(
    bool TradeInReducesBasis,
    bool DocumentationFeeIsTaxable,
    bool OtherFeesAreTaxable)
{
    /// <summary>
    /// What most US states do, offered as a starting point for a pack author and
    /// NOT as a default anything falls back to. A jurisdiction nobody has written
    /// rules for does not quietly get these; it gets a person entering the tax
    /// (ADR-024 R5).
    /// </summary>
    public static TaxBasisRules CommonUnitedStates { get; } = new(
        TradeInReducesBasis: true,
        DocumentationFeeIsTaxable: true,
        OtherFeesAreTaxable: false);
}
