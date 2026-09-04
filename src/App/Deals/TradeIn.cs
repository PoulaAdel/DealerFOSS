// Copyright (c) 2026 The DealerFOSS contributors.
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Overview: Purpose, File Design, and Engineering
//   TradeIn — the customer's old car, and what it is worth to this deal.
//
// Usage:
//   TradeIn.Create(...), set through Deal.SetTerms.
//
// Coding Instructions:
//   Allowance and payoff are separate on purpose and both are positive
//   numbers. The allowance is what the dealership gives for the car; the
//   payoff is what is still owed on it to a lender. Netting them into one
//   figure loses the fact that a trade can be in negative equity, which is
//   the single most common source of an argument at the desk.

namespace DealerFOSS.Deals;

public sealed record TradeIn(string Description, decimal Allowance, decimal Payoff)
{
    /// <summary>What the trade is worth to the deal: allowance less what is owed.</summary>
    public decimal Equity => Allowance - Payoff;

    /// <summary>True when the customer owes more than the car is worth.</summary>
    public bool IsNegativeEquity => Equity < 0;

    public static TradeIn Create(string description, decimal allowance, decimal payoff)
    {
        if (string.IsNullOrWhiteSpace(description))
        {
            throw new ArgumentException(
                "A trade-in needs a description — at least year, make and model.", nameof(description));
        }

        if (allowance < 0)
        {
            throw new ArgumentException("A trade-in allowance cannot be negative.", nameof(allowance));
        }

        if (payoff < 0)
        {
            throw new ArgumentException(
                "A payoff cannot be negative. A trade owned outright has a payoff of zero.", nameof(payoff));
        }

        return new TradeIn(description.Trim(), allowance, payoff);
    }
}
