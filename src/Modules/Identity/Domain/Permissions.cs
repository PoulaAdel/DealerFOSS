namespace OpenDealer360.Modules.Identity.Domain;

/// <summary>
/// The permission catalogue. Permissions are named centrally and granted to
/// roles; code never invents a permission string at a call site (doc 06 §3).
/// </summary>
public static class Permissions
{
    public const string OrganizationRead = "Organization.Read";
    public const string OrganizationManage = "Organization.Manage";

    public static IReadOnlyCollection<string> All { get; } =
    [
        OrganizationRead,
        OrganizationManage,
    ];
}

/// <summary>
/// The level a user assignment applies at. Organization-wide access is explicit
/// and separate — it is never inferred from holding many rooftops (doc 04 §5).
/// </summary>
public enum AssignmentScope
{
    /// <summary>Applies to every rooftop in the dealer organization.</summary>
    Organization = 0,

    /// <summary>Applies to one named rooftop.</summary>
    Rooftop = 1,
}
