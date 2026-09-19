// Copyright (c) 2026 The DealerFOSS contributors.
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Overview: Purpose, File Design, and Engineering
//   ICustomers — what other capabilities may call to reach customer records.
//
// Usage:
//   Leads, Sales, and Service confirm a customer exists and read a summary
//   through this. They never touch the customer tables.
//
// Coding Instructions:
//   Keep the returned shapes small. A capability that needs a field not here
//   should say why — widening the contract couples every caller to it.

using DealerFOSS.Core;

namespace DealerFOSS.Customers;

public interface ICustomers
{
    /// <summary>
    /// Finds customers by name, email, or phone, one page at a time. An empty
    /// term returns everyone, alphabetically by surname, so the screen has
    /// something to show before anybody types.
    /// </summary>
    Task<Result<Page<CustomerSummary>>> SearchAsync(
        string? term,
        int limit,
        int offset,
        CancellationToken cancellationToken);

    Task<Result<CustomerDetail>> GetAsync(Guid customerId, CancellationToken cancellationToken);

    /// <summary>
    /// Names for a known set of customers, in one query. A work list that shows
    /// customer names would otherwise fetch them one at a time — this exists so a
    /// caller never has to choose between a slow screen and a stale copy of the
    /// name. Unknown ids are simply absent from the result.
    /// </summary>
    Task<Result<IReadOnlyList<CustomerSummary>>> GetManyAsync(
        IReadOnlyCollection<Guid> customerIds,
        CancellationToken cancellationToken);

    Task<Result<CustomerDetail>> AddAsync(NewCustomer customer, CancellationToken cancellationToken);

    /// <summary>
    /// Finds the customer imported from a given record in another system, or null.
    /// </summary>
    /// <remarks>
    /// This is what makes importing the same file twice safe. It is a lookup and
    /// not a search: an external reference either identifies exactly one customer
    /// or identifies none, which is the property the unique index enforces.
    /// </remarks>
    Task<Result<CustomerDetail?>> FindByExternalReferenceAsync(
        string externalReference,
        CancellationToken cancellationToken);

    /// <summary>
    /// One page of customers in id order, for walking the whole set. Pass the
    /// last id seen to get the next page; null starts at the beginning.
    /// </summary>
    /// <remarks>
    /// Keyset rather than offset paging (doc 06 §6): an offset shifts under a
    /// concurrent insert, so a long export would silently skip or repeat
    /// somebody. Archived customers are included — an export is the dealership's
    /// own record and leaving people out of it would make it wrong.
    /// </remarks>
    Task<Result<IReadOnlyList<CustomerDetail>>> PageForExportAsync(
        Guid? after,
        int take,
        CancellationToken cancellationToken);

    /// <summary>
    /// Sets the most this customer may owe across every open bill, or clears the
    /// cap with null.
    /// </summary>
    Task<Result<CustomerDetail>> SetCreditLimitAsync(
        Guid customerId,
        decimal? limit,
        CancellationToken cancellationToken);

    /// <summary>
    /// Replaces this customer's mailing address, or clears it with null. This is
    /// the customer's own address for correspondence — not the registration or
    /// garaging address that decides tax on a sale, which is recorded on the
    /// deal instead and frozen with it (ADR-024).
    /// </summary>
    Task<Result<CustomerDetail>> SetAddressAsync(
        Guid customerId,
        AddressView? address,
        CancellationToken cancellationToken);

    /// <summary>
    /// The credit limit alone, or null if there is no cap.
    /// </summary>
    /// <remarks>
    /// NO PERMISSION CHECK, the same contract <c>IReceivables.OpenAsync</c> uses
    /// for the same reason: this is called while opening a debt, inside the
    /// caller's own transaction, by a request that has already been authorized
    /// to deliver the car or invoice the job. Coupling it to
    /// <c>Customers.Read</c> would make credit-limit enforcement depend on a
    /// permission a role built around Deals or RepairOrders may never have
    /// been given, for a value that is not personal data.
    /// </remarks>
    Task<Result<decimal?>> GetCreditLimitAsync(Guid customerId, CancellationToken cancellationToken);

    /// <summary>
    /// Records that the provider this customer came from no longer has them, or
    /// that it is serving them again (<paramref name="removedOn"/> null).
    /// </summary>
    /// <remarks>
    /// <para>
    /// For <see cref="Integrations.IRecordSink"/> implementations, and nothing
    /// else — a member of staff who wants a record out of the way uses
    /// archiving, which is the dealership's own decision. This one records
    /// somebody else's statement about their own database.
    /// </para>
    /// <para>
    /// IT DOES NOT HIDE OR DELETE ANYTHING. See ADR-026. Idempotent, because a
    /// held cursor replays the same deletion every night: marking a record
    /// already marked, or restoring one that was never marked, is a no-op that
    /// succeeds.
    /// </para>
    /// </remarks>
    Task<Result> SetRemovedAtProviderAsync(
        Guid customerId,
        DateTimeOffset? removedOn,
        CancellationToken cancellationToken);
}

/// <summary>Enough to identify a customer in a list.</summary>
public sealed record CustomerSummary(
    Guid Id,
    string DisplayName,
    string Kind,
    string? PrimaryEmail,
    string? PrimaryPhone,

    /// <summary>
    /// When the system this customer came from stopped having them, or null.
    /// A MARK, NOT A REMOVAL (ADR-026): the row stays listed and searchable and
    /// is barred only from being chosen for new work.
    /// </summary>
    DateTimeOffset? RemovedAtProviderOn = null);

/// <summary>One customer in full, as a screen or another module would show them.</summary>
public sealed record CustomerDetail(
    Guid Id,
    string DisplayName,
    string Kind,
    string FirstName,
    string LastName,
    RooftopId? HomeRooftopId,
    AddressView? Address,
    IReadOnlyList<ContactPointView> ContactPoints,
    string? ExternalReference = null,

    /// <summary>The most this customer may owe across every open bill, or null for no cap.</summary>
    decimal? CreditLimit = null,

    /// <summary>See <see cref="CustomerSummary.RemovedAtProviderOn"/>.</summary>
    DateTimeOffset? RemovedAtProviderOn = null);

public sealed record ContactPointView(Guid Id, string Kind, string Value, bool IsPrimary);

/// <summary>
/// An address as the API speaks it. <see cref="County"/> is separate from
/// <see cref="AdministrativeArea"/> because US sales tax varies by both
/// (ADR-024); it is null in most countries, which is expected.
/// </summary>
public sealed record AddressView(
    string Line1,
    string? Line2,
    string City,
    string? AdministrativeArea,
    string? County,
    string? PostalCode,
    string Country);

/// <summary>What a caller supplies to create a customer.</summary>
/// <param name="ExternalReference">
/// Their identifier in the system this record came from, when it was imported.
/// Null for a customer typed in by a person, which is most of them.
/// </param>
public sealed record NewCustomer(
    string Kind,
    string? FirstName,
    string LastName,
    RooftopId? HomeRooftopId,
    string? Email,
    string? Phone,
    AddressView? Address,
    string? ExternalReference = null);
