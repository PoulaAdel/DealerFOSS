// Copyright (c) 2026 The DealerFOSS contributors.
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Overview: Purpose, File Design, and Engineering
//   Role — a named bundle of permissions. Users hold roles, never permissions
//   directly, so access can be reasoned about and revoked as a unit.
//
// Usage:
//   New Role(id, "Service Advisor"), then Grant(Permissions.X).
//
// Coding Instructions:
//   Grant deliberately rejects anything outside the Permissions catalogue.
//   Keep that check — it is what stops a typo becoming a silent non-grant.
//
//   The second-factor requirement lives here rather than on the user,
//   because "who must have one" is a statement about responsibility, not a
//   list somebody has to remember to update when staff change.

using DealerFOSS.Core;

namespace DealerFOSS.Identity;

/// <summary>
/// A named set of permissions. Users are granted a role at a scope; permissions
/// are never attached to a user directly, so access can be reasoned about and
/// revoked as a unit (doc 06 §3).
/// </summary>
internal sealed class Role : AuditableEntity
{
    private readonly List<RolePermission> _permissions = [];

    public Guid Id { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public IReadOnlyCollection<RolePermission> Permissions => _permissions;

    /// <summary>
    /// Whether holding this role obliges the user to have a second factor. Off
    /// by default: switching it on is a decision a dealer organization makes,
    /// not something that happens to them on an upgrade.
    /// </summary>
    public bool RequiresSecondFactor { get; private set; }

    private Role()
    {
    }

    public Role(Guid id, string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Role name is required.", nameof(name));
        }

        Id = id;
        Name = name;
    }

    /// <summary>
    /// Turns the second-factor obligation on or off for everyone holding this
    /// role. Nobody is locked out by it: a user who is required but not yet
    /// enrolled can still sign in, and can do nothing but enrol.
    /// </summary>
    public void RequireSecondFactor(bool required) => RequiresSecondFactor = required;

    public void Grant(string permission)
    {
        // Fully qualified: this type has a Permissions property of its own, and
        // the catalogue is the static class, not that collection.
        if (!DealerFOSS.Identity.Permissions.All.Contains(permission))
        {
            throw new ArgumentException(
                $"'{permission}' is not in the permission catalogue.", nameof(permission));
        }

        if (_permissions.Exists(p => p.Permission == permission))
        {
            return;
        }

        _permissions.Add(new RolePermission(Id, permission));
    }
}

/// <summary>Join record granting one catalogued permission to one role.</summary>
internal sealed class RolePermission
{
    public Guid RoleId { get; private set; }

    public string Permission { get; private set; } = string.Empty;

    private RolePermission()
    {
    }

    public RolePermission(Guid roleId, string permission)
    {
        RoleId = roleId;
        Permission = permission;
    }
}
