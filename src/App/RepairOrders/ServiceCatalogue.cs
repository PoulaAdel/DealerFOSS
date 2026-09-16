// Copyright (c) 2026 The DealerFOSS contributors.
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Overview: Purpose, File Design, and Engineering
//   OpCode and LabourRate — the two things a workshop decides once and then
//   applies to every job, instead of retyping them onto every line.
//
//   WHAT THIS REPLACES. Until now a labour line carried a free-text description
//   and a rate somebody typed. Two advisors writing up the same job produced two
//   different descriptions at two different prices, and nothing could answer
//   "what do we charge for a front brake replacement" or "how long should that
//   take" — which is the question behind every efficiency figure a workshop
//   reads. EffectiveLabourRate in the labour report was, and still is, an
//   OUTPUT: what an hour actually realised. It was never a setting.
//
//   THE SPLIT IS DELIBERATE AND MIRRORS PARTS. An op code is
//   ORGANIZATION-WIDE, for the same reason a part number is: "front brakes,
//   1.4 hours" means the same job at every location, and one lot inventing its
//   own version of it is how a catalogue stops being comparable. A labour RATE
//   is PER ROOFTOP, because what an hour sells for is a local decision — a city
//   store and a rural one do not charge the same, and the group does not pretend
//   they do.
//
//   A RATE IS THE DEFAULT FOR A PAY TYPE, not a free-floating number. Warranty
//   work is reimbursed at a rate the manufacturer sets, internal work is charged
//   at something near cost, and retail is retail. Those are three different
//   prices for the same hour, and which one applies is decided by who is paying
//   — which the line already knows.
//
// Usage:
//   Through IServiceCatalogue. RepairOrderService resolves a line's description,
//   hours and rate from these when a line cites an op code.
//
// Coding Instructions:
//   NEITHER IS EVER DELETED. Withdrawn, so it stops being offered while every
//   job that already cites it still reads correctly. A repair order from March
//   must not change because somebody tidied the catalogue in September.
//
//   CHANGING A RATE DOES NOT REPRICE EXISTING WORK, and must not start to. The
//   rate is copied onto the line when the line is written, exactly as a part's
//   cost is frozen when it is issued. A rate rise that silently re-billed last
//   month's invoiced jobs would be a way to change what a customer already paid.
//
//   STANDARD HOURS ARE A DEFAULT, NOT A CAP. A seized bolt is real, and an
//   advisor who cannot bill the time it actually took will write the difference
//   in somewhere worse.

using DealerFOSS.Core;

namespace DealerFOSS.RepairOrders;

/// <summary>
/// A catalogued job: what it is called, what it is called everywhere, and how
/// long it ought to take.
/// </summary>
public sealed class OpCode : AuditableEntity
{
    public Guid Id { get; private set; }

    /// <summary>The short code an advisor knows it by — "BRK-FRT".</summary>
    public string Code { get; private set; } = string.Empty;

    public string Description { get; private set; } = string.Empty;

    /// <summary>
    /// How long the job ought to take. A default for the line, never a cap.
    /// </summary>
    public decimal StandardHours { get; private set; }

    /// <summary>
    /// Who normally pays for this job. A recall is warranty; a valet on our own
    /// stock is internal; most things are the customer.
    /// </summary>
    public ServicePayType DefaultPayType { get; private set; }

    /// <summary>Withdrawn codes stop being offered and keep reading correctly.</summary>
    public bool IsActive { get; private set; } = true;

    private OpCode()
    {
    }

    public OpCode(Guid id, string code, string description, decimal standardHours, ServicePayType defaultPayType)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            throw new ArgumentException("An op code needs a code.", nameof(code));
        }

        if (string.IsNullOrWhiteSpace(description))
        {
            throw new ArgumentException("An op code needs a description.", nameof(description));
        }

        // Zero is refused rather than stored. "This job takes no time" is never
        // true, and a zero standard hour makes every efficiency figure built on
        // it divide by nothing.
        if (standardHours <= 0m)
        {
            throw new ArgumentOutOfRangeException(
                nameof(standardHours), "A job takes some time. Standard hours must be above zero.");
        }

        Id = id;
        Code = Normalize(code);
        Description = description.Trim();
        StandardHours = standardHours;
        DefaultPayType = defaultPayType;
    }

    /// <summary>
    /// Codes are compared and stored upper-cased with the spaces out, so
    /// "brk frt" and "BRK-FRT" cannot both exist.
    /// </summary>
    public static string Normalize(string code) =>
        code.Trim().Replace(" ", string.Empty, StringComparison.Ordinal).ToUpperInvariant();

    public void Revise(string description, decimal standardHours, ServicePayType defaultPayType)
    {
        if (string.IsNullOrWhiteSpace(description))
        {
            throw new ArgumentException("An op code needs a description.", nameof(description));
        }

        if (standardHours <= 0m)
        {
            throw new ArgumentOutOfRangeException(
                nameof(standardHours), "A job takes some time. Standard hours must be above zero.");
        }

        Description = description.Trim();
        StandardHours = standardHours;
        DefaultPayType = defaultPayType;
    }

    public void Withdraw() => IsActive = false;

    public void Restore() => IsActive = true;
}

/// <summary>
/// What an hour sells for at one lot, for one kind of payer.
/// </summary>
public sealed class LabourRate : AuditableEntity
{
    public Guid Id { get; private set; }

    public RooftopId RooftopId { get; private set; }

    /// <summary>What the workshop calls it — "Retail", "Warranty", "Internal".</summary>
    public string Name { get; private set; } = string.Empty;

    public decimal AmountPerHour { get; private set; }

    public string Currency { get; private set; } = "USD";

    /// <summary>
    /// The pay type this rate is the default for. One rate per pay type per
    /// rooftop, enforced by a unique index — two defaults for the same payer is
    /// not a choice, it is an unanswered question.
    /// </summary>
    public ServicePayType AppliesTo { get; private set; }

    public bool IsActive { get; private set; } = true;

    private LabourRate()
    {
    }

    public LabourRate(Guid id, RooftopId rooftopId, string name, Money amountPerHour, ServicePayType appliesTo)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("A labour rate needs a name.", nameof(name));
        }

        // Zero is allowed and negative is not. An internal rate of zero is a real
        // decision — some groups carry their own recon at cost and charge the
        // workshop nothing — but an hour that pays the dealership to work is not.
        if (amountPerHour.Amount < 0m)
        {
            throw new ArgumentOutOfRangeException(
                nameof(amountPerHour), "An hour cannot sell for less than nothing.");
        }

        Id = id;
        RooftopId = rooftopId;
        Name = name.Trim();
        AmountPerHour = amountPerHour.Amount;
        Currency = amountPerHour.Currency;
        AppliesTo = appliesTo;
    }

    public void Reprice(string name, Money amountPerHour)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("A labour rate needs a name.", nameof(name));
        }

        if (amountPerHour.Amount < 0m)
        {
            throw new ArgumentOutOfRangeException(
                nameof(amountPerHour), "An hour cannot sell for less than nothing.");
        }

        if (amountPerHour.Currency != Currency)
        {
            throw new ArgumentException(
                $"This rate is in {Currency} and that is {amountPerHour.Currency}.", nameof(amountPerHour));
        }

        Name = name.Trim();
        AmountPerHour = amountPerHour.Amount;
    }

    public void Withdraw() => IsActive = false;

    public void Restore() => IsActive = true;
}
