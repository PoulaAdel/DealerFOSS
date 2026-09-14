// Copyright (c) 2026 The DealerFOSS contributors.
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Overview: Purpose, File Design, and Engineering
//   CustomerCredit — money the dealership is holding that belongs to a customer,
//   and what has been done with it since.
//
//   The mirror image of Receivable, in the same capability on purpose: one is
//   what a customer owes us, the other is what we owe them, and they are the two
//   halves of the same relationship. Account 1100 and account 2200 are the
//   ledger's version of the same pair.
//
//   EACH OVERPAYMENT IS ITS OWN ROW, drawn down by uses, rather than one running
//   balance per customer. A dealership asked "where did this $59.50 come from"
//   must be able to answer "you overpaid invoice RO-1084 on the 3rd", and a
//   single balance cannot answer it. It is the same reasoning as Receivable's
//   payments: the evidence is the rows, and the total is derived from them.
//
// Usage:
//   Raised by ReceivableService when somebody pays more than the bill.
//   Spent by ApplyCreditAsync (against another bill) or RefundCreditAsync.
//
// Coding Instructions:
//   THERE IS NO SETTER FOR THE REMAINING BALANCE, for the same reason Receivable
//   has none for what is outstanding. A stored balance is a balance that will one
//   day disagree with the uses underneath it, and it is the uses that a customer
//   arguing about their money is actually asking about.
//
//   A CREDIT IS NEVER DELETED AND NEVER EDITED. Raised in error, it is refunded
//   or applied — both leave a row. Money that appears and disappears from a
//   customer's account with no trail is the shape of every refund fraud there
//   has ever been.
//
//   USES ARE CAPPED AT WHAT IS LEFT, here, in the entity. The service checks
//   too, so that the message is a good one, but this is the check that cannot be
//   forgotten by a new caller.

using DealerFOSS.Core;

namespace DealerFOSS.Receivables;

/// <summary>
/// One overpayment, and what has since been done with it.
/// </summary>
public sealed class CustomerCredit : AuditableEntity
{
    private readonly List<CreditUse> _uses = [];

    public Guid Id { get; private set; }

    public RooftopId RooftopId { get; private set; }

    public Guid CustomerId { get; private set; }

    /// <summary>What was overpaid. Never changes.</summary>
    public decimal Amount { get; private set; }

    public string Currency { get; private set; } = "USD";

    /// <summary>
    /// The bill that was overpaid, so somebody can be told where their money came
    /// from without reading the journal.
    /// </summary>
    public string Reference { get; private set; } = string.Empty;

    /// <summary>The receivable the overpayment landed on, when there was one.</summary>
    public Guid? SourceReceivableId { get; private set; }

    public DateTimeOffset RaisedAt { get; private set; }

    public IReadOnlyList<CreditUse> Uses => _uses;

    /// <summary>Summed from the uses, never stored.</summary>
    public decimal Spent => _uses.Sum(u => u.Amount);

    /// <summary>What the dealership still owes this customer out of this credit.</summary>
    public decimal Remaining => Amount - Spent;

    public bool IsSpent => Remaining == 0m;

    private CustomerCredit()
    {
    }

    public static CustomerCredit Raise(
        Guid id,
        RooftopId rooftopId,
        Guid customerId,
        Money amount,
        string reference,
        Guid? sourceReceivableId,
        DateTimeOffset raisedAt)
    {
        if (string.IsNullOrWhiteSpace(reference))
        {
            throw new ArgumentException("A credit needs to say what it came from.", nameof(reference));
        }

        if (amount.Amount <= 0m)
        {
            throw new ArgumentOutOfRangeException(
                nameof(amount), "A credit is for an amount above zero.");
        }

        return new CustomerCredit
        {
            Id = id,
            RooftopId = rooftopId,
            CustomerId = customerId,
            Amount = amount.Amount,
            Currency = amount.Currency,
            Reference = reference.Trim(),
            SourceReceivableId = sourceReceivableId,
            RaisedAt = raisedAt,
        };
    }

    /// <summary>
    /// Spends part or all of the credit — against a bill, or back to the
    /// customer. Part uses are ordinary: a $200 credit can settle a $60 invoice
    /// and stay open for the rest.
    /// </summary>
    public CreditUse Spend(
        Guid id,
        Money amount,
        CreditUseKind kind,
        Guid? receivableId,
        DateTimeOffset usedAt,
        string? note)
    {
        if (amount.Currency != Currency)
        {
            throw new ArgumentException(
                $"This credit is in {Currency} and that is {amount.Currency}.", nameof(amount));
        }

        if (amount.Amount <= 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), "A use is for an amount above zero.");
        }

        if (amount.Amount > Remaining)
        {
            throw new ArgumentOutOfRangeException(
                nameof(amount),
                $"There is only {Currency} {Remaining} left on this credit.");
        }

        // A credit put against a bill has to say which bill, or it is just money
        // vanishing. A refund has no receivable and must not invent one.
        if (kind == CreditUseKind.AppliedToBill && receivableId is null)
        {
            throw new ArgumentException("Applying a credit needs the bill it paid.", nameof(receivableId));
        }

        if (kind == CreditUseKind.Refunded && receivableId is not null)
        {
            throw new ArgumentException("A refund does not settle a bill.", nameof(receivableId));
        }

        var use = CreditUse.Record(id, Id, amount, kind, receivableId, usedAt, note);
        _uses.Add(use);

        return use;
    }
}

/// <summary>
/// Part of a credit, spent. Append-only, like a payment: the customer's copy of
/// what happened does not disappear when ours does.
/// </summary>
public sealed class CreditUse : AuditableEntity, IAppendOnly
{
    public Guid Id { get; private set; }

    public Guid CustomerCreditId { get; private set; }

    public decimal Amount { get; private set; }

    public string Currency { get; private set; } = "USD";

    public CreditUseKind Kind { get; private set; }

    /// <summary>The bill it paid, when it paid one. Null on a refund.</summary>
    public Guid? ReceivableId { get; private set; }

    public DateTimeOffset UsedAt { get; private set; }

    /// <summary>How it was handed back, or why it was applied. Whatever was written down.</summary>
    public string? Note { get; private set; }

    private CreditUse()
    {
    }

    internal static CreditUse Record(
        Guid id,
        Guid customerCreditId,
        Money amount,
        CreditUseKind kind,
        Guid? receivableId,
        DateTimeOffset usedAt,
        string? note) =>
        new()
        {
            Id = id,
            CustomerCreditId = customerCreditId,
            Amount = amount.Amount,
            Currency = amount.Currency,
            Kind = kind,
            ReceivableId = receivableId,
            UsedAt = usedAt,
            Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim(),
        };
}

/// <summary>The two things that can happen to a credit. There is no third.</summary>
public enum CreditUseKind
{
    /// <summary>Put against another bill the same customer owes. No cash moves.</summary>
    AppliedToBill = 0,

    /// <summary>Handed back. Cash leaves the business.</summary>
    Refunded = 1,
}
