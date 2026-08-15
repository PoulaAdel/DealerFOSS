// BoundaryTests — the dependency rules the compiler cannot express (ADR-014,
// ADR-017).
//
// Use:  runs with the normal test suite; a violation fails the build.
// Edit: add a rule whenever a boundary breach reaches code review — a breach a
//       human had to catch is a missing test. Rehearse new rules by breaking
//       them deliberately; see the README in this folder.
//
//       Two walls exist for two different reasons. Core and Identity are
//       separate PROJECTS, so the compiler enforces them and these tests only
//       confirm. The features inside App share one project, so these tests are
//       the only thing holding them apart — which is why the feature rules below
//       matter more than the project ones.

using System.Reflection;
using FluentAssertions;
using NetArchTest.Rules;
using DealerFOSS.Core;
using DealerFOSS.Identity;
using Xunit;

namespace DealerFOSS.ArchitectureTests;

public sealed class BoundaryTests
{
    private static readonly Assembly Core = typeof(Result).Assembly;
    private static readonly Assembly Identity = typeof(IAccessDirectory).Assembly;

    [Fact]
    public void Core_must_not_depend_on_web_or_persistence_frameworks()
    {
        var result = Types.InAssembly(Core)
            .Should()
            .NotHaveDependencyOnAny(
                "Microsoft.AspNetCore",
                "Microsoft.EntityFrameworkCore")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            because: "Core is a domain-free kernel; offenders: "
                + string.Join(", ", result.FailingTypeNames ?? []));
    }

    [Fact]
    public void Core_must_not_know_about_identity_or_any_feature()
    {
        // No business concept (Deal, RepairOrder, Rooftop entities, Journal, ...)
        // may live in or be referenced by Core.
        var result = Types.InAssembly(Core)
            .Should()
            .NotHaveDependencyOnAny(
                "DealerFOSS.Identity",
                "DealerFOSS.App",
                "DealerFOSS.Data",
                "DealerFOSS.Tenancy",
                "DealerFOSS.Organization",
                "DealerFOSS.Customers",
                "DealerFOSS.Vehicles",
                "DealerFOSS.Inventory")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            because: "Core sits below everything and knows none of it; offenders: "
                + string.Join(", ", result.FailingTypeNames ?? []));
    }

    [Fact]
    public void Identity_must_not_depend_on_the_application_or_any_feature()
    {
        // Identity answers access questions; it must not know what the caller is
        // trying to reach, or the dependency becomes circular. The project
        // reference direction already prevents this — this test says so out loud.
        var result = Types.InAssembly(Identity)
            .Should()
            .NotHaveDependencyOnAny(
                "DealerFOSS.App",
                "DealerFOSS.Data",
                "DealerFOSS.Tenancy",
                "DealerFOSS.Organization",
                "DealerFOSS.Customers",
                "DealerFOSS.Vehicles",
                "DealerFOSS.Inventory")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            because: "Identity sits below the application; offenders: "
                + string.Join(", ", result.FailingTypeNames ?? []));
    }

    [Fact]
    public void Identity_exposes_only_its_access_and_sign_in_contracts()
    {
        // The point of keeping Identity a separate project: no feature can write
        // a user row or an audit row except through these types (ADR-017). If a
        // new public type appears here, it was a security decision — make it
        // deliberately, then add it to this list.
        var allowed = new[]
        {
            "IAccessDirectory", "AuthorizedScope",
            "IAuthenticator", "IssuedSession", "AuthenticatedCaller", "AuthErrors",
            "SignInOutcome", "SecondFactorChallenge", "MfaEnrolment",
            // Added deliberately: a policy nobody can change is not a policy, so
            // the application needs a way to read and set which roles must hold
            // a second factor. It exposes the rule and the role names — not the
            // Role entity, not assignments, and no way to grant anything.
            "ISecurityPolicy", "RoleSecondFactorPolicy",
            // Added deliberately: control-plane identity. Password verification,
            // TOTP, and session issuance must exist in exactly one project — the
            // one nothing else can reach — so the administrator store lives here
            // too, and this is the only door to it. Note what is absent: no type
            // that turns an administrator into a tenant caller, and no way to
            // widen an administrator session. Support access mints a separate
            // session for a separate principal, and says so in the dealership's
            // own audit trail.
            "IGlobalAdministration", "IssuedAdminSession", "AdministratorPrincipal",
            "GrantedSupportAccess", "SupportAccessRecord", "AdminErrors",
            "IdentityRegistration", "IdentitySeeder", "DevelopmentAccount",
            "ControlPlaneSeeder",
            "Permissions",
            // Added deliberately: passkeys. Credential verification and session
            // issuance must stay in the one project nothing else can reach, so
            // this is the only door to them. Note what is absent — no public
            // key ever leaves, no challenge is readable after it is issued,
            // there is no way to register a credential against anybody but the
            // signed-in user, and there is no "does this credential exist"
            // question a caller can ask. Every refusal is one error, because
            // naming the failed check helps a forger more than a person.
            "IPasskeys", "PasskeyRegistrationChallenge", "PasskeyRegistrationResponse",
            "PasskeySignInChallenge", "PasskeySignInResponse", "RegisteredPasskey",
            "PasskeyErrors",
            // Added deliberately: a dealership must be able to see and manage its
            // own staff, and until now that was a developer's job. Note what this
            // contract does NOT carry, because that is what makes it safe to
            // export: no password hash, no TOTP secret, no session or recovery
            // code, and no way to edit the permission catalogue — assigning
            // grants an EXISTING role at an EXISTING scope. The enrolment code is
            // returned in plaintext exactly once and stored only as a hash, so
            // this surface never hands out a reusable credential.
            "IStaffDirectory", "StaffMember", "StaffAssignment", "StaffRole",
            "NewStaffMember", "StaffEnrolmentCode", "StaffErrors", "StaffName",
            // Added deliberately, and it is a security decision rather than a
            // convenience: this is the only unauthenticated surface that can
            // change a password, so widening Identity for it deserves the same
            // scrutiny as the sign-in door itself (ADR-018).
            //
            // Note what it CANNOT do, because that is what makes it safe to
            // export. It cannot tell a caller whether an account exists — the
            // methods it offers are a property of the installation and take no
            // email, and every failure is one indistinguishable error. It issues
            // no session and returns no token, so a successful reset still leaves
            // the caller having to sign in. It cannot mint the manager-issued
            // code; that stays behind Staff.ResetPassword on IStaffDirectory. And
            // it cannot reach an account that never had a password, which is
            // enrolment's job and has its own preconditions.
            "IAccountRecovery", "RecoveryMethods", "RecoveryErrors",
        };

        // Migration classes are generated artifacts and are public by design;
        // they describe the schema and grant no access to it.
        var actual = Identity.GetExportedTypes()
            .Where(t => t.Namespace?.Contains(".Migrations", StringComparison.Ordinal) != true)
            .Select(t => t.Name)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();

        actual.Should().BeSubsetOf(allowed,
            because: "everything else in Identity must stay internal");
    }
}
