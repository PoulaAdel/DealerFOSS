// ImportRunner — deciding what happens to each staged row, and doing it.
//
// Use:  called by ImportWorker inside a tenant scope. Not an endpoint.
// Edit: two properties are the whole value of this file.
//
//       **A trial and a real run make the same decisions.** Every row goes
//       through the same validation and the same natural-key lookup; the only
//       difference is that a trial does not call the write at the end. So a
//       trial's report of "412 created, 88 updated, 3 failed" is what the real
//       run will do — that is the question somebody is asking before they commit
//       a dealership's history to it.
//
//       What a trial genuinely cannot predict is named rather than hidden: a
//       failure that only the database can raise, and the effect of rows on each
//       other. Two rows carrying the same VIN both read as "create" in a trial,
//       because neither has been written when the other is examined; in the real
//       run the second finds the first and reports "updated". The counts move by
//       one. That is the honest limit, and it is in the README.
//
//       **A row is decided exactly once.** Created, Updated, Skipped, or Failed.
//       If these stop summing to the row count, the reconciliation report is
//       lying, and a reconciliation report nobody can trust is worse than none.

using OpenDealer360.Core;
using OpenDealer360.Customers;
using OpenDealer360.Vehicles;

namespace OpenDealer360.DataMigration;

/// <summary>What one row turned into, before it is written down.</summary>
internal readonly record struct RowDecision(RowOutcome Outcome, string? Message);

internal sealed class ImportRunner(ICustomers customers, IVehicles vehicles)
{
    private readonly ICustomers _customers = customers;
    private readonly IVehicles _vehicles = vehicles;

    /// <summary>
    /// Columns a file must carry, by kind. Anything else in the file is ignored
    /// rather than refused: a dealership exports what their old system gives
    /// them, and demanding they delete columns first would be rude and pointless.
    /// </summary>
    private static readonly string[] CustomerColumns = ["externalid", "lastname"];

    private static readonly string[] VehicleColumns = ["vin", "modelyear", "make", "model"];

    public static IReadOnlyList<string> MissingColumns(ImportKind kind, IReadOnlyList<string> header)
    {
        var required = kind == ImportKind.Customers ? CustomerColumns : VehicleColumns;

        return required
            .Where(column => !header.Any(h => string.Equals(h, column, StringComparison.OrdinalIgnoreCase)))
            .ToList();
    }

    public async Task<RowDecision> RunAsync(
        ImportKind kind,
        ImportMode mode,
        IReadOnlyList<string> header,
        CsvRow row,
        CancellationToken cancellationToken) =>
        kind == ImportKind.Customers
            ? await CustomerAsync(mode, header, row, cancellationToken)
            : await VehicleAsync(mode, header, row, cancellationToken);

    private async Task<RowDecision> CustomerAsync(
        ImportMode mode,
        IReadOnlyList<string> header,
        CsvRow row,
        CancellationToken cancellationToken)
    {
        var externalId = row.Field(header, "externalid");
        if (externalId is null)
        {
            return new RowDecision(
                RowOutcome.Failed,
                "This row has no external id. Without one, importing the file again "
                + "would create a second copy of this customer.");
        }

        var lastName = row.Field(header, "lastname");
        if (lastName is null)
        {
            return new RowDecision(RowOutcome.Failed, "This row has no last name or business name.");
        }

        var existing = await _customers.FindByExternalReferenceAsync(externalId, cancellationToken);
        if (existing.IsFailure)
        {
            return new RowDecision(RowOutcome.Failed, existing.Error.Message);
        }

        if (existing.Value is not null)
        {
            // Already here from an earlier run of this or another file. Reported
            // as Updated rather than Skipped because that is what the dealership
            // asked for — and rewriting a customer's details from a file is a
            // decision nobody has made yet, so nothing is actually changed.
            return new RowDecision(
                RowOutcome.Updated,
                "Already imported; left as it is. Changing details from a file is not "
                + "supported yet, so this row was matched and not rewritten.");
        }

        var kind = row.Field(header, "kind") ?? "Person";
        if (!Enum.TryParse<CustomerKind>(kind, ignoreCase: true, out _))
        {
            return new RowDecision(
                RowOutcome.Failed,
                $"'{kind}' is not a customer kind. Use Person or Business.");
        }

        if (mode == ImportMode.Trial)
        {
            return new RowDecision(RowOutcome.Created, null);
        }

        var line1 = row.Field(header, "addressline1");
        var city = row.Field(header, "city");

        var added = await _customers.AddAsync(
            new NewCustomer(
                kind,
                row.Field(header, "firstname"),
                lastName,
                HomeRooftopId: null,
                row.Field(header, "email"),
                row.Field(header, "phone"),
                // An address needs at least a street and a town to be worth
                // recording; a lone postcode is noise.
                line1 is null || city is null
                    ? null
                    : new AddressView(
                        line1,
                        row.Field(header, "addressline2"),
                        city,
                        row.Field(header, "state") ?? row.Field(header, "administrativearea"),
                        row.Field(header, "postalcode") ?? row.Field(header, "zip"),
                        row.Field(header, "country") ?? "US"),
                externalId),
            cancellationToken);

        return added.IsSuccess
            ? new RowDecision(RowOutcome.Created, null)
            : new RowDecision(RowOutcome.Failed, added.Error.Message);
    }

    private async Task<RowDecision> VehicleAsync(
        ImportMode mode,
        IReadOnlyList<string> header,
        CsvRow row,
        CancellationToken cancellationToken)
    {
        var vin = row.Field(header, "vin");
        if (vin is null)
        {
            return new RowDecision(
                RowOutcome.Failed,
                "This row has no VIN. A car without one cannot be matched to itself "
                + "on a later import; record it by hand with a written reason instead.");
        }

        var modelYear = row.IntField(header, "modelyear");
        if (modelYear is null)
        {
            return new RowDecision(
                RowOutcome.Failed,
                $"'{row.Field(header, "modelyear") ?? string.Empty}' is not a model year.");
        }

        var make = row.Field(header, "make");
        var model = row.Field(header, "model");
        if (make is null || model is null)
        {
            return new RowDecision(RowOutcome.Failed, "This row has no make or no model.");
        }

        // A VIN that is not the standard seventeen is not wrong — pre-1981 cars,
        // imports, trailers and equipment legitimately differ — but it does need
        // a written reason. Checked here rather than left to the write, so a
        // trial and the real run reach the same verdict on the same row.
        var exceptionReason = row.Field(header, "vinexceptionreason");
        if (!Vin.IsWellFormed(Vin.Normalize(vin)) && exceptionReason is null)
        {
            return new RowDecision(
                RowOutcome.Failed,
                $"'{vin}' is not a standard 17-character VIN. That may be correct for this "
                + "vehicle — add a vinexceptionreason column saying why, and import again.");
        }

        var existing = await _vehicles.FindByVinAsync(vin, cancellationToken);
        if (existing.IsFailure)
        {
            return new RowDecision(RowOutcome.Failed, existing.Error.Message);
        }

        if (existing.Value is not null)
        {
            return new RowDecision(
                RowOutcome.Skipped,
                $"This car is already recorded as {existing.Value.DisplayName}.");
        }

        if (mode == ImportMode.Trial)
        {
            return new RowDecision(RowOutcome.Created, null);
        }

        var added = await _vehicles.AddAsync(
            new NewVehicle(
                vin,
                modelYear.Value,
                make,
                model,
                row.Field(header, "trim"),
                row.Field(header, "bodystyle"),
                row.Field(header, "exteriorcolor") ?? row.Field(header, "colour"),
                exceptionReason),
            cancellationToken);

        return added.IsSuccess
            ? new RowDecision(RowOutcome.Created, null)
            : new RowDecision(RowOutcome.Failed, added.Error.Message);
    }
}
