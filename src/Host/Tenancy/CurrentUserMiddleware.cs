// CurrentUserMiddleware — identifies the caller from their session cookie, after
// the tenant is known.
//
// Use:  automatic for every /api/v1 path; runs after TenantMiddleware. Login and
//       logout are exempt, since those are where a session is obtained or ended.
// Edit: the session is checked against the database on every request, so
//       revoking one takes effect immediately rather than whenever a token would
//       have expired. Do not cache that lookup without also solving revocation —
//       that trade is the whole reason sessions are durable rather than stateless.

using OpenDealer360.Core;
using OpenDealer360.Host.Auth;
using OpenDealer360.Identity.Contracts;

namespace OpenDealer360.Host.Tenancy;

/// <summary>
/// Resolves the calling user for the request. Tenant-scoped API calls without a
/// valid session are refused here rather than deep in a service.
/// </summary>
public sealed class CurrentUserMiddleware(RequestDelegate next)
{
    private const string ApiPrefix = "/api/v1";

    /// <summary>The only endpoints reachable without a session.</summary>
    private static readonly string[] AnonymousPaths =
    [
        "/api/v1/auth/login",
        "/api/v1/auth/logout",
    ];

    private readonly RequestDelegate _next = next;

    // IAuthenticator is resolved inside the method, not as a parameter. Injected
    // middleware parameters are constructed before the method body runs, and
    // building it reaches the tenant-bound DbContext — which throws on paths
    // like /health where no tenant was ever resolved.
    public async Task InvokeAsync(HttpContext context, ICurrentUser currentUser)
    {
        if (!context.Request.Path.StartsWithSegments(ApiPrefix)
            || AnonymousPaths.Contains(context.Request.Path.Value, StringComparer.OrdinalIgnoreCase))
        {
            await _next(context);
            return;
        }

        var authenticator = context.RequestServices.GetRequiredService<IAuthenticator>();

        if (!context.Request.Cookies.TryGetValue(AuthEndpoints.SessionCookie, out var token)
            || string.IsNullOrWhiteSpace(token))
        {
            await WriteProblemAsync(
                context,
                StatusCodes.Status401Unauthorized,
                "auth.session_required",
                "Sign in to use this endpoint.");
            return;
        }

        var result = await authenticator.ValidateAsync(token, context.RequestAborted);
        if (result.IsFailure)
        {
            await WriteProblemAsync(
                context,
                StatusCodes.Status401Unauthorized,
                result.Error.Code,
                result.Error.Message);
            return;
        }

        currentUser.Set(result.Value);
        await _next(context);
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
