// StaffDirectoryService — the staff list, and the changes a manager makes to it.
//
// Use:  through IStaffDirectory; the application never touches User, Role, or
//       UserAssignment directly.
// Edit: three things here are load-bearing.
//
//       Nothing that leaves this class is a credential. The projections build
//       StaffMember by hand rather than mapping an entity, so a field added to
//       User later cannot leak by accident — somebody has to come here and write
//       it out.
//
//       A starter is created WITHOUT a password hash. User.CanSignIn already
//       reads "active and holds a credential", so an un-enrolled account is
//       refused at sign-in by machinery that already exists, rather than by a new
//       check somebody could forget.
//
//       The enrolment code is compared in constant time and answers identically
//       for an unknown email, a wrong code, and an expired one. Distinguishing
//       them turns "I have a code" into "I can find out whose codes are live".

using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using DealerFOSS.Core;

namespace DealerFOSS.Identity;

internal sealed class StaffDirectoryService(
    IdentityDb db,
    IPasswordHasher<User> passwordHasher,
    IAuditSink audit,
    IClock clock)
    : IStaffDirectory
{
    private readonly IdentityDb _db = db;
    private readonly IPasswordHasher<User> _passwordHasher = passwordHasher;
    private readonly IAuditSink _audit = audit;
    private readonly IClock _clock = clock;

    public async Task<IReadOnlyList<StaffMember>> ListAsync(
        AuthorizedScope scope,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(scope);

        if (scope.GrantsNothing)
        {
            return [];
        }

        var query = _db.Users
            .AsNoTracking()
            .Include(u => u.Assignments)
            .AsQueryable();

        if (!scope.IsOrganizationWide)
        {
            // Somebody with organization-wide access appears for every caller:
            // they can reach this rooftop, and a list that hid them would
            // misrepresent who has access to it.
            var allowed = scope.Rooftops.ToList();
            query = query.Where(u => u.Assignments.Any(a =>
                a.Scope == AssignmentScope.Organization
                || (a.RooftopId != null && allowed.Contains(a.RooftopId.Value))));
        }

        var users = await query
            .OrderBy(u => u.DisplayName)
            .ToListAsync(cancellationToken);

        return await DescribeAsync(users, cancellationToken);
    }

    public async Task<StaffMember?> GetAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await _db.Users
            .AsNoTracking()
            .Include(u => u.Assignments)
            .SingleOrDefaultAsync(u => u.Id == userId, cancellationToken);

        if (user is null)
        {
            return null;
        }

        return (await DescribeAsync([user], cancellationToken))[0];
    }

    public async Task<IReadOnlyList<StaffName>> NamesForAsync(
        IReadOnlyCollection<Guid> userIds,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(userIds);

        if (userIds.Count == 0)
        {
            return [];
        }

        var wanted = userIds.Distinct().ToList();

        return await _db.Users
            .AsNoTracking()
            .Where(u => wanted.Contains(u.Id))
            .Select(u => new StaffName(u.Id, u.DisplayName))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<StaffRole>> ListRolesAsync(CancellationToken cancellationToken)
    {
        var roles = await _db.Roles
            .AsNoTracking()
            .Include(r => r.Permissions)
            .OrderBy(r => r.Name)
            .ToListAsync(cancellationToken);

        return roles
            .Select(r => new StaffRole(
                r.Id,
                r.Name,
                r.RequiresSecondFactor,
                r.Permissions.Select(p => p.Permission).OrderBy(p => p, StringComparer.Ordinal).ToList()))
            .ToList();
    }

    public async Task<Result<StaffMember>> AddAsync(
        NewStaffMember person,
        Guid actingUserId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(person);

        var email = (person.Email ?? string.Empty).Trim().ToLowerInvariant();

        if (await _db.Users.AnyAsync(u => u.Email == email, cancellationToken))
        {
            return Result.Failure<StaffMember>(StaffErrors.EmailTaken);
        }

        User user;
        try
        {
            // No password hash. The account exists and cannot sign in — which is
            // exactly the state a starter should be in until they set their own.
            user = new User(Guid.NewGuid(), email, person.DisplayName);
        }
        catch (ArgumentException ex)
        {
            return Result.Failure<StaffMember>(Error.Validation("staff.invalid", ex.Message));
        }

        _db.Users.Add(user);
        await _db.SaveChangesAsync(cancellationToken);

        await _audit.RecordAsync(
            new AuditEntry(
                actingUserId, "Staff.Added", AuditOutcome.Allowed, "User", user.Id.ToString(), null,
                $"Added '{user.DisplayName}' ({user.Email}), awaiting enrolment.", null, null),
            cancellationToken);

        return Result.Success((await DescribeAsync([user], cancellationToken))[0]);
    }

    public async Task<Result<StaffEnrolmentCode>> IssueEnrolmentCodeAsync(
        Guid userId,
        Guid actingUserId,
        CancellationToken cancellationToken)
    {
        var user = await _db.Users.SingleOrDefaultAsync(u => u.Id == userId, cancellationToken);

        if (user is null)
        {
            return Result.Failure<StaffEnrolmentCode>(StaffErrors.NotFound);
        }

        // Resetting a forgotten password is a different feature with different
        // safeguards, and quietly allowing it here would be that feature without
        // any of them.
        if (user.PasswordHash is not null)
        {
            return Result.Failure<StaffEnrolmentCode>(StaffErrors.AlreadyEnrolled);
        }

        var now = _clock.UtcNow;

        // One live code per account. Two doubles the guessing surface and buys
        // nothing — a mislaid code is reissued, not kept as a spare.
        //
        // Belt-and-braces, and knowingly so: redemption already looks at the
        // NEWEST live code only, so an older one fails the hash comparison
        // anyway. Rehearsal confirmed no test fails if this loop is removed. It
        // stays because the newest-only lookup stops protecting once the newest
        // code expires — at which point an older, still-unconsumed row would
        // become the one found. Closing that here is cheaper than remembering it
        // the day a password-reset feature lands.
        var outstanding = await _db.StaffEnrolments
            .Where(e => e.UserId == userId && e.ConsumedAt == null)
            .ToListAsync(cancellationToken);

        foreach (var old in outstanding)
        {
            old.Supersede(now);
        }

        var code = NewCode();
        _db.StaffEnrolments.Add(new StaffEnrolment(Guid.NewGuid(), userId, Hash(code), actingUserId, now));
        await _db.SaveChangesAsync(cancellationToken);

        await _audit.RecordAsync(
            new AuditEntry(
                actingUserId, "Staff.EnrolmentCodeIssued", AuditOutcome.Allowed, "User", userId.ToString(), null,
                $"Enrolment code issued for '{user.DisplayName}'.", null, null),
            cancellationToken);

        return Result.Success(new StaffEnrolmentCode(code, now.Add(StaffEnrolment.Lifetime)));
    }

    public async Task<Result> RedeemEnrolmentCodeAsync(
        string email,
        string code,
        string newPassword,
        CancellationToken cancellationToken)
    {
        var normalized = (email ?? string.Empty).Trim().ToLowerInvariant();
        var now = _clock.UtcNow;

        var user = await _db.Users.SingleOrDefaultAsync(u => u.Email == normalized, cancellationToken);

        // Every failure below returns the same error. An attacker holding a code
        // must not be able to learn whose account it belongs to by watching which
        // refusal comes back.
        if (user is null || user.PasswordHash is not null || !user.IsActive)
        {
            return Result.Failure(StaffErrors.EnrolmentRefused);
        }

        if (string.IsNullOrWhiteSpace(newPassword) || newPassword.Length < 12)
        {
            return Result.Failure(Error.Validation(
                "staff.password_too_short",
                "A password needs at least 12 characters. Length is what makes one hard to guess."));
        }

        var enrolment = await _db.StaffEnrolments
            .Where(e => e.UserId == user.Id && e.ConsumedAt == null)
            .OrderByDescending(e => e.IssuedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (enrolment is null || !enrolment.IsUsableAt(now))
        {
            return Result.Failure(StaffErrors.EnrolmentRefused);
        }

        if (!FixedTimeEquals(enrolment.CodeHash, Hash(code ?? string.Empty)))
        {
            enrolment.RecordFailure();
            await _db.SaveChangesAsync(cancellationToken);
            return Result.Failure(StaffErrors.EnrolmentRefused);
        }

        user.SetPasswordHash(_passwordHasher.HashPassword(user, newPassword));
        enrolment.Consume(now);
        await _db.SaveChangesAsync(cancellationToken);

        // Recorded against the user themselves: nobody else was involved, and an
        // account gaining a credential is exactly the kind of event the trail
        // exists for.
        await _audit.RecordAsync(
            new AuditEntry(
                user.Id, "Staff.Enrolled", AuditOutcome.Allowed, "User", user.Id.ToString(), null,
                "Set their own password with an enrolment code.", null, null),
            cancellationToken);

        return Result.Success();
    }

    public async Task<Result> AssignAsync(
        Guid userId,
        Guid roleId,
        RooftopId? rooftopId,
        Guid actingUserId,
        CancellationToken cancellationToken)
    {
        var user = await _db.Users
            .Include(u => u.Assignments)
            .SingleOrDefaultAsync(u => u.Id == userId, cancellationToken);

        if (user is null)
        {
            return Result.Failure(StaffErrors.NotFound);
        }

        var role = await _db.Roles.AsNoTracking().SingleOrDefaultAsync(r => r.Id == roleId, cancellationToken);
        if (role is null)
        {
            return Result.Failure(StaffErrors.RoleNotFound);
        }

        // Granting what is already held changes nothing, and an audit row saying
        // it happened would be a lie.
        var alreadyHeld = user.Assignments.Any(a =>
            a.RoleId == roleId
            && (rooftopId is null
                ? a.Scope == AssignmentScope.Organization
                : a.Scope == AssignmentScope.Rooftop && a.RooftopId == rooftopId));

        if (alreadyHeld)
        {
            return Result.Success();
        }

        user.Assignments.Add(rooftopId is { } rooftop
            ? UserAssignment.ForRooftop(Guid.NewGuid(), userId, roleId, rooftop)
            : UserAssignment.ForOrganization(Guid.NewGuid(), userId, roleId));

        await _db.SaveChangesAsync(cancellationToken);

        await _audit.RecordAsync(
            new AuditEntry(
                actingUserId, "Staff.RoleAssigned", AuditOutcome.Allowed, "User", userId.ToString(),
                rooftopId?.Value,
                $"Granted '{role.Name}' to '{user.DisplayName}' {(rooftopId is null ? "organization-wide" : "at one rooftop")}.",
                null, null),
            cancellationToken);

        return Result.Success();
    }

    public async Task<Result> UnassignAsync(
        Guid userId,
        Guid assignmentId,
        Guid actingUserId,
        CancellationToken cancellationToken)
    {
        var user = await _db.Users
            .Include(u => u.Assignments)
            .SingleOrDefaultAsync(u => u.Id == userId, cancellationToken);

        if (user is null)
        {
            return Result.Failure(StaffErrors.NotFound);
        }

        var assignment = user.Assignments.SingleOrDefault(a => a.Id == assignmentId);
        if (assignment is null)
        {
            return Result.Failure(StaffErrors.AssignmentNotFound);
        }

        var rooftop = assignment.RooftopId?.Value;
        user.Assignments.Remove(assignment);
        await _db.SaveChangesAsync(cancellationToken);

        await _audit.RecordAsync(
            new AuditEntry(
                actingUserId, "Staff.RoleRemoved", AuditOutcome.Allowed, "User", userId.ToString(), rooftop,
                $"Took a grant away from '{user.DisplayName}'.", null, null),
            cancellationToken);

        return Result.Success();
    }

    public async Task<Result> SetActiveAsync(
        Guid userId,
        bool active,
        Guid actingUserId,
        CancellationToken cancellationToken)
    {
        // Locking yourself out is never the intent, and recovering from it needs
        // somebody else anyway.
        if (userId == actingUserId && !active)
        {
            return Result.Failure(StaffErrors.CannotStopYourself);
        }

        var user = await _db.Users.SingleOrDefaultAsync(u => u.Id == userId, cancellationToken);
        if (user is null)
        {
            return Result.Failure(StaffErrors.NotFound);
        }

        if (user.IsActive == active)
        {
            return Result.Success();
        }

        if (active)
        {
            user.Reactivate();
        }
        else
        {
            user.Deactivate();

            // A stopped account must stop now, not when its session would have
            // expired. Sessions are checked against the database on every
            // request, so revoking them here is what makes "stopped" immediate.
            await _db.Sessions
                .Where(s => s.UserId == userId && s.RevokedAt == null)
                .ExecuteUpdateAsync(set => set.SetProperty(s => s.RevokedAt, _clock.UtcNow), cancellationToken);
        }

        await _db.SaveChangesAsync(cancellationToken);

        await _audit.RecordAsync(
            new AuditEntry(
                actingUserId, active ? "Staff.Reactivated" : "Staff.Deactivated", AuditOutcome.Allowed,
                "User", userId.ToString(), null,
                active
                    ? $"'{user.DisplayName}' can sign in again."
                    : $"'{user.DisplayName}' stopped, and their sessions ended.",
                null, null),
            cancellationToken);

        return Result.Success();
    }

    /// <summary>
    /// Builds the view by hand rather than mapping the entity, so a field added to
    /// User later cannot cross this boundary unless somebody comes here and
    /// writes it out.
    /// </summary>
    private async Task<IReadOnlyList<StaffMember>> DescribeAsync(
        List<User> users,
        CancellationToken cancellationToken)
    {
        if (users.Count == 0)
        {
            return [];
        }

        var roleNames = await _db.Roles
            .AsNoTracking()
            .ToDictionaryAsync(r => r.Id, r => r.Name, cancellationToken);

        return users
            .Select(u => new StaffMember(
                u.Id,
                u.Email,
                u.DisplayName,
                u.IsActive,
                u.MfaEnabled,
                u.CanSignIn,
                AwaitingEnrolment: u.PasswordHash is null,
                u.Assignments
                    .Select(a => new StaffAssignment(
                        a.Id,
                        a.RoleId,
                        roleNames.TryGetValue(a.RoleId, out var name) ? name : "(role no longer exists)",
                        a.Scope == AssignmentScope.Organization,
                        a.RooftopId?.Value))
                    .OrderBy(a => a.RoleName, StringComparer.Ordinal)
                    .ToList()))
            .ToList();
    }

    /// <summary>
    /// Readable aloud and still hard to guess. Uppercase letters and digits with
    /// the shapes that get misheard removed (no O/0, I/1, S/5), because this code
    /// is spoken across a desk far more often than it is copied.
    /// </summary>
    private static string NewCode()
    {
        const string alphabet = "ABCDEFGHJKLMNPQRTUVWXYZ2346789";
        var chars = new char[12];

        for (var i = 0; i < chars.Length; i++)
        {
            chars[i] = alphabet[RandomNumberGenerator.GetInt32(alphabet.Length)];
        }

        // Grouped for reading out; the groups are cosmetic and are stripped on the
        // way back in.
        return $"{new string(chars, 0, 4)}-{new string(chars, 4, 4)}-{new string(chars, 8, 4)}";
    }

    private static string Hash(string code) =>
        Convert.ToHexString(SHA256.HashData(
            Encoding.UTF8.GetBytes(code.Replace("-", string.Empty, StringComparison.Ordinal).ToUpperInvariant())));

    private static bool FixedTimeEquals(string left, string right) =>
        CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(left), Encoding.UTF8.GetBytes(right));
}
