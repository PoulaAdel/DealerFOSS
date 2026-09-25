// Copyright (c) 2026 The DealerFOSS contributors.
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Overview: Purpose, File Design, and Engineering
//   Package — a rooftop's records with their identities and their references to
//   each other intact, in a file this same API accepts back.
//
// Usage:
//   Built by PackageExporter, applied by PackageImporter, carried over the wire
//   as JSON by the two /migration/packages endpoints.
//
// Coding Instructions:
//   THIS IS NOT A REPLACEMENT FOR THE CSV EXPORT and must not become one. The
//   two CSVs answer "give me my customers in a spreadsheet"; a package answers
//   "move this lot's records to another installation without losing what points
//   at what". A dealership going to a competitor wants the first. Keep both.
//
//   EVERY RECORD CARRIES ITS OWN ID AND EVERY REFERENCE IS THAT ID. That is the
//   whole design. All five aggregates already accept a caller-supplied id in
//   their factories, so the receiving installation stores the same keys rather
//   than minting new ones and keeping a translation table. Relationships then
//   survive because they are the same numbers on both sides, and re-importing
//   the same package is a no-op because the ids collide with what is already
//   there. Do not "improve" this by generating fresh ids on the way in: the
//   moment you do, a package can only be imported once and the criterion this
//   was built for stops being met.
//
//   THE TOTALS ARE EVIDENCE, NOT DATA. Every deal and job carries the amount
//   the source system said it came to. The importer recomputes it from the
//   lines it just wrote and refuses the record if the two disagree. Nothing
//   reads AmountDue back out of the package afterwards — it exists so that a
//   package which lost a line on the way through says so, instead of arriving
//   quietly wrong. This is the same defect that has now cost this project four
//   separate fixes; here it is caught by construction.
//
//   A PACKAGE IS SELF-CONTAINED BY CONSTRUCTION. The exporter walks one
//   rooftop's units, deals and jobs and then adds exactly the customers and
//   vehicles those records point at. So a dangling reference in a package is
//   either corruption or somebody's hand edit, and the importer treats it as
//   the exception it is rather than the normal case.
//
//   Documents are absent, and that is correct rather than missing. This system
//   stores no files: IDocuments owns no data and renders a deal or a job into
//   HTML when somebody asks. Putting rendered HTML in the package would freeze
//   a copy that the receiving installation would immediately contradict. What
//   is carried instead is everything the renderer reads, and PackageTests
//   proves the point by rendering both documents on each side and comparing.

using System.Text.Json;
using System.Text.Json.Serialization;

namespace DealerFOSS.DataMigration;

/// <summary>
/// One rooftop's records. <paramref name="Format"/> and <paramref name="Version"/>
/// are checked before anything else is read.
/// </summary>
public sealed record RecordPackage(
    string Format,
    int Version,
    DateTimeOffset ProducedAt,
    PackageSource Source,
    IReadOnlyList<PackagedCustomer> Customers,
    IReadOnlyList<PackagedVehicle> Vehicles,
    IReadOnlyList<PackagedUnit> InventoryUnits,
    IReadOnlyList<PackagedDeal> Deals,
    IReadOnlyList<PackagedRepairOrder> RepairOrders)
{
    /// <summary>
    /// The only value <see cref="Format"/> may hold. A CSV renamed to .json, or
    /// somebody's unrelated export, is refused by name rather than by a
    /// deserialization error nobody can act on.
    /// </summary>
    public const string Marker = "dealerfoss.package";

    /// <summary>
    /// Bumped when a change would make an older reader wrong — not when a field
    /// is added, which an older reader simply ignores.
    /// </summary>
    public const int Current = 1;

    /// <summary>
    /// Indented, and the property order is the class's own. Both are for the
    /// person who opens the file in an editor to find out why a record was
    /// refused, which is the first thing anybody does with a failed migration.
    /// </summary>
    public static JsonSerializerOptions Json { get; } = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };
}

/// <summary>
/// Where the package came from, for the person reading it later. None of it is
/// used to decide anything on import — the receiving installation's own rooftop
/// is named by the caller, because a rooftop id from somewhere else means
/// nothing here.
/// </summary>
public sealed record PackageSource(
    string Organization,
    Guid RooftopId,
    string RooftopName,
    string RooftopCode);

public sealed record PackagedContact(string Kind, string Value, bool IsPrimary);

public sealed record PackagedAddress(
    string? Line1,
    string? Line2,
    string? City,
    string? AdministrativeArea,
    string? County,
    string? PostalCode,
    string Country);

/// <summary>
/// A customer, with the contact details and address the paperwork prints.
/// Customers belong to the organization rather than to one lot, so a package
/// carries only the ones its records actually point at.
/// </summary>
public sealed record PackagedCustomer(
    Guid Id,
    string Kind,
    string FirstName,
    string LastName,
    string? ExternalReference,
    decimal? CreditLimit,
    IReadOnlyList<PackagedContact> ContactPoints,
    PackagedAddress? Address);

public sealed record PackagedVehicle(
    Guid Id,
    string Vin,
    int ModelYear,
    string Make,
    string Model,
    string? Trim,
    string? BodyStyle,
    string? ExteriorColor,

    /// <summary>
    /// Why this car is recorded without a real VIN. Dropping it makes the row
    /// un-importable, because the justification is what permits the exception —
    /// the CSV export learned this the same way.
    /// </summary>
    string? VinExceptionReason);

public sealed record PackagedUnit(
    Guid Id,
    Guid VehicleId,
    string StockNumber,
    string Status,
    decimal? CostAmount,
    string? CostCurrency,
    DateOnly? AcquiredOn);

public sealed record PackagedCharge(string Kind, string Description, decimal Amount);

/// <summary>
/// A product sold on a deal. The id is the F&amp;I catalogue's, not the deal
/// line's, because that is the one the deal stores and the one that has to stay
/// unique within a deal. The receiving installation may not stock this cover at
/// all — the name, price and cost travel with the sale for exactly that reason.
/// </summary>
public sealed record PackagedProduct(
    Guid FinanceProductId,
    string Name,
    string? Provider,
    decimal Price,
    decimal Cost,
    int? TermMonths,
    int? TermMiles);

public sealed record PackagedTaxLine(
    string Description,
    string Jurisdiction,
    decimal Basis,
    decimal Rate,
    decimal Amount,
    string Provenance);

public sealed record PackagedTradeIn(string Description, decimal Allowance, decimal Payoff);

/// <summary>
/// A deal as the source system settled it. <paramref name="AmountDue"/> is the
/// check, not the value — see the note at the top of this file.
/// </summary>
public sealed record PackagedDeal(
    Guid Id,
    Guid CustomerId,
    Guid InventoryUnitId,
    string Currency,
    string Status,
    IReadOnlyList<PackagedCharge> Charges,
    IReadOnlyList<PackagedProduct> Products,
    IReadOnlyList<PackagedTaxLine> TaxLines,

    /// <summary>
    /// The address the tax was worked out from. Null exactly when there is no
    /// tax — a figure with no address behind it is one nobody can defend, and
    /// the domain refuses the pair.
    /// </summary>
    PackagedTaxAddress? TaxedAt,
    PackagedTradeIn? TradeIn,

    /// <summary>
    /// How the deal was being paid for over time. Null on a cash deal.
    /// </summary>
    /// <remarks>
    /// THE AMOUNT DUE CHECK CANNOT CATCH THIS ONE. Financing sits outside
    /// AmountDue on purpose — the down payment is how the customer pays, not a
    /// reduction in what they owe — so a package that dropped it would balance
    /// perfectly and still arrive having turned a sixty-month contract into a
    /// cash deal. It is carried because it is what the customer signed, and the
    /// paperwork comparison in PackageTests is what proves it survived.
    /// </remarks>
    PackagedFinancing? Financing,
    decimal AmountDue);

/// <summary>
/// A retail instalment structure as the source system settled it. The four
/// agreed figures only: the amount financed, the monthly payment and the finance
/// charge all follow from these and are recomputed on the other side.
/// </summary>
/// <param name="AnnualPercentageRate">A fraction: 0.0649 is 6.49%.</param>
public sealed record PackagedFinancing(
    string? Lender,
    decimal DownPayment,
    decimal AnnualPercentageRate,
    int TermMonths);

public sealed record PackagedTaxAddress(
    string? AdministrativeArea,
    string? County,
    string? PostalCode,
    string Country);

public sealed record PackagedServiceLine(
    Guid Id,
    string Kind,
    string Description,
    decimal? Hours,
    decimal? Rate,
    decimal Amount,
    string PayType,
    string Authorization);

/// <summary>
/// A job as the source system settled it. <paramref name="AmountDue"/> is the
/// check, not the value.
/// </summary>
public sealed record PackagedRepairOrder(
    Guid Id,
    Guid CustomerId,
    Guid VehicleId,
    string Number,
    string Complaint,
    string Status,
    string Currency,
    int? OdometerReading,
    DateTimeOffset OpenedAt,
    DateTimeOffset? InvoicedAt,
    IReadOnlyList<PackagedServiceLine> Lines,
    decimal AmountDue);

/// <summary>
/// What an import did, kind by kind, and what it would not do.
/// </summary>
/// <remarks>
/// <paramref name="Reused"/> is not a failure and is not a duplicate. It is a
/// record whose id was already present, which is what makes re-running an
/// import safe. An existing record is never overwritten: the receiving
/// dealership may have edited it since, and quietly reverting somebody's
/// correction is worse than doing nothing.
/// </remarks>
public sealed record PackageImportReport(
    Guid RooftopId,
    int Applied,
    int Reused,
    IReadOnlyList<PackageCount> ByKind,
    IReadOnlyList<PackageRefusal> Refused);

public sealed record PackageCount(string Kind, int Applied, int Reused, int Refused);

/// <summary>
/// One record the import would not write, and why in words. The id is the
/// source system's, because that is the one a person can find in the file they
/// are holding.
/// </summary>
public sealed record PackageRefusal(string Kind, Guid Id, string ReasonCode, string Reason);
