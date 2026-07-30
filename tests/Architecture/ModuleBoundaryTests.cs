// ModuleBoundaryTests — rules about what modules may reference (ADR-008, ADR-014).
//
// Use:  runs with the normal test suite; a violation fails the build.
// Edit: every new module needs its own rules here — domain free of EF and
//       ASP.NET, no reference to the Host, and other modules reachable only
//       through their Contracts namespace.

using System.Reflection;
using FluentAssertions;
using NetArchTest.Rules;
using OpenDealer360.Identity.Contracts;
using OpenDealer360.Customers.Domain;
using OpenDealer360.Organization.Domain;
using OpenDealer360.Vehicles.Domain;
using OpenDealer360.Tenancy;
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
    private static readonly Assembly Identity = typeof(IAccessDirectory).Assembly;
    private static readonly Assembly Customers = typeof(Customer).Assembly;
    private static readonly Assembly Vehicles = typeof(Vehicle).Assembly;
    private static readonly Assembly TenancyAssembly = typeof(HostCatalogDbContext).Assembly;

    [Fact]
    public void Organization_domain_must_not_depend_on_ef_or_aspnetcore()
    {
        // Domain holds entities and rules only. EF mapping lives in Data;
        // HTTP lives in Endpoints (doc 03 §3).
        var result = Types.InAssembly(Organization)
            .That().ResideInNamespace("OpenDealer360.Organization.Domain")
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
    public void Organization_may_reach_identity_only_through_its_contracts()
    {
        // Cross-module access goes through the published contract (ADR-008).
        // Reaching Identity's Domain, Data, or service types would couple the
        // two modules and make authorization rules impossible to reason about.
        var result = Types.InAssembly(Organization)
            .Should()
            .NotHaveDependencyOnAny(
                "OpenDealer360.Identity.Domain",
                "OpenDealer360.Identity.Data")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            because: "Identity is reachable only via its Contracts namespace; offenders: "
                + string.Join(", ", result.FailingTypeNames ?? []));
    }

    [Fact]
    public void Identity_must_not_depend_on_other_business_modules()
    {
        // Identity answers access questions; it must not know what the caller
        // is trying to reach, or the dependency becomes circular.
        var result = Types.InAssembly(Identity)
            .Should()
            .NotHaveDependencyOnAny("OpenDealer360.Organization", "OpenDealer360.Host")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            because: "Identity sits below business modules; offenders: "
                + string.Join(", ", result.FailingTypeNames ?? []));
    }

    [Fact]
    public void Customers_domain_must_not_depend_on_ef_or_aspnetcore()
    {
        var result = Types.InAssembly(Customers)
            .That().ResideInNamespace("OpenDealer360.Customers.Domain")
            .Should().NotHaveDependencyOnAny("Microsoft.EntityFrameworkCore", "Microsoft.AspNetCore")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            because: "domain must stay free of infrastructure; offenders: "
                + string.Join(", ", result.FailingTypeNames ?? []));
    }

    [Fact]
    public void Customers_must_not_depend_on_the_host_or_a_sibling_module()
    {
        // Sales and Service will depend on Customers later; it must not reach
        // back, or the dependency becomes circular.
        var result = Types.InAssembly(Customers)
            .Should()
            .NotHaveDependencyOnAny("OpenDealer360.Host", "OpenDealer360.Organization")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            because: "a module never references the host or a sibling's internals; offenders: "
                + string.Join(", ", result.FailingTypeNames ?? []));
    }

    [Fact]
    public void Customers_may_reach_identity_only_through_its_contracts()
    {
        var result = Types.InAssembly(Customers)
            .Should()
            .NotHaveDependencyOnAny("OpenDealer360.Identity.Domain", "OpenDealer360.Identity.Data")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            because: "Identity is reachable only via its Contracts namespace; offenders: "
                + string.Join(", ", result.FailingTypeNames ?? []));
    }

    [Fact]
    public void Vehicles_domain_must_not_depend_on_ef_or_aspnetcore()
    {
        var result = Types.InAssembly(Vehicles)
            .That().ResideInNamespace("OpenDealer360.Vehicles.Domain")
            .Should().NotHaveDependencyOnAny("Microsoft.EntityFrameworkCore", "Microsoft.AspNetCore")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            because: "domain must stay free of infrastructure; offenders: "
                + string.Join(", ", result.FailingTypeNames ?? []));
    }

    [Fact]
    public void Vehicles_must_not_depend_on_the_host_or_a_sibling_module()
    {
        // Sales will depend on Vehicles to price and sell a unit; a reference
        // back would make the dependency circular.
        var result = Types.InAssembly(Vehicles)
            .Should()
            .NotHaveDependencyOnAny(
                "OpenDealer360.Host",
                "OpenDealer360.Organization",
                "OpenDealer360.Customers")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            because: "a module never references the host or a sibling's internals; offenders: "
                + string.Join(", ", result.FailingTypeNames ?? []));
    }

    [Fact]
    public void Vehicles_may_reach_identity_only_through_its_contracts()
    {
        var result = Types.InAssembly(Vehicles)
            .Should()
            .NotHaveDependencyOnAny("OpenDealer360.Identity.Domain", "OpenDealer360.Identity.Data")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            because: "Identity is reachable only via its Contracts namespace; offenders: "
                + string.Join(", ", result.FailingTypeNames ?? []));
    }

    [Fact]
    public void Tenancy_must_not_depend_on_aspnetcore()
    {
        // Web concerns (middleware, HttpContext) live in Host, not in the
        // persistence infrastructure project.
        var result = Types.InAssembly(TenancyAssembly)
            .Should().NotHaveDependencyOn("Microsoft.AspNetCore")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            because: "tenant routing is web-agnostic; offenders: "
                + string.Join(", ", result.FailingTypeNames ?? []));
    }
}
