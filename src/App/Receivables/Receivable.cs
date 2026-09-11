// Copyright (c) 2026 The DealerFOSS contributors.
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Overview: Purpose, File Design, and Engineering
//   Receivable — one customer owing the dealership one amount, and the payments
//   made against it.
//
//   This is the customer sub-ledger, and it exists because account 1100 alone
//   cannot answer the question a dealership actually asks. The ledger knows it
//   is owed $84,000; only this knows that $19,000 of it is Ashgrove Couriers and
//   has been outstanding for six weeks. Every accounting system that has ever
//   worked keeps both, and they must agree: the sum of what is outstanding here
//   is the balance of 1100 there.
//
// Usage:
//   Opened by Deals when a car is delivered and by RepairOrders when a job is
//   invoiced. Settled by ReceivableService.RecordPaymentAsync.
//
// Coding Instructions:
//   PAYMENTS ARE APPEND-ONLY AND THE OUTSTANDING FIGURE IS DERIVED. Do not add
//   a setter for what is left. A running balance somebody can write to is a
//   running balance that will one day disagree with the payments underneath it,
//   and the payments are the evidence — a customer disputing a bill is asking
//   about those rows, not about a total.
//
//   OVERPAYMENT IS REFUSED, NOT ABSORBED. Taking more than is owed and quietly
//   showing zero loses real money: the difference belongs to the customer and
//   somebody has to give it back. Credit balances are a real thing and are not
//   built here, so the honest answer for now is to refuse and say why.

using DealerFOSS.Core;

namespace DealerFOSS.Receivables;

/// <summary>
/// What one customer owes against one sale or one job, and what has been paid.
/// </summary>
public sealed class Receivable : AuditableEntity
{
    private readonly List<Payment> _payments = [];

    public Guid Id { get; private set; }

    public RooftopId RooftopId { get; private set; }

    public Guid CustomerId { get; private set; }

    /// <summary>Which kind of thing was billed — a car, or a job in the workshop.</summary>
    public ReceivableSource Source { get; private set; }

    /// <summary>
    /// The deal id or the repair order number, as the ledger's own entry
    /// references it. This is what ties a row here to an entry there.
    /// </summary>
    public string Reference { get; private set; } = string.Empty;

    public decimal Amount { get; private set; }

    public string Currency { get; private set; } = "USD";

    public DateTimeOffset BilledAt { get; private set; }

    public IReadOnlyList<Payment> Payments => _payments;

    /// <summary>What has come in. Summed from the payments, never stored.</summary>
    public decimal Paid => _payments.Sum(p => p.Amount);

    /// <summary>What is still owed. Never negative, because overpayment is refused.</summary>
    public decimal Outstanding => Amount - Paid;

    public bool IsSettled => Outstanding == 0m;

    private Receivable()
    {
    }

    public static Receivable Open(
        Guid id,
        RooftopId rooftopId,
        Guid customerId,
        ReceivableSource source,
        string reference,
        Money amount,
        DateTimeOffset billedAt)
    {
        if (string.IsNullOrWhiteSpace(reference))
        {
            throw new ArgumentException("A receivable needs a reference.", nameof(reference));
        }

        // Zero is refused rather than stored as an instantly-settled row. A bill
        // for nothing is not a debt, and a list of them would bury the real ones.
        if (amount.Amount <= 0m)
        {
            throw new ArgumentOutOfRangeException(
                nameof(amount), "A receivable is for an amount above zero.");
        }

        return new Receivable
        {
            Id = id,
            RooftopId = rooftopId,
            CustomerId = customerId,
            Source = source,
            Reference = reference.Trim(),
            Amount = amount.Amount,
            Currency = amount.Currency,
            BilledAt = billedAt,
        };
    }

    /// <summary>
    /// Records money arriving. Part-payments are ordinary — a deposit is one —
    /// so this takes any amount up to what is left and no more.
    /// </summary>
    public Payment Take(Guid id, Money amount, PaymentMethod method, DateTimeOffset receivedAt, string? note)
    {
        if (amount.Currency != Currency)
        {
            throw new ArgumentException(
                $"This is a {Currency} account and the payment is in {amount.Currency}.", nameof(amount));
        }

        if (amount.Amount <= 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), "A payment is for an amount above zero.");
        }

        if (amount.Amount > Outstanding)
        {
            throw new ArgumentOutOfRangeException(
                nameof(amount),
                $"That is more than the {Currency} {Outstanding} still owed. Taking the extra would owe it back.");
        }

        var payment = Payment.Record(id, Id, amount, method, receivedAt, note);
        _payments.Add(payment);

        return payment;
    }
}

/// <summary>What was billed. Kept apart because the two are read differently.</summary>
public enum ReceivableSource
{
    /// <summary>A car left the lot.</summary>
    Deal = 0,

    /// <summary>A job in the workshop was invoiced.</summary>
    RepairOrder = 1,
}

/// <summary>
/// Money arriving against a receivable. Append-only: a payment recorded in error
/// is corrected by a reversal, the same way a ledger entry is, because the
/// customer's copy of the receipt does not disappear when ours does.
/// </summary>
public sealed class Payment : AuditableEntity, IAppendOnly
{
    public Guid Id { get; private set; }

    public Guid ReceivableId { get; private set; }

    public decimal Amount { get; private set; }

    public string Currency { get; private set; } = "USD";

    public PaymentMethod Method { get; private set; }

    public DateTimeOffset ReceivedAt { get; private set; }

    /// <summary>A cheque number, a card reference, whatever the person wrote down.</summary>
    public string? Note { get; private set; }

    private Payment()
    {
    }

    internal static Payment Record(
        Guid id,
        Guid receivableId,
        Money amount,
        PaymentMethod method,
        DateTimeOffset receivedAt,
        string? note) =>
        new()
        {
            Id = id,
            ReceivableId = receivableId,
            Amount = amount.Amount,
            Currency = amount.Currency,
            Method = method,
            ReceivedAt = receivedAt,
            Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim(),
        };
}

/// <summary>
/// How the money arrived. This is a record of fact, not a routing instruction —
/// nothing here talks to a card terminal or a bank.
/// </summary>
public enum PaymentMethod
{
    Cash = 0,
    Card = 1,
    BankTransfer = 2,
    Cheque = 3,

    /// <summary>
    /// A lender settling a financed car. The customer signed for the total and a
    /// finance house sends the money, so the payer is not the person who owes it
    /// — which is why this is a method rather than a separate kind of receivable.
    /// </summary>
    Finance = 4,
}
