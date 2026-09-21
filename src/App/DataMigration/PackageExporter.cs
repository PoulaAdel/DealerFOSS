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
    IRepairOrders repairOrders)
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
    /// </summary>
    private const int MaxPerKind = 20_000;

    private readonly IOrganization _organization = organization;
    private readonly ICustomers _customers = customers;
    private readonly IVehicles _vehicles = vehicles;
    private readonly IInventory _inventory = inventory;
    private readonly IDeals _deals = deals;
    private readonly IRepairOrders _repairOrders = repairOrders;

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

        for (var offset = 0; offset < MaxPerKind; offset += PageSize)
        {
            var page = await _inventory.ListAsync(
                new InventoryQuery(RooftopId: rooftopId, Limit: PageSize, Offset: offset),
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

            if (packaged.Count >= page.Value.Total)
            {
                break;
            }
        }

        return Result.Success(packaged);
    }

    private async Task<Result<List<PackagedDeal>>> DealsAsync(
        RooftopId rooftopId,
        CancellationToken cancellationToken)
    {
        var packaged = new List<PackagedDeal>();

        for (var offset = 0; offset < MaxPerKind; offset += PageSize)
        {
            var page = await _deals.ListAsync(
                new DealQuery(RooftopId: rooftopId, Limit: PageSize, Offset: offset),
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
                        .Select(p => new PackagedProduct(
                            p.FinanceProductId, p.Name, p.Provider, p.Price, p.Cost, p.TermMonths, p.TermMiles))],
                    [.. d.TaxLines.Select(t => new PackagedTaxLine(
                        t.Description, t.Jurisdiction, t.Basis, t.Rate, t.Amount, t.Provenance))],
                    d.TaxedAt is { } at
                        ? new PackagedTaxAddress(at.AdministrativeArea, at.County, at.PostalCode, at.Country)
                        : null,
                    d.TradeIn is { } trade
                        ? new PackagedTradeIn(trade.Description, trade.Allowance, trade.Payoff)
                        : null,
                    d.AmountDue));
            }

            if (packaged.Count >= page.Value.Total)
            {
                break;
            }
        }

        return Result.Success(packaged);
    }

    private async Task<Result<List<PackagedRepairOrder>>> JobsAsync(
        RooftopId rooftopId,
        CancellationToken cancellationToken)
    {
        var packaged = new List<PackagedRepairOrder>();

        for (var offset = 0; offset < MaxPerKind; offset += PageSize)
        {
            var page = await _repairOrders.ListAsync(
                new RepairOrderQuery(RooftopId: rooftopId, Limit: PageSize, Offset: offset),
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

            if (packaged.Count >= page.Value.Total)
            {
                break;
            }
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
