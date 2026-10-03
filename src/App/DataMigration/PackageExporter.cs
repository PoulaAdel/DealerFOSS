// Copyright (c) 2026 The DealerFOSS contributors.
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Overview: Purpose, File Design, and Engineering
//   PackageExporter — walks one rooftop and builds a self-contained package of
//   its records.
//
// Usage:
//   Called by MigrationService.ExportPackageAsync. Nothing else.
//
// Coding Instructions:
//   EVERYTHING IS READ THROUGH A CAPABILITY CONTRACT, never through TenantDb.
//   The CSV exporter set that precedent and it is load-bearing rather than
//   tidy: reading through IDeals means the caller's rooftop scope and read
//   permission have already been applied, so an export cannot become a way to
//   see a lot you are not assigned to. A query written here against the tables
//   would answer with everything.
//
//   The reads are deliberately N+1 — a list, then a detail for each row. An
//   export is not a hot path and it runs once when a dealership leaves or
//   arrives; the alternative is five more PageForExportAsync methods on five
//   contracts, each a new way for the scope check to be forgotten. If a
//   dealership large enough to notice ever appears, add keyset paging to the
//   list calls rather than a second read path.
//
//   A SHORT PACKAGE IS A REFUSAL, NEVER A SUCCESS. Each paging loop below is
//   bounded by MaxPerKind and also reads the rooftop's true count on every
//   page, so "there were more than I took" is always knowable here. It was not
//   checked until 2026-10-03, and the cost of that was the worst kind of bug
//   this file could have: a dealership exercising its right to leave with its
//   records, receiving a file quietly missing some of them, and finding out
//   somewhere else. A round trip cannot catch it either — the existing one
//   compares the package against itself, so a truncated package round-trips
//   perfectly. The test that guards this compares against the SOURCE count.
//
//   THE PACKAGE IS CLOSED OVER ITS REFERENCES. Units, deals and jobs are
//   collected first; the customers and vehicles they point at are fetched
//   afterwards, by id, from the set that was actually referenced. So an
//   exported package cannot contain a dangling reference, which is what lets
//   the importer treat one as corruption rather than as a normal case.

using DealerFOSS.Core;
using DealerFOSS.Customers;
using DealerFOSS.Deals;
using DealerFOSS.Inventory;
using DealerFOSS.Organization;
using DealerFOSS.RepairOrders;
using DealerFOSS.Vehicles;

namespace DealerFOSS.DataMigration;

internal sealed class PackageExporter(
    IOrganization organization,
    ICustomers customers,
    IVehicles vehicles,
    IInventory inventory,
    IDeals deals,
    IRepairOrders repairOrders,
    int maxPerKind = PackageExporter.MaxPerKind)
{
    /// <summary>
    /// One list request's worth. Large enough that a real lot comes back in one
    /// or two passes, small enough that a runaway loop is visible.
    /// </summary>
    private const int PageSize = 200;

    /// <summary>
    /// The most of any one kind a package carries. A lot with more records than
    /// this is not a migration problem, it is a backup problem, and backup is
    /// deploy/backup.ps1 rather than this.
    ///
    /// Exceeding it is REFUSED, not truncated. Until 2026-10-03 each loop below
    /// simply stopped at this bound and returned <c>Result.Success</c> with a
    /// short list, so a rooftop holding 25,000 repair orders exported 20,000 of
    /// them and the package said nothing about the other 5,000.
    ///
    /// It is the default of a constructor parameter rather than used directly,
    /// so a test can set a ceiling of two and prove the refusal with three
    /// records instead of twenty thousand and one. A guard nobody can afford to
    /// exercise is a guard nobody knows works — which is how this one shipped
    /// broken. Production passes nothing and gets this number.
    /// </summary>
    internal const int MaxPerKind = 20_000;

    private readonly IOrganization _organization = organization;
    private readonly ICustomers _customers = customers;
    private readonly IVehicles _vehicles = vehicles;
    private readonly IInventory _inventory = inventory;
    private readonly IDeals _deals = deals;
    private readonly IRepairOrders _repairOrders = repairOrders;
    private readonly int _maxPerKind = maxPerKind;

    public async Task<Result<RecordPackage>> BuildAsync(
        RooftopId rooftopId,
        DateTimeOffset producedAt,
        CancellationToken cancellationToken)
    {
        // Read the rooftop first. It is the cheapest call that fails for a
        // caller who may not see this lot, so an unauthorized export stops
        // before it has walked a single record.
        var rooftop = await _organization.GetRooftopAsync(rooftopId, cancellationToken);
        if (rooftop.IsFailure)
        {
            return Result.Failure<RecordPackage>(rooftop.Error);
        }

        var structure = await _organization.GetStructureAsync(cancellationToken);

        var units = await UnitsAsync(rooftopId, cancellationToken);
        if (units.IsFailure)
        {
            return Result.Failure<RecordPackage>(units.Error);
        }

        var sales = await DealsAsync(rooftopId, cancellationToken);
        if (sales.IsFailure)
        {
            return Result.Failure<RecordPackage>(sales.Error);
        }

        var jobs = await JobsAsync(rooftopId, cancellationToken);
        if (jobs.IsFailure)
        {
            return Result.Failure<RecordPackage>(jobs.Error);
        }

        var customerIds = sales.Value.Select(d => d.CustomerId)
            .Concat(jobs.Value.Select(j => j.CustomerId))
            .Distinct()
            .ToList();

        var vehicleIds = units.Value.Select(u => u.VehicleId)
            .Concat(jobs.Value.Select(j => j.VehicleId))
            .Distinct()
            .ToList();

        var people = await PeopleAsync(customerIds, cancellationToken);
        if (people.IsFailure)
        {
            return Result.Failure<RecordPackage>(people.Error);
        }

        var cars = await CarsAsync(vehicleIds, cancellationToken);
        if (cars.IsFailure)
        {
            return Result.Failure<RecordPackage>(cars.Error);
        }

        return Result.Success(new RecordPackage(
            RecordPackage.Marker,
            RecordPackage.Current,
            producedAt,
            new PackageSource(
                structure.IsSuccess ? structure.Value.Name : string.Empty,
                rooftopId.Value,
                rooftop.Value.Name,
                rooftop.Value.Code),
            people.Value,
            cars.Value,
            units.Value,
            sales.Value,
            jobs.Value));
    }

    private async Task<Result<List<PackagedUnit>>> UnitsAsync(
        RooftopId rooftopId,
        CancellationToken cancellationToken)
    {
        var packaged = new List<PackagedUnit>();
        var held = 0;

        var offset = 0;

        while (offset < _maxPerKind)
        {
            // Clamped, so the ceiling is the number it says rather than that
            // number rounded up to a page. It also lets a test set a ceiling of
            // two and reach it with three records.
            var limit = Math.Min(PageSize, _maxPerKind - offset);

            var page = await _inventory.ListAsync(
                new InventoryQuery(RooftopId: rooftopId, Limit: limit, Offset: offset),
                cancellationToken);

            if (page.IsFailure)
            {
                return Result.Failure<List<PackagedUnit>>(page.Error);
            }

            if (page.Value.Rows.Count == 0)
            {
                break;
            }

            foreach (var row in page.Value.Rows)
            {
                var unit = await _inventory.GetAsync(row.Id, cancellationToken);
                if (unit.IsFailure)
                {
                    return Result.Failure<List<PackagedUnit>>(unit.Error);
                }

                packaged.Add(new PackagedUnit(
                    unit.Value.Id,
                    unit.Value.VehicleId,
                    unit.Value.StockNumber,
                    unit.Value.Status,
                    unit.Value.CostAmount,
                    unit.Value.CostCurrency,
                    unit.Value.AcquiredOn));
            }

            held = page.Value.Total;
            offset += limit;

            if (packaged.Count >= held)
            {
                break;
            }
        }

        // The loop can also end by exhausting the ceiling, and that exit used to
        // return Success with a short list. See MigrationErrors.TooManyToPackage.
        if (packaged.Count < held)
        {
            return Result.Failure<List<PackagedUnit>>(
                MigrationErrors.TooManyToPackage("vehicles in stock", held, _maxPerKind));
        }

        return Result.Success(packaged);
    }

    private async Task<Result<List<PackagedDeal>>> DealsAsync(
        RooftopId rooftopId,
        CancellationToken cancellationToken)
    {
        var packaged = new List<PackagedDeal>();
        var held = 0;

        var offset = 0;

        while (offset < _maxPerKind)
        {
            var limit = Math.Min(PageSize, _maxPerKind - offset);

            var page = await _deals.ListAsync(
                new DealQuery(RooftopId: rooftopId, Limit: limit, Offset: offset),
                cancellationToken);

            if (page.IsFailure)
            {
                return Result.Failure<List<PackagedDeal>>(page.Error);
            }

            if (page.Value.Rows.Count == 0)
            {
                break;
            }

            foreach (var row in page.Value.Rows)
            {
                var deal = await _deals.GetAsync(row.Id, cancellationToken);
                if (deal.IsFailure)
                {
                    return Result.Failure<List<PackagedDeal>>(deal.Error);
                }

                var d = deal.Value;

                packaged.Add(new PackagedDeal(
                    d.Id,
                    d.CustomerId,
                    d.InventoryUnitId,
                    d.Currency,
                    d.Status,
                    [.. d.Charges.Select(c => new PackagedCharge(c.Kind, c.Description, c.Amount))],

                    // A cancelled product is not carried. It was sold and then
                    // undone; what it left behind is a credit and a ledger entry,
                    // and this package carries neither. Bringing the sale across
                    // without its cancellation would re-sell it.
                    [.. d.Products
                        .Where(p => !p.IsCancelled)
                        // Cost is non-null by the time it reaches here:
                        // ExportPackageAsync refuses a caller who may not see it,
                        // precisely so this file never carries a blank where a
                        // figure belongs. Asserted rather than defaulted — a zero
                        // written here would arrive at the far side looking like a
                        // free product, and nothing downstream could tell.
                        .Select(p => new PackagedProduct(
                            p.FinanceProductId, p.Name, p.Provider, p.Price, p.Cost!.Value,
                            p.TermMonths, p.TermMiles))],
                    [.. d.TaxLines.Select(t => new PackagedTaxLine(
                        t.Description, t.Jurisdiction, t.Basis, t.Rate, t.Amount, t.Provenance))],
                    d.TaxedAt is { } at
                        ? new PackagedTaxAddress(at.AdministrativeArea, at.County, at.PostalCode, at.Country)
                        : null,
                    d.TradeIn is { } trade
                        ? new PackagedTradeIn(trade.Description, trade.Allowance, trade.Payoff)
                        : null,

                    // The four agreed figures only. The amount financed and the
                    // payments on the view beside them are derived, and the
                    // receiving installation works them out from these.
                    d.Financing is { } financing
                        ? new PackagedFinancing(
                            financing.Lender,
                            financing.DownPayment,
                            financing.AnnualPercentageRate,
                            financing.TermMonths)
                        : null,
                    d.AmountDue));
            }

            held = page.Value.Total;
            offset += limit;

            if (packaged.Count >= held)
            {
                break;
            }
        }

        if (packaged.Count < held)
        {
            return Result.Failure<List<PackagedDeal>>(
                MigrationErrors.TooManyToPackage("deals", held, _maxPerKind));
        }

        return Result.Success(packaged);
    }

    private async Task<Result<List<PackagedRepairOrder>>> JobsAsync(
        RooftopId rooftopId,
        CancellationToken cancellationToken)
    {
        var packaged = new List<PackagedRepairOrder>();
        var held = 0;

        var offset = 0;

        while (offset < _maxPerKind)
        {
            var limit = Math.Min(PageSize, _maxPerKind - offset);

            var page = await _repairOrders.ListAsync(
                new RepairOrderQuery(RooftopId: rooftopId, Limit: limit, Offset: offset),
                cancellationToken);

            if (page.IsFailure)
            {
                return Result.Failure<List<PackagedRepairOrder>>(page.Error);
            }

            if (page.Value.Rows.Count == 0)
            {
                break;
            }

            foreach (var row in page.Value.Rows)
            {
                var job = await _repairOrders.GetAsync(row.Id, cancellationToken);
                if (job.IsFailure)
                {
                    return Result.Failure<List<PackagedRepairOrder>>(job.Error);
                }

                var j = job.Value;

                packaged.Add(new PackagedRepairOrder(
                    j.Id,
                    j.CustomerId,
                    j.VehicleId,
                    j.Number,
                    j.Complaint,
                    j.Status,
                    j.Currency,
                    j.OdometerReading,
                    j.OpenedAt,
                    j.InvoicedAt,
                    [.. j.Lines.Select(l => new PackagedServiceLine(
                        l.Id, l.Kind, l.Description, l.Hours, l.Rate, l.Amount, l.PayType, l.Authorization))],
                    j.AmountDue));
            }

            held = page.Value.Total;
            offset += limit;

            if (packaged.Count >= held)
            {
                break;
            }
        }

        // Repair orders reach the ceiling first in practice: a busy shop writes
        // five to ten thousand a year, so twenty thousand arrives in two to four
        // years while customers are still well under it.
        if (packaged.Count < held)
        {
            return Result.Failure<List<PackagedRepairOrder>>(
                MigrationErrors.TooManyToPackage("repair orders", held, _maxPerKind));
        }

        return Result.Success(packaged);
    }

    private async Task<Result<List<PackagedCustomer>>> PeopleAsync(
        IReadOnlyList<Guid> ids,
        CancellationToken cancellationToken)
    {
        var packaged = new List<PackagedCustomer>();

        foreach (var id in ids)
        {
            var customer = await _customers.GetAsync(id, cancellationToken);
            if (customer.IsFailure)
            {
                return Result.Failure<List<PackagedCustomer>>(customer.Error);
            }

            var c = customer.Value;

            packaged.Add(new PackagedCustomer(
                c.Id,
                c.Kind,
                c.FirstName,
                c.LastName,
                c.ExternalReference,
                c.CreditLimit,
                [.. c.ContactPoints.Select(p => new PackagedContact(p.Kind, p.Value, p.IsPrimary))],
                c.Address is { } a
                    ? new PackagedAddress(
                        a.Line1, a.Line2, a.City, a.AdministrativeArea, a.County, a.PostalCode, a.Country)
                    : null));
        }

        return Result.Success(packaged);
    }

    private async Task<Result<List<PackagedVehicle>>> CarsAsync(
        IReadOnlyList<Guid> ids,
        CancellationToken cancellationToken)
    {
        var packaged = new List<PackagedVehicle>();

        foreach (var id in ids)
        {
            var vehicle = await _vehicles.GetAsync(id, cancellationToken);
            if (vehicle.IsFailure)
            {
                return Result.Failure<List<PackagedVehicle>>(vehicle.Error);
            }

            var v = vehicle.Value;

            packaged.Add(new PackagedVehicle(
                v.Id, v.Vin, v.ModelYear, v.Make, v.Model, v.Trim, v.BodyStyle, v.ExteriorColor,
                v.VinExceptionReason));
        }

        return Result.Success(packaged);
    }
}
