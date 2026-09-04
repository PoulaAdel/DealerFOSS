// Copyright (c) 2026 The DealerFOSS contributors.
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Overview: Purpose, File Design, and Engineering
//   Who is making the current request. Like ITenantContext, a holder rather
//   than a resolver, and the type every capability hands to the access
//   directory when it asks whether a caller may see something.
//
//   That makes it a load-bearing part of the security model rather than a
//   convenience. Control-plane identities NEVER reach it: an administrator has
//   their own middleware, cookie and store, so an administrator cookie on a
//   business endpoint resolves to nobody and is refused before any endpoint
//   runs. An architecture test fails the build if anything in the control plane
//   so much as references this type.
//
// Usage:
//   Inject ICurrentUser, read Id.
//
// Coding Instructions:
//   NEVER accept a user id as a parameter from a client. That would let a
//   caller act as somebody else, and it is the single easiest way to undo every
//   permission check in the product.
//
//   Resolution lives in src/App/Tenancy/CurrentUserMiddleware.cs. Background
//   work sets it from the requester of the job, so the work is authorized by
//   their permissions and audited under their name.

namespace DealerFOSS.Core;

/// <summary>
/// The authenticated caller for the current request. Resolved once, before any
/// endpoint runs, from the session (doc 06 §2). Endpoints and services never
/// take a user id as a parameter from the client — that would let a caller act
/// as someone else.
/// </summary>
public interface ICurrentUser
{
    bool IsAuthenticated { get; }

    /// <summary>The caller's id. Throws when the request is unauthenticated.</summary>
    Guid Id { get; }

    /// <summary>
    /// Whether this caller is signed in but owes their organization a second
    /// factor. Such a caller may do nothing except set one up; the refusal is
    /// enforced before any endpoint runs, not by each endpoint remembering.
    /// </summary>
    bool MustEnrolSecondFactor { get; }

    void Set(Guid userId, bool mustEnrolSecondFactor = false);
}

/// <summary>Scoped, write-once holder of the caller for the current request.</summary>
public sealed class CurrentUser : ICurrentUser
{
    private Guid? _id;

    public bool IsAuthenticated => _id is not null;

    public Guid Id => _id
        ?? throw new InvalidOperationException(
            "No user is resolved for this request. An authorized operation ran outside authentication.");

    public bool MustEnrolSecondFactor { get; private set; }

    public void Set(Guid userId, bool mustEnrolSecondFactor = false)
    {
        if (_id is not null)
        {
            throw new InvalidOperationException("The user for this request is already resolved.");
        }

        _id = userId;
        MustEnrolSecondFactor = mustEnrolSecondFactor;
    }
}
