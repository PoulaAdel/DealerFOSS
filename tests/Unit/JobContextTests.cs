// Copyright (c) 2026 The DealerFOSS contributors.
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Overview: Purpose, File Design, and Engineering
//   JobContextTests — proves a background job cannot be anonymous by accident.
//
// Usage:
//   Runs with the normal test suite; no infrastructure required.
//
// Coding Instructions:
//   The compile-time half of this guarantee cannot be asserted here — a call
//   that does not compile cannot be written down in a test. BoundaryTests
//   guards the SHAPE that makes it a compile error (no string overload, no Set
//   on the interfaces); these tests guard the runtime edges of the shape.
//
//   The empty-Guid case is the one that matters most. An anonymous job wearing
//   Guid.Empty would satisfy every type in the system, be attributed to a user
//   who does not exist, and pass a permission check against nobody's roles.

using FluentAssertions;
using DealerFOSS.Tenancy;

namespace DealerFOSS.UnitTests;

public sealed class JobContextTests
{
    private static readonly Guid Requester = Guid.Parse("11111111-1111-1111-1111-111111111111");

    [Fact]
    public void Work_for_a_person_carries_that_person()
    {
        var job = JobContext.RequestedBy("northgroup", Requester, "csv import");

        job.TenantKey.Should().Be("northgroup");
        job.RequestedByUserId.Should().Be(Requester);
        job.Reason.Should().Be("csv import");
        job.IsUnattended.Should().BeFalse();
    }

    [Fact]
    public void Work_nobody_asked_for_says_so_rather_than_naming_a_placeholder()
    {
        var job = JobContext.Unattended("northgroup", "capture expiry");

        job.IsUnattended.Should().BeTrue();
        job.RequestedByUserId.Should().BeNull(
            because: "a sweep that invented a requester would attribute a machine's work to a person");
    }

    [Fact]
    public void An_empty_user_id_is_not_a_requester()
    {
        // The failure this prevents: `RequestedBy(slug, job.RequestedBy, ...)`
        // where the field was never populated. Every type is satisfied, the job
        // runs, and the rows it writes name a user who does not exist.
        var anonymous = () => JobContext.RequestedBy("northgroup", Guid.Empty, "csv import");

        anonymous.Should().Throw<ArgumentException>()
            .WithMessage("*Unattended*",
                because: "the message has to name the correct alternative, or the fix is to invent an id");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Work_must_name_the_dealership_it_runs_against(string tenantKey)
    {
        var unnamed = () => JobContext.Unattended(tenantKey, "capture expiry");

        unnamed.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Work_must_say_why_it_is_running(string reason)
    {
        // The reason is what a maintainer reads when a row is attributed to the
        // system and nobody remembers which sweep wrote it.
        var unexplained = () => JobContext.Unattended("northgroup", reason);

        unexplained.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Surrounding_whitespace_does_not_make_two_different_tenants()
    {
        var padded = JobContext.Unattended("  northgroup  ", "  capture expiry  ");

        padded.TenantKey.Should().Be("northgroup");
        padded.Reason.Should().Be("capture expiry");
    }

    [Fact]
    public void An_unattended_job_reads_as_unattended_in_a_log()
    {
        JobContext.Unattended("northgroup", "capture expiry").ToString()
            .Should().Contain("unattended");
    }
}
