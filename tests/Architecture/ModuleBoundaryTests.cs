using System.Reflection;
using FluentAssertions;
using NetArchTest.Rules;
using OpenDealer360.Modules.Organization.Domain;
using OpenDealer360.Platform.Persistence.HostCatalog;
using Xunit;

namespace OpenDealer360.ArchitectureTests;

/// <summary>
/// Boundary rules for the persistence infrastructure and the first business
/// module (ADR-014, doc 03 §5). These grow as modules land; the pattern is the
/// point — a forbidden reference fails the build, not a review.
/// </summary>
public sealed class ModuleBoundaryTests
{
    private static readonly Assembly Organization = typeof(DealerOrganization).Assembly;
    private static readonly Assembly PlatformPersistence = typeof(HostCatalogDbContext).Assembly;

    [Fact]
    public void Organization_domain_must_not_depend_on_ef_or_aspnetcore()
    {
        // Domain holds entities and rules only. EF mapping lives in Data;
        // HTTP lives in Endpoints (doc 03 §3).
        var result = Types.InAssembly(Organization)
            .That().ResideInNamespace("OpenDealer360.Modules.Organization.Domain")
            .Should().NotHaveDependencyOnAny("Microsoft.EntityFrameworkCore", "Microsoft.AspNetCore")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            because: "domain must stay free of infrastructure; offenders: "
                + string.Join(", ", result.FailingTypeNames ?? []));
    }

    [Fact]
    public void Organization_must_not_depend_on_the_host()
    {
        var result = Types.InAssembly(Organization)
            .Should().NotHaveDependencyOn("OpenDealer360.Host")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            because: "a module never references the composition host; offenders: "
                + string.Join(", ", result.FailingTypeNames ?? []));
    }

    [Fact]
    public void Platform_persistence_must_not_depend_on_aspnetcore()
    {
        // Web concerns (middleware, HttpContext) live in Host, not in the
        // persistence infrastructure project.
        var result = Types.InAssembly(PlatformPersistence)
            .Should().NotHaveDependencyOn("Microsoft.AspNetCore")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            because: "persistence infrastructure is web-agnostic; offenders: "
                + string.Join(", ", result.FailingTypeNames ?? []));
    }
}
