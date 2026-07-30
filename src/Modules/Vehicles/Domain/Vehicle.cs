// Vehicle — a specific physical vehicle, identified by its VIN.
//
// Use:  Vehicle.Record(...). A vehicle exists independently of whether the
//       dealership currently owns it — the same car arrives as a trade-in years
//       after it was sold.
// Edit: a vehicle belongs to the whole dealer organization, not to one rooftop
//       (doc 04 §1). Rooftop ownership lives on InventoryUnit. Do not add a
//       rooftop to this class, and do not add a unique index on VIN — see
//       VehiclesDbContext for why.

using OpenDealer360.Core;

namespace OpenDealer360.Vehicles.Domain;

public sealed class Vehicle : AuditableEntity
{
    /// <summary>Earliest year worth accepting; older vehicles predate the VIN itself.</summary>
    private const int EarliestModelYear = 1900;

    /// <summary>A model year runs ahead of the calendar, so allow a margin.</summary>
    private const int LatestModelYear = 2100;

    public Guid Id { get; private set; }

    /// <summary>Normalized: upper-cased, with formatting characters removed.</summary>
    public string Vin { get; private set; } = string.Empty;

    /// <summary>
    /// Why this VIN does not have the standard shape, when it does not. Null for
    /// an ordinary VIN; a required sentence otherwise.
    /// </summary>
    public string? VinExceptionReason { get; private set; }

    public bool HasVinException => VinExceptionReason is not null;

    public int ModelYear { get; private set; }

    public string Make { get; private set; } = string.Empty;

    public string Model { get; private set; } = string.Empty;

    public string? Trim { get; private set; }

    public string? BodyStyle { get; private set; }

    public string? ExteriorColor { get; private set; }

    /// <summary>What a person reads in a list: "2021 Toyota RAV4 XLE".</summary>
    public string DisplayName =>
        string.Join(' ', new[] { ModelYear.ToString(System.Globalization.CultureInfo.InvariantCulture), Make, Model, Trim }
            .Where(part => !string.IsNullOrWhiteSpace(part)));

    private Vehicle()
    {
    }

    /// <summary>
    /// Records a vehicle. A VIN that is not well-formed is accepted only with a
    /// reason, which is then kept with the record so the exception is visible
    /// rather than assumed to be a typo (doc 04 §4).
    /// </summary>
    public static Vehicle Record(
        Guid id,
        string? vin,
        int modelYear,
        string make,
        string model,
        string? trim = null,
        string? vinExceptionReason = null,
        string? bodyStyle = null,
        string? exteriorColor = null)
    {
        var normalizedVin = Domain.Vin.Normalize(vin);
        var reason = string.IsNullOrWhiteSpace(vinExceptionReason) ? null : vinExceptionReason.Trim();

        if (normalizedVin.Length == 0 && reason is null)
        {
            throw new ArgumentException(
                "A vehicle needs a VIN, or a written reason it has none.", nameof(vin));
        }

        if (normalizedVin.Length > 0 && !Domain.Vin.IsWellFormed(normalizedVin) && reason is null)
        {
            throw new ArgumentException(
                $"'{normalizedVin}' is not a standard {Domain.Vin.StandardLength}-character VIN. "
                + "Record it anyway by giving a reason — an import, a pre-1981 vehicle, or a "
                + "number confirmed against the door plate.",
                nameof(vin));
        }

        if (modelYear is < EarliestModelYear or > LatestModelYear)
        {
            throw new ArgumentException(
                $"A model year must be between {EarliestModelYear} and {LatestModelYear}.",
                nameof(modelYear));
        }

        if (string.IsNullOrWhiteSpace(make))
        {
            throw new ArgumentException("A vehicle needs a make.", nameof(make));
        }

        if (string.IsNullOrWhiteSpace(model))
        {
            throw new ArgumentException("A vehicle needs a model.", nameof(model));
        }

        return new Vehicle
        {
            Id = id,
            Vin = normalizedVin,
            // A well-formed VIN carries no exception, whatever the caller sent.
            VinExceptionReason = Domain.Vin.IsWellFormed(normalizedVin) ? null : reason,
            ModelYear = modelYear,
            Make = make.Trim(),
            Model = model.Trim(),
            Trim = Blank(trim),
            BodyStyle = Blank(bodyStyle),
            ExteriorColor = Blank(exteriorColor),
        };
    }

    public void Describe(string? trim, string? bodyStyle, string? exteriorColor)
    {
        Trim = Blank(trim);
        BodyStyle = Blank(bodyStyle);
        ExteriorColor = Blank(exteriorColor);
    }

    private static string? Blank(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
