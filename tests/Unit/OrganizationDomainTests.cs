// OrganizationDomainTests — proves the structure entities cannot be constructed
// in an invalid state.
//
// Use:  runs with the normal test suite; no infrastructure required.
// Edit: invariants belong in constructors so an invalid record cannot exist at
//       all, rather than being caught later by validation. Add a case here when
//       you add an invariant.

using FluentAssertions;
using OpenDealer360.Core;
using OpenDealer360.Identity;
using OpenDealer360.Organization;

namespace OpenDealer360.UnitTests;

public sealed class OrganizationDomainTests
{
    [Theory]
    [InlineData("", "slug")]
    [InlineData("   ", "slug")]
    [InlineData("North Auto", "")]
    [InlineData("North Auto", "   ")]
    public void A_dealer_organization_needs_both_a_name_and_a_slug(string name, string slug)
    {
        var construct = () => new DealerOrganization(DealerOrganizationId.New(), name, slug);

        construct.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void A_legal_entity_needs_a_name()
    {
        var construct = () => new LegalEntity(LegalEntityId.New(), DealerOrganizationId.New(), "  ");

        construct.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData("", "NAG-01")]
    [InlineData("Downtown", "")]
    public void A_rooftop_needs_both_a_name_and_a_code(string name, string code)
    {
        var construct = () => new Rooftop(RooftopId.New(), LegalEntityId.New(), name, code, "UTC");

        construct.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void A_rooftop_without_a_time_zone_falls_back_to_utc_rather_than_an_empty_string()
    {
        // An empty time zone would fail only later, when a local date is
        // derived — far from the cause.
        var rooftop = new Rooftop(RooftopId.New(), LegalEntityId.New(), "Downtown", "NAG-01", "");

        rooftop.TimeZone.Should().Be("UTC");
    }

    [Fact]
    public void A_rooftop_keeps_the_time_zone_it_is_given()
    {
        var rooftop = new Rooftop(
            RooftopId.New(), LegalEntityId.New(), "Downtown", "NAG-01", "America/New_York");

        rooftop.TimeZone.Should().Be("America/New_York");
    }

    [Fact]
    public void A_department_needs_a_name()
    {
        var construct = () => new Department(DepartmentId.New(), RooftopId.New(), "", DepartmentKind.Sales);

        construct.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Ids_of_different_kinds_are_not_interchangeable()
    {
        // The whole reason these are separate types: a RooftopId must not be
        // usable where a LegalEntityId is expected, even though both wrap a Guid.
        var shared = Guid.NewGuid();

        var rooftop = new RooftopId(shared);
        var legalEntity = new LegalEntityId(shared);

        rooftop.Value.Should().Be(legalEntity.Value);
        rooftop.GetType().Should().NotBe(legalEntity.GetType());
    }

    [Theory]
    [InlineData("GM@Dev.Local", "gm@dev.local")]
    [InlineData("  advisor@dev.local  ", "advisor@dev.local")]
    public void A_user_email_is_normalized_so_lookups_are_case_insensitive(string input, string expected)
    {
        new User(Guid.NewGuid(), input, "Someone").Email.Should().Be(expected);
    }

    [Fact]
    public void A_user_needs_an_email_and_a_display_name()
    {
        var noEmail = () => new User(Guid.NewGuid(), "  ", "Someone");
        var noName = () => new User(Guid.NewGuid(), "a@b.c", "  ");

        noEmail.Should().Throw<ArgumentException>();
        noName.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void A_user_is_active_until_deactivated()
    {
        var user = new User(Guid.NewGuid(), "a@b.c", "Someone");
        user.IsActive.Should().BeTrue();

        user.Deactivate();

        user.IsActive.Should().BeFalse(
            because: "AccessService denies an inactive user, so this flag is a security control");
    }
}
