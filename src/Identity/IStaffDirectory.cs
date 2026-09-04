// Copyright (c) 2026 The DealerFOSS contributors.
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Overview: Purpose, File Design, and Engineering
//   IStaffDirectory — seeing and managing the people who work for this dealer
//   organization.
//
// Usage:
//   List staff, see what a role grants, assign somebody to a rooftop, stop a
//   leaver, and add a starter who then sets their own password with a
//   one-time code. The caller checks the permission first — this contract
//   does what it is told and records who told it.
//
// Coding Instructions:
//   This is the FOURTH public surface of the Identity project and it was
//   opened deliberately (BoundaryTests names it). Two rules keep it safe:
//
//   It returns NO credential material. No password hash, no TOTP secret, no
//   session or recovery-code value ever crosses this boundary. What a
//   colleague may see is an identifier (email), an administrative fact
//   (active), and one actionable yes/no (holds a second factor). Notably
//   absent: when somebody last signed in — its honest use is spotting a
//   dormant account, but on a shared screen it answers "when was Dave
//   working", and the audit trail is the right place to look when there is a
//   reason to.
//
//   It cannot invent permissions. Roles are read-only here; assigning grants
//   an EXISTING role at an EXISTING scope. Editing the catalogue is not on
//   this contract, and adding it would be a different decision entirely.

using DealerFOSS.Core;

namespace DealerFOSS.Identity;

/// <summary>
/// The people who work for this dealer organization, and what they may reach.
/// Lives in the tenant's own database, so one organization's staff list is never
/// visible to another.
/// </summary>
public interface IStaffDirectory
{
    /// <summary>
    /// The people whose access touches any rooftop in <paramref name="scope"/>.
    /// An organization-wide colleague appears for every caller, because they can
    /// reach that caller's rooftop — hiding them would misrepresent who has
    /// access.
    /// </summary>
    Task<IReadOnlyList<StaffMember>> ListAsync(AuthorizedScope scope, CancellationToken cancellationToken);

    Task<StaffMember?> GetAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>
    /// Just the names, for a known set of ids, in one query. A work list showing
    /// who is chasing what needs to print a name, and that is not the same act as
    /// reading the staff directory — so this deliberately carries no email, no
    /// roles, and no security state, and needs no <c>Staff.Read</c>. Unknown ids
    /// are simply absent.
    /// </summary>
    Task<IReadOnlyList<StaffName>> NamesForAsync(
        IReadOnlyCollection<Guid> userIds,
        CancellationToken cancellationToken);

    /// <summary>Every role, and what holding it grants. Read-only.</summary>
    Task<IReadOnlyList<StaffRole>> ListRolesAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Creates an account that cannot yet sign in. A starter exists, can be
    /// assigned work, and holds no credential until they redeem an enrolment code
    /// — so no plaintext password is ever typed by, shown to, or remembered by
    /// somebody other than its owner.
    /// </summary>
    Task<Result<StaffMember>> AddAsync(
        NewStaffMember person,
        Guid actingUserId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Mints a single-use, short-lived code for somebody to set their own
    /// password with. Returned in plaintext exactly once — only its hash is
    /// stored, so a stolen database yields nothing and a lost code is reissued
    /// rather than recovered. Issuing a new one kills any code still outstanding.
    /// </summary>
    Task<Result<StaffEnrolmentCode>> IssueEnrolmentCodeAsync(
        Guid userId,
        Guid actingUserId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Mints a single-use code for somebody who ALREADY has a password and has
    /// forgotten it — the backstop of ADR-018, for a person who has lost their
    /// phone as well.
    /// </summary>
    /// <remarks>
    /// Separate from <see cref="IssueEnrolmentCodeAsync"/> in three ways that all
    /// matter. It needs <c>Staff.ResetPassword</c>, not <c>Staff.Manage</c>:
    /// handing over the ability to sign in as an existing person — possibly one
    /// more privileged than the issuer — is not the same act as fixing a rota. It
    /// requires the account to HAVE a password, where enrolment requires it not
    /// to. And the code it mints is marked for recovery, so it cannot be redeemed
    /// down the enrolment path and skip that check.
    ///
    /// The issue is visible on the staff record, not only in the audit trail: a
    /// dealership must be able to see that somebody handed out access.
    /// </remarks>
    Task<Result<StaffEnrolmentCode>> IssueRecoveryCodeAsync(
        Guid userId,
        Guid actingUserId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Exchanges a code for a password. Reached by somebody who cannot sign in
    /// yet, so it takes the email rather than a session — and answers identically
    /// whether the email is unknown, the code is wrong, or the code has expired,
    /// because distinguishing them tells an attacker which is which.
    /// </summary>
    Task<Result> RedeemEnrolmentCodeAsync(
        string email,
        string code,
        string newPassword,
        CancellationToken cancellationToken);

    /// <summary>
    /// Grants an existing role at one rooftop, or organization-wide when
    /// <paramref name="rooftopId"/> is null. Granting the same thing twice is not
    /// an error — it is already true.
    /// </summary>
    Task<Result> AssignAsync(
        Guid userId,
        Guid roleId,
        RooftopId? rooftopId,
        Guid actingUserId,
        CancellationToken cancellationToken);

    /// <summary>Takes one grant away. The person and their other grants remain.</summary>
    Task<Result> UnassignAsync(
        Guid userId,
        Guid assignmentId,
        Guid actingUserId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Stops a leaver signing in, or lets a returner back. Deliberately does not
    /// delete anything: their name still has to appear against the deals they did
    /// and the jobs they worked.
    /// </summary>
    Task<Result> SetActiveAsync(
        Guid userId,
        bool active,
        Guid actingUserId,
        CancellationToken cancellationToken);
}

/// <summary>
/// One colleague, as another colleague may see them. Carries no credential
/// material — see the note at the top of this file for what is deliberately
/// absent and why.
/// </summary>
public sealed record StaffMember(
    Guid Id,
    string Email,
    string DisplayName,
    bool IsActive,

    /// <summary>
    /// Whether they hold a second factor. A yes/no, not a secret — and the thing
    /// a manager needs once a role obliges one.
    /// </summary>
    bool HasSecondFactor,

    /// <summary>
    /// False for a starter who has not redeemed their code yet, as well as for a
    /// leaver. The screen must say which, because they need opposite actions.
    /// </summary>
    bool CanSignIn,

    /// <summary>True when they have never held a credential — a starter, not a leaver.</summary>
    bool AwaitingEnrolment,

    /// <summary>
    /// When a reset code was last handed out for this account and is still live,
    /// or null. On the record rather than only in the audit trail, because a
    /// dealership must be able to SEE that somebody handed out access without
    /// going looking for it (ADR-018).
    /// </summary>
    DateTimeOffset? RecoveryIssuedAt,
    IReadOnlyList<StaffAssignment> Assignments);

/// <summary>
/// A colleague's name against their id, and nothing else. The smallest thing that
/// answers "who is this?" — deliberately not a StaffMember, so printing a name
/// never drags a security state onto a screen that only needed a label.
/// </summary>
public sealed record StaffName(Guid Id, string DisplayName);

/// <summary>One grant: a role, at a rooftop or across the organization.</summary>
public sealed record StaffAssignment(
    Guid Id,
    Guid RoleId,
    string RoleName,
    bool IsOrganizationWide,
    Guid? RooftopId);

/// <summary>
/// A role and what it grants, so somebody deciding whether to hand it over can
/// see what they are handing over rather than guessing from the name.
/// </summary>
public sealed record StaffRole(
    Guid Id,
    string Name,
    bool RequiresSecondFactor,
    IReadOnlyList<string> Permissions);

public sealed record NewStaffMember(string Email, string DisplayName);

/// <summary>
/// A one-time code, returned in plaintext exactly once. Show it, let it be read
/// out, and do not store it anywhere — it cannot be retrieved again.
/// </summary>
public sealed record StaffEnrolmentCode(string Code, DateTimeOffset ExpiresAt);

/// <summary>Stable error codes for the staff directory (doc 06 §6).</summary>
public static class StaffErrors
{
    public static Error NotFound { get; } = Error.NotFound(
        "staff.not_found",
        "There is nobody here by that id.");

    public static Error EmailTaken { get; } = Error.Conflict(
        "staff.email_taken",
        "Somebody here already uses that email address.");

    public static Error RoleNotFound { get; } = Error.NotFound(
        "staff.role_not_found",
        "That is not a role in this dealership.");

    public static Error AssignmentNotFound { get; } = Error.NotFound(
        "staff.assignment_not_found",
        "That grant is not held by this person.");

    /// <summary>
    /// Deliberately identical for an unknown email, a wrong code, and an expired
    /// one. Telling somebody which of the three it was is telling them how to
    /// make progress.
    /// </summary>
    public static Error EnrolmentRefused { get; } = Error.Validation(
        "staff.enrolment_refused",
        "That code is not usable. Ask for a new one.");

    public static Error AlreadyEnrolled { get; } = Error.Conflict(
        "staff.already_enrolled",
        "That account already has a password. Somebody who has forgotten theirs needs a reset code instead.");

    /// <summary>
    /// The mirror of <see cref="AlreadyEnrolled"/>. Nothing to recover on an
    /// account that never had a password — that person needs a starter code, and
    /// quietly issuing one here would be enrolment under a permission meant for
    /// something else.
    /// </summary>
    public static Error NothingToRecover { get; } = Error.Conflict(
        "staff.nothing_to_recover",
        "That account has never had a password. Issue a starter code instead.");

    public static Error CannotStopYourself { get; } = Error.Validation(
        "staff.cannot_stop_yourself",
        "You cannot deactivate your own account. Ask a colleague.");
}
