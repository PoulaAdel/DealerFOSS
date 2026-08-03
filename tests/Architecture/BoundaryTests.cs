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
using OpenDealer360.Core;
using OpenDealer360.Identity;
using Xunit;

namespace OpenDealer360.ArchitectureTests;

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
                "OpenDealer360.Identity",
                "OpenDealer360.App",
                "OpenDealer360.Data",
                "OpenDealer360.Tenancy",
                "OpenDealer360.Organization",
                "OpenDealer360.Customers",
                "OpenDealer360.Vehicles",
                "OpenDealer360.Inventory")
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
                "OpenDealer360.App",
                "OpenDealer360.Data",
                "OpenDealer360.Tenancy",
                "OpenDealer360.Organization",
                "OpenDealer360.Customers",
                "OpenDealer360.Vehicles",
                "OpenDealer360.Inventory")
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
