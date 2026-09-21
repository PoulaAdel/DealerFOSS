// Copyright (c) 2026 The DealerFOSS contributors.
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Overview: Purpose, File Design, and Engineering
//   PackageImporter — applies a package to this installation, in the order the
//   references require, and says what it would not do.
//
// Usage:
//   Called by MigrationService.ImportPackageAsync. Nothing else.
//
// Coding Instructions:
//   THE ORDER IS THE ALGORITHM. Customers and vehicles first because nothing
//   points at anything else; then units, which name a vehicle; then deals,
//   which name a customer and a unit; then jobs, which name a customer and a
//   vehicle. The package's own array order is ignored — a file hand-edited into
//   a different order still imports, because the order is fixed here rather
//   than trusted from outside. This is the reordered-delivery lesson from the
//   connector runtime, applied to files instead of batches.
//
//   ONE REFUSAL DOES NOT STOP THE OTHERS, and a refusal is an answer rather
//   than a failure. A package with one bad record should land the rest and name
//   the one, exactly as the CSV importer names a bad row by its line number.
//   What it must never do is land a deal whose customer was refused: the
//   capability checks its own references, so that is structural rather than
//   something remembered here.
//
//   NOTHING IS ROLLED BACK. There is no transaction around the whole package,
//   on purpose. A half-applied package is safe to run again — every id that
//   landed answers AlreadyPresent the second time — whereas a package that has
//   to succeed entirely or not at all cannot be retried at all once it is big
//   enough to time out. Retry is the recovery mechanism, so retry has to be the
//   cheap thing.

using DealerFOSS.Core;
using DealerFOSS.Customers;
using DealerFOSS.Deals;
using DealerFOSS.Inventory;
using DealerFOSS.RepairOrders;
using DealerFOSS.Vehicles;

namespace DealerFOSS.DataMigration;

internal sealed class PackageImporter(
    ICustomers customers,
    IVehicles vehicles,
    IInventory inventory,
    IDeals deals,
    IRepairOrders repairOrders)
{
    private readonly ICustomers _customers = customers;
    private readonly IVehicles _vehicles = vehicles;
    private readonly IInventory _inventory = inventory;
    private readonly IDeals _deals = deals;
    private readonly IRepairOrders _repairOrders = repairOrders;

    public async Task<Result<PackageImportReport>> ApplyAsync(
        RecordPackage package,
        RooftopId into,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(package);

        if (!string.Equals(package.Format, RecordPackage.Marker, StringComparison.Ordinal))
        {
            return Result.Failure<PackageImportReport>(MigrationErrors.NotAPackage);
        }

        // A newer package may contain shapes this build cannot read correctly,
        // and reading it anyway would be the worst outcome — a silent partial
        // import. An OLDER one is fine and is the normal case after an upgrade.
        if (package.Version > RecordPackage.Current)
        {
            return Result.Failure<PackageImportReport>(
                MigrationErrors.PackageIsNewer(package.Version, RecordPackage.Current));
        }

        var tally = new Tally();

        foreach (var customer in package.Customers)
        {
            await tally.RecordAsync(
                "Customers",
                customer.Id,
                () => _customers.ImportAsync(
                    new ImportedCustomer(
                        customer.Id,
                        customer.Kind,
                        customer.FirstName,
                        customer.LastName,
                        customer.ExternalReference,
                        customer.CreditLimit,
                        [.. customer.ContactPoints.Select(p => new ImportedContact(p.Kind, p.Value, p.IsPrimary))],
                        customer.Address is { } a
                            ? new AddressView(
                                a.Line1 ?? string.Empty, a.Line2, a.City ?? string.Empty,
                                a.AdministrativeArea, a.County, a.PostalCode, a.Country)
                            : null),
                    cancellationToken));
        }

        foreach (var vehicle in package.Vehicles)
        {
            await tally.RecordAsync(
                "Vehicles",
                vehicle.Id,
                () => _vehicles.ImportAsync(
                    new ImportedVehicle(
                        vehicle.Id, vehicle.Vin, vehicle.ModelYear, vehicle.Make, vehicle.Model,
                        vehicle.Trim, vehicle.BodyStyle, vehicle.ExteriorColor, vehicle.VinExceptionReason),
                    cancellationToken));
        }

        foreach (var unit in package.InventoryUnits)
        {
            await tally.RecordAsync(
                "InventoryUnits",
                unit.Id,
                () => _inventory.ImportAsync(
                    new ImportedInventoryUnit(
                        unit.Id, unit.VehicleId, into, unit.StockNumber, unit.Status,
                        unit.CostAmount, unit.CostCurrency, unit.AcquiredOn),
                    cancellationToken));
        }

        foreach (var deal in package.Deals)
        {
            await tally.RecordAsync(
                "Deals",
                deal.Id,
                () => _deals.ImportAsync(
                    new ImportedDeal(
                        deal.Id,
                        into,
                        deal.CustomerId,
                        deal.InventoryUnitId,
                        deal.Currency,
                        deal.Status,
                        [.. deal.Charges.Select(c => new ChargeView(c.Kind, c.Description, c.Amount))],
                        [.. deal.Products.Select(p => new ImportedDealProduct(
                            p.FinanceProductId, p.Name, p.Provider, p.Price, p.Cost, p.TermMonths, p.TermMiles))],
                        [.. deal.TaxLines.Select(t => new NewTaxLine(
                            t.Description, t.Jurisdiction, t.Basis, t.Rate, t.Amount, t.Provenance))],
                        deal.TaxedAt is { } at
                            ? new TaxAddressView(at.AdministrativeArea, at.County, at.PostalCode, at.Country)
                            : null,
                        deal.TradeIn is { } trade
                            ? new TradeInView(
                                trade.Description,
                                trade.Allowance,
                                trade.Payoff,
                                trade.Allowance - trade.Payoff,
                                trade.Payoff > trade.Allowance)
                            : null,
                        deal.AmountDue),
                    cancellationToken));
        }

        foreach (var job in package.RepairOrders)
        {
            await tally.RecordAsync(
                "RepairOrders",
                job.Id,
                () => _repairOrders.ImportAsync(
                    new ImportedRepairOrder(
                        job.Id,
                        into,
                        job.CustomerId,
                        job.VehicleId,
                        job.Number,
                        job.Complaint,
                        job.Status,
                        job.Currency,
                        job.OdometerReading,
                        job.OpenedAt,
                        job.InvoicedAt,
                        [.. job.Lines.Select(l => new ImportedServiceLine(
                            l.Kind, l.Description, l.Hours, l.Rate, l.Amount, l.PayType, l.Authorization))],
                        job.AmountDue),
                    cancellationToken));
        }

        return Result.Success(tally.Report(into));
    }

    /// <summary>
    /// Counts outcomes per kind and keeps every refusal. Written as a small
    /// class rather than five pairs of counters because the five loops above
    /// then differ only in what they are importing, which is the one thing worth
    /// reading in them.
    /// </summary>
    private sealed class Tally
    {
        private readonly Dictionary<string, int[]> _counts = [];
        private readonly List<PackageRefusal> _refused = [];

        /// <summary>
        /// A forbidden answer is the only one that is not about this record. It
        /// means the caller may not write here at all, so it is recorded like
        /// any other refusal rather than thrown — the report then names every
        /// record that did not land instead of stopping at the first.
        /// </summary>
        public async Task RecordAsync(string kind, Guid id, Func<Task<Result<ImportOutcome>>> apply)
        {
            var slot = _counts.TryGetValue(kind, out var existing) ? existing : _counts[kind] = new int[3];

            var result = await apply();

            if (result.IsFailure)
            {
                slot[2]++;
                _refused.Add(new PackageRefusal(kind, id, result.Error.Code, result.Error.Message));
                return;
            }

            slot[result.Value == ImportOutcome.Created ? 0 : 1]++;
        }

        public PackageImportReport Report(RooftopId into) =>
            new(into.Value,
                _counts.Values.Sum(c => c[0]),
                _counts.Values.Sum(c => c[1]),
                [.. _counts.Select(c => new PackageCount(c.Key, c.Value[0], c.Value[1], c.Value[2]))],
                _refused);
    }
}
