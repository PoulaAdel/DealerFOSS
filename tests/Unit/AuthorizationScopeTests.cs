// Copyright (c) 2026 The DealerFOSS contributors.
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Overview: Purpose, File Design, and Engineering
//   AuthorizationScopeTests — proves the scope rules that rooftop isolation rests
//   on, at the unit level. The end-to-end proof lives in tests/Integration; these
//   pin the logic itself so a refactor cannot quietly invert it.
//
// Usage:
//   Runs with the normal test suite; no infrastructure required.
//
// Coding Instructions:
//   "empty means deny" and "organization-wide is not a list of rooftops" are
//   the two rules that keep a future rooftop safe by default. Do not relax
//   either without changing the security documentation first.

using FluentAssertions;
using DealerFOSS.Core;
using DealerFOSS.Identity;

namespace DealerFOSS.UnitTests;

public sealed class AuthorizationScopeTests
{
    private static readonly RooftopId Downtown = RooftopId.New();
    private static readonly RooftopId Uptown = RooftopId.New();

    [Fact]
    public void No_scope_grants_nothing_and_covers_no_rooftop()
    {
        AuthorizedScope.None.GrantsNothing.Should().BeTrue();
        AuthorizedScope.None.Covers(Downtown).Should().BeFalse(
            because: "an empty scope must read as a refusal, never as 'unfiltered'");
    }

    [Fact]
    public void Organization_wide_covers_every_rooftop_including_ones_not_yet_created()
    {
        var scope = AuthorizedScope.OrganizationWide;

        scope.GrantsNothing.Should().BeFalse();
        scope.Covers(Downtown).Should().BeTrue();
        scope.Covers(Uptown).Should().BeTrue();
        // A rooftop opened next year has an id nobody has seen yet. It must be
        // covered automatically, which is why this is a flag and not a list.
        scope.Covers(RooftopId.New()).Should().BeTrue();
    }

    [Fact]
    public void A_rooftop_scope_covers_only_the_rooftops_it_names()
    {
        var scope = new AuthorizedScope(false, new HashSet<RooftopId> { Downtown });

        scope.Covers(Downtown).Should().BeTrue();
        scope.Covers(Uptown).Should().BeFalse();
        scope.Covers(RooftopId.New()).Should().BeFalse(
            because: "a rooftop added later must NOT be covered by an explicit grant");
    }

    [Fact]
    public void A_scope_listing_rooftops_is_not_treated_as_empty()
    {
        var scope = new AuthorizedScope(false, new HashSet<RooftopId> { Downtown });

        scope.GrantsNothing.Should().BeFalse();
    }

    // --- records, not only rooftops (ADR-030) --------------------------------

    [Fact]
    public void An_ordinary_scope_allows_any_record_at_a_rooftop_it_covers()
    {
        var scope = new AuthorizedScope(false, new HashSet<RooftopId> { Downtown }, false, Me);

        scope.Allows(Downtown, Somebody).Should().BeTrue(
            because: "without the narrowing, who a record belongs to is not a question");
        scope.Allows(Downtown, null).Should().BeTrue();
        scope.Allows(Uptown, Me).Should().BeFalse(
            because: "the rooftop still decides first — owning it does not import it");
    }

    [Fact]
    public void A_narrowed_scope_allows_only_records_it_is_named_on()
    {
        var scope = new AuthorizedScope(false, new HashSet<RooftopId> { Downtown }, true, Me);

        scope.Allows(Downtown, Me).Should().BeTrue();
        scope.Allows(Downtown, Somebody).Should().BeFalse();
    }

    [Fact]
    public void A_narrowed_scope_refuses_a_record_naming_nobody()
    {
        // The decision worth pinning: an unowned record is NOT everybody's. A
        // capability that genuinely means the other thing — the enquiry pool —
        // says so itself rather than having it assumed here for every record in
        // the product.
        var scope = new AuthorizedScope(false, new HashSet<RooftopId> { Downtown }, true, Me);

        scope.Allows(Downtown, null).Should().BeFalse();
    }

    [Fact]
    public void The_narrowing_does_not_widen_an_organization_scope_into_one_rooftop()
    {
        // Own-records-only says WHICH records, never which lots. Somebody
        // group-wide and narrowed still reaches their own work everywhere.
        var scope = new AuthorizedScope(true, new HashSet<RooftopId>(), true, Me);

        scope.Allows(Uptown, Me).Should().BeTrue();
        scope.Allows(Uptown, Somebody).Should().BeFalse();
    }

    private static readonly Guid Me = Guid.NewGuid();
    private static readonly Guid Somebody = Guid.NewGuid();

    [Fact]
    public void An_organization_assignment_covers_any_rooftop()
    {
        var assignment = UserAssignment.ForOrganization(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

        assignment.Scope.Should().Be(AssignmentScope.Organization);
        assignment.RooftopId.Should().BeNull();
        assignment.Covers(Downtown).Should().BeTrue();
        assignment.Covers(Uptown).Should().BeTrue();
    }

    [Fact]
    public void A_rooftop_assignment_covers_only_its_own_rooftop()
    {
        var assignment = UserAssignment.ForRooftop(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Downtown);

        assignment.Scope.Should().Be(AssignmentScope.Rooftop);
        assignment.RooftopId.Should().Be(Downtown);
        assignment.Covers(Downtown).Should().BeTrue();
        assignment.Covers(Uptown).Should().BeFalse();
    }
}
