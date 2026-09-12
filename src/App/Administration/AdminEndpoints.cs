// Copyright (c) 2026 The DealerFOSS contributors.
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Overview: Purpose, File Design, and Engineering
//   AdminEndpoints — the control plane: operating the deployment, and the one
//   deliberate door into a dealership's data.
//
// Usage:
//   POST /api/v1/admin/login sets the administrator cookie. Everything else
//   under /api/v1/admin needs it. Support access additionally needs the
//   X-Tenant header naming the dealership being entered.
//
// Coding Instructions:
//   What an administrator may reach is decided by which endpoints exist here,
//   not by a permission check. There is no business capability on this
//   router, and adding one would be the mistake this whole area prevents —
//   an administrator who wants a customer record opens support access and
//   reads it as the dealership's own support principal, in their own log.
//
//   The administrator cookies are named apart from the tenant ones on
//   purpose. After support access is granted a browser holds both, and a
//   shared name would mean one silently overwriting the other.

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using DealerFOSS.App;
using DealerFOSS.Core;
using DealerFOSS.Identity;
using DealerFOSS.Tenancy;

namespace DealerFOSS.Administration;

internal static class AdminEndpoints
{
    /// <summary>The administrator session cookie. Means nothing to a tenant endpoint.</summary>
    public const string AdminSessionCookie = "dfoss_admin";

    /// <summary>Script-readable, so the client can copy it into the header below.</summary>
    public const string AdminAntiForgeryCookie = "dfoss_admin_csrf";

    /// <summary>Separate from the tenant header, because a browser may hold both.</summary>
    public const string AdminAntiForgeryHeader = "X-Admin-CSRF-Token";

    public static void MapAdministration(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/admin").WithTags("Administration");

        // The account that can step into any dealership is the one worth
        // guessing at, so this is limited like the dealership door.
        group.MapPost("/login", LoginAsync).RequireRateLimiting(RateLimits.Credentials);
        group.MapPost("/logout", LogoutAsync);
        group.MapGet("/me", Me);

        group.MapPost("/mfa/enrol", EnrolMfaAsync);

        // Limited for the same reason as the dealership's own confirm: it takes
        // a guessable six-digit code and has no per-secret attempt counter
        // behind it. An administrator's second factor is the one guarding every
        // dealership in the installation.
        group.MapPost("/mfa/confirm", ConfirmMfaAsync)
            .RequireRateLimiting(RateLimits.Credentials);

        // Operating the installation: which dealerships exist, and whether each
        // is usable. Routing and lifecycle only — never their contents.
        group.MapGet("/tenants", ListTenantsAsync);
        group.MapPost("/tenants", CreateTenantAsync);
        group.MapPost("/tenants/{slug}/status", SetTenantStatusAsync);

        group.MapPost("/support-access", OpenSupportAccessAsync);
        group.MapGet("/support-access", ListSupportAccessAsync);
        group.MapPost("/support-access/{id:guid}/end", EndSupportAccessAsync);
    }

    private static async Task<IResult> LoginAsync(
        AdminLoginRequest request,
        HttpContext context,
        IGlobalAdministration administration,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var result = await administration.SignInAsync(
            request.Email, request.Password, request.Code, DescribeDevice(context), cancellationToken);

        if (result.IsFailure)
        {
            return Results.Problem(
                title: result.Error.Code,
                detail: result.Error.Message,
                statusCode: StatusCodes.Status401Unauthorized,
                extensions: new Dictionary<string, object?> { ["code"] = result.Error.Code });
        }

        var session = result.Value;

        context.Response.Cookies.Append(
            AdminSessionCookie,
            session.Token,
            AuthEndpoints.BuildCookieOptions(context, session.AbsoluteExpiresAt));

        context.Response.Cookies.Append(
            AdminAntiForgeryCookie,
            session.AntiForgeryToken,
            AuthEndpoints.BuildCookieOptions(context, session.AbsoluteExpiresAt, readableByScript: true));

        return Results.Ok(new { expiresAt = session.AbsoluteExpiresAt });
    }

    private static async Task<IResult> LogoutAsync(
        HttpContext context,
        IGlobalAdministration administration,
        CancellationToken cancellationToken)
    {
        if (context.Request.Cookies.TryGetValue(AdminSessionCookie, out var token))
        {
            await administration.RevokeAsync(token, cancellationToken);
        }

        context.Response.Cookies.Append(
            AdminSessionCookie, string.Empty,
            AuthEndpoints.BuildCookieOptions(context, DateTimeOffset.UnixEpoch));
        context.Response.Cookies.Append(
            AdminAntiForgeryCookie, string.Empty,
            AuthEndpoints.BuildCookieOptions(context, DateTimeOffset.UnixEpoch, readableByScript: true));

        return Results.NoContent();
    }

    /// <summary>
    /// Reachable even by an administrator who owes a second factor — it is how a
    /// client learns to show the enrolment screen rather than the console.
    /// </summary>
    private static IResult Me(ICurrentAdministrator administrator) =>
        Results.Ok(new
        {
            administratorId = administrator.Id,
            email = administrator.Email,
            mustEnrolSecondFactor = administrator.MustEnrolSecondFactor,
        });

    private static async Task<IResult> EnrolMfaAsync(
        ICurrentAdministrator administrator,
        IGlobalAdministration administration,
        CancellationToken cancellationToken)
    {
        var result = await administration.BeginMfaEnrolmentAsync(administrator.Id, cancellationToken);
        return result.IsSuccess ? Results.Ok(result.Value) : result.Error.ToProblem();
    }

    private static async Task<IResult> ConfirmMfaAsync(
        AdminCodeRequest request,
        ICurrentAdministrator administrator,
        IGlobalAdministration administration,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var result = await administration.ConfirmMfaAsync(
            administrator.Id, request.Code, cancellationToken);

        return result.IsSuccess ? Results.NoContent() : result.Error.ToProblem();
    }

    /// <summary>
    /// Routing and lifecycle for every dealership on this installation. Note what
    /// each row carries: a name, a key, a state, and a schema version. Nothing an
    /// administrator could learn about a dealership's business from reading it.
    /// </summary>
    /// <summary>
    /// Creates a dealership. Reachable only behind the administrator cookie like
    /// everything else in this group — and the enrolment code in the response is
    /// the only time it can be seen.
    /// </summary>
    private static async Task<IResult> CreateTenantAsync(
        NewTenant request,
        ITenantProvisioning provisioning,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var result = await provisioning.CreateAsync(request, cancellationToken);

        return result.IsSuccess
            ? Results.Created($"/api/v1/admin/tenants/{result.Value.Slug}", result.Value)
            : result.Error.ToProblem();
    }

    private static async Task<IResult> ListTenantsAsync(
        HostDb hostCatalog,
        CancellationToken cancellationToken,
        int limit = 50,
        int offset = 0)
    {
        var take = Paging.Limit(limit);
        var skip = Paging.Offset(offset);

        var rows = hostCatalog.Tenants.AsNoTracking();
        var total = await rows.CountAsync(cancellationToken);

        // Slug is unique, so it is already a total order and needs no tiebreak.
        var tenants = await rows
            .OrderBy(t => t.Slug)
            .Skip(skip)
            .Take(take)
            .Select(t => new
            {
                t.Slug,
                t.Name,
                status = t.Status.ToString(),
                t.DatabaseVersion,
                t.CreatedAt,
            })
            .ToListAsync(cancellationToken);

        return Results.Ok(new Page<object>(tenants, total, skip, take));
    }

    private static async Task<IResult> SetTenantStatusAsync(
        string slug,
        TenantStatusRequest request,
        HostDb hostCatalog,
        ITenantResolver resolver,
        IClock clock,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!Enum.TryParse<TenantStatus>(request.Status, ignoreCase: true, out var status))
        {
            return Error.Validation(
                "admin.tenant_status_unknown",
                $"'{request.Status}' is not a tenant status. Use one of: "
                + string.Join(", ", Enum.GetNames<TenantStatus>()) + ".").ToProblem();
        }

        var tenant = await hostCatalog.Tenants
            .SingleOrDefaultAsync(t => t.Slug == slug, cancellationToken);

        if (tenant is null)
        {
            return Error.NotFound(
                "admin.tenant_unknown",
                "No dealer organization on this installation has that key.").ToProblem();
        }

        tenant.Status = status;
        tenant.ModifiedAt = clock.UtcNow;
        await hostCatalog.SaveChangesAsync(cancellationToken);

        // Routing is cached in process. Without this a suspension would take
        // effect whenever the cache happened to expire, which is not a control.
        resolver.Invalidate(slug);

        return Results.Ok(new { tenant.Slug, status = tenant.Status.ToString() });
    }

    /// <summary>
    /// Opens a time-limited way into the dealership named by X-Tenant, and returns
    /// a tenant session for it. The cookies set here belong to the dealership's
    /// own support principal — a read-only account no password can open — so
    /// everything done next is attributed to it in the dealership's own log.
    /// </summary>
    private static async Task<IResult> OpenSupportAccessAsync(
        SupportAccessRequest request,
        HttpContext context,
        ICurrentAdministrator administrator,
        IGlobalAdministration administration,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var result = await administration.GrantSupportAccessAsync(
            administrator.Id,
            request.Reason,
            TimeSpan.FromMinutes(request.Minutes),
            cancellationToken);

        if (result.IsFailure)
        {
            return result.Error.ToProblem();
        }

        var granted = result.Value;

        context.Response.Cookies.Append(
            AuthEndpoints.SessionCookie,
            granted.SessionToken,
            AuthEndpoints.BuildCookieOptions(context, granted.ExpiresAt));

        context.Response.Cookies.Append(
            AuthEndpoints.AntiForgeryCookie,
            granted.AntiForgeryToken,
            AuthEndpoints.BuildCookieOptions(context, granted.ExpiresAt, readableByScript: true));

        return Results.Ok(new
        {
            grantId = granted.GrantId,
            tenant = granted.TenantSlug,
            expiresAt = granted.ExpiresAt,
        });
    }

    private static async Task<IResult> ListSupportAccessAsync(
        IGlobalAdministration administration,
        CancellationToken cancellationToken) =>
        Results.Ok(await administration.ListSupportAccessAsync(cancellationToken));

    private static async Task<IResult> EndSupportAccessAsync(
        Guid id,
        HttpContext context,
        ICurrentAdministrator administrator,
        IGlobalAdministration administration,
        CancellationToken cancellationToken)
    {
        var result = await administration.EndSupportAccessAsync(
            id, administrator.Id, cancellationToken);

        if (result.IsFailure)
        {
            return result.Error.ToProblem();
        }

        // The tenant cookies are cleared too, so the browser cannot keep
        // presenting a session that has just been revoked.
        context.Response.Cookies.Append(
            AuthEndpoints.SessionCookie, string.Empty,
            AuthEndpoints.BuildCookieOptions(context, DateTimeOffset.UnixEpoch));
        context.Response.Cookies.Append(
            AuthEndpoints.AntiForgeryCookie, string.Empty,
            AuthEndpoints.BuildCookieOptions(context, DateTimeOffset.UnixEpoch, readableByScript: true));

        return Results.NoContent();
    }

    private static string? DescribeDevice(HttpContext context)
    {
        var agent = context.Request.Headers.UserAgent.ToString();
        if (string.IsNullOrWhiteSpace(agent))
        {
            return null;
        }

        return agent.Length <= 60 ? agent : agent[..60];
    }
}

/// <summary>
/// Administrator credentials. The code is in the same request as the password
/// because a second factor is mandatory here — there is no branch where the
/// password alone means anything, so there is nothing for a challenge to protect.
/// </summary>
internal sealed record AdminLoginRequest(string Email, string Password, string? Code);

/// <summary>A code from the authenticator app.</summary>
internal sealed record AdminCodeRequest(string Code);

/// <summary>A new lifecycle state for one dealership's routing row.</summary>
internal sealed record TenantStatusRequest(string Status);

/// <summary>
/// Why support is being given access, and for how long. The reason is required
/// and the window is clamped; asking for a day gets an hour.
/// </summary>
internal sealed record SupportAccessRequest(string Reason, int Minutes);
