// Copyright (c) 2026 The DealerFOSS contributors.
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Overview: Purpose, File Design, and Engineering
//   DealService — working a deal, and the two controls that make it safe.
//
// Usage:
//   Through IDeals.
//
// Coding Instructions:
//   Three things here are load-bearing.
//
//   The rooftop scope, as everywhere else: every read is filtered in the
//   query and every write authorizes the rooftop first.
//
//   Writing a deal and approving one are separate permissions. A salesperson
//   builds the numbers; somebody holding Deals.Approve signs them off. That
//   split is the point of the capability, not decoration.
//
//   Starting a deal HOLDS the car and cancelling releases it, both through
//   IInventory. Because the deal and the hold must never disagree, they are
//   committed in one transaction — the same TenantDb instance is shared by
//   both services within a request, so a single transaction covers both.

using Microsoft.EntityFrameworkCore;
using DealerFOSS.Accounting;
using DealerFOSS.Core;
using DealerFOSS.Customers;
using DealerFOSS.Data;
using DealerFOSS.Finance;
using DealerFOSS.Identity;
using DealerFOSS.Inventory;
using DealerFOSS.Receivables;

namespace DealerFOSS.Deals;

public sealed class DealService(
    TenantDb db,
    IAccessDirectory access,
    ICustomers customers,
    IInventory inventory,
    IAccounting accounting,
    IReceivables receivables,
    IFinanceProducts products,
    ICurrentUser currentUser,
    IAuditSink audit,
    IClock clock)
    : IDeals
{
    private const string ReadPermission = Permissions.DealsRead;
    private const string WritePermission = Permissions.DealsWrite;
    private const string ApprovePermission = Permissions.DealsApprove;

    /// <summary>The only status a car can be in when a deal starts on it.</summary>
    private const string SellableStatus = "Available";

    private const int MaxResults = 200;

    private readonly TenantDb _db = db;
    private readonly IAccessDirectory _access = access;
    private readonly ICustomers _customers = customers;
    private readonly IInventory _inventory = inventory;
    private readonly IAccounting _accounting = accounting;
    private readonly IReceivables _receivables = receivables;
    private readonly IFinanceProducts _products = products;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly IAuditSink _audit = audit;
    private readonly IClock _clock = clock;

    public async Task<Result<Page<DealSummary>>> ListAsync(
        DealQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var scope = await _access.GetAuthorizedScopeAsync(_currentUser.Id, ReadPermission, cancellationToken);
        if (scope.GrantsNothing)
        {
            return Result.Failure<Page<DealSummary>>(DealErrors.Forbidden);
        }

        if (query.RooftopId is { } requested && !scope.Covers(requested))
        {
            return Result.Failure<Page<DealSummary>>(DealErrors.Forbidden);
        }

        DealStatus? status = null;
        if (!string.IsNullOrWhiteSpace(query.Status))
        {
            if (!Enum.TryParse(query.Status, ignoreCase: true, out DealStatus parsed))
            {
                return Result.Failure<Page<DealSummary>>(DealErrors.UnknownStatus);
            }

            status = parsed;
        }

        var take = Paging.Limit(query.Limit);
        var skip = Paging.Offset(query.Offset);
        var deals = _db.Deals.AsNoTracking();

        if (!scope.IsOrganizationWide)
        {
            var allowed = scope.Rooftops.ToList();
            deals = deals.Where(d => allowed.Contains(d.RooftopId));
        }

        if (query.RooftopId is { } only)
        {
            deals = deals.Where(d => d.RooftopId == only);
        }

        if (status is { } wanted)
        {
            deals = deals.Where(d => d.Status == wanted);
        }

        if (query.CustomerId is { } customer)
        {
            deals = deals.Where(d => d.CustomerId == customer);
        }

        if (query.SalespersonUserId is { } salesperson)
        {
            deals = deals.Where(d => d.SalespersonUserId == salesperson);
        }

        if (query.OpenOnly)
        {
            deals = deals.Where(d => d.Status != DealStatus.Delivered && d.Status != DealStatus.Cancelled);
        }

        // Counted over the same filters as the page, and before it is taken.
        var total = await deals.CountAsync(cancellationToken);

        var rows = await deals
            .Include(d => d.Charges)
            .OrderByDescending(d => d.CreatedAt)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken);

        var context = await LookupAsync(rows, cancellationToken);
        if (context.IsFailure)
        {
            return Result.Failure<Page<DealSummary>>(context.Error);
        }

        return Result.Success(new Page<DealSummary>(
            rows.Select(d =>
            {
                var unit = context.Value.Unit(d.InventoryUnitId);
                return new DealSummary(
                    d.Id,
                    d.RooftopId,
                    d.Status.ToString(),
                    d.CustomerId,
                    context.Value.CustomerName(d.CustomerId),
                    d.InventoryUnitId,
                    unit is null ? "(not on file)" : unit.StockNumber,
                    unit is null ? "(not on file)" : unit.VehicleDisplayName,
                    d.AmountDue.Amount,
                    d.Currency,
                    d.SalespersonUserId,
                    d.ApprovedByUserId is not null);
            }).ToList(),
            total,
            skip,
            take));
    }

    public async Task<Result<DealDetail>> GetAsync(Guid dealId, CancellationToken cancellationToken)
    {
        var deal = await LoadAsync(dealId, tracked: false, cancellationToken);

        // Unknown and unauthorized answer identically, so a caller cannot probe
        // for deals belonging to a rooftop they may not see.
        if (deal is null)
        {
            return Result.Failure<DealDetail>(DealErrors.Forbidden);
        }

        if (!await _access.IsAuthorizedAsync(_currentUser.Id, ReadPermission, deal.RooftopId, cancellationToken))
        {
            return Result.Failure<DealDetail>(DealErrors.Forbidden);
        }

        return await DescribeAsync(deal, cancellationToken);
    }

    /// <summary>
    /// The order a person reads a deal in: the car, then what was added to it,
    /// then the fees, then what came off.
    /// </summary>
    /// <remarks>
    /// Written out rather than left to the enum's numbering, which is storage
    /// order and happens to put the discount above the documentation fee.
    ///
    /// It exists at all because charges came back in a DIFFERENT order after a
    /// round trip through a records package — the rows carry fresh ids on the
    /// far side and nothing had ever said what order they print in, so the
    /// database's was used. A printed order whose lines rearrange themselves
    /// between two copies of the same deal is not the same document, and the
    /// package's paperwork test found it on 2026-09-21.
    /// </remarks>
    private static int PrintOrder(ChargeKind kind) => kind switch
    {
        ChargeKind.VehiclePrice => 0,
        ChargeKind.Accessory => 1,
        ChargeKind.Fee => 2,
        ChargeKind.DocumentationFee => 3,
        ChargeKind.Discount => 4,
        _ => 5,
    };

    public async Task<Result<ImportOutcome>> ImportAsync(
        ImportedDeal deal,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(deal);

        if (!await _access.IsAuthorizedAsync(_currentUser.Id, WritePermission, deal.RooftopId, cancellationToken))
        {
            return Result.Failure<ImportOutcome>(DealErrors.Forbidden);
        }

        if (await _db.Deals.AsNoTracking().AnyAsync(d => d.Id == deal.Id, cancellationToken))
        {
            return Result.Success(ImportOutcome.AlreadyPresent);
        }

        if (!Enum.TryParse<DealStatus>(deal.Status, ignoreCase: true, out var status))
        {
            return Result.Failure<ImportOutcome>(DealErrors.UnknownStatus);
        }

        // Both references are checked before the write, so a deal whose customer
        // or car did not survive the package is named as that rather than as a
        // constraint violation. Through the contracts, not the tables: Deals may
        // ask ICustomers and IInventory questions and may not touch their rows,
        // which FeatureBoundaryTests enforces — it caught a first draft of this
        // method querying _db.Customers directly.
        if ((await _customers.GetAsync(deal.CustomerId, cancellationToken)).IsFailure)
        {
            return Result.Failure<ImportOutcome>(DealErrors.CustomerNotFound);
        }

        if ((await _inventory.GetAsync(deal.InventoryUnitId, cancellationToken)).IsFailure)
        {
            return Result.Failure<ImportOutcome>(DealErrors.UnitNotAvailable);
        }

        Deal arriving;
        try
        {
            arriving = Deal.Import(
                deal.Id,
                deal.RooftopId,
                deal.CustomerId,
                deal.InventoryUnitId,
                deal.Currency,
                status,
                deal.Charges.Select(c => (
                    Kind: Enum.Parse<ChargeKind>(c.Kind, ignoreCase: true),
                    c.Description,
                    c.Amount)),
                deal.TradeIn is { } trade
                    ? new TradeIn(trade.Description, trade.Allowance, trade.Payoff)
                    : null,
                deal.Products.Select(p => (
                    ProductId: p.FinanceProductId, p.Name, p.Price, p.Cost, p.TermMonths, p.TermMiles)),
                deal.TaxLines.Select(t => (
                    t.Description,
                    t.Jurisdiction,
                    t.Basis,
                    t.Rate,
                    t.Amount,
                    Provenance: Enum.Parse<TaxProvenance>(t.Provenance, ignoreCase: true),
                    t.PackId,
                    t.PackVersion)),
                deal.TaxedAt is { } at
                    ? TaxAddress.Create(at.AdministrativeArea, at.County, at.PostalCode, at.Country)
                    : null,
                _clock.UtcNow,
                _currentUser.Id);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or FormatException)
        {
            // Domain invariants speak in plain sentences, and an unparsed kind or
            // provenance is the same class of problem: the file said something
            // this system has no word for.
            return Result.Failure<ImportOutcome>(Error.Validation("deals.invalid", ex.Message));
        }

        // The check that makes the whole package trustworthy. The source system
        // said what this deal came to; if the lines just written do not reach
        // the same figure, something was lost in the middle and the right answer
        // is to refuse this record by name rather than store a quietly wrong one.
        if (arriving.AmountDue.Amount != deal.AmountDue)
        {
            return Result.Failure<ImportOutcome>(
                DealErrors.TotalDisagrees(deal.AmountDue, arriving.AmountDue.Amount));
        }

        _db.Deals.Add(arriving);

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex)
        {
            _db.ForgetPendingWrites();
            return Result.Failure<ImportOutcome>(DealErrors.CouldNotBeWritten(ex));
        }

        await _audit.RecordAsync(
            new AuditEntry(_currentUser.Id, WritePermission, AuditOutcome.Allowed,
                "Deal", arriving.Id.ToString(), deal.RooftopId.Value,
                "Imported from a package", null, null),
            cancellationToken);

        return Result.Success(ImportOutcome.Created);
    }

    public async Task<Result<DealDetail>> StartAsync(NewDeal deal, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(deal);

        if (!await _access.IsAuthorizedAsync(_currentUser.Id, WritePermission, deal.RooftopId, cancellationToken))
        {
            return Result.Failure<DealDetail>(DealErrors.Forbidden);
        }

        var customer = await _customers.GetAsync(deal.CustomerId, cancellationToken);
        if (customer.IsFailure)
        {
            return Result.Failure<DealDetail>(
                customer.Error.Type == ErrorType.NotFound ? DealErrors.CustomerNotFound : customer.Error);
        }

        var unit = await _inventory.GetAsync(deal.InventoryUnitId, cancellationToken);
        if (unit.IsFailure)
        {
            return Result.Failure<DealDetail>(DealErrors.UnitNotAvailable);
        }

        if (unit.Value.RooftopId != deal.RooftopId)
        {
            return Result.Failure<DealDetail>(DealErrors.UnitAtAnotherRooftop);
        }

        // The car must be on the lot and unspoken for. This is what stops the same
        // car being sold twice — the second deal finds it OnHold and is refused.
        if (!string.Equals(unit.Value.Status, SellableStatus, StringComparison.Ordinal))
        {
            return Result.Failure<DealDetail>(DealErrors.UnitNotAvailable);
        }

        Deal started;
        try
        {
            started = Deal.Start(
                Guid.NewGuid(),
                deal.RooftopId,
                deal.CustomerId,
                deal.InventoryUnitId,
                deal.Currency,
                _clock.UtcNow,
                deal.SalespersonUserId ?? _currentUser.Id,
                deal.LeadId);
        }
        catch (ArgumentException ex)
        {
            return Result.Failure<DealDetail>(Error.Validation("deals.invalid", ex.Message));
        }

        // The deal and the hold on the car must never disagree, so they commit
        // together or not at all.
        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);

        var held = await _inventory.ChangeStatusAsync(
            deal.InventoryUnitId,
            new StatusChangeRequest("OnHold", $"Held for a deal at {DateOnly.FromDateTime(_clock.UtcNow.UtcDateTime):yyyy-MM-dd}."),
            cancellationToken);

        if (held.IsFailure)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result.Failure<DealDetail>(held.Error);
        }

        _db.Deals.Add(started);
        await _db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        await _audit.RecordAsync(
            new AuditEntry(_currentUser.Id, WritePermission, AuditOutcome.Allowed,
                "Deal", started.Id.ToString(), deal.RooftopId.Value, "Started", null, null),
            cancellationToken);

        return await DescribeAsync(started, cancellationToken);
    }

    public async Task<Result<DealDetail>> SetTermsAsync(
        Guid dealId,
        DealTerms terms,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(terms);

        var deal = await LoadAsync(dealId, tracked: true, cancellationToken);
        if (deal is null)
        {
            return Result.Failure<DealDetail>(DealErrors.Forbidden);
        }

        if (!await _access.IsAuthorizedAsync(_currentUser.Id, WritePermission, deal.RooftopId, cancellationToken))
        {
            return Result.Failure<DealDetail>(DealErrors.Forbidden);
        }

        var charges = new List<(ChargeKind, string, decimal)>();
        foreach (var charge in terms.Charges ?? [])
        {
            if (!Enum.TryParse<ChargeKind>(charge.Kind, ignoreCase: true, out var kind))
            {
                return Result.Failure<DealDetail>(DealErrors.UnknownChargeKind);
            }

            charges.Add((kind, charge.Description, charge.Amount));
        }

        try
        {
            TradeIn? trade = null;
            if (terms.TradeIn is { } t)
            {
                trade = TradeIn.Create(t.Description, t.Allowance, t.Payoff);
            }

            deal.SetTerms(charges, trade);
        }
        catch (ArgumentException ex)
        {
            return Result.Failure<DealDetail>(Error.Validation("deals.invalid_terms", ex.Message));
        }
        catch (InvalidOperationException ex)
        {
            // Editing a frozen deal is an ordinary refusal with a readable reason.
            return Result.Failure<DealDetail>(Error.Conflict("deals.terms_frozen", ex.Message));
        }

        await _db.SaveChangesAsync(cancellationToken);
        return await DescribeAsync(deal, cancellationToken);
    }

    public async Task<Result<DealDetail>> SetProductsAsync(
        Guid dealId,
        IReadOnlyList<SoldProduct> products,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(products);

        var deal = await LoadAsync(dealId, tracked: true, cancellationToken);
        if (deal is null)
        {
            return Result.Failure<DealDetail>(DealErrors.Forbidden);
        }

        if (!await _access.IsAuthorizedAsync(_currentUser.Id, WritePermission, deal.RooftopId, cancellationToken))
        {
            return Result.Failure<DealDetail>(DealErrors.Forbidden);
        }

        // The catalogue supplies the NAME only. The price and cost come from the
        // caller, because they are negotiated per deal — reading them from the
        // catalogue here would be the bug this whole design avoids.
        var catalogue = await _products.GetManyAsync(
            products.Select(p => p.FinanceProductId).ToList(), cancellationToken);

        if (catalogue.IsFailure)
        {
            return Result.Failure<DealDetail>(catalogue.Error);
        }

        var sold = new List<(Guid, string, decimal, decimal, int?, int?)>();

        foreach (var product in products)
        {
            var known = catalogue.Value.SingleOrDefault(c => c.Id == product.FinanceProductId);
            if (known is null)
            {
                return Result.Failure<DealDetail>(DealErrors.UnknownProduct);
            }

            // A withdrawn product cannot be added to a NEW deal, but deals that
            // already carry it are untouched — that is what withdrawal means.
            if (!known.IsAvailable && !deal.Products.Any(p => p.FinanceProductId == product.FinanceProductId))
            {
                return Result.Failure<DealDetail>(DealErrors.ProductWithdrawn);
            }

            sold.Add((
                product.FinanceProductId,
                known.Name,
                product.Price,
                product.Cost,
                product.TermMonths ?? known.TermMonths,
                product.TermMiles ?? known.TermMiles));
        }

        try
        {
            deal.SetProducts(sold);
        }
        catch (ArgumentException ex)
        {
            return Result.Failure<DealDetail>(Error.Validation("deals.invalid_products", ex.Message));
        }
        catch (InvalidOperationException ex)
        {
            return Result.Failure<DealDetail>(Error.Conflict("deals.terms_frozen", ex.Message));
        }

        await _db.SaveChangesAsync(cancellationToken);
        return await DescribeAsync(deal, cancellationToken);
    }

    public async Task<Result<DealDetail>> CancelProductAsync(
        Guid dealId,
        Guid dealProductId,
        CancelProduct cancellation,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(cancellation);

        var deal = await LoadAsync(dealId, tracked: true, cancellationToken);
        if (deal is null)
        {
            return Result.Failure<DealDetail>(DealErrors.Forbidden);
        }

        if (!await _access.IsAuthorizedAsync(_currentUser.Id, WritePermission, deal.RooftopId, cancellationToken))
        {
            return Result.Failure<DealDetail>(DealErrors.Forbidden);
        }

        var product = deal.Products.SingleOrDefault(p => p.Id == dealProductId);
        if (product is null)
        {
            return Result.Failure<DealDetail>(DealErrors.UnknownProduct);
        }

        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            deal.CancelProduct(dealProductId, _clock.UtcNow, cancellation.RefundAmount, cancellation.Reason);
        }
        catch (ArgumentException ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result.Failure<DealDetail>(Error.Validation("deals.cancel_product_invalid", ex.Message));
        }
        catch (InvalidOperationException ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result.Failure<DealDetail>(Error.Conflict("deals.cancel_product_not_allowed", ex.Message));
        }

        // Zero is a real answer — a product cancelled inside a non-refundable
        // window — and needs no financial movement at all: nothing is owed back,
        // so nothing posts and no credit is raised.
        if (cancellation.RefundAmount > 0m)
        {
            var posted = await _accounting.PostProductCancellationAsync(
                new ProductCancellationPosting(
                    deal.RooftopId,
                    deal.Id.ToString(),
                    deal.Currency,
                    cancellation.RefundAmount,
                    $"{product.Name} cancelled"),
                cancellationToken);

            if (posted.IsFailure)
            {
                await transaction.RollbackAsync(cancellationToken);
                return Result.Failure<DealDetail>(posted.Error);
            }

            // The same mechanism an overpayment uses: money the dealership now
            // owes back, which the desk can apply to what the customer still
            // owes on this deal or hand back directly — see ReceivableService's
            // ApplyCreditAsync and RefundCreditAsync.
            var credited = await _receivables.RaiseCreditAsync(
                new NewCredit(
                    deal.RooftopId,
                    deal.CustomerId,
                    cancellation.RefundAmount,
                    deal.Currency,
                    deal.Id.ToString(),
                    _clock.UtcNow),
                cancellationToken);

            if (credited.IsFailure)
            {
                await transaction.RollbackAsync(cancellationToken);
                return Result.Failure<DealDetail>(credited.Error);
            }
        }

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result.Failure<DealDetail>(DealErrors.ChangedElsewhere);
        }

        await transaction.CommitAsync(cancellationToken);

        await _audit.RecordAsync(
            new AuditEntry(_currentUser.Id, WritePermission, AuditOutcome.Allowed,
                "Deal", deal.Id.ToString(), deal.RooftopId.Value,
                $"{product.Name} cancelled, {cancellation.RefundAmount} {deal.Currency} credited",
                null, null),
            cancellationToken);

        return await DescribeAsync(deal, cancellationToken);
    }

    public async Task<Result<DealDetail>> SetTaxAsync(
        Guid dealId,
        DealTaxEntry tax,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(tax);

        var deal = await LoadAsync(dealId, tracked: true, cancellationToken);
        if (deal is null)
        {
            return Result.Failure<DealDetail>(DealErrors.Forbidden);
        }

        if (!await _access.IsAuthorizedAsync(_currentUser.Id, WritePermission, deal.RooftopId, cancellationToken))
        {
            return Result.Failure<DealDetail>(DealErrors.Forbidden);
        }

        var lines = new List<(string, string, decimal, decimal, decimal, TaxProvenance, string?, int?)>();

        foreach (var line in tax.Lines)
        {
            // Parsed, never defaulted. A line whose provenance did not parse
            // would otherwise silently become "a person typed it", which is the
            // one answer nobody can challenge.
            if (!Enum.TryParse<TaxProvenance>(line.Provenance, ignoreCase: true, out var provenance))
            {
                return Result.Failure<DealDetail>(DealErrors.UnknownTaxProvenance);
            }

            lines.Add((
                line.Description, line.Jurisdiction, line.Basis, line.Rate,
                line.Amount, provenance, line.PackId, line.PackVersion));
        }

        TaxAddress? taxedAt;

        try
        {
            taxedAt = tax.TaxedAt is null
                ? null
                : TaxAddress.Create(
                    tax.TaxedAt.AdministrativeArea, tax.TaxedAt.County,
                    tax.TaxedAt.PostalCode, tax.TaxedAt.Country);

            deal.SetTax(lines, taxedAt);
        }
        catch (ArgumentException ex)
        {
            return Result.Failure<DealDetail>(Error.Validation("deals.invalid_tax", ex.Message));
        }
        catch (InvalidOperationException ex)
        {
            return Result.Failure<DealDetail>(Error.Conflict("deals.terms_frozen", ex.Message));
        }

        await _db.SaveChangesAsync(cancellationToken);
        return await DescribeAsync(deal, cancellationToken);
    }

    public async Task<Result<DealDetail>> SetRegistrationAddressAsync(
        Guid dealId,
        RegistrationAddressView? address,
        CancellationToken cancellationToken)
    {
        var deal = await LoadAsync(dealId, tracked: true, cancellationToken);
        if (deal is null)
        {
            return Result.Failure<DealDetail>(DealErrors.Forbidden);
        }

        if (!await _access.IsAuthorizedAsync(_currentUser.Id, WritePermission, deal.RooftopId, cancellationToken))
        {
            return Result.Failure<DealDetail>(DealErrors.Forbidden);
        }

        try
        {
            deal.SetRegistrationAddress(address is null
                ? null
                : RegistrationAddress.Create(
                    address.Line1, address.Line2, address.City,
                    address.AdministrativeArea, address.County, address.PostalCode, address.Country));
        }
        catch (ArgumentException ex)
        {
            return Result.Failure<DealDetail>(Error.Validation("deals.invalid_registration_address", ex.Message));
        }
        catch (InvalidOperationException ex)
        {
            return Result.Failure<DealDetail>(Error.Conflict("deals.terms_frozen", ex.Message));
        }

        await _db.SaveChangesAsync(cancellationToken);
        return await DescribeAsync(deal, cancellationToken);
    }

    public async Task<Result<DealDetail>> ChangeStatusAsync(
        Guid dealId,
        DealStatusChangeRequest change,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(change);

        var deal = await LoadAsync(dealId, tracked: true, cancellationToken);
        if (deal is null)
        {
            return Result.Failure<DealDetail>(DealErrors.Forbidden);
        }

        if (!Enum.TryParse(change.Status, ignoreCase: true, out DealStatus next))
        {
            return Result.Failure<DealDetail>(DealErrors.UnknownStatus);
        }

        // Signing a deal off is a different right from building one. A salesperson
        // who could approve their own numbers is not a control at all.
        var required = next == DealStatus.Approved ? ApprovePermission : WritePermission;
        if (!await _access.IsAuthorizedAsync(_currentUser.Id, required, deal.RooftopId, cancellationToken))
        {
            return Result.Failure<DealDetail>(
                next == DealStatus.Approved ? DealErrors.ApprovalForbidden : DealErrors.Forbidden);
        }

        // Holding Deals.Approve is not enough: it must not be your own deal. The
        // entity enforces this too; checking here is what makes the answer a 403
        // with a readable reason rather than a generic conflict.
        if (next == DealStatus.Approved && deal.SalespersonUserId == _currentUser.Id)
        {
            await _audit.RecordAsync(
                AuditEntry.Denied(_currentUser.Id, ApprovePermission, "Deal", deal.Id.ToString(),
                    deal.RooftopId.Value, "Attempted to approve their own deal."),
                cancellationToken);

            return Result.Failure<DealDetail>(DealErrors.CannotApproveOwnDeal);
        }

        var from = deal.Status;

        // Read the cost before the car moves — a delivered unit still has to
        // report what it cost, and the ledger needs it to show gross profit.
        //
        // BOOK VALUE, NOT ACQUISITION COST. Until 2026-09-19 this read
        // CostAmount, so a car that had been through the workshop was relieved
        // from 1300 for less than went in: used-vehicle gross was overstated by
        // exactly the reconditioning spend — the failure the posting comment in
        // AccountingService says it exists to prevent — and the recon stayed in
        // vehicle inventory after the car had gone. BookValueAmount is
        // acquisition plus everything capitalised onto this stay in stock.
        decimal vehicleCost = 0m;
        if (next == DealStatus.Delivered)
        {
            var unit = await _inventory.GetAsync(deal.InventoryUnitId, cancellationToken);
            if (unit.IsFailure)
            {
                return Result.Failure<DealDetail>(unit.Error);
            }

            // Null when nobody recorded a purchase price. Relieving nothing is
            // what happened before and stays right: inventing a cost at the
            // moment of sale would put a made-up gross on the books.
            vehicleCost = unit.Value.BookValueAmount ?? 0m;
        }

        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            deal.ChangeStatus(next, _clock.UtcNow, _currentUser.Id, change.Note);
        }
        catch (InvalidOperationException ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result.Failure<DealDetail>(Error.Conflict("deals.status_not_allowed", ex.Message));
        }

        // The car follows the deal: delivered means sold, cancelled means it goes
        // back on the lot for somebody else.
        var carMove = next switch
        {
            DealStatus.Delivered => "Sold",
            DealStatus.Cancelled => "Available",
            _ => null,
        };

        if (carMove is not null)
        {
            var moved = await _inventory.ChangeStatusAsync(
                deal.InventoryUnitId,
                new StatusChangeRequest(carMove, $"Deal {next.ToString().ToLowerInvariant()}."),
                cancellationToken);

            if (moved.IsFailure)
            {
                await transaction.RollbackAsync(cancellationToken);
                return Result.Failure<DealDetail>(moved.Error);
            }
        }

        // A car leaving the lot is an accounting event. It posts inside the same
        // transaction as the delivery, so the ledger and the deal can never
        // disagree about whether the sale happened.
        if (next == DealStatus.Delivered)
        {
            var posted = await _accounting.PostDeliveryAsync(
                BuildPosting(deal, vehicleCost), cancellationToken);

            if (posted.IsFailure)
            {
                await transaction.RollbackAsync(cancellationToken);
                return Result.Failure<DealDetail>(posted.Error);
            }

            // The ledger now says somebody owes this; the sub-ledger says WHO.
            // Opened in the same transaction, because a debit to 1100 that nobody
            // is recorded as owing is a figure with no way to chase it.
            var owed = await _receivables.OpenAsync(
                new NewReceivable(
                    deal.RooftopId,
                    deal.CustomerId,
                    ReceivableSource.Deal,
                    deal.Id.ToString(),
                    deal.AmountDue.Amount,
                    deal.AmountDue.Currency,
                    _clock.UtcNow),
                cancellationToken);

            if (owed.IsFailure)
            {
                await transaction.RollbackAsync(cancellationToken);
                return Result.Failure<DealDetail>(owed.Error);
            }
        }

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result.Failure<DealDetail>(DealErrors.ChangedElsewhere);
        }

        await transaction.CommitAsync(cancellationToken);

        await _audit.RecordAsync(
            new AuditEntry(_currentUser.Id, required, AuditOutcome.Allowed,
                "Deal", deal.Id.ToString(), deal.RooftopId.Value, $"{from} to {next}", null, null),
            cancellationToken);

        return await DescribeAsync(deal, cancellationToken);
    }

    /// <summary>
    /// Restates the deal in the terms the ledger needs, so Accounting never has
    /// to know what a charge kind is and Deals never has to know what an account
    /// is.
    /// </summary>
    private static DeliveryPosting BuildPosting(Deal deal, decimal vehicleCost) =>
        new(
            deal.RooftopId,
            deal.Id.ToString(),
            deal.Currency,
            VehiclePrice: deal.Charges.Where(c => c.Kind == ChargeKind.VehiclePrice).Sum(c => c.Amount),
            // DocumentationFee is listed explicitly and must stay listed. It
            // became its own ChargeKind on 2026-09-09 so the taxable basis could
            // treat it differently from a registration fee — and this mapping was
            // not updated, so the fee sat in AmountDue with nothing credited
            // against it and every delivery carrying one was refused for not
            // balancing, out by exactly the fee. A new ChargeKind has to be
            // answered here as well as in TaxableBasis.
            Fees: deal.Charges
                .Where(c => c.Kind is ChargeKind.Fee
                    or ChargeKind.Accessory
                    or ChargeKind.DocumentationFee)
                .Sum(c => c.Amount),
            Discount: deal.Charges.Where(c => c.Kind == ChargeKind.Discount).Sum(c => c.Amount),
            TradeAllowance: deal.Trade?.Allowance ?? 0m,
            TradePayoff: deal.Trade?.Payoff ?? 0m,
            AmountDue: deal.AmountDue.Amount,
            VehicleCost: vehicleCost,
            // F&I is its own revenue and its own cost, kept apart from the car's
            // so a dealer principal can read the two as the separate businesses
            // they are. Zero when nothing was sold with the car.
            ProductRevenue: deal.ProductRevenue.Amount,
            ProductCost: deal.ProductCost.Amount,
            TaxCollected: deal.TaxTotal.Amount,
            Memo: $"Delivered deal {deal.Id}");

    private async Task<Deal?> LoadAsync(Guid dealId, bool tracked, CancellationToken cancellationToken)
    {
        var query = _db.Deals.Include(d => d.Charges).AsQueryable();
        if (!tracked)
        {
            query = query.AsNoTracking();
        }

        return await query.SingleOrDefaultAsync(d => d.Id == dealId, cancellationToken);
    }

    /// <summary>
    /// The customer names and stock numbers for a page of deals, in two queries
    /// rather than two per row, and always through the published contracts.
    /// </summary>
    private async Task<Result<DealLookup>> LookupAsync(
        List<Deal> deals,
        CancellationToken cancellationToken)
    {
        if (deals.Count == 0)
        {
            return Result.Success(new DealLookup([], []));
        }

        var names = await _customers.GetManyAsync(
            deals.Select(d => d.CustomerId).Distinct().ToList(), cancellationToken);

        if (names.IsFailure)
        {
            return Result.Failure<DealLookup>(names.Error);
        }

        var units = await _inventory.GetManyAsync(
            deals.Select(d => d.InventoryUnitId).Distinct().ToList(), cancellationToken);

        if (units.IsFailure)
        {
            return Result.Failure<DealLookup>(units.Error);
        }

        return Result.Success(new DealLookup(names.Value, units.Value));
    }

    private sealed record DealLookup(
        IReadOnlyList<CustomerSummary> Customers,
        IReadOnlyList<InventoryUnitSummary> Units)
    {
        public string CustomerName(Guid id)
        {
            var match = Customers.FirstOrDefault(c => c.Id == id);
            return match is null ? "(customer no longer on file)" : match.DisplayName;
        }

        public InventoryUnitSummary? Unit(Guid id) => Units.FirstOrDefault(u => u.Id == id);
    }

    private async Task<Result<DealDetail>> DescribeAsync(Deal deal, CancellationToken cancellationToken)
    {
        var context = await LookupAsync([deal], cancellationToken);
        if (context.IsFailure)
        {
            return Result.Failure<DealDetail>(context.Error);
        }


        var unit = context.Value.Unit(deal.InventoryUnitId);

        var history = await _db.DealHistory
            .AsNoTracking()
            .Where(h => h.DealId == deal.Id)
            .OrderBy(h => h.OccurredAt)
            .ThenBy(h => h.Sequence)
            .ToListAsync(cancellationToken);

        // A deal that has only just been started has its first history row in
        // memory rather than in a separate query's results.
        if (history.Count == 0)
        {
            history = deal.History.ToList();
        }

        // Provider names for whatever this deal sold, in one query. Absent for a
        // product since removed from the catalogue, which is why the sale carries
        // its own copy of the name.
        var providers = new Dictionary<Guid, string>();
        if (deal.Products.Count > 0)
        {
            var catalogue = await _products.GetManyAsync(
                deal.Products.Select(p => p.FinanceProductId).ToList(), cancellationToken);

            if (catalogue.IsSuccess)
            {
                providers = catalogue.Value.ToDictionary(c => c.Id, c => c.Provider);
            }
        }

        return Result.Success(new DealDetail(
            deal.Id,
            deal.RooftopId,
            deal.Status.ToString(),
            deal.CustomerId,
            context.Value.CustomerName(deal.CustomerId),
            deal.InventoryUnitId,
            unit is null ? "(not on file)" : unit.StockNumber,
            unit is null ? "(not on file)" : unit.VehicleDisplayName,
            deal.LeadId,
            deal.Currency,
            deal.Subtotal.Amount,
            deal.AmountDue.Amount,
            deal.Trade is null
                ? null
                : new TradeInView(
                    deal.Trade.Description, deal.Trade.Allowance, deal.Trade.Payoff,
                    deal.Trade.Equity, deal.Trade.IsNegativeEquity),
            deal.Charges
                .OrderBy(c => PrintOrder(c.Kind))
                .ThenBy(c => c.Description, StringComparer.Ordinal)
                .Select(c => new ChargeView(c.Kind.ToString(), c.Description, c.Amount))
                .ToList(),
            deal.Products
                .OrderBy(p => p.Name, StringComparer.Ordinal)
                .Select(p => new DealProductView(
                    p.Id,
                    p.FinanceProductId,
                    p.Name,
                    // The provider comes from the catalogue and may be absent if
                    // the entry was withdrawn. The NAME never is — it was copied
                    // onto the sale.
                    providers.TryGetValue(p.FinanceProductId, out var provider) ? provider : null,
                    p.Price,
                    p.Cost,
                    p.Gross,
                    p.TermMonths,
                    p.TermMiles,
                    p.IsCancelled,
                    p.CancelledAt,
                    p.RefundAmount,
                    p.CancellationReason))
                .ToList(),
            deal.ProductGross.Amount,
            deal.SalespersonUserId,
            deal.ApprovedByUserId,
            deal.ApprovedAt,
            deal.TermsAreOpen,
            deal.TaxLines
                .OrderBy(t => t.Jurisdiction, StringComparer.Ordinal)
                .ThenBy(t => t.Description, StringComparer.Ordinal)
                .Select(t => new TaxLineView(
                    t.Id, t.Description, t.Jurisdiction, t.Basis, t.Rate, t.Amount,
                    t.Provenance.ToString(), t.PackId, t.PackVersion))
                .ToList(),
            deal.TaxTotal.Amount,
            deal.TaxedAt is null
                ? null
                : new TaxAddressView(
                    deal.TaxedAt.AdministrativeArea, deal.TaxedAt.County,
                    deal.TaxedAt.PostalCode, deal.TaxedAt.Country),
            deal.RegistrationAddress is null
                ? null
                : new RegistrationAddressView(
                    deal.RegistrationAddress.Line1, deal.RegistrationAddress.Line2, deal.RegistrationAddress.City,
                    deal.RegistrationAddress.AdministrativeArea, deal.RegistrationAddress.County,
                    deal.RegistrationAddress.PostalCode, deal.RegistrationAddress.Country),
            history
                .OrderBy(h => h.OccurredAt)
                .ThenBy(h => h.Sequence)
                .Select(h => new DealHistoryEntry(
                    h.FromStatus?.ToString(), h.ToStatus.ToString(), h.OccurredAt,
                    h.ChangedByUserId, h.Note, h.AmountAtChange))
                .ToList()));
    }
}

/// <summary>Stable error codes for the Deals capability (doc 06 §6).</summary>
internal static class DealErrors
{
    public static Error Forbidden { get; } = Error.Forbidden(
        "deals.forbidden",
        "You do not have access to this rooftop's deals.");

    public static Error ApprovalForbidden { get; } = Error.Forbidden(
        "deals.approval_forbidden",
        "Approving a deal needs a manager. Ask somebody who holds Deals.Approve.");

    public static Error CannotApproveOwnDeal { get; } = Error.Forbidden(
        "deals.cannot_approve_own_deal",
        "You cannot approve your own deal. A sales manager approves it.");

    public static Error UnknownStatus { get; } = Error.Validation(
        "deals.unknown_status",
        "That is not a deal status.");

    public static Error UnknownTaxProvenance { get; } = Error.Validation(
        "deals.unknown_tax_provenance",
        "A tax line must say where its figure came from: EnteredByPerson, Pack, or Vendor.");

    public static Error UnknownChargeKind { get; } = Error.Validation(
        "deals.unknown_charge_kind",
        "That is not a kind of charge.");

    public static Error UnknownProduct { get; } = Error.NotFound(
        "deals.unknown_product",
        "That is not a product in the catalogue.");

    public static Error ProductWithdrawn { get; } = Error.Conflict(
        "deals.product_withdrawn",
        "That product is no longer offered. Deals that already carry it keep it; it cannot be "
            + "added to a new one.");

    public static Error CustomerNotFound { get; } = Error.NotFound(
        "deals.customer_not_found",
        "Record the customer before starting their deal.");

    public static Error UnitNotAvailable { get; } = Error.Conflict(
        "deals.unit_not_available",
        "That car is not available. It may already be on another deal.");

    /// <summary>
    /// An arriving deal does not come to what the system it left said it came
    /// to. Both figures are named, because "the totals disagree" without them
    /// sends somebody diffing two files by hand.
    /// </summary>
    /// <summary>
    /// The database refused an arriving deal for a reason nothing checked for.
    /// One record's problem is one line in an import report rather than a failed
    /// request.
    /// </summary>
    public static Error CouldNotBeWritten(Exception cause) => Error.Conflict(
        "deals.could_not_be_written",
        $"That deal could not be written: {cause?.InnerException?.Message ?? cause?.Message}");

    public static Error TotalDisagrees(decimal claimed, decimal computed) => Error.Validation(
        "deals.total_disagrees",
        $"This deal says it comes to {claimed} and its own lines come to {computed}. "
        + "Something was lost between the two systems, so it has not been written.");

    public static Error UnitAtAnotherRooftop { get; } = Error.Conflict(
        "deals.unit_at_another_rooftop",
        "That car is on another location's lot. Transfer it first.");

    public static Error ChangedElsewhere { get; } = Error.Conflict(
        "deals.changed_elsewhere",
        "Somebody else changed this deal. Reload it and try again.");
}
