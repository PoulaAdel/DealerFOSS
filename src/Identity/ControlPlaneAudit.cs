// Copyright (c) 2026 The DealerFOSS contributors.
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Overview: Purpose, File Design, and Engineering
//   ControlPlaneAudit — the deployment's own append-only log.
//
// Usage:
//   Written by GlobalAdministrationService. Never updated or deleted;
//   ControlPlaneDb refuses both (ADR-016).
//
// Coding Instructions:
//   This is a separate table from a tenant's identity.AuditEvents on purpose.
//   A tenant's audit trail belongs to that dealership and travels with their
//   database; what an administrator did to the installation belongs to the
//   installation. Support access writes to both — the dealership must be able
//   to see somebody came in without being given the control plane's log.
//
//   Never record a credential, a token, or any dealership business data here.

namespace DealerFOSS.Identity;

internal sealed class ControlPlaneAudit
{
    public Guid Id { get; private set; }

    /// <summary>Null for a failed sign-in against an address that matches nobody.</summary>
    public Guid? AdministratorId { get; private set; }

    public string Action { get; private set; } = string.Empty;

    public string Outcome { get; private set; } = string.Empty;

    /// <summary>The dealership involved, by routing key, when there is one.</summary>
    public string? TenantSlug { get; private set; }

    public string? Reason { get; private set; }

    public DateTimeOffset OccurredAt { get; private set; }

    private ControlPlaneAudit()
    {
    }

    public ControlPlaneAudit(
        Guid? administratorId,
        string action,
        string outcome,
        string? tenantSlug,
        string? reason,
        DateTimeOffset now)
    {
        Id = Guid.NewGuid();
        AdministratorId = administratorId;
        Action = action;
        Outcome = outcome;
        TenantSlug = tenantSlug;
        Reason = reason;
        OccurredAt = now;
    }
}
