// Copyright (c) 2026 The DealerFOSS contributors.
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Overview: Purpose, File Design, and Engineering
//   Financing — the retail instalment structure agreed on a deal: what the
//   customer put down, at what rate, over how long, and with whom. Plus the
//   arithmetic that turns those four facts into the number every retail buyer
//   asks for first, which is what they pay a month.
//
// Usage:
//   Financing.Create(...), set through Deal.SetFinancing while the deal is
//   Draft. Deal.Instalments reads the derived plan back.
//
// Coding Instructions:
//   FOUR FIGURES ARE STORED AND EVERYTHING ELSE IS DERIVED. The amount
//   financed is AmountDue less the cash down, and the payment follows from it.
//   Storing either alongside these four would give the deal two answers to the
//   same question, and they would disagree within a month — the same reasoning
//   as DealProduct.Gross, and the reason there is no AmountFinanced column.
//
//   THE DOWN PAYMENT IS NOT A DISCOUNT. It is how the customer pays, not a
//   reduction in what they owe, so it is deliberately OUTSIDE Deal.AmountDue.
//   The receivable opens at AmountDue and the down payment settles part of it
//   like any other receipt; netting it here would make the money vanish twice.
//   This is also what keeps the two "read the column down and reach the total"
//   tests true — DocumentTests.The_printed_order_adds_up_to_its_own_total and
//   its counterpart on the deal desk — so a finance figure never belongs in
//   that column.
//
//   THE RATE IS A FRACTION, NOT A PERCENTAGE. 0.0649 is 6.49%, the same
//   convention and the same decimal(9,6) precision as a tax rate, and for the
//   same reason: a rate is not money and rounding it to the cent would hide
//   the rounding somewhere nobody can see it. Create refuses anything at or
//   above 1, because a rate typed as "6.49" is the one data-entry slip this
//   field will actually see.
//
//   THE SCHEDULE IS WALKED, NOT ESTIMATED. A rounded payment times the term is
//   not what a customer pays: the last instalment clears whatever is left, and
//   the finance charge is the difference between what is actually paid and what
//   was financed. PlanFor walks the months in decimal so the balance provably
//   reaches zero. Do not replace it with payment x term — that is the class of
//   arithmetic this project has now corrected five times.

namespace DealerFOSS.Deals;

/// <summary>
/// How a deal is being paid for over time. Null on a cash deal, and null is the
/// ordinary case rather than missing data.
/// </summary>
/// <param name="Lender">
/// Who the paper went to, as the dealership writes it. A NAME, not a record:
/// nothing here submits a credit application or receives a decision, and
/// inventing a lender entity to hold a string would imply it does.
/// </param>
/// <param name="AnnualPercentageRate">A fraction: 0.0649 is 6.49%.</param>
public sealed record Financing(
    string? Lender,
    decimal DownPayment,
    decimal AnnualPercentageRate,
    int TermMonths)
{
    /// <summary>
    /// Ten years. Longer than any real retail auto contract — the longest sold
    /// anywhere is 96 months — so this catches a term typed in days or a stray
    /// digit without refusing anything a dealership would actually write.
    /// </summary>
    public const int LongestTermMonths = 120;

    public static Financing Create(
        string? lender,
        decimal downPayment,
        decimal annualPercentageRate,
        int termMonths)
    {
        if (downPayment < 0m)
        {
            throw new ArgumentException(
                "A down payment cannot be negative. Nothing down is zero.", nameof(downPayment));
        }

        if (annualPercentageRate < 0m)
        {
            throw new ArgumentException(
                "An interest rate cannot be negative.", nameof(annualPercentageRate));
        }

        // The one slip this field will really see. Stated as a fraction because
        // that is what the tax lines do, and a system that accepted both would
        // have to guess which was meant at 0.5.
        if (annualPercentageRate >= 1m)
        {
            throw new ArgumentException(
                "A rate is a fraction, not a percentage: 6.49% is 0.0649. "
                + $"{annualPercentageRate} would be {annualPercentageRate * 100m}% a year.",
                nameof(annualPercentageRate));
        }

        if (termMonths < 1)
        {
            throw new ArgumentException(
                "A financed deal runs for at least one month.", nameof(termMonths));
        }

        if (termMonths > LongestTermMonths)
        {
            throw new ArgumentException(
                $"{termMonths} months is longer than any retail contract. "
                + $"The longest this accepts is {LongestTermMonths}.",
                nameof(termMonths));
        }

        return new Financing(
            string.IsNullOrWhiteSpace(lender) ? null : lender.Trim(),
            downPayment,
            annualPercentageRate,
            termMonths);
    }

    /// <summary>
    /// The instalments this structure produces over an amount financed, or null
    /// when there is nothing left to finance.
    /// </summary>
    /// <remarks>
    /// NULL RATHER THAN A THROW, because the caller is usually a screen. A deal
    /// can legitimately sit in Draft with a down payment that a later reprice
    /// has made larger than the total, and a computed property that threw would
    /// turn that half-worked deal into a failed request instead of something a
    /// person can see and fix. Deal.EnsureReadyToSubmit is where it is refused.
    /// </remarks>
    public InstalmentPlan? PlanFor(decimal amountFinanced)
    {
        if (amountFinanced <= 0m)
        {
            return null;
        }

        var monthlyRate = AnnualPercentageRate / 12m;
        var monthly = Cents(Instalment(amountFinanced, monthlyRate));

        // Walked rather than multiplied. Every figure below comes out of this
        // loop, so the plan cannot claim a total that the payments do not reach.
        var balance = amountFinanced;
        var paid = 0m;
        var last = 0m;

        for (var month = 1; month <= TermMonths && balance > 0m; month++)
        {
            var interest = Cents(balance * monthlyRate);

            // The last instalment clears the balance, and no instalment may take
            // more than is owed — which is what stops a tiny amount over a long
            // term from being overpaid by a cent a month.
            var due = month == TermMonths ? balance + interest : monthly;
            if (due > balance + interest)
            {
                due = balance + interest;
            }

            balance = balance + interest - due;
            paid += due;
            last = due;
        }

        return new InstalmentPlan(monthly, last, paid, paid - amountFinanced);
    }

    /// <summary>
    /// The level payment that amortises an amount over the term:
    /// <c>P·i·(1+i)^n / ((1+i)^n − 1)</c>, and <c>P/n</c> at nothing percent.
    /// </summary>
    /// <remarks>
    /// The power is computed by repeated decimal multiplication rather than
    /// Math.Pow. decimal has no Pow and converting to double for it would put a
    /// binary approximation at the root of every payment in the system; n is at
    /// most <see cref="LongestTermMonths"/>, so the loop costs nothing and the
    /// whole calculation stays in the type the money is in.
    ///
    /// Zero percent is a real offer, not a division to guard against.
    /// </remarks>
    private decimal Instalment(decimal amountFinanced, decimal monthlyRate)
    {
        if (monthlyRate == 0m)
        {
            return amountFinanced / TermMonths;
        }

        var growth = 1m;
        for (var month = 0; month < TermMonths; month++)
        {
            growth *= 1m + monthlyRate;
        }

        return amountFinanced * monthlyRate * growth / (growth - 1m);
    }

    /// <summary>
    /// To the cent, away from zero — the same rounding as a service line's
    /// amount, so two parts of the product do not round a half-cent differently.
    /// </summary>
    private static decimal Cents(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);
}

/// <summary>
/// What the customer actually pays, derived from a <see cref="Financing"/> and an
/// amount financed. Never stored.
/// </summary>
/// <param name="MonthlyPayment">The level instalment. The number the customer asked for.</param>
/// <param name="FinalPayment">
/// The last one, which clears the balance and is usually a few cents different
/// from the rest. Named rather than hidden, because it is what the contract says.
/// </param>
/// <param name="TotalOfPayments">Everything the customer will pay over the term.</param>
/// <param name="FinanceCharge">
/// What the credit costs: the total of payments less the amount financed.
/// </param>
public sealed record InstalmentPlan(
    decimal MonthlyPayment,
    decimal FinalPayment,
    decimal TotalOfPayments,
    decimal FinanceCharge);
