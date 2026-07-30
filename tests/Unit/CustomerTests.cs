// CustomerTests — proves the customer rules that later modules will rely on.
//
// Use:  runs with the normal test suite; no infrastructure required.
// Edit: the contact-point rules matter most. Normalization is what makes search
//       work at all, and the deliberate absence of uniqueness is what lets a
//       couple share a phone number without staff inventing fake data.

using FluentAssertions;
using OpenDealer360.Core;
using OpenDealer360.Customers;

namespace OpenDealer360.UnitTests;

public sealed class CustomerTests
{
    [Fact]
    public void A_person_displays_as_first_then_last_name()
    {
        var customer = Customer.Person(Guid.NewGuid(), "Marisol", "Alvarez");

        customer.DisplayName.Should().Be("Marisol Alvarez");
        customer.Kind.Should().Be(CustomerKind.Person);
    }

    [Fact]
    public void A_person_with_no_first_name_still_displays_sensibly()
    {
        // Plenty of imported records have only a surname. It must not render as
        // " Alvarez" with a leading space.
        var customer = Customer.Person(Guid.NewGuid(), "", "Alvarez");

        customer.DisplayName.Should().Be("Alvarez");
    }

    [Fact]
    public void A_business_displays_as_its_name_alone()
    {
        var customer = Customer.Business(Guid.NewGuid(), "Brightline Facilities Ltd");

        customer.DisplayName.Should().Be("Brightline Facilities Ltd");
        customer.FirstName.Should().BeEmpty();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void A_customer_must_have_a_last_name_or_business_name(string name)
    {
        var person = () => Customer.Person(Guid.NewGuid(), "Marisol", name);
        var business = () => Customer.Business(Guid.NewGuid(), name);

        person.Should().Throw<ArgumentException>();
        business.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void A_customer_belongs_to_the_organization_not_a_rooftop()
    {
        // Home rooftop is optional and is only where they were first met.
        Customer.Person(Guid.NewGuid(), "Marisol", "Alvarez").HomeRooftopId.Should().BeNull();

        var rooftop = RooftopId.New();
        Customer.Person(Guid.NewGuid(), "Daniel", "Okafor", rooftop).HomeRooftopId.Should().Be(rooftop);
    }

    [Theory]
    [InlineData("(555) 010-2030", "5550102030")]
    [InlineData("555 010 2030", "5550102030")]
    [InlineData("555-010-2030", "5550102030")]
    [InlineData("+1 555 010 2030", "+15550102030")]
    public void A_phone_number_is_stored_as_digits_so_formatting_does_not_matter(string typed, string stored)
    {
        ContactPoint.Normalize(ContactKind.Phone, typed).Should().Be(stored);
    }

    [Fact]
    public void An_email_is_stored_lower_case_so_search_is_case_insensitive()
    {
        ContactPoint.Normalize(ContactKind.Email, "  Marisol.Alvarez@Example.TEST ")
            .Should().Be("marisol.alvarez@example.test");
    }

    [Fact]
    public void A_phone_number_with_no_digits_is_rejected()
    {
        var normalize = () => ContactPoint.Normalize(ContactKind.Phone, "call me");

        normalize.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void The_first_contact_of_a_kind_becomes_primary_automatically()
    {
        // Otherwise a customer with exactly one phone number has no primary one,
        // and every screen has to special-case it.
        var customer = Customer.Person(Guid.NewGuid(), "Marisol", "Alvarez");

        var phone = customer.AddContactPoint(Guid.NewGuid(), ContactKind.Phone, "5550102030");

        phone.IsPrimary.Should().BeTrue();
    }

    [Fact]
    public void Marking_a_new_contact_primary_demotes_the_previous_one_of_that_kind()
    {
        var customer = Customer.Person(Guid.NewGuid(), "Marisol", "Alvarez");
        var first = customer.AddContactPoint(Guid.NewGuid(), ContactKind.Phone, "5550102030");

        var second = customer.AddContactPoint(Guid.NewGuid(), ContactKind.Phone, "5550109999", isPrimary: true);

        second.IsPrimary.Should().BeTrue();
        first.IsPrimary.Should().BeFalse();
        customer.ContactPoints.Count(p => p.Kind == ContactKind.Phone && p.IsPrimary)
            .Should().Be(1, because: "exactly one number per kind is the one to call first");
    }

    [Fact]
    public void Each_kind_keeps_its_own_primary()
    {
        var customer = Customer.Person(Guid.NewGuid(), "Marisol", "Alvarez");

        var email = customer.AddContactPoint(Guid.NewGuid(), ContactKind.Email, "m@example.test");
        var phone = customer.AddContactPoint(Guid.NewGuid(), ContactKind.Phone, "5550102030");

        email.IsPrimary.Should().BeTrue();
        phone.IsPrimary.Should().BeTrue();
    }

    [Fact]
    public void Adding_the_same_value_twice_does_not_create_a_duplicate()
    {
        // Re-importing the same record must not accumulate contact points.
        var customer = Customer.Person(Guid.NewGuid(), "Marisol", "Alvarez");

        customer.AddContactPoint(Guid.NewGuid(), ContactKind.Phone, "(555) 010-2030");
        customer.AddContactPoint(Guid.NewGuid(), ContactKind.Phone, "555-010-2030");

        customer.ContactPoints.Should().HaveCount(1,
            because: "the two spellings are the same number once normalized");
    }

    [Fact]
    public void An_archived_customer_is_kept_rather_than_deleted()
    {
        var customer = Customer.Person(Guid.NewGuid(), "Marisol", "Alvarez");

        customer.Archive();

        customer.IsArchived.Should().BeTrue();
        customer.DisplayName.Should().Be("Marisol Alvarez",
            because: "past deals and repair orders must stay attributable to a name");
    }

    [Fact]
    public void An_address_needs_a_line_a_city_and_a_two_letter_country()
    {
        var noLine = () => Address.Create("", null, "Springfield", "IL", "62704", "US");
        var noCity = () => Address.Create("18 Kestrel Way", null, " ", "IL", "62704", "US");
        var badCountry = () => Address.Create("18 Kestrel Way", null, "Springfield", "IL", "62704", "USA");

        noLine.Should().Throw<ArgumentException>();
        noCity.Should().Throw<ArgumentException>();
        badCountry.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void An_address_keeps_a_postal_code_exactly_as_given()
    {
        // Leading zeros are real, and plenty of postcodes are not numeric at all.
        var address = Address.Create("1 High St", null, "Boston", "MA", "02108", "us");

        address.PostalCode.Should().Be("02108");
        address.Country.Should().Be("US", because: "country codes are normalized, postcodes are not");
    }

    [Fact]
    public void An_optional_address_line_is_null_rather_than_blank()
    {
        var address = Address.Create("1 High St", "   ", "Boston", null, null, "US");

        address.Line2.Should().BeNull();
        address.AdministrativeArea.Should().BeNull();
    }
}
