// IGlobalAdministration — the control plane's sign-in surface, and the only door
// between running the deployment and reading a dealership's data.
//
// Use:  SignInAsync at /api/v1/admin/login; ValidateAsync on every admin request;
//       GrantSupportAccessAsync when a dealership asks for help.
// Edit: notice what is NOT here. There is no method that turns an administrator
//       into a tenant caller, and no flag that widens an administrator session.
//       Support access mints a separate tenant session belonging to a separate
//       principal, with its own expiry and its own entry in the dealership's own
//       audit trail. Keep it that way: a flag on an existing session is a
//       privilege escalation with better manners.

using OpenDealer360.Core;

namespace OpenDealer360.Identity;

/// <summary>
/// Control-plane identity (doc 06 §2): the people who operate the installation.
/// They have no implicit access to any tenant's business data, and reach it only
/// through a deliberate, time-limited, audited support grant.
/// </summary>
public interface IGlobalAdministration
{
    /// <summary>
    /// Verifies an administrator's credentials. A second factor is mandatory, so
    /// the code is supplied here rather than in a second round trip; an account
    /// that has not enrolled yet gets a session restricted to enrolling.
    /// </summary>
    Task<Result<IssuedAdminSession>> SignInAsync(
        string email,
        string password,
        string? code,
        string? deviceSummary,
        CancellationToken cancellationToken);

    /// <summary>
    /// Resolves a session token to its administrator, sliding the idle window
    /// forward. A revoked, expired, or unknown token fails.
    /// </summary>
    Task<Result<AdministratorPrincipal>> ValidateAsync(string token, CancellationToken cancellationToken);

    /// <summary>
    /// Confirms the anti-forgery token presented with a control-plane write was
    /// issued to this very session. Same rule as the tenant side, separate store.
    /// </summary>
    Task<bool> VerifyAntiForgeryAsync(
        string sessionToken,
        string? antiForgeryToken,
        CancellationToken cancellationToken);

    /// <summary>Ends an administrator session immediately. Unknown tokens succeed silently.</summary>
    Task RevokeAsync(string token, CancellationToken cancellationToken);

    /// <summary>Generates a secret and returns what an authenticator app needs.</summary>
    Task<Result<MfaEnrolment>> BeginMfaEnrolmentAsync(
        Guid administratorId,
        CancellationToken cancellationToken);

    /// <summary>Turns the second factor on, once a working code has been produced.</summary>
    Task<Result> ConfirmMfaAsync(Guid administratorId, string code, CancellationToken cancellationToken);

    /// <summary>
    /// Opens a time-limited, audited way into the dealer organization resolved for
    /// this request, and returns a tenant session for it. Requires a written
    /// reason, and is refused to an administrator who has not enrolled a second
    /// factor.
    /// </summary>
    Task<Result<GrantedSupportAccess>> GrantSupportAccessAsync(
        Guid administratorId,
        string reason,
        TimeSpan requestedDuration,
        CancellationToken cancellationToken);

    /// <summary>Every grant ever made, newest first. The installation's own record.</summary>
    Task<IReadOnlyList<SupportAccessRecord>> ListSupportAccessAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Ends a grant now, revoking the tenant session with it. Ending the record
    /// and ending the access are one act on purpose.
    /// </summary>
    Task<Result> EndSupportAccessAsync(
        Guid grantId,
        Guid administratorId,
        CancellationToken cancellationToken);
}

/// <summary>
/// A newly started administrator session. Shaped like <see cref="IssuedSession"/>
/// and deliberately a different type: the two are not interchangeable, and the
/// compiler should say so.
/// </summary>
public sealed record IssuedAdminSession(
    string Token,
    string AntiForgeryToken,
    DateTimeOffset AbsoluteExpiresAt);

/// <summary>
/// Who is making the current control-plane request. Carries no tenant, no
/// rooftop, and no permission — an administrator holds none of the three.
/// </summary>
public sealed record AdministratorPrincipal(
    Guid Id,
    string Email,
    bool MustEnrolSecondFactor);

/// <summary>
/// The result of opening support access: a tenant session for the support
/// principal, and when it dies.
/// </summary>
public sealed record GrantedSupportAccess(
    Guid GrantId,
    string TenantSlug,
    string SessionToken,
    string AntiForgeryToken,
    DateTimeOffset ExpiresAt);

/// <summary>One entry in the installation's record of who went where, and why.</summary>
public sealed record SupportAccessRecord(
    Guid Id,
    Guid AdministratorId,
    string AdministratorEmail,
    string TenantSlug,
    string Reason,
    DateTimeOffset GrantedAt,
    DateTimeOffset ExpiresAt,
    DateTimeOffset? EndedAt,
    bool IsActive);

/// <summary>Stable failures for the control plane (doc 06 §6).</summary>
public static class AdminErrors
{
    /// <summary>
    /// One error for an unknown address, a wrong password, a wrong code, and a
    /// deactivated account alike. The caller cannot tell which.
    /// </summary>
    public static Error InvalidCredentials { get; } = Error.Forbidden(
        "admin.invalid_credentials",
        "Those administrator credentials are not valid.");

    public static Error SessionInvalid { get; } = Error.Forbidden(
        "admin.session_invalid",
        "Your administrator session has ended. Sign in again.");

    public static Error AntiForgeryFailed { get; } = Error.Forbidden(
        "admin.antiforgery_failed",
        "This change was not accompanied by a valid anti-forgery token for your session. "
        + "Reload the page and sign in again.");

    /// <summary>
    /// The session is real; it may do nothing but enrol. A control-plane account
    /// without a second factor is the single most valuable credential in the
    /// deployment, so this one is not the dealership's choice to make.
    /// </summary>
    public static Error SecondFactorRequired { get; } = Error.Forbidden(
        "admin.second_factor_required",
        "A second factor is required for administrator accounts. Set one up to continue: "
        + "POST /api/v1/admin/mfa/enrol, then confirm it with a code from your authenticator app.");

    public static Error MfaAlreadyOn { get; } = Error.Conflict(
        "admin.mfa_already_on",
        "A second factor is already set up on this administrator account.");

    public static Error SecondFactorRejected { get; } = Error.Forbidden(
        "admin.second_factor_rejected",
        "That code is not valid.");

    /// <summary>
    /// Named separately from a validation failure so the log distinguishes "asked
    /// badly" from "asked for something we do not allow".
    /// </summary>
    public static Error ReasonRequired { get; } = Error.Validation(
        "admin.support_reason_required",
        "Support access needs a written reason. Access nobody had to justify is not deliberate.");

    public static Error TenantRequired { get; } = Error.Validation(
        "admin.support_tenant_required",
        "Name the dealer organization to enter, using the X-Tenant header.");

    public static Error UnknownGrant { get; } = Error.NotFound(
        "admin.support_grant_unknown",
        "There is no support grant with that id.");

    /// <summary>
    /// Refused to a control-plane identity on a tenant endpoint. Not 401: the
    /// administrator session is perfectly valid, and it is still the wrong kind
    /// of identity for this request.
    /// </summary>
    public static Error NotATenantCaller { get; } = Error.Forbidden(
        "admin.not_a_tenant_caller",
        "An administrator session cannot read dealership data. Open support access first: "
        + "POST /api/v1/admin/support-access with a reason.");
}
