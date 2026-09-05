// Copyright (c) 2026 The DealerFOSS contributors.
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Overview: Purpose, File Design, and Engineering
//   JobContextTests — proves a background job cannot be anonymous by accident,
//   and that the two kinds of job stay two kinds.
//
// Usage:
//   Runs with the normal test suite; no infrastructure required.
//
// Coding Instructions:
//   The compile-time half of this guarantee cannot be asserted here — a call
//   that does not compile cannot be written down in a test. BoundaryTests
//   guards the SHAPE that makes it a compile error (no bare-string factory, no
//   Set on the holder interfaces, no IServiceProvider on UnattendedScope);
//   these tests guard the runtime edges of the shape.
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
    }

    [Fact]
    public void The_requester_is_not_nullable_so_there_is_no_anonymous_JobContext()
    {
        // The property type carries the guarantee: JobContext.RequestedByUserId
        // is Guid, not Guid?. There is no value of it meaning "nobody", and no
        // factory that leaves it unset. Work with no requester is a different
        // type entirely.
        typeof(JobContext).GetProperty(nameof(JobContext.RequestedByUserId))!
            .PropertyType.Should().Be<Guid>();
    }

    [Fact]
    public void Work_nobody_asked_for_is_a_different_type_rather_than_a_null_field()
    {
        var sweep = UnattendedJob.For("northgroup", "capture expiry");

        sweep.TenantKey.Should().Be("northgroup");
        sweep.Reason.Should().Be("capture expiry");

        // Not assignable in either direction. That is what lets the factory
        // return a scope that can see less, which is the whole point.
        typeof(JobContext).IsAssignableFrom(typeof(UnattendedJob)).Should().BeFalse();
        typeof(UnattendedJob).IsAssignableFrom(typeof(JobContext)).Should().BeFalse();
    }

    [Fact]
    public void An_empty_user_id_is_not_a_requester()
    {
        // The failure this prevents: `RequestedBy(slug, job.RequestedBy, ...)`
        // where the field was never populated. Every type is satisfied, the job
        // runs, and the rows it writes name a user who does not exist.
        var anonymous = () => JobContext.RequestedBy("northgroup", Guid.Empty, "csv import");

        anonymous.Should().Throw<ArgumentException>()
            .WithMessage("*UnattendedJob*",
                because: "the message has to name the correct alternative, or the fix is to invent an id");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Work_must_name_the_dealership_it_runs_against(string tenantKey)
    {
        var unnamedJob = () => JobContext.RequestedBy(tenantKey, Requester, "csv import");
        var unnamedSweep = () => UnattendedJob.For(tenantKey, "capture expiry");

        unnamedJob.Should().Throw<ArgumentException>();
        unnamedSweep.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Work_must_say_why_it_is_running(string reason)
    {
        // The reason is what a maintainer reads when a row is attributed to the
        // system and nobody remembers which sweep wrote it.
        var unexplainedJob = () => JobContext.RequestedBy("northgroup", Requester, reason);
        var unexplainedSweep = () => UnattendedJob.For("northgroup", reason);

        unexplainedJob.Should().Throw<ArgumentException>();
        unexplainedSweep.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Surrounding_whitespace_does_not_make_two_different_tenants()
    {
        var padded = UnattendedJob.For("  northgroup  ", "  capture expiry  ");

        padded.TenantKey.Should().Be("northgroup");
        padded.Reason.Should().Be("capture expiry");
    }

    [Fact]
    public void An_unattended_job_reads_as_unattended_in_a_log()
    {
        UnattendedJob.For("northgroup", "capture expiry").ToString()
            .Should().Contain("unattended");
    }
}
