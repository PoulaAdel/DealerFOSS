// Vin — reading and checking a vehicle identification number.
//
// Use:  Vin.Normalize(typed) before storing or comparing; Vin.IsWellFormed to
//       decide whether a documented exception is required.
// Edit: a VIN that fails this check is NOT automatically wrong. Pre-1981
//       vehicles, imports, trailers, and equipment legitimately have shorter or
//       oddly shaped numbers, and source data is frequently mistyped. The rule is
//       "well-formed, or say why not" — never a hard refusal, and never a
//       universal unique index (doc 04 §4).

namespace DealerFOSS.Vehicles;

/// <summary>
/// VIN rules. A modern VIN is 17 characters drawn from an alphabet that excludes
/// I, O, and Q — they are omitted so they cannot be confused with 1 and 0.
/// </summary>
/// <remarks>
/// The North American check-digit rule is deliberately not enforced. It does not
/// hold worldwide, and applying it would reject legitimate imported vehicles
/// while doing nothing about the far more common problem: a number typed
/// correctly for a car that is genuinely non-standard.
/// </remarks>
public static class Vin
{
    /// <summary>The length of a VIN issued since the 1981 standard.</summary>
    public const int StandardLength = 17;

    /// <summary>
    /// Makes a typed VIN comparable: upper-cased, with the spaces and hyphens
    /// people add for readability removed.
    /// </summary>
    public static string Normalize(string? value)
    {
        var trimmed = (value ?? string.Empty).Trim().ToUpperInvariant();

        return new string(trimmed
            .Where(c => c is not (' ' or '-' or '.' or '_'))
            .ToArray());
    }

    /// <summary>
    /// Whether a normalized VIN has the standard shape. A false answer means a
    /// documented reason is required, not that the vehicle is invalid.
    /// </summary>
    public static bool IsWellFormed(string? normalizedVin)
    {
        if (normalizedVin is null || normalizedVin.Length != StandardLength)
        {
            return false;
        }

        return normalizedVin.All(IsVinCharacter);
    }

    private static bool IsVinCharacter(char c) =>
        c is >= '0' and <= '9'
        || (c is >= 'A' and <= 'Z' && c is not ('I' or 'O' or 'Q'));
}
