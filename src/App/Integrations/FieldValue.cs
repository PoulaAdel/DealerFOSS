// FieldValue — ADR-021 in code: a value that does not fit becomes absent.
//
// Use:  Coerce.Amount(raw, min, max, "deal.frontGross") returns either a value
//       or an absence carrying the raw text and the reason. Never returns a
//       substitute.
// Edit: the temptation, every time, is to return 0 or a sentinel date so the
//       caller has something to write. Don't. A substituted 0 is a plausible
//       sale amount and a sentinel date is a plausible delivery date; both
//       survive every downstream check and are indistinguishable from fact by
//       the time anybody asks. Absent is visibly missing and can be chased.
//
//       Truncation is the single exception and only for free text — never for
//       anything a later lookup is keyed on, because a truncated key silently
//       matches the wrong record, which is worse than no record at all.

using System.Globalization;

namespace DealerFOSS.Integrations;

/// <summary>Why a provider value could not be stored as it arrived.</summary>
public enum MappingWarningKind
{
    /// <summary>Free text longer than the field allows; the excess was dropped.</summary>
    Truncated = 1,

    /// <summary>Not parseable as the expected type.</summary>
    Unparseable = 2,

    /// <summary>Parsed, but outside the range the field can hold.</summary>
    OutOfRange = 3,

    /// <summary>Too long for a field a lookup is keyed on, so it was not stored at all.</summary>
    KeyTooLong = 4,
}

/// <summary>
/// A record of one value that did not survive the crossing intact. Travels with
/// the record — not to a log — because the record is what every downstream
/// consumer actually sees.
/// </summary>
/// <param name="Field">The contract field, e.g. <c>deal.frontGross</c>.</param>
/// <param name="Kind">What went wrong.</param>
/// <param name="Raw">The provider's original text, preserved verbatim.</param>
public sealed record MappingWarning(string Field, MappingWarningKind Kind, string? Raw);

/// <summary>
/// Either a usable value or a recorded absence. There is deliberately no way to
/// read a default from an absent one: the caller must decide what an absence
/// means for its field, which is the decision ADR-021 exists to force.
/// </summary>
public readonly record struct FieldValue<T>
{
    private readonly T? _value;

    internal FieldValue(T? value, bool hasValue, MappingWarning? warning)
    {
        _value = value;
        HasValue = hasValue;
        Warning = warning;
    }

    public bool HasValue { get; }

    /// <summary>Set whenever the value did not arrive intact, present or not.</summary>
    public MappingWarning? Warning { get; }

    /// <summary>The value. Throws when absent — check <see cref="HasValue"/> first.</summary>
    public T Value => HasValue
        ? _value!
        : throw new InvalidOperationException(
            $"The value for '{Warning?.Field ?? "(unknown field)"}' is absent and has no substitute. " +
            "ADR-021: check HasValue and decide what absence means for this field.");
}

/// <summary>
/// Builds a <see cref="FieldValue{T}"/>. Non-generic so the factories are not
/// static members on a generic type — the same shape <c>Core.Result</c> uses.
/// </summary>
public static class FieldValue
{
    public static FieldValue<T> Present<T>(T value) => new(value, true, null);

    public static FieldValue<T> Present<T>(T value, MappingWarning warning) => new(value, true, warning);

    public static FieldValue<T> Absent<T>(MappingWarning warning) => new(default, false, warning);
}

/// <summary>Turns raw provider text into a storable value, or into an honest absence.</summary>
public static class Coerce
{
    /// <summary>
    /// Free text. Over-long input is truncated with a warning; over-long input
    /// for a keyed field is refused entirely.
    /// </summary>
    /// <param name="isKey">
    /// True for identifiers, codes, and anything a later lookup matches on. A
    /// truncated key matches the wrong record instead of failing.
    /// </param>
    public static FieldValue<string> Text(string? raw, int maxLength, string field, bool isKey = false)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxLength);

        if (string.IsNullOrWhiteSpace(raw))
        {
            return FieldValue.Absent<string>(new MappingWarning(field, MappingWarningKind.Unparseable, raw));
        }

        var trimmed = raw.Trim();
        if (trimmed.Length <= maxLength)
        {
            return FieldValue.Present(trimmed);
        }

        return isKey
            ? FieldValue.Absent<string>(new MappingWarning(field, MappingWarningKind.KeyTooLong, trimmed))
            : FieldValue.Present(
                trimmed[..maxLength],
                new MappingWarning(field, MappingWarningKind.Truncated, trimmed));
    }

    /// <summary>A money amount. Never becomes zero.</summary>
    public static FieldValue<decimal> Amount(string? raw, decimal minimum, decimal maximum, string field)
    {
        if (!decimal.TryParse(raw, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed))
        {
            return FieldValue.Absent<decimal>(new MappingWarning(field, MappingWarningKind.Unparseable, raw));
        }

        return parsed < minimum || parsed > maximum
            ? FieldValue.Absent<decimal>(new MappingWarning(field, MappingWarningKind.OutOfRange, raw))
            : FieldValue.Present(parsed);
    }

    /// <summary>A whole number — a model year, a mileage. Never becomes zero.</summary>
    public static FieldValue<int> Number(string? raw, int minimum, int maximum, string field)
    {
        if (!int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
        {
            return FieldValue.Absent<int>(new MappingWarning(field, MappingWarningKind.Unparseable, raw));
        }

        return parsed < minimum || parsed > maximum
            ? FieldValue.Absent<int>(new MappingWarning(field, MappingWarningKind.OutOfRange, raw))
            : FieldValue.Present(parsed);
    }

    /// <summary>
    /// A date. Never becomes a sentinel — a whole dealership's records sharing
    /// one delivery date is a report nobody questions until it is a legal
    /// problem.
    /// </summary>
    public static FieldValue<DateOnly> Date(string? raw, int minimumYear, int maximumYear, string field)
    {
        if (!DateTime.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
        {
            return FieldValue.Absent<DateOnly>(new MappingWarning(field, MappingWarningKind.Unparseable, raw));
        }

        return parsed.Year < minimumYear || parsed.Year > maximumYear
            ? FieldValue.Absent<DateOnly>(new MappingWarning(field, MappingWarningKind.OutOfRange, raw))
            : FieldValue.Present(DateOnly.FromDateTime(parsed));
    }
}
