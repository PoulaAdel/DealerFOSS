// Permissions — the catalogue of actions the system can authorize, plus the
// levels an assignment can apply at.
//
// Use:  reference the constants; never invent a permission string at a call site.
// Edit: adding a permission means granting it to a role somewhere, or it can
//       never be held. Role.Grant rejects anything not listed here.

namespace OpenDealer360.Identity;

/// <summary>
/// The permission catalogue. Permissions are named centrally and granted to
/// roles; code never invents a permission string at a call site (doc 06 §3).
/// </summary>
public static class Permissions
{
    public const string OrganizationRead = "Organization.Read";
    public const string OrganizationManage = "Organization.Manage";

    public const string CustomersRead = "Customers.Read";
    public const string CustomersCreate = "Customers.Create";

    /// <summary>Reading vehicles. Organization-wide, like the vehicles themselves.</summary>
    public const string VehiclesRead = "Vehicles.Read";

    /// <summary>Seeing what is on a lot. Held per rooftop, or organization-wide.</summary>
    public const string InventoryRead = "Inventory.Read";

    /// <summary>Taking stock in, moving its status, and recording a vehicle.</summary>
    public const string InventoryManage = "Inventory.Manage";

    /// <summary>Seeing a lot's enquiries. Held per rooftop, or organization-wide.</summary>
    public const string LeadsRead = "Leads.Read";

    /// <summary>Capturing an enquiry, moving it on, and reassigning it.</summary>
    public const string LeadsManage = "Leads.Manage";

    /// <summary>Seeing a lot's deals.</summary>
    public const string DealsRead = "Deals.Read";

    /// <summary>Building a deal: starting one, pricing it, submitting it.</summary>
    public const string DealsWrite = "Deals.Write";

    /// <summary>
    /// Signing a deal off. Deliberately separate from writing one — a salesperson
    /// who can approve their own numbers is not a control at all.
    /// </summary>
    public const string DealsApprove = "Deals.Approve";

    /// <summary>Reading the ledger.</summary>
    public const string AccountingRead = "Accounting.Read";

    /// <summary>
    /// Putting something in the ledger, or reversing it. Held by whoever is
    /// accountable for the numbers — normally not the salesperson.
    /// </summary>
    public const string AccountingPost = "Accounting.Post";

    public static IReadOnlyCollection<string> All { get; } =
    [
        OrganizationRead,
        OrganizationManage,
        CustomersRead,
        CustomersCreate,
        VehiclesRead,
        InventoryRead,
        InventoryManage,
        LeadsRead,
        LeadsManage,
        DealsRead,
        DealsWrite,
        DealsApprove,
        AccountingRead,
        AccountingPost,
    ];
}

/// <summary>
/// The level a user assignment applies at. Organization-wide access is explicit
/// and separate — it is never inferred from holding many rooftops (doc 04 §5).
/// </summary>
internal enum AssignmentScope
{
    /// <summary>Applies to every rooftop in the dealer organization.</summary>
    Organization = 0,

    /// <summary>Applies to one named rooftop.</summary>
    Rooftop = 1,
}
