// ProblemResults — turns a business Error into the HTTP response the API
// contract promises.
//
// Use:  result.Error.ToProblem() at the end of an endpoint.
// Edit: this mapping is the public error contract (doc 06 §6). Adding an
//       ErrorType without adding a case here silently turns a business failure
//       into a 500 — the switch is deliberately exhaustive over the enum.
//       Lives at the HTTP edge because Core must stay free of ASP.NET.

using Microsoft.AspNetCore.Http;
using DealerFOSS.Core;

namespace DealerFOSS.App;

/// <summary>
/// Maps a business <see cref="Error"/> to RFC 7807 Problem Details. Every feature
/// uses this one mapping, so the same failure never answers differently on two
/// endpoints.
/// </summary>
public static class ProblemResults
{
    public static IResult ToProblem(this Error error)
    {
        ArgumentNullException.ThrowIfNull(error);

        var status = error.Type switch
        {
            ErrorType.Forbidden => StatusCodes.Status403Forbidden,
            ErrorType.NotFound => StatusCodes.Status404NotFound,
            ErrorType.Conflict => StatusCodes.Status409Conflict,
            ErrorType.Validation => StatusCodes.Status400BadRequest,
            ErrorType.Unavailable => StatusCodes.Status503ServiceUnavailable,
            _ => StatusCodes.Status500InternalServerError,
        };

        return Results.Problem(
            title: error.Code,
            detail: error.Message,
            statusCode: status,
            extensions: new Dictionary<string, object?> { ["code"] = error.Code });
    }
}
