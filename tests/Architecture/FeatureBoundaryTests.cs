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
using DealerFOSS.Core;
using DealerFOSS.Vehicles;
using Xunit;

namespace DealerFOSS.ArchitectureTests;

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
        { "DealerFOSS.Organization", ["DealerFOSS.Customers", "DealerFOSS.Vehicles", "DealerFOSS.Inventory", "DealerFOSS.Leads", "DealerFOSS.Deals"] },
        { "DealerFOSS.Customers", ["DealerFOSS.Organization", "DealerFOSS.Vehicles", "DealerFOSS.Inventory", "DealerFOSS.Leads", "DealerFOSS.Deals"] },
        { "DealerFOSS.Vehicles", ["DealerFOSS.Organization", "DealerFOSS.Customers", "DealerFOSS.Inventory", "DealerFOSS.Leads", "DealerFOSS.Deals"] },
        { "DealerFOSS.Inventory", ["DealerFOSS.Organization", "DealerFOSS.Customers", "DealerFOSS.Leads", "DealerFOSS.Deals"] },
        { "DealerFOSS.Leads", ["DealerFOSS.Organization", "DealerFOSS.Deals", "DealerFOSS.Accounting"] },
        { "DealerFOSS.Accounting", ["DealerFOSS.Customers", "DealerFOSS.Vehicles", "DealerFOSS.Inventory", "DealerFOSS.Leads", "DealerFOSS.Deals"] },
    };

    /// <summary>
    /// The entity types a capability owns. Another capability may use the
    /// <c>I&lt;Feature&gt;</c> contract next to them, but never these.
    /// </summary>
    public static TheoryData<string, string[]> ForbiddenEntityDependencies() => new()
    {
        { "DealerFOSS.Leads", ["DealerFOSS.Customers.Customer", "DealerFOSS.Customers.ContactPoint", "DealerFOSS.Vehicles.Vehicle", "DealerFOSS.Inventory.InventoryUnit"] },
        { "DealerFOSS.Deals", ["DealerFOSS.Customers.Customer", "DealerFOSS.Customers.ContactPoint", "DealerFOSS.Vehicles.Vehicle", "DealerFOSS.Inventory.InventoryUnit", "DealerFOSS.Inventory.InventoryStatusChange", "DealerFOSS.Accounting.JournalEntry", "DealerFOSS.Accounting.Account"] },
        { "DealerFOSS.Accounting", ["DealerFOSS.Organization.Rooftop", "DealerFOSS.Organization.LegalEntity", "DealerFOSS.Organization.DealerOrganization"] },
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
            .That().ResideInNamespace("DealerFOSS.Tenancy")
            .Should()
            .NotHaveDependencyOnAny(
                "DealerFOSS.Organization",
                "DealerFOSS.Customers",
                "DealerFOSS.Vehicles",
                "DealerFOSS.Inventory")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            because: "tenant routing is business-agnostic; offenders: "
                + string.Join(", ", result.FailingTypeNames ?? []));
    }

    [Fact]
    public void The_control_plane_must_not_be_able_to_become_a_tenant_caller()
    {
        // ICurrentUser is what every capability hands to IAccessDirectory when it
        // asks "may this person see this dealership's data?". An administrator
        // has no answer to that question, so nothing in the control plane may
        // touch the type — which is what makes the refusal structural rather than
        // a permission check the next capability has to remember (doc 06 §3).
        //
        // Support access is not an exception to this rule. It mints a session for
        // the tenant's own support principal and hands it back as cookies; the
        // ordinary tenant middleware resolves it, in the ordinary way, as that
        // user. No administrator ever becomes an ICurrentUser.
        var result = Types.InAssembly(App)
            .That().ResideInNamespace("DealerFOSS.Administration")
            .Should().NotHaveDependencyOn("DealerFOSS.Core.ICurrentUser")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            because: "an administrator must never resolve as a dealership caller; offenders: "
                + string.Join(", ", result.FailingTypeNames ?? []));
    }

    [Fact]
    public void The_control_plane_must_not_reach_a_business_capability()
    {
        // Running the installation and reading a dealership's records are
        // different jobs. If this rule ever needs relaxing, the honest change is
        // a new support-access capability — not a reference from here.
        var result = Types.InAssembly(App)
            .That().ResideInNamespace("DealerFOSS.Administration")
            .Should()
            .NotHaveDependencyOnAny(
                "DealerFOSS.Organization",
                "DealerFOSS.Customers",
                "DealerFOSS.Vehicles",
                "DealerFOSS.Inventory",
                "DealerFOSS.Leads",
                "DealerFOSS.Deals",
                "DealerFOSS.Accounting")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            because: "the control plane operates the deployment and reads none of it; offenders: "
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
            .That().ResideInNamespaceStartingWith("DealerFOSS.")
            .And().DoNotResideInNamespace("DealerFOSS.Data")
            .And().DoNotResideInNamespace("DealerFOSS.Tenancy")
            .And().DoNotResideInNamespace("DealerFOSS.App")
            .Should().NotHaveDependencyOn("Microsoft.EntityFrameworkCore.DbContextOptionsBuilder`1")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            because: "only the data layer chooses a connection; offenders: "
                + string.Join(", ", result.FailingTypeNames ?? []));
    }
}
