// RoleTests — proves a role can only grant permissions that actually exist.
//
// Use:  runs with the normal test suite; no infrastructure required.
// Edit: the catalogue check is what turns a typo into a loud failure instead of
//       a permission nobody holds. Keep it, and keep Grant idempotent so
//       re-running a seeder cannot produce duplicate rows.

using FluentAssertions;
using OpenDealer360.Identity;

namespace OpenDealer360.UnitTests;

public sealed class RoleTests
{
    [Fact]
    public void A_role_starts_with_no_permissions()
    {
        new Role(Guid.NewGuid(), "Service Advisor").Permissions.Should().BeEmpty(
            because: "a new role must grant nothing until something is granted explicitly");
    }

    [Fact]
    public void Granting_a_catalogued_permission_records_it()
    {
        var role = new Role(Guid.NewGuid(), "Manager");

        role.Grant(Permissions.OrganizationRead);

        role.Permissions.Should().ContainSingle()
            .Which.Permission.Should().Be(Permissions.OrganizationRead);
    }

    [Fact]
    public void Granting_the_same_permission_twice_is_idempotent()
    {
        var role = new Role(Guid.NewGuid(), "Manager");

        role.Grant(Permissions.OrganizationRead);
        role.Grant(Permissions.OrganizationRead);

        role.Permissions.Should().HaveCount(1,
            because: "seeding runs repeatedly and must not accumulate duplicate rows");
    }

    [Fact]
    public void Granting_a_permission_outside_the_catalogue_is_refused()
    {
        var role = new Role(Guid.NewGuid(), "Manager");

        var grant = () => role.Grant("Organizaton.Reed");

        grant.Should().Throw<ArgumentException>(
            because: "a typo must fail loudly, not become a permission nobody can hold");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void A_role_must_have_a_name(string name)
    {
        var construct = () => new Role(Guid.NewGuid(), name);

        construct.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Every_catalogued_permission_can_actually_be_granted()
    {
        // Guards against a permission being added to the catalogue in a shape
        // Grant rejects, which would make it permanently unusable.
        var role = new Role(Guid.NewGuid(), "Everything");

        foreach (var permission in Permissions.All)
        {
            role.Grant(permission);
        }

        role.Permissions.Should().HaveCount(Permissions.All.Count);
    }
}
