// Copyright (c) 2026 The DealerFOSS contributors.
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Overview: Purpose, File Design, and Engineering
//   LeadEndpoints — the HTTP surface for enquiries.
//
// Usage:
//   Mapped from Program.cs; routes under /api/v1/leads.
//   GET  /api/v1/leads?rooftopId=&status=Working&assignedTo=&openOnly=true
//   GET  /api/v1/leads/{id}
//   POST /api/v1/leads
//   POST /api/v1/leads/{id}/status
//   POST /api/v1/leads/{id}/assign
//
// Coding Instructions:
//   Keep it thin — delegate, then map a Result to a status code. The rooftop
//   scope is applied in LeadService, not here, so a background caller gets
//   the same check.

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using DealerFOSS.App;
using DealerFOSS.Core;

namespace DealerFOSS.Leads;

internal static class LeadEndpoints
{
    public static void MapLeads(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/leads").WithTags("Leads");

        group.MapGet("", ListAsync);
        group.MapGet("/{leadId:guid}", GetAsync);
        group.MapPost("", CaptureAsync);
        group.MapPost("/{leadId:guid}/status", ChangeStatusAsync);
        group.MapPost("/{leadId:guid}/assign", AssignAsync);
    }

    private static async Task<IResult> ListAsync(
        ILeads leads,
        CancellationToken cancellationToken,
        Guid? rooftopId = null,
        string? status = null,
        Guid? assignedTo = null,
        Guid? customerId = null,
        bool openOnly = false,
        int limit = 50)
    {
        var query = new LeadQuery(
            rooftopId is null ? null : new RooftopId(rooftopId.Value),
            status,
            assignedTo,
            customerId,
            openOnly,
            limit);

        var result = await leads.ListAsync(query, cancellationToken);
        return result.IsSuccess ? Results.Ok(result.Value) : result.Error.ToProblem();
    }

    private static async Task<IResult> GetAsync(
        Guid leadId,
        ILeads leads,
        CancellationToken cancellationToken)
    {
        var result = await leads.GetAsync(leadId, cancellationToken);
        return result.IsSuccess ? Results.Ok(result.Value) : result.Error.ToProblem();
    }

    private static async Task<IResult> CaptureAsync(
        NewLead request,
        ILeads leads,
        CancellationToken cancellationToken)
    {
        var result = await leads.CaptureAsync(request, cancellationToken);

        return result.IsSuccess
            ? Results.Created($"/api/v1/leads/{result.Value.Id}", result.Value)
            : result.Error.ToProblem();
    }

    private static async Task<IResult> ChangeStatusAsync(
        Guid leadId,
        LeadStatusChangeRequest request,
        ILeads leads,
        CancellationToken cancellationToken)
    {
        var result = await leads.ChangeStatusAsync(leadId, request, cancellationToken);
        return result.IsSuccess ? Results.Ok(result.Value) : result.Error.ToProblem();
    }

    private static async Task<IResult> AssignAsync(
        Guid leadId,
        AssignLeadRequest request,
        ILeads leads,
        CancellationToken cancellationToken)
    {
        var result = await leads.AssignAsync(leadId, request, cancellationToken);
        return result.IsSuccess ? Results.Ok(result.Value) : result.Error.ToProblem();
    }
}
