// TenantMiddleware — resolves which dealer organization a request belongs to,
// once, before any endpoint runs.
//
// Use:  automatic for every /api/v1 path. Health and root are exempt so probes
//       work without a tenant.
// Edit: the X-Tenant header is provisional. When sessions land (ADR-009) the
//       tenant comes from the authenticated session and this header path is
//       deleted. Refusing early is deliberate — a missing tenant must never
//       reach a data call.

using OpenDealer360.Tenancy;
using OpenDealer360.Core;

namespace OpenDealer360.Tenancy;

/// <summary>
/// Resolves the request's dealer organization once, before any endpoint runs
/// (doc 04 §5). Health and root are exempt. Tenant-scoped API calls without a
/// resolvable tenant are rejected here, not deep in a data call.
/// </summary>
/// <remarks>
/// Provisional resolution source: the <c>X-Tenant</c> header (routing key). When
/// the Identity module lands (ADR-009), the tenant is taken from the authenticated
/// session and this header path is removed.
/// </remarks>
public sealed class TenantMiddleware(RequestDelegate next)
{
    public const string TenantHeader = "X-Tenant";

    private const string ApiPrefix = "/api/v1";

    /// <summary>
    /// Control-plane paths. A dealer organization is optional there rather than
    /// required: listing the installation's tenants belongs to no single one of
    /// them, while opening support access names the one being entered. The header
    /// is still honoured and still validated when it is supplied — "optional" is
    /// not "unchecked".
    /// </summary>
    private const string AdminPrefix = "/api/v1/admin";

    public async Task InvokeAsync(
        HttpContext context,
        ITenantResolver resolver,
        ITenantContext tenantContext)
    {
        if (!context.Request.Path.StartsWithSegments(ApiPrefix))
        {
            await next(context);
            return;
        }

        var isControlPlane = context.Request.Path.StartsWithSegments(AdminPrefix);
        var tenantKey = context.Request.Headers[TenantHeader].ToString();

        if (string.IsNullOrWhiteSpace(tenantKey))
        {
            if (isControlPlane)
            {
                await next(context);
                return;
            }

            await WriteProblemAsync(
                context,
                StatusCodes.Status400BadRequest,
                "tenant_required",
                $"Provide the {TenantHeader} header identifying the dealer organization.");
            return;
        }

        var resolved = await resolver.ResolveByKeyAsync(tenantKey, context.RequestAborted);
        if (resolved is null)
        {
            await WriteProblemAsync(
                context,
                StatusCodes.Status404NotFound,
                "tenant_not_found",
                "No active dealer organization matches the supplied tenant key.");
            return;
        }

        tenantContext.Set(resolved);
        await next(context);
    }

    private static Task WriteProblemAsync(HttpContext context, int status, string code, string detail)
    {
        context.Response.StatusCode = status;
        return context.Response.WriteAsJsonAsync(new
        {
            type = "about:blank",
            title = code,
            status,
            code,
            detail,
        });
    }
}
