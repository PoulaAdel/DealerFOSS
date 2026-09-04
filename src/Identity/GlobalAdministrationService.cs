// Copyright (c) 2026 The DealerFOSS contributors.
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Overview: Purpose, File Design, and Engineering
//   GlobalAdministrationService — sign-in for the people who run the deployment,
//   and the one deliberate door from there into a dealership's data.
//
// Usage:
//   Through IGlobalAdministration; the application never touches control-plane
//   tables directly.
//
// Coding Instructions:
//   The same three rules that hold Authenticator together hold here — one
//   error for every credential failure, tokens hashed before they are stored
//   or compared, and a password verified even when the account does not exist.
//
//   The rule specific to this file: nothing here may return a tenant caller
//   for an administrator. GrantSupportAccessAsync mints a session for the
//   tenant's own support principal — a separate user row, in the dealership's
//   database, with read-only access, that no password can sign into. That is
//   what makes the access visible to the dealership in their own audit trail
//   and revocable by ending one session.

using DealerFOSS.Core;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Security.Cryptography;
using System.Text;

namespace DealerFOSS.Identity;

internal sealed class GlobalAdministrationService(
    ControlPlaneDb control,
    ITenantContext tenant,
    IClock clock,
    IPasswordHasher<Administrator> passwordHasher,
    ISecretProtector secretProtector,
    IServiceProvider services)
    : IGlobalAdministration
{
    /// <summary>Shown as the account issuer in an authenticator app.</summary>
    private const string Issuer = "DealerFOSS Administration";

    /// <summary>
    /// The support principal, the same id in every tenant so the dealership sees
    /// one recognisable account rather than a new one each visit. Deliberately
    /// not one of the development account ids.
    /// </summary>
    internal static Guid SupportUserId { get; } = new("00000000-0000-0000-0000-00000000f001");

    internal const string SupportUserEmail = "support@dealerfoss.invalid";

    internal const string SupportRoleName = "DealerFOSS Support";

    /// <summary>
    /// What support may do: look, and nothing else. Every permission here ends in
    /// Read. Adding a write permission to this list is a decision about what a
    /// vendor may do inside a customer's business, not a convenience — and the
    /// dealership cannot see the change, which is why it belongs in review.
    /// </summary>
    private static readonly string[] SupportPermissions =
    [
        Permissions.OrganizationRead,
        Permissions.CustomersRead,
        Permissions.VehiclesRead,
        Permissions.InventoryRead,
        Permissions.LeadsRead,
        Permissions.DealsRead,
        Permissions.AccountingRead,
    ];

    private static readonly string DecoyHash =
        new PasswordHasher<Administrator>().HashPassword(null!, "not-a-real-password");

    private readonly ControlPlaneDb _control = control;
    private readonly ITenantContext _tenant = tenant;
    private readonly IClock _clock = clock;
    private readonly IPasswordHasher<Administrator> _passwordHasher = passwordHasher;
    private readonly ISecretProtector _secretProtector = secretProtector;
    private readonly IServiceProvider _services = services;

    public async Task<Result<IssuedAdminSession>> SignInAsync(
        string email,
        string password,
        string? code,
        string? deviceSummary,
        CancellationToken cancellationToken)
    {
        var normalized = (email ?? string.Empty).Trim().ToLowerInvariant();
        var now = _clock.UtcNow;

        var administrator = await _control.Administrators
            .SingleOrDefaultAsync(a => a.Email == normalized, cancellationToken);

        if (administrator is null || !administrator.CanSignIn)
        {
            // Still do the work, so absence is not detectable by timing.
            _passwordHasher.VerifyHashedPassword(null!, DecoyHash, password ?? string.Empty);
            await RecordAsync(administrator?.Id, "Admin.SignIn", AuditOutcome.Denied,
                null, "Invalid credentials.", now, cancellationToken);
            return Result.Failure<IssuedAdminSession>(AdminErrors.InvalidCredentials);
        }

        var verification = _passwordHasher.VerifyHashedPassword(
            administrator, administrator.PasswordHash!, password ?? string.Empty);

        if (verification == PasswordVerificationResult.Failed)
        {
            await RecordAsync(administrator.Id, "Admin.SignIn", AuditOutcome.Denied,
                null, "Invalid credentials.", now, cancellationToken);
            return Result.Failure<IssuedAdminSession>(AdminErrors.InvalidCredentials);
        }

        if (verification == PasswordVerificationResult.SuccessRehashNeeded)
        {
            administrator.SetPasswordHash(_passwordHasher.HashPassword(administrator, password!));
        }

        // One round trip rather than a challenge exchange: a second factor is
        // mandatory here, so there is no branch where the password alone is the
        // whole answer and nothing for a challenge token to protect.
        if (administrator.MfaEnabled && !VerifyTotp(administrator, code, now))
        {
            await RecordAsync(administrator.Id, "Admin.SignIn", AuditOutcome.Denied,
                null, "Invalid second factor.", now, cancellationToken);
            return Result.Failure<IssuedAdminSession>(AdminErrors.InvalidCredentials);
        }

        var (token, tokenHash) = NewToken();
        var (antiForgeryToken, antiForgeryHash) = NewToken();

        var session = new AdminSession(
            Guid.NewGuid(), administrator.Id, tokenHash, antiForgeryHash, now, deviceSummary);

        _control.AdminSessions.Add(session);
        await RecordAsync(administrator.Id, "Admin.SignIn", AuditOutcome.Allowed,
            null, null, now, cancellationToken);

        return Result.Success(new IssuedAdminSession(
            token, antiForgeryToken, session.AbsoluteExpiresAt));
    }

    public async Task<Result<AdministratorPrincipal>> ValidateAsync(
        string token,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return Result.Failure<AdministratorPrincipal>(AdminErrors.SessionInvalid);
        }

        var hash = Hash(token);
        var session = await _control.AdminSessions
            .SingleOrDefaultAsync(s => s.TokenHash == hash, cancellationToken);

        var now = _clock.UtcNow;
        if (session is null || !session.IsActiveAt(now))
        {
            return Result.Failure<AdministratorPrincipal>(AdminErrors.SessionInvalid);
        }

        var administrator = await _control.Administrators
            .AsNoTracking()
            .SingleOrDefaultAsync(a => a.Id == session.AdministratorId && a.IsActive, cancellationToken);

        if (administrator is null)
        {
            return Result.Failure<AdministratorPrincipal>(AdminErrors.SessionInvalid);
        }

        session.Touch(now);
        await _control.SaveChangesAsync(cancellationToken);

        return Result.Success(new AdministratorPrincipal(
            administrator.Id,
            administrator.Email,
            MustEnrolSecondFactor: !administrator.MfaEnabled));
    }

    public async Task<bool> VerifyAntiForgeryAsync(
        string sessionToken,
        string? antiForgeryToken,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(sessionToken) || string.IsNullOrWhiteSpace(antiForgeryToken))
        {
            return false;
        }

        var sessionHash = Hash(sessionToken);
        var session = await _control.AdminSessions
            .AsNoTracking()
            .SingleOrDefaultAsync(s => s.TokenHash == sessionHash, cancellationToken);

        if (session is null || !session.IsActiveAt(_clock.UtcNow))
        {
            return false;
        }

        var presented = Encoding.UTF8.GetBytes(Hash(antiForgeryToken));
        var expected = Encoding.UTF8.GetBytes(session.AntiForgeryHash);

        if (CryptographicOperations.FixedTimeEquals(presented, expected))
        {
            return true;
        }

        await RecordAsync(session.AdministratorId, "Admin.AntiForgery", AuditOutcome.Denied, null,
            "Write refused: the anti-forgery token did not belong to this session.",
            _clock.UtcNow, cancellationToken);

        return false;
    }

    public async Task RevokeAsync(string token, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return;
        }

        var hash = Hash(token);
        var session = await _control.AdminSessions
            .SingleOrDefaultAsync(s => s.TokenHash == hash, cancellationToken);

        if (session is null)
        {
            return;
        }

        var now = _clock.UtcNow;
        session.Revoke(now);
        await RecordAsync(session.AdministratorId, "Admin.SignOut", AuditOutcome.Allowed,
            null, null, now, cancellationToken);
    }

    public async Task<Result<MfaEnrolment>> BeginMfaEnrolmentAsync(
        Guid administratorId,
        CancellationToken cancellationToken)
    {
        var administrator = await _control.Administrators
            .SingleOrDefaultAsync(a => a.Id == administratorId, cancellationToken);

        if (administrator is null)
        {
            return Result.Failure<MfaEnrolment>(AdminErrors.SessionInvalid);
        }

        if (administrator.MfaEnabled)
        {
            return Result.Failure<MfaEnrolment>(AdminErrors.MfaAlreadyOn);
        }

        var secret = Totp.NewSecret();
        administrator.BeginMfaEnrolment(_secretProtector.Protect(secret));
        await _control.SaveChangesAsync(cancellationToken);

        return Result.Success(new MfaEnrolment(
            secret, Totp.EnrolmentUri(secret, Issuer, administrator.Email)));
    }

    public async Task<Result> ConfirmMfaAsync(
        Guid administratorId,
        string code,
        CancellationToken cancellationToken)
    {
        var administrator = await _control.Administrators
            .SingleOrDefaultAsync(a => a.Id == administratorId, cancellationToken);

        if (administrator?.MfaSecretProtected is null)
        {
            return Result.Failure(AdminErrors.SecondFactorRejected);
        }

        var now = _clock.UtcNow;
        if (!VerifyTotp(administrator, code, now))
        {
            return Result.Failure(AdminErrors.SecondFactorRejected);
        }

        administrator.ConfirmMfa(now);
        await RecordAsync(administrator.Id, "Admin.MfaEnabled", AuditOutcome.Allowed,
            null, null, now, cancellationToken);

        return Result.Success();
    }

    public async Task<Result<GrantedSupportAccess>> GrantSupportAccessAsync(
        Guid administratorId,
        string reason,
        TimeSpan requestedDuration,
        CancellationToken cancellationToken)
    {
        if (!_tenant.IsResolved)
        {
            return Result.Failure<GrantedSupportAccess>(AdminErrors.TenantRequired);
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            return Result.Failure<GrantedSupportAccess>(AdminErrors.ReasonRequired);
        }

        var administrator = await _control.Administrators
            .AsNoTracking()
            .SingleOrDefaultAsync(a => a.Id == administratorId, cancellationToken);

        if (administrator is null)
        {
            return Result.Failure<GrantedSupportAccess>(AdminErrors.SessionInvalid);
        }

        var slug = _tenant.Current.Key;
        var now = _clock.UtcNow;
        var duration = SupportGrant.Clamp(requestedDuration);

        // Resolved here rather than injected: building it reaches the
        // tenant-bound connection, which does not exist on the control-plane
        // paths that have no X-Tenant header at all.
        var identity = _services.GetRequiredService<IdentityDb>();

        await EnsureSupportPrincipalAsync(identity, cancellationToken);

        var (token, tokenHash) = NewToken();
        var (antiForgeryToken, antiForgeryHash) = NewToken();

        var session = new Session(
            Guid.NewGuid(), SupportUserId, tokenHash, antiForgeryHash, now,
            deviceSummary: $"Support access ({administrator.Email})",
            lifetime: duration);

        identity.Sessions.Add(session);
        await identity.SaveChangesAsync(cancellationToken);

        // Written into the dealership's own trail, not only the installation's.
        // Somebody coming in from outside must be visible to the people whose
        // data it is, using the log they already read.
        var audit = _services.GetRequiredService<IAuditSink>();
        await audit.RecordAsync(
            new AuditEntry(
                SupportUserId,
                "Support.AccessOpened",
                AuditOutcome.Allowed,
                "Session",
                session.Id.ToString(),
                null,
                $"Support access opened by {administrator.Email} until {session.AbsoluteExpiresAt:u}. "
                + $"Stated reason: {reason.Trim()}",
                null,
                null),
            cancellationToken);

        var grant = new SupportGrant(
            Guid.NewGuid(), administrator.Id, slug, reason, session.Id, SupportUserId, now, duration);

        _control.SupportGrants.Add(grant);
        await RecordAsync(administrator.Id, "Admin.SupportAccessOpened", AuditOutcome.Allowed,
            slug, grant.Reason, now, cancellationToken);

        return Result.Success(new GrantedSupportAccess(
            grant.Id, slug, token, antiForgeryToken, grant.ExpiresAt));
    }

    public async Task<IReadOnlyList<SupportAccessRecord>> ListSupportAccessAsync(
        CancellationToken cancellationToken)
    {
        var now = _clock.UtcNow;

        var rows = await _control.SupportGrants
            .AsNoTracking()
            .OrderByDescending(g => g.GrantedAt)
            .Join(
                _control.Administrators.AsNoTracking(),
                grant => grant.AdministratorId,
                admin => admin.Id,
                (grant, admin) => new { grant, admin.Email })
            .Take(200)
            .ToListAsync(cancellationToken);

        return [.. rows.Select(r => new SupportAccessRecord(
            r.grant.Id,
            r.grant.AdministratorId,
            r.Email,
            r.grant.TenantSlug,
            r.grant.Reason,
            r.grant.GrantedAt,
            r.grant.ExpiresAt,
            r.grant.EndedAt,
            r.grant.IsActiveAt(now)))];
    }

    public async Task<Result> EndSupportAccessAsync(
        Guid grantId,
        Guid administratorId,
        CancellationToken cancellationToken)
    {
        var grant = await _control.SupportGrants
            .SingleOrDefaultAsync(g => g.Id == grantId, cancellationToken);

        if (grant is null)
        {
            return Result.Failure(AdminErrors.UnknownGrant);
        }

        var now = _clock.UtcNow;
        grant.End(now);

        // Ending the record and ending the access are one act. The session is in
        // the tenant's database, so this needs that tenant resolved — which is
        // why the endpoint asks for the X-Tenant header even to close a grant.
        if (_tenant.IsResolved && _tenant.Current.Key == grant.TenantSlug)
        {
            var identity = _services.GetRequiredService<IdentityDb>();
            var session = await identity.Sessions
                .SingleOrDefaultAsync(s => s.Id == grant.TenantSessionId, cancellationToken);

            if (session is not null)
            {
                session.Revoke(now);
                await identity.SaveChangesAsync(cancellationToken);
            }
        }

        await RecordAsync(administratorId, "Admin.SupportAccessClosed", AuditOutcome.Allowed,
            grant.TenantSlug, null, now, cancellationToken);

        return Result.Success();
    }

    /// <summary>
    /// Creates the tenant's support role, principal, and organization-wide
    /// assignment if they are missing, and does nothing if they are there.
    ///
    /// The principal is created with no password hash, so <c>User.CanSignIn</c> is
    /// false and no credential can ever open it — the only way to hold a session
    /// as this user is through a grant recorded above.
    /// </summary>
    private static async Task EnsureSupportPrincipalAsync(
        IdentityDb identity,
        CancellationToken cancellationToken)
    {
        var role = await identity.Roles
            .SingleOrDefaultAsync(r => r.Name == SupportRoleName, cancellationToken);

        if (role is null)
        {
            role = new Role(Guid.NewGuid(), SupportRoleName);
            identity.Roles.Add(role);
        }

        foreach (var permission in SupportPermissions)
        {
            role.Grant(permission);
        }

        var user = await identity.Users
            .SingleOrDefaultAsync(u => u.Id == SupportUserId, cancellationToken);

        if (user is null)
        {
            identity.Users.Add(new User(SupportUserId, SupportUserEmail, "DealerFOSS Support"));
        }

        var hasAssignment = await identity.UserAssignments
            .AnyAsync(a => a.UserId == SupportUserId && a.RoleId == role.Id, cancellationToken);

        if (!hasAssignment)
        {
            identity.UserAssignments.Add(
                UserAssignment.ForOrganization(Guid.NewGuid(), SupportUserId, role.Id));
        }

        await identity.SaveChangesAsync(cancellationToken);
    }

    private bool VerifyTotp(Administrator administrator, string? code, DateTimeOffset now)
    {
        if (administrator.MfaSecretProtected is null)
        {
            return false;
        }

        return Totp.Verify(_secretProtector.Unprotect(administrator.MfaSecretProtected), code, now);
    }

    /// <summary>
    /// Appends to the installation's own log and commits. Every path that changes
    /// a control-plane row goes through this, so no change lands unrecorded.
    /// </summary>
    private async Task RecordAsync(
        Guid? administratorId,
        string action,
        string outcome,
        string? tenantSlug,
        string? reason,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        _control.AuditEvents.Add(
            new ControlPlaneAudit(administratorId, action, outcome, tenantSlug, reason, now));

        await _control.SaveChangesAsync(cancellationToken);
    }

    private static (string Token, string Hash) NewToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        var token = Convert.ToBase64String(bytes);
        return (token, Hash(token));
    }

    private static string Hash(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
