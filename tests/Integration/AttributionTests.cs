// Copyright (c) 2026 The DealerFOSS contributors.
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Overview: Purpose, File Design, and Engineering
//   AttributionTests — a business row records WHO wrote it, not only when.
//
//   These read CreatedBy and ModifiedBy directly with SQL rather than through an
//   endpoint, and that is deliberate: the data context writes both columns on
//   save and no API exposes either, so looking at the row is the only honest way
//   to assert them.
//
//   The gap they were written to close: until 2026-09-04 `AuditableEntity`
//   defaulted CreatedBy to "system" and nothing ever assigned it. Every row in
//   the database said "system" — 1,450 across 21 tables, including deals and
//   repair orders created by signed-in people through the API — while three
//   documents claimed the columns were stamped on save. Nothing failed, because
//   nothing looked.
//
// Usage:
//   dotnet test DealerFOSS.slnx -c Release
//
// Coding Instructions:
//   DO NOT relax these into "CreatedBy is not empty". The defect being guarded
//   against is a column filled with a plausible constant, and "not empty" passes
//   against exactly that — which is why the second test makes two different
//   people write and compares them.
//
//   The seeder and tenant provisioning legitimately write "system": no person
//   asked for those rows, and inventing one would be worse than saying so. That
//   is asserted too, so the fallback cannot quietly become a user id later.

using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Data.SqlClient;
using DealerFOSS.App;
using Xunit;

namespace DealerFOSS.IntegrationTests;

[Collection(nameof(HostCollection))]
public sealed class AttributionTests(HostFixture fixture)
{
    private const string Tenant = "northgroup";

    private readonly HostFixture _fixture = fixture;

    /// <summary>One column, straight from the row. No endpoint exposes these.</summary>
    private static async Task<string?> ColumnAsync(string sql, Guid id)
    {
        await using var connection = new SqlConnection(HostFixture.TenantConnectionString(Tenant));
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.AddWithValue("@id", id);

        var value = await command.ExecuteScalarAsync();
        return value == DBNull.Value ? null : value?.ToString();
    }

    private async Task<HttpResponseMessage> PostAsync(string email, string path, object body)
    {
        using var client = _fixture.CreateClient();
        var session = await _fixture.SignInAsync(email, Tenant);

        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(path, UriKind.Relative))
        {
            Content = JsonContent.Create(body),
        };
        request.Headers.Add("X-Tenant", Tenant);
        request.Headers.Add("Cookie", $"dfoss_session={session.SessionToken}");
        request.Headers.Add("X-CSRF-Token", session.AntiForgeryToken);

        return await client.SendAsync(request);
    }

    private async Task<Guid> AddCustomerAsync(string email)
    {
        using var response = await PostAsync(email, "/api/v1/customers", new
        {
            kind = "Person",
            firstName = "Attribution",
            lastName = $"Row{Guid.NewGuid():N}"[..14],
        });

        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("id").GetGuid();
    }

    [Fact]
    public async Task A_row_records_the_person_who_created_it()
    {
        var id = await AddCustomerAsync(DevelopmentSeeder.DevUsers.OrganizationWideEmail);

        var createdBy = await ColumnAsync(
            "SELECT CreatedBy FROM customers.Customers WHERE Id = @id", id);

        createdBy.Should().Be(
            DevelopmentSeeder.DevUsers.OrganizationWide.ToString(),
            because: "the row must name the person who made it, not a constant");
    }

    [Fact]
    public async Task Two_people_writing_produce_two_different_authors()
    {
        // The test a constant cannot pass. Any single value — "system", or even
        // one real user id hard-coded somewhere — satisfies "not empty" and
        // fails this.
        var byManager = await AddCustomerAsync(DevelopmentSeeder.DevUsers.OrganizationWideEmail);
        var bySalesperson = await AddCustomerAsync(DevelopmentSeeder.DevUsers.SalespersonEmail);

        var first = await ColumnAsync("SELECT CreatedBy FROM customers.Customers WHERE Id = @id", byManager);
        var second = await ColumnAsync("SELECT CreatedBy FROM customers.Customers WHERE Id = @id", bySalesperson);

        first.Should().Be(DevelopmentSeeder.DevUsers.OrganizationWide.ToString());
        second.Should().Be(DevelopmentSeeder.DevUsers.Salesperson.ToString());
        first.Should().NotBe(second);
    }

    [Fact]
    public async Task Changing_a_row_records_who_changed_it_without_losing_who_made_it()
    {
        // Two columns answering two different questions. A correction that
        // overwrote CreatedBy would destroy the more valuable of the two.
        var lead = await CaptureLeadAsync(DevelopmentSeeder.DevUsers.OrganizationWideEmail);

        using var moved = await PostAsync(
            DevelopmentSeeder.DevUsers.SalespersonEmail,
            $"/api/v1/leads/{lead}/status",
            new { status = "Working", note = "Picked it up." });

        moved.EnsureSuccessStatusCode();

        var createdBy = await ColumnAsync("SELECT CreatedBy FROM leads.Leads WHERE Id = @id", lead);
        var modifiedBy = await ColumnAsync("SELECT ModifiedBy FROM leads.Leads WHERE Id = @id", lead);

        createdBy.Should().Be(DevelopmentSeeder.DevUsers.OrganizationWide.ToString());
        modifiedBy.Should().Be(DevelopmentSeeder.DevUsers.Salesperson.ToString());
    }

    [Fact]
    public async Task Seeded_rows_stay_attributed_to_the_system_because_nobody_asked_for_them()
    {
        // Not a loophole — the honest answer, and asserted so that the fallback
        // cannot quietly start naming a person who was not there.
        await using var connection = new SqlConnection(HostFixture.TenantConnectionString(Tenant));
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT TOP 1 CreatedBy FROM org.DealerOrganizations";

        (await command.ExecuteScalarAsync())?.ToString().Should().Be("system");
    }

    private async Task<Guid> CaptureLeadAsync(string email)
    {
        var customer = await AddCustomerAsync(email);

        using var response = await PostAsync(email, "/api/v1/leads", new
        {
            rooftopId = await RooftopIdAsync(email),
            customerId = customer,
            source = "Phone",
            enquiry = "Asked what is on the lot.",
        });

        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("id").GetGuid();
    }

    /// <summary>The first rooftop, read from the API rather than assumed.</summary>
    private async Task<string> RooftopIdAsync(string email)
    {
        using var client = _fixture.CreateClient();
        var session = await _fixture.SignInAsync(email, Tenant);

        using var request = new HttpRequestMessage(
            HttpMethod.Get, new Uri("/api/v1/organization", UriKind.Relative));
        request.Headers.Add("X-Tenant", Tenant);
        request.Headers.Add("Cookie", $"dfoss_session={session.SessionToken}");

        using var response = await client.SendAsync(request);
        var root = await response.Content.ReadFromJsonAsync<JsonElement>();

        return root.GetProperty("legalEntities")
            .EnumerateArray()
            .SelectMany(entity => entity.GetProperty("rooftops").EnumerateArray())
            .First()
            .GetProperty("id")
            .ToString();
    }
}
