// Copyright (c) 2026 The DealerFOSS contributors.
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Overview: Purpose, File Design, and Engineering
//   A business failure identified by a stable code and carrying a message
//   written for the person who hit it. The code is the machine-readable half
//   and part of the public API contract; the message is the human half and may
//   be reworded freely.
//
//   ErrorType exists so that one mapping — ProblemResults.cs — turns any Error
//   into an HTTP status. That is why a new ErrorType is not a local change: it
//   is a new status code for the whole API, and a type the mapping does not
//   handle would fall through to something misleading.
//
// Usage:
//   Error.Validation("deal.price_required", "A price is required.")
//   Error.NotFound / Conflict / Forbidden / Unavailable
//
// Coding Instructions:
//   CHANGING A CODE BREAKS CLIENTS. Codes are contract; treat a rename as a
//   breaking API change and version it.
//
//   Adding an ErrorType means updating the status mapping in
//   src/App/ProblemResults.cs in the same commit. Check it before you add one.

namespace DealerFOSS.Core;

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

    /// <summary>
    /// Something outside this system did not answer. Distinct from every other
    /// type because it says nothing about the request: the caller did nothing
    /// wrong, the data may be perfectly fine, and trying again later may work.
    /// </summary>
    public static Error Unavailable(string code, string message) => new(code, message, ErrorType.Unavailable);
}

public enum ErrorType
{
    None = 0,
    Validation = 1,
    NotFound = 2,
    Conflict = 3,
    Forbidden = 4,

    /// <summary>A dependency outside this system was unreachable. Maps to 503.</summary>
    Unavailable = 5,
}
