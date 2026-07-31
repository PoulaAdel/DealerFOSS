// FeatureBoundaryTests — rules about what the features inside App may reference.
//
// Use:  runs with the normal test suite; a violation fails the build.
// Edit: these are the only thing keeping the features apart, because they share
//       one project and the compiler will happily let Customers reach into
//       Vehicles (ADR-017). Every new feature needs its own row in the table
//       below: entities free of EF and ASP.NET, and no dependency on a sibling
//       except through that sibling's I<Feature> contract.

using System.Reflection;
using FluentAssertions;
using NetArchTest.Rules;
using OpenDealer360.Core;
using OpenDealer360.Vehicles;
using Xunit;

namespace OpenDealer360.ArchitectureTests;

public sealed class FeatureBoundaryTests
{
    private static readonly Assembly App = typeof(Vehicle).Assembly;

    /// <summary>
    /// Every feature namespace inside the application, with the ones it may not
    /// touch. Inventory may see Vehicles — a unit is a vehicle on a lot — but
    /// nothing may see Inventory, and no feature may see another's entities.
    /// </summary>
    public static TheoryData<string, string[]> ForbiddenFeatureDependencies() => new()
    {
        { "OpenDealer360.Organization", ["OpenDealer360.Customers", "OpenDealer360.Vehicles", "OpenDealer360.Inventory", "OpenDealer360.Leads"] },
        { "OpenDealer360.Customers", ["OpenDealer360.Organization", "OpenDealer360.Vehicles", "OpenDealer360.Inventory", "OpenDealer360.Leads"] },
        { "OpenDealer360.Vehicles", ["OpenDealer360.Organization", "OpenDealer360.Customers", "OpenDealer360.Inventory", "OpenDealer360.Leads"] },
        { "OpenDealer360.Inventory", ["OpenDealer360.Organization", "OpenDealer360.Customers", "OpenDealer360.Leads"] },
    };

    /// <summary>
    /// The entity types a capability owns. Another capability may use the
    /// <c>I&lt;Feature&gt;</c> contract next to them, but never these.
    /// </summary>
    public static TheoryData<string, string[]> ForbiddenEntityDependencies() => new()
    {
        { "OpenDealer360.Leads", ["OpenDealer360.Customers.Customer", "OpenDealer360.Customers.ContactPoint", "OpenDealer360.Vehicles.Vehicle", "OpenDealer360.Inventory.InventoryUnit"] },
    };

    [Theory]
    [MemberData(nameof(ForbiddenEntityDependencies))]
    public void A_feature_may_use_a_siblings_contract_but_not_its_entities(string feature, string[] forbidden)
    {
        // Flattening the folders put ICustomers and Customer in the same
        // namespace, so the namespace rule above cannot tell them apart. This
        // one names the entity types directly — it is what keeps "through the
        // contract only" a real rule rather than a convention (ADR-017).
        var result = Types.InAssembly(App)
            .That().ResideInNamespace(feature)
            .Should().NotHaveDependencyOnAny(forbidden)
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            because: $"{feature} must go through the published interface, not the entity; offenders: "
                + string.Join(", ", result.FailingTypeNames ?? []));
    }

    [Theory]
    [MemberData(nameof(ForbiddenFeatureDependencies))]
    public void A_feature_must_not_reach_into_another_feature(string feature, string[] forbidden)
    {
        var result = Types.InAssembly(App)
            .That().ResideInNamespace(feature)
            .Should().NotHaveDependencyOnAny(forbidden)
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            because: $"{feature} must reach a sibling only through its published contract; offenders: "
                + string.Join(", ", result.FailingTypeNames ?? []));
    }

    [Fact]
    public void Entities_must_not_depend_on_ef_or_aspnetcore()
    {
        // Follows the type rather than the folder: a record that carries audit
        // columns is a business entity wherever it lives, and its mapping belongs
        // in a XTables.cs file instead.
        var result = Types.InAssembly(App)
            .That().Inherit(typeof(AuditableEntity))
            .Should().NotHaveDependencyOnAny("Microsoft.EntityFrameworkCore", "Microsoft.AspNetCore")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            because: "entities hold rules, not infrastructure; offenders: "
                + string.Join(", ", result.FailingTypeNames ?? []));
    }

    [Fact]
    public void Tenancy_must_not_depend_on_a_business_feature()
    {
        // Tenant routing decides which database a request uses. It must not know
        // what is stored in it.
        var result = Types.InAssembly(App)
            .That().ResideInNamespace("OpenDealer360.Tenancy")
            .Should()
            .NotHaveDependencyOnAny(
                "OpenDealer360.Organization",
                "OpenDealer360.Customers",
                "OpenDealer360.Vehicles",
                "OpenDealer360.Inventory")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            because: "tenant routing is business-agnostic; offenders: "
                + string.Join(", ", result.FailingTypeNames ?? []));
    }

    [Fact]
    public void A_feature_must_not_build_its_own_database_connection()
    {
        // Services query through TenantDb, which is bound to the tenant resolved
        // for the request. A feature constructing its own options builder would
        // be choosing a connection itself, and could address another dealer's
        // database (doc 04 §5). Only the data layer, tenancy, and the
        // development seeder may do that.
        var result = Types.InAssembly(App)
            .That().ResideInNamespaceStartingWith("OpenDealer360.")
            .And().DoNotResideInNamespace("OpenDealer360.Data")
            .And().DoNotResideInNamespace("OpenDealer360.Tenancy")
            .And().DoNotResideInNamespace("OpenDealer360.App")
            .Should().NotHaveDependencyOn("Microsoft.EntityFrameworkCore.DbContextOptionsBuilder`1")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            because: "only the data layer chooses a connection; offenders: "
                + string.Join(", ", result.FailingTypeNames ?? []));
    }
}
