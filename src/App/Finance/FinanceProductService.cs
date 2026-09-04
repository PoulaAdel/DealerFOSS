// Copyright (c) 2026 The DealerFOSS contributors.
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Overview: Purpose, File Design, and Engineering
//   FinanceProductService — the F&I catalogue.
//
// Usage:
//   Through IFinanceProducts.
//
// Coding Instructions:
//   The catalogue is organization-wide in both directions, and that is not an
//   oversight. Reading it needs only Deals.Read, because a salesperson has to
//   see what they may offer; changing it needs Finance.ManageProducts held
//   organization-wide, because a product's provider and price list are a
//   group-level arrangement and one lot inventing its own would produce two
//   versions of the same warranty.

using Microsoft.EntityFrameworkCore;
using DealerFOSS.Core;
using DealerFOSS.Data;
using DealerFOSS.Identity;

namespace DealerFOSS.Finance;

public sealed class FinanceProductService(
    TenantDb db,
    IAccessDirectory access,
    ICurrentUser currentUser,
    IAuditSink audit)
    : IFinanceProducts
{
    /// <summary>
    /// Seeing the catalogue rides on Deals.Read. Anybody building a deal needs to
    /// know what can go on it, and a separate read permission would mean every
    /// salesperson is one missing grant away from a menu with nothing on it.
    /// </summary>
    private const string ReadPermission = Permissions.DealsRead;

    private const string ManagePermission = Permissions.FinanceManageProducts;

    private readonly TenantDb _db = db;
    private readonly IAccessDirectory _access = access;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly IAuditSink _audit = audit;

    public async Task<Result<IReadOnlyList<FinanceProductView>>> ListAsync(
        bool availableOnly,
        CancellationToken cancellationToken)
    {
        var scope = await _access.GetAuthorizedScopeAsync(_currentUser.Id, ReadPermission, cancellationToken);
        if (scope.GrantsNothing)
        {
            return Result.Failure<IReadOnlyList<FinanceProductView>>(FinanceErrors.Forbidden);
        }

        var products = _db.FinanceProducts.AsNoTracking();

        if (availableOnly)
        {
            products = products.Where(p => p.IsAvailable);
        }

        var rows = await products
            .OrderBy(p => p.Kind)
            .ThenBy(p => p.Name)
            .ToListAsync(cancellationToken);

        return Result.Success<IReadOnlyList<FinanceProductView>>(rows.Select(Describe).ToList());
    }

    public async Task<Result<IReadOnlyList<FinanceProductView>>> GetManyAsync(
        IReadOnlyCollection<Guid> productIds,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(productIds);

        var scope = await _access.GetAuthorizedScopeAsync(_currentUser.Id, ReadPermission, cancellationToken);
        if (scope.GrantsNothing)
        {
            return Result.Failure<IReadOnlyList<FinanceProductView>>(FinanceErrors.Forbidden);
        }

        if (productIds.Count == 0)
        {
            return Result.Success<IReadOnlyList<FinanceProductView>>([]);
        }

        var wanted = productIds.Distinct().ToList();

        var rows = await _db.FinanceProducts
            .AsNoTracking()
            .Where(p => wanted.Contains(p.Id))
            .ToListAsync(cancellationToken);

        return Result.Success<IReadOnlyList<FinanceProductView>>(rows.Select(Describe).ToList());
    }

    public async Task<Result<FinanceProductView>> AddAsync(
        NewFinanceProduct product,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(product);

        var refusal = await RefuseUnlessOrganizationWideAsync(
            "Attempted to add a finance product.", cancellationToken);

        if (refusal is not null)
        {
            return Result.Failure<FinanceProductView>(refusal);
        }

        if (!Enum.TryParse<FinanceProductKind>(product.Kind, ignoreCase: true, out var kind))
        {
            return Result.Failure<FinanceProductView>(FinanceErrors.UnknownKind);
        }

        var name = (product.Name ?? string.Empty).Trim();

        if (await _db.FinanceProducts.AnyAsync(p => p.Name == name, cancellationToken))
        {
            return Result.Failure<FinanceProductView>(FinanceErrors.NameTaken);
        }

        FinanceProduct created;
        try
        {
            created = new FinanceProduct(
                Guid.NewGuid(),
                product.Name ?? string.Empty,
                kind,
                product.Provider ?? string.Empty,
                product.DefaultPrice,
                product.DefaultCost,
                product.Currency,
                product.TermMonths,
                product.TermMiles);
        }
        catch (ArgumentException ex)
        {
            return Result.Failure<FinanceProductView>(Error.Validation("finance.invalid_product", ex.Message));
        }

        _db.FinanceProducts.Add(created);
        await _db.SaveChangesAsync(cancellationToken);

        await _audit.RecordAsync(
            new AuditEntry(
                _currentUser.Id, "Finance.ProductAdded", AuditOutcome.Allowed,
                "FinanceProduct", created.Id.ToString(), null,
                $"Added '{created.Name}' from {created.Provider}.", null, null),
            cancellationToken);

        return Result.Success(Describe(created));
    }

    public async Task<Result<FinanceProductView>> RepriceAsync(
        Guid productId,
        decimal defaultPrice,
        decimal defaultCost,
        CancellationToken cancellationToken)
    {
        var refusal = await RefuseUnlessOrganizationWideAsync(
            "Attempted to reprice a finance product.", cancellationToken);

        if (refusal is not null)
        {
            return Result.Failure<FinanceProductView>(refusal);
        }

        var product = await _db.FinanceProducts.SingleOrDefaultAsync(p => p.Id == productId, cancellationToken);
        if (product is null)
        {
            return Result.Failure<FinanceProductView>(FinanceErrors.NotFound);
        }

        var wasPrice = product.DefaultPrice;
        var wasCost = product.DefaultCost;

        try
        {
            product.Reprice(defaultPrice, defaultCost);
        }
        catch (ArgumentOutOfRangeException ex)
        {
            return Result.Failure<FinanceProductView>(Error.Validation("finance.invalid_price", ex.Message));
        }

        await _db.SaveChangesAsync(cancellationToken);

        await _audit.RecordAsync(
            new AuditEntry(
                _currentUser.Id, "Finance.ProductRepriced", AuditOutcome.Allowed,
                "FinanceProduct", productId.ToString(), null,
                $"'{product.Name}' now {defaultPrice:0.00} costing {defaultCost:0.00}, was "
                    + $"{wasPrice:0.00} costing {wasCost:0.00}. Deals already done keep their own figures.",
                null, null),
            cancellationToken);

        return Result.Success(Describe(product));
    }

    public async Task<Result<FinanceProductView>> SetAvailableAsync(
        Guid productId,
        bool available,
        CancellationToken cancellationToken)
    {
        var refusal = await RefuseUnlessOrganizationWideAsync(
            "Attempted to withdraw or restore a finance product.", cancellationToken);

        if (refusal is not null)
        {
            return Result.Failure<FinanceProductView>(refusal);
        }

        var product = await _db.FinanceProducts.SingleOrDefaultAsync(p => p.Id == productId, cancellationToken);
        if (product is null)
        {
            return Result.Failure<FinanceProductView>(FinanceErrors.NotFound);
        }

        if (product.IsAvailable == available)
        {
            return Result.Success(Describe(product));
        }

        if (available)
        {
            product.Restore();
        }
        else
        {
            product.Withdraw();
        }

        await _db.SaveChangesAsync(cancellationToken);

        await _audit.RecordAsync(
            new AuditEntry(
                _currentUser.Id, available ? "Finance.ProductRestored" : "Finance.ProductWithdrawn",
                AuditOutcome.Allowed, "FinanceProduct", productId.ToString(), null,
                available
                    ? $"'{product.Name}' can be sold again."
                    : $"'{product.Name}' withdrawn. Deals already sold against it are untouched.",
                null, null),
            cancellationToken);

        return Result.Success(Describe(product));
    }

    private async Task<Error?> RefuseUnlessOrganizationWideAsync(
        string what,
        CancellationToken cancellationToken)
    {
        var scope = await _access.GetAuthorizedScopeAsync(_currentUser.Id, ManagePermission, cancellationToken);
        if (scope.IsOrganizationWide)
        {
            return null;
        }

        await _audit.RecordAsync(
            AuditEntry.Denied(_currentUser.Id, ManagePermission, "FinanceProduct", string.Empty, null, what),
            cancellationToken);

        return FinanceErrors.CatalogueForbidden;
    }

    private static FinanceProductView Describe(FinanceProduct p) =>
        new(p.Id, p.Name, p.Kind.ToString(), p.Provider, p.DefaultPrice, p.DefaultCost,
            p.Currency, p.TermMonths, p.TermMiles, p.IsAvailable);
}

/// <summary>Stable error codes for the Finance capability (doc 06 §6).</summary>
internal static class FinanceErrors
{
    public static Error Forbidden { get; } = Error.Forbidden(
        "finance.forbidden",
        "You do not have access to finance products.");

    public static Error CatalogueForbidden { get; } = Error.Forbidden(
        "finance.catalogue_forbidden",
        "Changing the product catalogue needs organization-wide permission — a provider "
            + "arrangement is made for the group, not for one location.");

    public static Error NotFound { get; } = Error.NotFound(
        "finance.not_found",
        "There is no product with that id.");

    public static Error NameTaken { get; } = Error.Conflict(
        "finance.name_taken",
        "There is already a product with that name.");

    public static Error UnknownKind { get; } = Error.Validation(
        "finance.unknown_kind",
        "That is not a kind of finance product.");
}
