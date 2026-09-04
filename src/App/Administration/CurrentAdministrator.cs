// Copyright (c) 2026 The DealerFOSS contributors.
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Overview: Purpose, File Design, and Engineering
//   CurrentAdministrator — who is making the current control-plane request.
//
// Usage:
//   Inject it into an admin endpoint and read Id. Resolved once, by
//   AdministratorMiddleware, before any endpoint runs.
//
// Coding Instructions:
//   This is deliberately NOT ICurrentUser, and must never be made to
//   implement it. ICurrentUser is what every business capability passes to
//   IAccessDirectory when it asks "may this person see this dealership's
//   data?" — an administrator has no answer to that question, and giving
//   them one is the whole thing this milestone exists to prevent.

namespace DealerFOSS.Administration;

/// <summary>
/// The authenticated administrator for the current request, if any. Carries no
/// tenant, no rooftop, and no permission from the tenant catalogue (doc 06 §2).
/// </summary>
public interface ICurrentAdministrator
{
    bool IsAuthenticated { get; }

    /// <summary>The administrator's id. Throws when the request is not an admin one.</summary>
    Guid Id { get; }

    string Email { get; }

    /// <summary>
    /// Whether this administrator has yet to enrol a second factor. Such a caller
    /// may do nothing but enrol — including, and especially, open support access.
    /// </summary>
    bool MustEnrolSecondFactor { get; }

    void Set(Guid id, string email, bool mustEnrolSecondFactor);
}

/// <summary>Scoped, write-once holder of the administrator for the current request.</summary>
public sealed class CurrentAdministrator : ICurrentAdministrator
{
    private Guid? _id;

    public bool IsAuthenticated => _id is not null;

    public Guid Id => _id
        ?? throw new InvalidOperationException(
            "No administrator is resolved for this request. A control-plane operation ran "
            + "outside administrator middleware.");

    public string Email { get; private set; } = string.Empty;

    public bool MustEnrolSecondFactor { get; private set; }

    public void Set(Guid id, string email, bool mustEnrolSecondFactor)
    {
        if (_id is not null)
        {
            throw new InvalidOperationException("The administrator for this request is already resolved.");
        }

        _id = id;
        Email = email;
        MustEnrolSecondFactor = mustEnrolSecondFactor;
    }
}
