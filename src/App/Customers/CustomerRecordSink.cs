// Copyright (c) 2026 The DealerFOSS contributors.
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Overview: Purpose, File Design, and Engineering
//   CustomerRecordSink — how a customer arriving from another system becomes a
//   customer here.
//
// Usage:
//   Registered in Program.cs. The integration runtime finds it by contract
//   name and version, and hands it a batch.
//
// Coding Instructions:
//   This file lives in Customers rather than in Integrations on purpose, and
//   the direction is the point. FeatureBoundaryTests forbids Integrations
//   from referencing any capability, so the port is declared at the edge and
//   the adapter belongs to whoever owns the data. A connector able to write
//   a customer row directly would be a route around every rule this
//   capability enforces, arriving from outside the building.
//
//   IDEMPOTENCE IS THE WHOLE JOB. The cursor deliberately refuses to advance
//   whenever a provider will not account for its window, which means the
//   same records arrive again tomorrow — by design, and routinely. Every
//   insert here is guarded by an external-reference lookup, and there is a
//   test that delivers the same batch twice and counts the rows.
//
//   What this deliberately does NOT do is update an existing customer.
//   Field ownership — who wins when the provider and a member of staff
//   disagree about a phone number — is a real decision (doc 05 §4) and
//   nobody has made it. Silently overwriting a person's correction with
//   stale provider data would be worse than doing nothing, so an existing
//   record is reported Unchanged and left alone.

using DealerFOSS.Core;
using DealerFOSS.Integrations;

namespace DealerFOSS.Customers;

/// <summary>Applies <c>Customers</c> v1 records from any connector.</summary>
public sealed class CustomerRecordSink(ICustomers customers) : IRecordSink
{
    /// <summary>Longest value we will accept for a name, matching the column.</summary>
    private const int NameLength = 120;

    private const int AddressLength = 200;

    private readonly ICustomers _customers = customers;

    public string Contract => CustomerFields.Contract;

    public int Version => CustomerFields.Version;

    public async Task<Result<ApplyOutcome>> ApplyAsync(
        RooftopId rooftopId,
        IReadOnlyList<ProviderRecord> records,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(records);

        var applied = 0;
        var unchanged = 0;
        var rejected = new List<RejectedRecord>();
        var warnings = new List<MappingWarning>();

        foreach (var record in records)
        {
            // The idempotence guard. Cheap, and the reason a held cursor is safe
            // rather than expensive.
            var existing = await _customers
                .FindByExternalReferenceAsync(record.ExternalId, cancellationToken)
                .ConfigureAwait(false);

            if (existing.IsFailure)
            {
                // A failed lookup is not a bad record — it is us being unable to
                // answer. Failing the batch is right: quarantining would blame
                // the provider for our own problem.
                return Result.Failure<ApplyOutcome>(existing.Error);
            }

            if (existing.Value is not null)
            {
                unchanged++;
                continue;
            }

            var mapped = Map(record, rooftopId, warnings);
            if (mapped.IsFailure)
            {
                rejected.Add(new RejectedRecord(record, mapped.Error));
                continue;
            }

            var added = await _customers.AddAsync(mapped.Value, cancellationToken).ConfigureAwait(false);
            if (added.IsFailure)
            {
                // Distinguish "this record is wrong" from "we cannot write at
                // all". A permission failure would otherwise quarantine every
                // record in the batch and look like a data quality problem.
                if (added.Error.Type is ErrorType.Forbidden)
                {
                    return Result.Failure<ApplyOutcome>(added.Error);
                }

                rejected.Add(new RejectedRecord(record, added.Error));
                continue;
            }

            applied++;
        }

        return Result.Success(new ApplyOutcome(applied, unchanged, rejected, warnings));
    }

    /// <summary>
    /// Contract fields to a customer, collecting anything that did not survive
    /// intact rather than discarding it.
    /// </summary>
    private static Result<NewCustomer> Map(
        ProviderRecord record,
        RooftopId rooftopId,
        List<MappingWarning> warnings)
    {
        // A surname is the one field a customer cannot exist without: it is the
        // display name, and a blank one produces a row nobody can find again.
        var lastName = Coerce.Text(Raw(record, CustomerFields.LastName), NameLength, CustomerFields.LastName);
        if (!lastName.HasValue)
        {
            return Result.Failure<NewCustomer>(CustomerErrors.MissingName);
        }

        Collect(warnings, lastName.Warning);

        var kind = KindOf(record);
        if (kind is null)
        {
            // Guessing "Person" would be wrong roughly as often as a dealership
            // sells to businesses, and the mistake is invisible afterwards.
            return Result.Failure<NewCustomer>(CustomerErrors.UnknownKind);
        }

        var firstName = Text(record, CustomerFields.FirstName, NameLength, warnings);
        var email = Text(record, CustomerFields.Email, NameLength, warnings);
        var phone = Text(record, CustomerFields.Phone, 40, warnings);

        return Result.Success(new NewCustomer(
            kind,
            firstName,
            lastName.Value,
            rooftopId,
            email,
            phone,
            AddressOf(record, warnings),
            record.ExternalId));
    }

    /// <summary>
    /// An address only counts if it has the two parts that make it deliverable.
    /// A city with no street is not a partial address, it is noise.
    /// </summary>
    private static AddressView? AddressOf(ProviderRecord record, List<MappingWarning> warnings)
    {
        var line1 = Text(record, CustomerFields.AddressLine1, AddressLength, warnings);
        var city = Text(record, CustomerFields.City, NameLength, warnings);

        if (line1 is null || city is null)
        {
            return null;
        }

        return new AddressView(
            line1,
            Text(record, CustomerFields.AddressLine2, AddressLength, warnings),
            city,
            Text(record, CustomerFields.AdministrativeArea, NameLength, warnings),
            Text(record, CustomerFields.County, NameLength, warnings),
            Text(record, CustomerFields.PostalCode, 20, warnings),
            Text(record, CustomerFields.Country, NameLength, warnings) ?? string.Empty);
    }

    /// <summary>
    /// The provider's word for what kind of customer this is. Matched loosely
    /// because providers disagree about capitalisation and little else here —
    /// but an unrecognised word is a rejection, never a default.
    /// </summary>
    private static string? KindOf(ProviderRecord record)
    {
        var raw = Raw(record, CustomerFields.Kind)?.Trim();

        return raw?.ToUpperInvariant() switch
        {
            "PERSON" or "INDIVIDUAL" or "RETAIL" => "Person",
            "BUSINESS" or "COMPANY" or "COMMERCIAL" or "FLEET" => "Business",
            _ => null,
        };
    }

    /// <summary>
    /// An optional text field. Absent stays absent — <see cref="Coerce"/> reports
    /// a missing value as unparseable, which for an optional field is simply
    /// "not supplied" and not worth a warning.
    /// </summary>
    private static string? Text(
        ProviderRecord record,
        string field,
        int maxLength,
        List<MappingWarning> warnings)
    {
        var raw = Raw(record, field);
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        var value = Coerce.Text(raw, maxLength, field);
        Collect(warnings, value.Warning);

        return value.HasValue ? value.Value : null;
    }

    private static string? Raw(ProviderRecord record, string field) =>
        record.Fields.TryGetValue(field, out var value) ? value : null;

    private static void Collect(List<MappingWarning> warnings, MappingWarning? warning)
    {
        if (warning is not null)
        {
            warnings.Add(warning);
        }
    }
}
