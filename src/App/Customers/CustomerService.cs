// Copyright (c) 2026 The DealerFOSS contributors.
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Overview: Purpose, File Design, and Engineering
//   CustomerService — finding, reading, and adding customers.
//
// Usage:
//   Through ICustomers.
//
// Coding Instructions:
//   Customers are shared across the dealer organization, so there is no
//   rooftop filter here — only a permission check. That is the documented
//   model (doc 04 §1), not an oversight: the same person buys at one
//   location and services at another, and hiding them would make staff
//   create duplicates.

using Microsoft.EntityFrameworkCore;
using DealerFOSS.Core;
using DealerFOSS.Data;
using DealerFOSS.Customers;
using DealerFOSS.Identity;

namespace DealerFOSS.Customers;

public sealed class CustomerService(
    TenantDb db,
    IAccessDirectory access,
    ICurrentUser currentUser,
    IAuditSink audit)
    : ICustomers
{
    private const string ReadPermission = "Customers.Read";
    private const string CreatePermission = "Customers.Create";
    private const string ManagePermission = "Customers.Manage";

    /// <summary>Caps how many rows a single search can return, however it is called.</summary>
    private const int MaxResults = 100;

    private readonly TenantDb _db = db;
    private readonly IAccessDirectory _access = access;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly IAuditSink _audit = audit;

    public async Task<Result<Page<CustomerSummary>>> SearchAsync(
        string? term,
        int limit,
        int offset,
        CancellationToken cancellationToken)
    {
        if (!await IsAllowedAsync(ReadPermission, cancellationToken))
        {
            return Result.Failure<Page<CustomerSummary>>(CustomerErrors.Forbidden);
        }

        var take = Paging.Limit(limit, fallback: 25);
        var skip = Paging.Offset(offset);
        var query = _db.Customers.AsNoTracking().Where(c => !c.IsArchived);

        var search = (term ?? string.Empty).Trim();
        if (search.Length > 0)
        {
            // Match how the value is stored, so "(555) 010-2030" finds the phone
            // recorded as "5550102030".
            var digits = new string(search.Where(char.IsDigit).ToArray());
            var lowered = search.ToLowerInvariant();

            query = query.Where(c =>
                EF.Functions.Like(c.LastName, $"%{search}%")
                || EF.Functions.Like(c.FirstName, $"%{search}%")
                || c.ContactPoints.Any(p =>
                    p.Value.Contains(lowered)
                    || (digits.Length > 0 && p.Value.Contains(digits))));
        }

        var total = await query.CountAsync(cancellationToken);

        // Id breaks ties. Two customers called J. Smith is not a hypothetical,
        // and without a total order the database may put one of them on page one
        // and page two and the other on neither.
        var customers = await query
            .OrderBy(c => c.LastName)
            .ThenBy(c => c.FirstName)
            .ThenBy(c => c.Id)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken);

        return Result.Success(new Page<CustomerSummary>(
            customers.Select(Summarize).ToList(),
            total,
            skip,
            take));
    }

    public async Task<Result<CustomerDetail>> GetAsync(Guid customerId, CancellationToken cancellationToken)
    {
        if (!await IsAllowedAsync(ReadPermission, cancellationToken))
        {
            return Result.Failure<CustomerDetail>(CustomerErrors.Forbidden);
        }

        var customer = await _db.Customers
            .AsNoTracking()
            .SingleOrDefaultAsync(c => c.Id == customerId, cancellationToken);

        return customer is null
            ? Result.Failure<CustomerDetail>(CustomerErrors.NotFound)
            : Result.Success(Describe(customer));
    }

    public async Task<Result<IReadOnlyList<CustomerSummary>>> GetManyAsync(
        IReadOnlyCollection<Guid> customerIds,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(customerIds);

        if (!await IsAllowedAsync(ReadPermission, cancellationToken))
        {
            return Result.Failure<IReadOnlyList<CustomerSummary>>(CustomerErrors.Forbidden);
        }

        if (customerIds.Count == 0)
        {
            return Result.Success<IReadOnlyList<CustomerSummary>>([]);
        }

        // Capped like every other read here: a caller asking for thousands of ids
        // is a bug, and answering it would be a denial-of-service on ourselves.
        var wanted = customerIds.Distinct().Take(MaxResults).ToList();

        var customers = await _db.Customers
            .AsNoTracking()
            .Where(c => wanted.Contains(c.Id))
            .ToListAsync(cancellationToken);

        return Result.Success<IReadOnlyList<CustomerSummary>>(
            customers.Select(Summarize).ToList());
    }

    public async Task<Result<CustomerDetail>> AddAsync(
        NewCustomer customer,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(customer);

        if (!await IsAllowedAsync(CreatePermission, cancellationToken))
        {
            return Result.Failure<CustomerDetail>(CustomerErrors.Forbidden);
        }

        if (!Enum.TryParse<CustomerKind>(customer.Kind, ignoreCase: true, out var kind))
        {
            return Result.Failure<CustomerDetail>(CustomerErrors.UnknownKind);
        }

        Customer created;
        try
        {
            created = kind == CustomerKind.Business
                ? Customer.Business(Guid.NewGuid(), customer.LastName, customer.HomeRooftopId)
                : Customer.Person(Guid.NewGuid(), customer.FirstName ?? string.Empty, customer.LastName, customer.HomeRooftopId);

            if (!string.IsNullOrWhiteSpace(customer.Email))
            {
                created.AddContactPoint(Guid.NewGuid(), ContactKind.Email, customer.Email, isPrimary: true);
            }

            if (!string.IsNullOrWhiteSpace(customer.Phone))
            {
                created.AddContactPoint(Guid.NewGuid(), ContactKind.Phone, customer.Phone, isPrimary: true);
            }

            if (customer.Address is { } address)
            {
                created.SetAddress(Address.Create(
                    address.Line1, address.Line2, address.City,
                    address.AdministrativeArea, address.County, address.PostalCode, address.Country));
            }

            created.SetExternalReference(customer.ExternalReference);
        }
        catch (ArgumentException ex)
        {
            // Domain invariants speak in plain sentences; surface that rather
            // than a generic "invalid request".
            return Result.Failure<CustomerDetail>(
                Error.Validation("customer.invalid", ex.Message));
        }

        _db.Customers.Add(created);
        await _db.SaveChangesAsync(cancellationToken);

        await _audit.RecordAsync(
            new AuditEntry(_currentUser.Id, CreatePermission, AuditOutcome.Allowed,
                "Customer", created.Id.ToString(), created.HomeRooftopId?.Value, null, null, null),
            cancellationToken);

        return Result.Success(Describe(created));
    }

    public async Task<Result<CustomerDetail?>> FindByExternalReferenceAsync(
        string externalReference,
        CancellationToken cancellationToken)
    {
        if (!await IsAllowedAsync(ReadPermission, cancellationToken))
        {
            return Result.Failure<CustomerDetail?>(CustomerErrors.Forbidden);
        }

        var reference = (externalReference ?? string.Empty).Trim();
        if (reference.Length == 0)
        {
            return Result.Success<CustomerDetail?>(null);
        }

        var customer = await _db.Customers
            .AsNoTracking()
            .SingleOrDefaultAsync(c => c.ExternalReference == reference, cancellationToken);

        return Result.Success(customer is null ? null : Describe(customer));
    }

    public async Task<Result<IReadOnlyList<CustomerDetail>>> PageForExportAsync(
        Guid? after,
        int take,
        CancellationToken cancellationToken)
    {
        if (!await IsAllowedAsync(ReadPermission, cancellationToken))
        {
            return Result.Failure<IReadOnlyList<CustomerDetail>>(CustomerErrors.Forbidden);
        }

        var query = _db.Customers.AsNoTracking();
        if (after is { } cursor)
        {
            query = query.Where(c => c.Id.CompareTo(cursor) > 0);
        }

        var customers = await query
            .OrderBy(c => c.Id)
            .Take(Math.Clamp(take <= 0 ? 500 : take, 1, 1000))
            .ToListAsync(cancellationToken);

        return Result.Success<IReadOnlyList<CustomerDetail>>(
            customers.Select(Describe).ToList());
    }

    public async Task<Result<CustomerDetail>> SetCreditLimitAsync(
        Guid customerId,
        decimal? limit,
        CancellationToken cancellationToken)
    {
        if (!await IsAllowedAsync(ManagePermission, cancellationToken))
        {
            return Result.Failure<CustomerDetail>(CustomerErrors.Forbidden);
        }

        var customer = await _db.Customers
            .SingleOrDefaultAsync(c => c.Id == customerId, cancellationToken);

        if (customer is null)
        {
            return Result.Failure<CustomerDetail>(CustomerErrors.NotFound);
        }

        try
        {
            customer.SetCreditLimit(limit);
        }
        catch (ArgumentOutOfRangeException ex)
        {
            return Result.Failure<CustomerDetail>(Error.Validation("customer.invalid", ex.Message));
        }

        await _db.SaveChangesAsync(cancellationToken);

        await _audit.RecordAsync(
            new AuditEntry(_currentUser.Id, ManagePermission, AuditOutcome.Allowed,
                "Customer", customer.Id.ToString(), customer.HomeRooftopId?.Value,
                limit is null ? "Credit limit cleared" : $"Credit limit set to {limit}", null, null),
            cancellationToken);

        return Result.Success(Describe(customer));
    }

    public async Task<Result<CustomerDetail>> SetAddressAsync(
        Guid customerId,
        AddressView? address,
        CancellationToken cancellationToken)
    {
        if (!await IsAllowedAsync(ManagePermission, cancellationToken))
        {
            return Result.Failure<CustomerDetail>(CustomerErrors.Forbidden);
        }

        var customer = await _db.Customers
            .SingleOrDefaultAsync(c => c.Id == customerId, cancellationToken);

        if (customer is null)
        {
            return Result.Failure<CustomerDetail>(CustomerErrors.NotFound);
        }

        try
        {
            customer.SetAddress(address is null
                ? null
                : Address.Create(
                    address.Line1, address.Line2, address.City,
                    address.AdministrativeArea, address.County, address.PostalCode, address.Country));
        }
        catch (ArgumentException ex)
        {
            return Result.Failure<CustomerDetail>(Error.Validation("customer.invalid", ex.Message));
        }

        await _db.SaveChangesAsync(cancellationToken);

        await _audit.RecordAsync(
            new AuditEntry(_currentUser.Id, ManagePermission, AuditOutcome.Allowed,
                "Customer", customer.Id.ToString(), customer.HomeRooftopId?.Value,
                address is null ? "Address cleared" : "Address set", null, null),
            cancellationToken);

        return Result.Success(Describe(customer));
    }

    /// <summary>No permission check — see the remarks on ICustomers.GetCreditLimitAsync.</summary>
    public async Task<Result<decimal?>> GetCreditLimitAsync(Guid customerId, CancellationToken cancellationToken)
    {
        var customer = await _db.Customers
            .AsNoTracking()
            .SingleOrDefaultAsync(c => c.Id == customerId, cancellationToken);

        return customer is null
            ? Result.Failure<decimal?>(CustomerErrors.NotFound)
            : Result.Success(customer.CreditLimit);
    }

    public async Task<Result> SetRemovedAtProviderAsync(
        Guid customerId,
        DateTimeOffset? removedOn,
        CancellationToken cancellationToken)
    {
        // Manage, not Create: this changes an existing record. The sink runs as
        // whoever the integration runs as, and that caller must hold the same
        // right a person would need to alter a customer.
        if (!await IsAllowedAsync(ManagePermission, cancellationToken))
        {
            return Result.Failure(CustomerErrors.Forbidden);
        }

        var customer = await _db.Customers
            .SingleOrDefaultAsync(c => c.Id == customerId, cancellationToken);

        if (customer is null)
        {
            return Result.Failure(CustomerErrors.NotFound);
        }

        // Idempotent on purpose — a held cursor replays the same deletion every
        // night, so "already marked" has to be a success rather than a conflict.
        // Compared before writing so an unchanged record is not touched at all,
        // which keeps the audit trail to real events.
        if (customer.RemovedAtProviderOn == removedOn)
        {
            return Result.Success();
        }

        if (removedOn is { } when)
        {
            customer.MarkRemovedAtProvider(when);
        }
        else
        {
            customer.RestoreAtProvider();
        }

        await _db.SaveChangesAsync(cancellationToken);

        await _audit.RecordAsync(
            new AuditEntry(_currentUser.Id, ManagePermission, AuditOutcome.Allowed,
                "Customer", customerId.ToString(), null,
                removedOn is null
                    ? "The provider is serving this customer again"
                    : "The provider no longer has this customer",
                null, null),
            cancellationToken);

        return Result.Success();
    }

    /// <summary>
    /// A customer is organization-wide, so holding the permission anywhere is
    /// enough. Denials are audited by the access directory.
    /// </summary>
    private async Task<bool> IsAllowedAsync(string permission, CancellationToken cancellationToken)
    {
        var scope = await _access.GetAuthorizedScopeAsync(_currentUser.Id, permission, cancellationToken);
        return !scope.GrantsNothing;
    }

    private static CustomerSummary Summarize(Customer c) =>
        new(c.Id,
            c.DisplayName,
            c.Kind.ToString(),
            Primary(c, ContactKind.Email),
            Primary(c, ContactKind.Phone) ?? Primary(c, ContactKind.Mobile),
            c.RemovedAtProviderOn);

    private static CustomerDetail Describe(Customer c) =>
        new(c.Id,
            c.DisplayName,
            c.Kind.ToString(),
            c.FirstName,
            c.LastName,
            c.HomeRooftopId,
            c.Address is null
                ? null
                : new AddressView(
                    c.Address.Line1, c.Address.Line2, c.Address.City,
                    c.Address.AdministrativeArea, c.Address.County, c.Address.PostalCode, c.Address.Country),
            c.ContactPoints
                .OrderByDescending(p => p.IsPrimary)
                .ThenBy(p => p.Kind)
                .Select(p => new ContactPointView(p.Id, p.Kind.ToString(), p.Value, p.IsPrimary))
                .ToList(),
            c.ExternalReference,
            c.CreditLimit,
            c.RemovedAtProviderOn);

    private static string? Primary(Customer c, ContactKind kind) =>
        c.ContactPoints.FirstOrDefault(p => p.Kind == kind && p.IsPrimary)?.Value;
}

/// <summary>Stable error codes for the Customers capability (doc 06 §6).</summary>
internal static class CustomerErrors
{
    public static Error Forbidden { get; } = Error.Forbidden(
        "customers.forbidden",
        "You do not have access to customer records.");

    public static Error NotFound { get; } = Error.NotFound(
        "customers.not_found",
        "No such customer.");

    public static Error UnknownKind { get; } = Error.Validation(
        "customers.unknown_kind",
        "A customer must be either a Person or a Business.");

    /// <summary>
    /// Used by <see cref="CustomerRecordSink"/> when an arriving record has no
    /// usable surname. A customer with a blank display name is a row nobody can
    /// find again, so it is quarantined rather than created.
    /// </summary>
    public static Error MissingName { get; } = Error.Validation(
        "customers.missing_name",
        "A customer record arrived without a usable name.");
}
