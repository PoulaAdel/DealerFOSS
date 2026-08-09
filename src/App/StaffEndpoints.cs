// StaffEndpoints — who works here, and what they may reach.
//
// Use:  GET    /api/v1/staff                      the people whose access touches my rooftops
//       GET    /api/v1/staff/roles                every role and what it grants
//       GET    /api/v1/staff/{id}                 one colleague
//       POST   /api/v1/staff                      add a starter (no credential yet)
//       POST   /api/v1/staff/{id}/enrolment       mint their one-time code
//       POST   /api/v1/staff/{id}/assignments     grant a role at a rooftop, or organization-wide
//       DELETE /api/v1/staff/{id}/assignments/{a} take a grant away
//       POST   /api/v1/staff/{id}/active          stop a leaver, or let a returner back
//       POST   /api/v1/auth/enrol                 ANONYMOUS — redeem a code, set a password
// Edit: the permission is checked here rather than inside Identity, for the same
//       reason as SecurityEndpoints — Identity answers "what may this user
//       reach?" and must not also decide who may change the answer, which would
//       be circular.
//
//       The scoping rule is the important part and it is not uniform:
//       granting a role AT A ROOFTOP needs Staff.Manage at that rooftop, while
//       granting ORGANIZATION-WIDE access needs it organization-wide. Without
//       that split a single-lot manager could hand themselves the group. Stopping
//       an account is organization-wide too, because signing in is not a
//       per-rooftop thing.
//
//       /auth/enrol is reached by somebody who cannot sign in yet, so it is
//       anonymous and exempt from anti-forgery — like /auth/login, and for the
//       same reason: there is no session to have issued a token.

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using DealerFOSS.Core;
using DealerFOSS.Identity;

namespace DealerFOSS.App;

internal static class StaffEndpoints
{
    public static void MapStaff(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/staff").WithTags("Staff");

        group.MapGet("", ListAsync);
        group.MapGet("/roles", ListRolesAsync);
        group.MapGet("/{userId:guid}", GetAsync);
        group.MapPost("", AddAsync);
        group.MapPost("/{userId:guid}/enrolment", IssueEnrolmentAsync);

        // Its own permission, not Staff.Manage — see Permissions.StaffResetPassword.
        group.MapPost("/{userId:guid}/recovery", IssueRecoveryAsync);

        group.MapPost("/{userId:guid}/assignments", AssignAsync);
        group.MapDelete("/{userId:guid}/assignments/{assignmentId:guid}", UnassignAsync);
        group.MapPost("/{userId:guid}/active", SetActiveAsync);

        // Deliberately outside the group: no session, no permission, no tenant
        // user. Only a valid one-time code gets anything done here.
        app.MapPost("/api/v1/auth/enrol", RedeemAsync)
            .WithTags("Staff")
            .RequireRateLimiting(RateLimits.Credentials);

        // The three recovery routes, all anonymous for the same reason and all
        // rate limited with the other credential endpoints (ADR-018). Reading
        // the offered methods is limited too: it is anonymous and uncached, so
        // leaving it open is a free way to make the server work.
        app.MapGet("/api/v1/auth/recover", OfferedAsync)
            .WithTags("Staff")
            .RequireRateLimiting(RateLimits.Credentials);

        app.MapPost("/api/v1/auth/recover/authenticator", RecoverWithAuthenticatorAsync)
            .WithTags("Staff")
            .RequireRateLimiting(RateLimits.Credentials);

        app.MapPost("/api/v1/auth/recover/code", RecoverWithIssuedCodeAsync)
            .WithTags("Staff")
            .RequireRateLimiting(RateLimits.Credentials);
    }

    private static async Task<IResult> ListAsync(
        ICurrentUser currentUser,
        IAccessDirectory access,
        IStaffDirectory staff,
        CancellationToken cancellationToken)
    {
        var scope = await access.GetAuthorizedScopeAsync(
            currentUser.Id, Permissions.StaffRead, cancellationToken);

        if (scope.GrantsNothing)
        {
            return ReadRefused.ToProblem();
        }

        return Results.Ok(await staff.ListAsync(scope, cancellationToken));
    }

    private static async Task<IResult> ListRolesAsync(
        ICurrentUser currentUser,
        IAccessDirectory access,
        IStaffDirectory staff,
        CancellationToken cancellationToken)
    {
        var scope = await access.GetAuthorizedScopeAsync(
            currentUser.Id, Permissions.StaffRead, cancellationToken);

        if (scope.GrantsNothing)
        {
            return ReadRefused.ToProblem();
        }

        return Results.Ok(await staff.ListRolesAsync(cancellationToken));
    }

    private static async Task<IResult> GetAsync(
        Guid userId,
        ICurrentUser currentUser,
        IAccessDirectory access,
        IStaffDirectory staff,
        CancellationToken cancellationToken)
    {
        var scope = await access.GetAuthorizedScopeAsync(
            currentUser.Id, Permissions.StaffRead, cancellationToken);

        if (scope.GrantsNothing)
        {
            return ReadRefused.ToProblem();
        }

        var person = await staff.GetAsync(userId, cancellationToken);

        // Somebody outside the caller's scope answers exactly like somebody who
        // does not exist, so the list cannot be probed around.
        if (person is null || !VisibleTo(person, scope))
        {
            return StaffErrors.NotFound.ToProblem();
        }

        return Results.Ok(person);
    }

    private static async Task<IResult> AddAsync(
        NewStaffMember request,
        ICurrentUser currentUser,
        IAccessDirectory access,
        IStaffDirectory staff,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        // Adding a person to the organization is an organization-level act: the
        // account they get can then be granted anything.
        if (!await MayManageOrganizationAsync(currentUser, access, cancellationToken))
        {
            return OrganizationRefused.ToProblem();
        }

        var result = await staff.AddAsync(request, currentUser.Id, cancellationToken);

        return result.IsSuccess
            ? Results.Created($"/api/v1/staff/{result.Value.Id}", result.Value)
            : result.Error.ToProblem();
    }

    private static async Task<IResult> IssueEnrolmentAsync(
        Guid userId,
        ICurrentUser currentUser,
        IAccessDirectory access,
        IStaffDirectory staff,
        CancellationToken cancellationToken)
    {
        if (!await MayManageOrganizationAsync(currentUser, access, cancellationToken))
        {
            return OrganizationRefused.ToProblem();
        }

        var result = await staff.IssueEnrolmentCodeAsync(userId, currentUser.Id, cancellationToken);
        return result.IsSuccess ? Results.Ok(result.Value) : result.Error.ToProblem();
    }

    private static async Task<IResult> AssignAsync(
        Guid userId,
        AssignRoleRequest request,
        ICurrentUser currentUser,
        IAccessDirectory access,
        IStaffDirectory staff,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var scope = await access.GetAuthorizedScopeAsync(
            currentUser.Id, Permissions.StaffManage, cancellationToken);

        // The rule the whole endpoint exists to hold: you may only hand over
        // access you hold yourself, at the scope you hold it.
        if (request.RooftopId is { } rooftop)
        {
            if (!scope.Covers(new RooftopId(rooftop)))
            {
                return RooftopRefused.ToProblem();
            }
        }
        else if (!scope.IsOrganizationWide)
        {
            return OrganizationRefused.ToProblem();
        }

        var result = await staff.AssignAsync(
            userId,
            request.RoleId,
            request.RooftopId is { } id ? new RooftopId(id) : null,
            currentUser.Id,
            cancellationToken);

        return result.IsSuccess ? Results.NoContent() : result.Error.ToProblem();
    }

    private static async Task<IResult> UnassignAsync(
        Guid userId,
        Guid assignmentId,
        ICurrentUser currentUser,
        IAccessDirectory access,
        IStaffDirectory staff,
        CancellationToken cancellationToken)
    {
        var person = await staff.GetAsync(userId, cancellationToken);
        if (person is null)
        {
            return StaffErrors.NotFound.ToProblem();
        }

        var assignment = person.Assignments.SingleOrDefault(a => a.Id == assignmentId);
        if (assignment is null)
        {
            return StaffErrors.AssignmentNotFound.ToProblem();
        }

        var scope = await access.GetAuthorizedScopeAsync(
            currentUser.Id, Permissions.StaffManage, cancellationToken);

        // Symmetrical with granting: taking away organization-wide access is an
        // organization-level act, whoever is on the receiving end.
        if (assignment.IsOrganizationWide)
        {
            if (!scope.IsOrganizationWide)
            {
                return OrganizationRefused.ToProblem();
            }
        }
        else if (assignment.RooftopId is not { } rooftop || !scope.Covers(new RooftopId(rooftop)))
        {
            return RooftopRefused.ToProblem();
        }

        var result = await staff.UnassignAsync(userId, assignmentId, currentUser.Id, cancellationToken);
        return result.IsSuccess ? Results.NoContent() : result.Error.ToProblem();
    }

    private static async Task<IResult> SetActiveAsync(
        Guid userId,
        SetActiveRequest request,
        ICurrentUser currentUser,
        IAccessDirectory access,
        IStaffDirectory staff,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        // Signing in is not per-rooftop, so neither is taking it away. A one-lot
        // manager must not be able to lock somebody out of the whole group.
        if (!await MayManageOrganizationAsync(currentUser, access, cancellationToken))
        {
            return OrganizationRefused.ToProblem();
        }

        var result = await staff.SetActiveAsync(userId, request.Active, currentUser.Id, cancellationToken);
        return result.IsSuccess ? Results.NoContent() : result.Error.ToProblem();
    }

    /// <summary>
    /// Anonymous by necessity: the person redeeming a code has no password yet,
    /// so they cannot have a session. The code is the only thing authorizing this.
    /// </summary>
    private static async Task<IResult> RedeemAsync(
        EnrolRequest request,
        IStaffDirectory staff,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var result = await staff.RedeemEnrolmentCodeAsync(
            request.Email, request.Code, request.Password, cancellationToken);

        return result.IsSuccess ? Results.NoContent() : result.Error.ToProblem();
    }

    /// <summary>
    /// Mints the backstop code. Behind <c>Staff.ResetPassword</c> rather than
    /// <c>Staff.Manage</c>, because handing somebody the ability to sign in as an
    /// existing colleague is not the same act as fixing a rota.
    /// </summary>
    private static async Task<IResult> IssueRecoveryAsync(
        Guid userId,
        ICurrentUser currentUser,
        IAccessDirectory access,
        IStaffDirectory staff,
        CancellationToken cancellationToken)
    {
        var scope = await access.GetAuthorizedScopeAsync(
            currentUser.Id, Permissions.StaffResetPassword, cancellationToken);

        // Organization-wide, like stopping an account: signing in is not a
        // per-rooftop thing, so neither is taking it over.
        if (!scope.IsOrganizationWide)
        {
            return ResetRefused.ToProblem();
        }

        var result = await staff.IssueRecoveryCodeAsync(userId, currentUser.Id, cancellationToken);
        return result.IsSuccess ? Results.Ok(result.Value) : result.Error.ToProblem();
    }

    /// <summary>
    /// What this installation can offer somebody who is locked out. Takes no
    /// email and says nothing about any account — see IAccountRecovery.
    /// </summary>
    private static IResult OfferedAsync(IAccountRecovery recovery) => Results.Ok(recovery.Offered);

    /// <summary>
    /// Anonymous by necessity: the caller cannot sign in, which is the problem.
    /// The authenticator code is the only thing authorizing this.
    /// </summary>
    private static async Task<IResult> RecoverWithAuthenticatorAsync(
        RecoverRequest request,
        IAccountRecovery recovery,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var result = await recovery.ResetWithAuthenticatorAsync(
            request.Email, request.Code, request.Password, cancellationToken);

        return result.IsSuccess ? Results.NoContent() : result.Error.ToProblem();
    }

    private static async Task<IResult> RecoverWithIssuedCodeAsync(
        RecoverRequest request,
        IAccountRecovery recovery,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var result = await recovery.ResetWithIssuedCodeAsync(
            request.Email, request.Code, request.Password, cancellationToken);

        return result.IsSuccess ? Results.NoContent() : result.Error.ToProblem();
    }

    /// <summary>
    /// Whether this colleague's access touches anything the caller can see. An
    /// organization-wide person is visible to everyone, because they really can
    /// reach every caller's rooftop.
    /// </summary>
    private static bool VisibleTo(StaffMember person, AuthorizedScope scope) =>
        scope.IsOrganizationWide
        || person.Assignments.Any(a =>
            a.IsOrganizationWide
            || (a.RooftopId is { } rooftop && scope.Covers(new RooftopId(rooftop))));

    private static async Task<bool> MayManageOrganizationAsync(
        ICurrentUser currentUser,
        IAccessDirectory access,
        CancellationToken cancellationToken)
    {
        var scope = await access.GetAuthorizedScopeAsync(
            currentUser.Id, Permissions.StaffManage, cancellationToken);

        return scope.IsOrganizationWide;
    }

    private static Error ReadRefused { get; } = Error.Forbidden(
        "staff.read_forbidden",
        "You do not have access to the staff list.");

    private static Error OrganizationRefused { get; } = Error.Forbidden(
        "staff.organization_scope_required",
        "This needs organization-wide permission. Access covering one location is not enough.");

    /// <summary>
    /// Named apart from the general refusal because the answer is different:
    /// somebody may well be able to manage staff and still not be allowed to
    /// hand out a password reset, and a generic message would send them looking
    /// for the wrong permission.
    /// </summary>
    private static Error ResetRefused { get; } = Error.Forbidden(
        "staff.reset_forbidden",
        "Issuing a password reset needs the Staff.ResetPassword permission, organization-wide. Managing staff is not enough on its own.");

    private static Error RooftopRefused { get; } = Error.Forbidden(
        "staff.rooftop_forbidden",
        "You can only change access at a location you manage.");
}

/// <summary>Grant a role. A null rooftop means organization-wide.</summary>
internal sealed record AssignRoleRequest(Guid RoleId, Guid? RooftopId);

internal sealed record SetActiveRequest(bool Active);

/// <summary>Redeem a one-time code and set a password. No session involved.</summary>
internal sealed record EnrolRequest(string Email, string Code, string Password);

/// <summary>
/// Everything a reset needs, in one request. There is deliberately no separate
/// "prove" step issuing a ticket — see IAccountRecovery for why.
/// </summary>
internal sealed record RecoverRequest(string Email, string Code, string Password);
