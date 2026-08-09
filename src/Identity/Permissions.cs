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
    /// Opening a month and closing it at the end of the close work. Held
    /// organization-wide: the books close as a whole, not one lot at a time.
    /// </summary>
    public const string AccountingClosePeriod = "Accounting.ClosePeriod";

    /// <summary>
    /// Unlocking a month that was already closed.
    ///
    /// Deliberately separate from closing. Closing is routine month-end work;
    /// reopening lets a figure somebody has already reported move, so it should
    /// be holdable by fewer people than the job that closed it in the first
    /// place. Every reopen needs a written reason and is kept in the period's
    /// history.
    /// </summary>
    public const string AccountingReopenPeriod = "Accounting.ReopenPeriod";

    /// <summary>
    /// Adding, repricing, and withdrawing F&amp;I products. Organization-wide: a
    /// provider arrangement is made for the group, and one lot inventing its own
    /// version of the same warranty is how a catalogue stops being trustworthy.
    ///
    /// Note there is no matching read permission — seeing the catalogue rides on
    /// <see cref="DealsRead"/>, because anybody building a deal needs to know what
    /// may go on it.
    /// </summary>
    public const string FinanceManageProducts = "Finance.ManageProducts";

    /// <summary>Seeing the parts catalogue and what is on the shelf.</summary>
    public const string PartsRead = "Parts.Read";

    /// <summary>
    /// Booking stock in, and — organization-wide only — adding a part to the
    /// catalogue or changing how parts are costed. A part number means the same
    /// thing at every location, and how the group values its stock is not a
    /// decision one lot makes.
    /// </summary>
    public const string PartsManage = "Parts.Manage";

    /// <summary>
    /// Seeing who works here and what they may reach. Held per rooftop, or
    /// organization-wide: a one-lot manager sees the people whose access touches
    /// their lot, which includes anybody organization-wide, because those people
    /// really can reach it.
    /// </summary>
    public const string StaffRead = "Staff.Read";

    /// <summary>
    /// Adding a starter, granting and removing roles, and stopping a leaver.
    ///
    /// Checked at the scope of the grant being made, not at "somewhere": handing
    /// somebody a role at one rooftop needs this permission at that rooftop, and
    /// handing them organization-wide access needs it organization-wide. Without
    /// that split, a single-lot manager could grant themselves the group.
    /// Stopping an account is organization-wide too — signing in is not a
    /// per-rooftop thing, so neither is taking it away.
    /// </summary>
    public const string StaffManage = "Staff.Manage";

    /// <summary>
    /// Handing somebody a code that lets them set a NEW password on an account
    /// that already has one.
    ///
    /// Deliberately not part of <see cref="StaffManage"/> (ADR-018). Adding a
    /// starter and stopping a leaver are administrative acts; issuing a reset is
    /// handing over the ability to sign in AS an existing person, including a
    /// person more privileged than the issuer. Somebody trusted to fix a rota is
    /// not automatically trusted with that, and folding the two together would
    /// make the distinction unexpressible.
    ///
    /// Organization-wide, for the same reason stopping an account is: signing in
    /// is not a per-rooftop thing, so neither is taking it over.
    /// </summary>
    public const string StaffResetPassword = "Staff.ResetPassword";

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
        StaffRead,
        StaffManage,
        StaffResetPassword,
        PartsRead,
        PartsManage,
        AccountingClosePeriod,
        AccountingReopenPeriod,
        FinanceManageProducts,
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
