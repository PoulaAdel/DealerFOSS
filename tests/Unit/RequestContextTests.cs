// Copyright (c) 2026 The DealerFOSS contributors.
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Overview: Purpose, File Design, and Engineering
//   RequestContextTests — proves the per-request tenant and caller holders fail
//   loudly rather than defaulting.
//
// Usage:
//   Runs with the normal test suite; no infrastructure required.
//
// Coding Instructions:
//   These two types are small but security-relevant. Reading before
//   resolution must throw, and resolving twice must throw, or a request
//   could silently act as the wrong tenant or the wrong user.

using FluentAssertions;
using DealerFOSS.Core;

namespace DealerFOSS.UnitTests;

public sealed class RequestContextTests
{
    private static ResolvedTenant SampleTenant(string key = "northgroup") =>
        new(DealerOrganizationId.New(), key, "Server=.;Database=X", DatabaseVersion: 1);

    [Fact]
    public void An_unresolved_tenant_context_reports_it_and_throws_on_read()
    {
        var context = new TenantContext();

        context.IsResolved.Should().BeFalse();
        var read = () => context.Current;
        read.Should().Throw<InvalidOperationException>(
            because: "code running outside tenant middleware must fail, not get a default tenant");
    }

    [Fact]
    public void A_resolved_tenant_context_returns_what_was_set()
    {
        var tenant = SampleTenant();
        var context = new TenantContext();

        context.Set(tenant);

        context.IsResolved.Should().BeTrue();
        context.Current.Should().Be(tenant);
    }

    [Fact]
    public void A_tenant_cannot_be_switched_mid_request()
    {
        var context = new TenantContext();
        context.Set(SampleTenant("northgroup"));

        var switchTenant = () => context.Set(SampleTenant("citymotors"));

        switchTenant.Should().Throw<InvalidOperationException>(
            because: "one request resolves exactly one dealer organization (ADR-003)");
    }

    [Fact]
    public void An_unidentified_caller_reports_it_and_throws_on_read()
    {
        var user = new CurrentUser();

        user.IsAuthenticated.Should().BeFalse();
        var read = () => user.Id;
        read.Should().Throw<InvalidOperationException>(
            because: "an authorized operation must never run with an anonymous default");
    }

    [Fact]
    public void An_identified_caller_returns_their_id()
    {
        var id = Guid.NewGuid();
        var user = new CurrentUser();

        user.Set(id);

        user.IsAuthenticated.Should().BeTrue();
        user.Id.Should().Be(id);
    }

    [Fact]
    public void A_caller_cannot_be_switched_mid_request()
    {
        var user = new CurrentUser();
        user.Set(Guid.NewGuid());

        var impersonate = () => user.Set(Guid.NewGuid());

        impersonate.Should().Throw<InvalidOperationException>(
            because: "re-setting the caller mid-request is how impersonation bugs appear");
    }
}
