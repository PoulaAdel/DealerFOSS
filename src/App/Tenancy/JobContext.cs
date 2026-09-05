// Copyright (c) 2026 The DealerFOSS contributors.
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Overview: Purpose, File Design, and Engineering
//   JobContext — what a unit of background work must name about itself before
//   it is allowed to touch a dealership's data: which dealership, and on whose
//   behalf.
//
// Usage:
//   await using var scope = await factory.OpenAsync(
//       JobContext.RequestedBy(slug, job.RequestedByUserId, "csv import"), ct);
//
//   await using var scope = await factory.OpenAsync(
//       JobContext.Unattended(slug, "capture expiry"), ct);
//
// Coding Instructions:
//   THIS TYPE EXISTS TO MAKE A COMMENT INTO A COMPILER ERROR. Before it, a
//   worker opened a tenant scope with a string and was trusted to remember to
//   set the caller afterwards. ImportWorker's own header said "it runs as the
//   person who asked" — which was true of ImportWorker and true of nothing
//   else, because nothing enforced it. The second background worker would have
//   inherited the sentence and not the behaviour.
//
//   There is deliberately NO constructor and NO overload taking a bare tenant
//   key. The only two ways to make one are the two named factories below, and
//   both make the answer to "who is this running as" part of the call.
//
//   Unattended is a real answer, not a loophole. Retention sweeps, expiry, and
//   the dispatcher that claims a queued job are genuinely nobody's request, and
//   pretending otherwise would attribute a machine's work to a person. What it
//   is not is a DEFAULT: it has to be typed, it costs a reason, and it is
//   greppable. Rows it writes are attributed to "system" (see AuditableEntity),
//   which is the honest record of nobody having asked.
//
//   The reason is required and is not decoration. It is what a maintainer reads
//   in six months when a row is attributed to the system and nobody remembers
//   which sweep wrote it.

namespace DealerFOSS.Tenancy;

/// <summary>
/// The identity of one unit of background work: the dealership it runs against,
/// and the person it runs as — or an explicit statement that nobody asked for it.
/// </summary>
/// <remarks>
/// Required by <see cref="ITenantScopeFactory.OpenAsync"/>, so a worker that
/// tries to reach tenant data without answering both questions does not compile
/// (doc 04 §5).
/// </remarks>
public sealed record JobContext
{
    private JobContext(string tenantKey, Guid? requestedByUserId, string reason)
    {
        TenantKey = tenantKey;
        RequestedByUserId = requestedByUserId;
        Reason = reason;
    }

    /// <summary>The routing key of the dealer organization this work runs against.</summary>
    public string TenantKey { get; }

    /// <summary>
    /// The person whose request this work is carrying out, or null when nobody
    /// asked. Never <see cref="Guid.Empty"/> — that is rejected at construction,
    /// because a default Guid is an anonymous job wearing a person's name.
    /// </summary>
    public Guid? RequestedByUserId { get; }

    /// <summary>Why this work is running. Required, and short enough to log.</summary>
    public string Reason { get; }

    /// <summary>True when nobody asked for this work.</summary>
    public bool IsUnattended => RequestedByUserId is null;

    /// <summary>
    /// Work carried out on behalf of a person — a queued import, an export they
    /// asked for. It runs with their permissions and is audited under their name.
    /// </summary>
    public static JobContext RequestedBy(string tenantKey, Guid requestedByUserId, string reason)
    {
        if (requestedByUserId == Guid.Empty)
        {
            throw new ArgumentException(
                "An empty user id is not a requester. Use JobContext.Unattended when nobody asked.",
                nameof(requestedByUserId));
        }

        return new JobContext(Key(tenantKey), requestedByUserId, Why(reason));
    }

    /// <summary>
    /// Work nobody asked for — a sweep, an expiry, the dispatcher that claims a
    /// queued job. Deliberately verbose to write, and what it writes is
    /// attributed to the system rather than to a person.
    /// </summary>
    public static JobContext Unattended(string tenantKey, string reason) =>
        new(Key(tenantKey), null, Why(reason));

    public override string ToString() =>
        IsUnattended
            ? $"{TenantKey}: {Reason} (unattended)"
            : $"{TenantKey}: {Reason} (for {RequestedByUserId})";

    private static string Key(string tenantKey) =>
        string.IsNullOrWhiteSpace(tenantKey)
            ? throw new ArgumentException(
                "Background work must name the dealership it runs against.", nameof(tenantKey))
            : tenantKey.Trim();

    private static string Why(string reason) =>
        string.IsNullOrWhiteSpace(reason)
            ? throw new ArgumentException(
                "Background work must say why it is running; the reason is read from an audit trail.",
                nameof(reason))
            : reason.Trim();
}
