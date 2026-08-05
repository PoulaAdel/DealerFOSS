// Permissions — the catalogue of actions the system can authorize, plus the
// levels an assignment can apply at.
//
// Use:  reference the constants; never invent a permission string at a call site.
// Edit: adding a permission means granting it to a role somewhere, or it can
//       never be held. Role.Grant rejects anything not listed here.

namespace DealerFOSS.Identity;

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

    /// <summary>Seeing a workshop's jobs. Held per rooftop, or organization-wide.</summary>
    public const string ServiceRead = "Service.Read";

    /// <summary>Booking a car in, writing up the work, and moving the job along.</summary>
    public const string ServiceWrite = "Service.Write";

    /// <summary>
    /// Recording that the customer agreed to pay for work found mid-job.
    /// Deliberately separate from writing it up — noticing that the discs are
    /// gone and having the conversation about paying for them are different acts,
    /// and only the second one may put money on a bill.
    ///
    /// Unlike Deals.Approve there is no ban on the same person doing both: in an
    /// independent workshop the advisor who spots the work is usually the one who
    /// telephones, and forbidding that would stop real shops working. The control
    /// is that the answer is a distinct, permissioned, timestamped act.
    /// </summary>
    public const string ServiceAuthorize = "Service.Authorize";

    /// <summary>Reading the ledger.</summary>
    public const string AccountingRead = "Accounting.Read";

    /// <summary>
    /// Putting something in the ledger. Anybody who can finish a transaction that
    /// posts — delivering a car, invoicing a repair order — needs it, because the
    /// business event and its entry commit together.
    /// </summary>
    public const string AccountingPost = "Accounting.Post";

    /// <summary>
    /// Undoing a posted entry by posting its opposite. Separate from
    /// <see cref="AccountingPost"/>, because this is the operation that can hide a
    /// mistake — the one act in the ledger a person might want to perform quietly.
    /// Whoever is accountable for the numbers holds it; the people who merely
    /// finish sales and jobs do not.
    /// </summary>
    public const string AccountingReverse = "Accounting.Reverse";

    /// <summary>
    /// Changing the security rules the organization applies to its own staff —
    /// today, which roles must hold a second factor. Held organization-wide by
    /// design: a rule about the whole dealership is not set from one lot.
    /// </summary>
    public const string SecurityManagePolicy = "Security.ManagePolicy";

    /// <summary>
    /// Bringing a dealership's existing records in from a file. Held
    /// organization-wide by design: an import writes across every rooftop at
    /// once, so a rooftop-scoped grant cannot express it, and treating a partial
    /// scope as sufficient would let a one-lot manager rewrite the group's
    /// customer list.
    /// </summary>
    public const string MigrationImport = "Migration.Import";

    /// <summary>
    /// Taking the dealership's records out as a file. Separate from importing
    /// because it is a different act — this is bulk personal data leaving the
    /// building (doc 06 §3), and somebody trusted to load a supplier's stock
    /// list is not automatically trusted to walk out with every customer the
    /// group has. Organization-wide, for the same reason importing is.
    /// </summary>
    public const string MigrationExport = "Migration.Export";

    public static IReadOnlyCollection<string> All { get; } =
    [
        SecurityManagePolicy,
        MigrationImport,
        MigrationExport,
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
        ServiceRead,
        ServiceWrite,
        ServiceAuthorize,
        AccountingRead,
        AccountingPost,
        AccountingReverse,
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
