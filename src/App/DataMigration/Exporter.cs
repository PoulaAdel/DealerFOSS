// Copyright (c) 2026 The DealerFOSS contributors.
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Overview: Purpose, File Design, and Engineering
//   Exporter — a dealership's records on their way out, in a file this same API
//   would accept back.
//
// Usage:
//   Called by MigrationService.ExportAsync.
//
// Coding Instructions:
//   The column names here are not a choice. They are exactly what
//   ImportRunner reads, in an order it accepts, because **an export must be
//   a valid import**. That is the promise an open DMS makes: a dealership
//   can take their records to a competitor, or bring them back, without
//   anybody here writing them a converter. `ExportTests` round-trips a real
//   export through the importer and compares, so this cannot drift quietly.
//
//   Everything is quoted. It costs a few bytes and removes the entire class
//   of bug where a customer called "Smith, Jones & Co" silently becomes two
//   columns in whatever the receiving system is.

using System.Globalization;
using System.Text;
using DealerFOSS.Customers;
using DealerFOSS.Vehicles;

namespace DealerFOSS.DataMigration;

internal static class Exporter
{
    /// <summary>
    /// The customer columns, matching <c>ImportRunner</c>'s reader exactly.
    /// </summary>
    public static readonly string[] CustomerColumns =
    [
        "externalid", "kind", "firstname", "lastname", "email", "phone",
        "addressline1", "addressline2", "city", "state", "county", "postalcode", "country",
    ];

    /// <summary>
    /// `vinexceptionreason` is here because the round-trip test found it
    /// missing. A car recorded without a real VIN carries a written reason that
    /// permitted the exception; drop it on export and the row is refused on
    /// import, because the justification is gone. The reason is part of the
    /// record, not commentary on it.
    /// </summary>
    public static readonly string[] VehicleColumns =
    [
        "vin", "modelyear", "make", "model", "trim", "bodystyle", "exteriorcolor",
        "vinexceptionreason",
    ];

    public static string HeaderFor(ImportKind kind) =>
        Line(kind == ImportKind.Customers ? CustomerColumns : VehicleColumns);

    /// <summary>
    /// One customer as a row.
    /// </summary>
    /// <remarks>
    /// A customer created by hand has no external reference, and a row without
    /// one is refused on import — which would make an export of hand-typed
    /// customers un-importable, i.e. not an export at all. So their own id is
    /// used instead. That is not a fallback so much as the truthful answer: in
    /// the system receiving this file, the identifier this record had here *is*
    /// its reference to the outside.
    /// </remarks>
    public static string Row(CustomerDetail customer)
    {
        var email = Primary(customer, "Email");
        var phone = Primary(customer, "Phone") ?? Primary(customer, "Mobile");
        var address = customer.Address;

        return Line(
        [
            customer.ExternalReference ?? customer.Id.ToString(),
            customer.Kind,
            customer.FirstName,
            customer.LastName,
            email ?? string.Empty,
            phone ?? string.Empty,
            address?.Line1 ?? string.Empty,
            address?.Line2 ?? string.Empty,
            address?.City ?? string.Empty,
            address?.AdministrativeArea ?? string.Empty,
            address?.County ?? string.Empty,
            address?.PostalCode ?? string.Empty,
            address?.Country ?? string.Empty,
        ]);
    }

    public static string Row(VehicleDetail vehicle) =>
        Line(
        [
            vehicle.Vin,
            vehicle.ModelYear.ToString(CultureInfo.InvariantCulture),
            vehicle.Make,
            vehicle.Model,
            vehicle.Trim ?? string.Empty,
            vehicle.BodyStyle ?? string.Empty,
            vehicle.ExteriorColor ?? string.Empty,
            vehicle.VinExceptionReason ?? string.Empty,
        ]);

    private static string? Primary(CustomerDetail customer, string kind) =>
        customer.ContactPoints
            .FirstOrDefault(p => p.IsPrimary
                && string.Equals(p.Kind, kind, StringComparison.OrdinalIgnoreCase))
            ?.Value;

    /// <summary>
    /// RFC 4180: every field quoted, and a literal quote doubled. Quoting
    /// unconditionally rather than only when needed — the rule is then one line
    /// long and cannot be got subtly wrong for the one value that mattered.
    /// </summary>
    private static string Line(string[] fields)
    {
        var line = new StringBuilder();

        for (var i = 0; i < fields.Length; i++)
        {
            if (i > 0)
            {
                line.Append(',');
            }

            line.Append('"')
                .Append(fields[i].Replace("\"", "\"\"", StringComparison.Ordinal))
                .Append('"');
        }

        return line.ToString();
    }
}
