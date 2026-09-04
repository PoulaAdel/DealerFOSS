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

    /// <summary>Caps how many rows a single search can return, however it is called.</summary>
    private const int MaxResults = 100;

    private readonly TenantDb _db = db;
    private readonly IAccessDirectory _access = access;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly IAuditSink _audit = audit;

    public async Task<Result<IReadOnlyList<CustomerSummary>>> SearchAsync(
        string? term,
        int limit,
        CancellationToken cancellationToken)
    {
        if (!await IsAllowedAsync(ReadPermission, cancellationToken))
        {
            return Result.Failure<IReadOnlyList<CustomerSummary>>(CustomerErrors.Forbidden);
        }

        var take = Math.Clamp(limit <= 0 ? 25 : limit, 1, MaxResults);
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

        var customers = await query
            .OrderBy(c => c.LastName)
            .ThenBy(c => c.FirstName)
            .Take(take)
            .ToListAsync(cancellationToken);

        return Result.Success<IReadOnlyList<CustomerSummary>>(
            customers.Select(Summarize).ToList());
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
                    address.AdministrativeArea, address.PostalCode, address.Country));
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
            Primary(c, ContactKind.Phone) ?? Primary(c, ContactKind.Mobile));

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
                    c.Address.AdministrativeArea, c.Address.PostalCode, c.Address.Country),
            c.ContactPoints
                .OrderByDescending(p => p.IsPrimary)
                .ThenBy(p => p.Kind)
                .Select(p => new ContactPointView(p.Id, p.Kind.ToString(), p.Value, p.IsPrimary))
                .ToList(),
            c.ExternalReference);

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
