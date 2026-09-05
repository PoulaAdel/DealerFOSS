// Copyright (c) 2026 The DealerFOSS contributors.
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Overview: Purpose, File Design, and Engineering
//   JobContext and UnattendedJob — what a unit of background work must name
//   about itself before it is allowed to touch a dealership's data.
//
//   TWO TYPES, NOT ONE WITH A NULLABLE FIELD, and that is the entire design.
//   They open different kinds of scope, and the unattended one cannot reach a
//   service that authorizes against a person. If both kinds of work shared a
//   type, the scope could not differ, and "runs as nobody" would go back to
//   being a runtime discovery.
//
// Usage:
//   await using var scope = await factory.OpenAsync(
//       JobContext.RequestedBy(slug, job.RequestedByUserId, "csv import"), ct);
//
//   await using var sweep = await factory.OpenUnattendedAsync(
//       UnattendedJob.For(slug, "capture expiry"), ct);
//
// Coding Instructions:
//   THESE TYPES EXIST TO MAKE A COMMENT INTO A COMPILER ERROR. Before them, a
//   worker opened a tenant scope with a string and was trusted to remember to
//   set the caller afterwards. ImportWorker's own header said "it runs as the
//   person who asked" — which was true of ImportWorker and true of nothing
//   else, because nothing enforced it. The second background worker would have
//   inherited the sentence and not the behaviour.
//
//   There is deliberately NO constructor and NO factory taking a bare tenant
//   key. Both questions — which dealership, and on whose behalf — are answered
//   at the call site or the code does not build.
//
//   UnattendedJob is a real answer, not a loophole. Retention sweeps, expiry,
//   and the dispatcher that claims a queued job are genuinely nobody's request,
//   and pretending otherwise would attribute a machine's work to a person —
//   corrupting the audit trail this whole area exists to keep honest. What it
//   is not is a CHEAPER answer: it is longer to type, it costs a reason, it is
//   greppable, and the scope it opens can see almost nothing.
//
//   The reason is required on both and is not decoration. It is what a
//   maintainer reads in six months when a row is attributed to the system and
//   nobody remembers which sweep wrote it.

namespace DealerFOSS.Tenancy;

/// <summary>
/// Background work carried out on behalf of a person — a queued import, an
/// export they asked for. It runs with their permissions and is audited under
/// their name.
/// </summary>
/// <remarks>
/// Required by <see cref="ITenantScopeFactory.OpenAsync"/>. There is no factory
/// that omits the requester, so a worker cannot run anonymously through this
/// type; work that genuinely has no requester says so with
/// <see cref="UnattendedJob"/> and gets a scope that can see far less.
/// </remarks>
public sealed record JobContext
{
    private JobContext(string tenantKey, Guid requestedByUserId, string reason)
    {
        TenantKey = tenantKey;
        RequestedByUserId = requestedByUserId;
        Reason = reason;
    }

    /// <summary>The routing key of the dealer organization this work runs against.</summary>
    public string TenantKey { get; }

    /// <summary>
    /// The person whose request this work is carrying out. Never
    /// <see cref="Guid.Empty"/> — that is rejected at construction, because a
    /// default Guid is an anonymous job wearing a person's name.
    /// </summary>
    public Guid RequestedByUserId { get; }

    /// <summary>Why this work is running. Required, and short enough to log.</summary>
    public string Reason { get; }

    /// <summary>The only way to make one.</summary>
    public static JobContext RequestedBy(string tenantKey, Guid requestedByUserId, string reason)
    {
        if (requestedByUserId == Guid.Empty)
        {
            throw new ArgumentException(
                "An empty user id is not a requester. Use UnattendedJob when nobody asked.",
                nameof(requestedByUserId));
        }

        return new JobContext(JobNaming.Key(tenantKey), requestedByUserId, JobNaming.Why(reason));
    }

    public override string ToString() => $"{TenantKey}: {Reason} (for {RequestedByUserId})";
}

/// <summary>
/// Background work nobody asked for — a sweep, an expiry, the dispatcher that
/// claims a queued job. What it writes is attributed to the system rather than
/// to a person.
/// </summary>
/// <remarks>
/// Opens an <see cref="UnattendedScope"/>, which exposes only services marked
/// <see cref="DealerFOSS.Core.IUnattendedSafe"/>. A sweep therefore cannot
/// compile a call to a capability that authorizes against a caller, because
/// there is no caller for it to authorize against.
/// </remarks>
public sealed record UnattendedJob
{
    private UnattendedJob(string tenantKey, string reason)
    {
        TenantKey = tenantKey;
        Reason = reason;
    }

    /// <summary>The routing key of the dealer organization this work runs against.</summary>
    public string TenantKey { get; }

    /// <summary>Why this work is running, and the only record that it was intended.</summary>
    public string Reason { get; }

    /// <summary>The only way to make one.</summary>
    public static UnattendedJob For(string tenantKey, string reason) =>
        new(JobNaming.Key(tenantKey), JobNaming.Why(reason));

    public override string ToString() => $"{TenantKey}: {Reason} (unattended)";
}

/// <summary>The two questions both kinds of job must answer, answered the same way.</summary>
internal static class JobNaming
{
    internal static string Key(string tenantKey) =>
        string.IsNullOrWhiteSpace(tenantKey)
            ? throw new ArgumentException(
                "Background work must name the dealership it runs against.", nameof(tenantKey))
            : tenantKey.Trim();

    internal static string Why(string reason) =>
        string.IsNullOrWhiteSpace(reason)
            ? throw new ArgumentException(
                "Background work must say why it is running; the reason is read from an audit trail.",
                nameof(reason))
            : reason.Trim();
}
