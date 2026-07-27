// Error — a stable, code-identified business failure.
//
// Use:  Error.Validation("deal.price_required", "A price is required."). The
//       Code is part of the public API contract; changing one breaks clients.
// Edit: adding an ErrorType means every endpoint's status-code mapping must
//       handle it — check OrganizationEndpoints.Problem before you do.

namespace OpenDealer360.Core;

/// <summary>
/// A stable, code-identified business error. The <see cref="Code"/> is part of
/// the API contract (doc 06 §6: "stable application error codes") and maps to an
/// RFC 7807 Problem Details response at the edge.
/// </summary>
public sealed record Error(string Code, string Message, ErrorType Type)
{
    public static readonly Error None = new(string.Empty, string.Empty, ErrorType.None);

    public static Error Validation(string code, string message) => new(code, message, ErrorType.Validation);

    public static Error NotFound(string code, string message) => new(code, message, ErrorType.NotFound);

    public static Error Conflict(string code, string message) => new(code, message, ErrorType.Conflict);

    public static Error Forbidden(string code, string message) => new(code, message, ErrorType.Forbidden);
}

public enum ErrorType
{
    None = 0,
    Validation = 1,
    NotFound = 2,
    Conflict = 3,
    Forbidden = 4,
}
