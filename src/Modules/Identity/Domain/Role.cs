// Role — a named bundle of permissions. Users hold roles, never permissions
// directly, so access can be reasoned about and revoked as a unit.
//
// Use:  new Role(id, "Service Advisor"), then Grant(Permissions.X).
// Edit: Grant deliberately rejects anything outside the Permissions catalogue.
//       Keep that check — it is what stops a typo becoming a silent non-grant.

using OpenDealer360.Core;

namespace OpenDealer360.Identity.Domain;

/// <summary>
/// A named set of permissions. Users are granted a role at a scope; permissions
/// are never attached to a user directly, so access can be reasoned about and
/// revoked as a unit (doc 06 §3).
/// </summary>
public sealed class Role : AuditableEntity
{
    private readonly List<RolePermission> _permissions = [];

    public Guid Id { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public IReadOnlyCollection<RolePermission> Permissions => _permissions;

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

    public void Grant(string permission)
    {
        if (!Domain.Permissions.All.Contains(permission))
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
public sealed class RolePermission
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
