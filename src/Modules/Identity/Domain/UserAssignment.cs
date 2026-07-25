using OpenDealer360.Core;

namespace OpenDealer360.Modules.Identity.Domain;

/// <summary>
/// Grants a user a role at a scope: either organization-wide, or at one named
/// rooftop. A user may hold different roles at different rooftops (doc 04 §1).
/// </summary>
public sealed class UserAssignment : AuditableEntity
{
    public Guid Id { get; private set; }

    public Guid UserId { get; private set; }

    public Guid RoleId { get; private set; }

    public AssignmentScope Scope { get; private set; }

    /// <summary>Set when <see cref="Scope"/> is <see cref="AssignmentScope.Rooftop"/>; otherwise null.</summary>
    public RooftopId? RooftopId { get; private set; }

    private UserAssignment()
    {
    }

    private UserAssignment(Guid id, Guid userId, Guid roleId, AssignmentScope scope, RooftopId? rooftopId)
    {
        Id = id;
        UserId = userId;
        RoleId = roleId;
        Scope = scope;
        RooftopId = rooftopId;
    }

    /// <summary>Grants the role across every rooftop in the organization.</summary>
    public static UserAssignment ForOrganization(Guid id, Guid userId, Guid roleId) =>
        new(id, userId, roleId, AssignmentScope.Organization, rooftopId: null);

    /// <summary>Grants the role at exactly one rooftop.</summary>
    public static UserAssignment ForRooftop(Guid id, Guid userId, Guid roleId, RooftopId rooftopId) =>
        new(id, userId, roleId, AssignmentScope.Rooftop, rooftopId);

    /// <summary>Whether this assignment covers the given rooftop.</summary>
    public bool Covers(RooftopId rooftopId) =>
        Scope == AssignmentScope.Organization || RooftopId == rooftopId;
}
