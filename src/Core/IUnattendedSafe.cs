// Copyright (c) 2026 The DealerFOSS contributors.
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Overview: Purpose, File Design, and Engineering
//   IUnattendedSafe — a service a background job may use when nobody asked for
//   the work, so there is no person to authorize it against.
//
//   The alternative was a rule in a comment saying "do not use a
//   permission-checked service from a sweep". A rule like that is remembered
//   by whoever wrote it and by nobody else, and its failure mode is silent:
//   the sweep runs, the permission check finds no caller, and whether that
//   throws or quietly passes depends on how the service happened to be written.
//   Implementing an interface is something the compiler and the reviewer both
//   see. Same mechanism, and the same reasoning, as IAppendOnly.
//
// Usage:
//   Implement it on a service that does not authorize against a person.
//   UnattendedScope.Get<T>() is constrained to it, so a sweep can reach that
//   service and cannot compile a call to anything else.
//
// Coding Instructions:
//   THIS IS AN ALLOW-LIST, AND THAT DIRECTION IS THE WHOLE POINT. A service
//   without the marker is unreachable from an unattended job until somebody
//   deliberately adds it — so forgetting produces a COMPILE ERROR at the call
//   site, not a silent privilege. A deny-list would fail the other way: a new
//   service would be reachable by default, and forgetting to exclude it is
//   invisible.
//
//   Do not add this marker to make a compile error go away. The error is the
//   feature. If a sweep needs a permission-checked capability, the honest
//   answer is almost always that the work has a requester after all and should
//   say so with JobContext.RequestedBy.
//
//   What this marker does NOT claim: that the service is harmless. TenantDb
//   carries it, and TenantDb can write any table — attributed to "system",
//   under the append-only rules, and through domain constructors that still
//   enforce their own invariants. The guarantee is narrower and worth stating
//   exactly: an unattended job cannot use a service that authorizes against a
//   person, because there is no person for it to authorize against.

namespace DealerFOSS.Core;

/// <summary>
/// A service that is safe to resolve from a background job nobody asked for —
/// because it does not check permissions against a caller (doc 04 §5).
/// </summary>
/// <remarks>
/// Constrains <c>UnattendedScope.Get&lt;T&gt;()</c>. A capability that reads
/// <see cref="ICurrentUser"/>.<see cref="ICurrentUser.Id"/> must never carry
/// this marker: reading it throws when nobody is signed in, which is correct
/// behaviour and a runtime failure this interface exists to turn into a
/// compile-time one.
/// </remarks>
public interface IUnattendedSafe;
