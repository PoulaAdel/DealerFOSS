// AdministratorMiddleware — identifies the caller on control-plane paths, from a
// cookie that means nothing anywhere else.
//
// Use:  automatic for every /api/v1/admin path, and only those. Runs after
//       TenantMiddleware, because opening support access needs to know which
//       dealership is being entered.
// Edit: read what this file does NOT do. It never calls ICurrentUser.Set, so an
//       administrator cannot become a tenant caller by any route through here.
//       The reverse holds by construction too: CurrentUserMiddleware only ever
//       looks at the tenant session cookie against the tenant's own database, so
//       an administrator cookie presented to a business endpoint resolves to
//       nobody and is refused before any endpoint runs.
//
//       That is the separation, and it is structural rather than a permission
//       check each capability has to remember (doc 06 §3).

using OpenDealer360.Core;
using OpenDealer360.Identity;

namespace OpenDealer360.Administration;

/// <summary>
/// Resolves the calling administrator for a control-plane request. Refuses here
/// rather than deep in an endpoint.
/// </summary>
public sealed class AdministratorMiddleware(RequestDelegate next)
{
    /// <summary>Every control-plane path, and nothing else, sits under this.</summary>
    public const string AdminPrefix = "/api/v1/admin";

    /// <summary>
    /// The only control-plane endpoints reachable without an administrator
    /// session: the ones where a session is obtained or ended.
    /// </summary>
    private static readonly string[] AnonymousPaths =
    [
        "/api/v1/admin/login",
        "/api/v1/admin/logout",
    ];

    /// <summary>
    /// All an administrator who has not enrolled a second factor may reach.
    /// Nothing that touches a dealership is on this list — an unenrolled account
    /// cannot open support access, which is the point of requiring it.
    /// </summary>
    private static readonly string[] EnrolmentPaths =
    [
        "/api/v1/admin/me",
        "/api/v1/admin/mfa/enrol",
        "/api/v1/admin/mfa/confirm",
        "/api/v1/admin/logout",
    ];

    private readonly RequestDelegate _next = next;

    public async Task InvokeAsync(HttpContext context, ICurrentAdministrator currentAdministrator)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(currentAdministrator);

        if (!context.Request.Path.StartsWithSegments(AdminPrefix)
            || AnonymousPaths.Contains(context.Request.Path.Value, StringComparer.OrdinalIgnoreCase))
        {
            await _next(context);
            return;
        }

        if (!context.Request.Cookies.TryGetValue(AdminEndpoints.AdminSessionCookie, out var token)
            || string.IsNullOrWhiteSpace(token))
        {
            // A perfectly good tenant session is still the wrong kind of identity
            // here, and says so, rather than pretending the endpoint is missing.
            await WriteProblemAsync(
                context,
                StatusCodes.Status401Unauthorized,
                "admin.session_required",
                "Sign in as an administrator to use the control plane. A dealership sign-in "
                + "does not reach it.");
            return;
        }

        var administration = context.RequestServices.GetRequiredService<IGlobalAdministration>();
        var result = await administration.ValidateAsync(token, context.RequestAborted);

        if (result.IsFailure)
        {
            await WriteProblemAsync(
                context, StatusCodes.Status401Unauthorized, result.Error.Code, result.Error.Message);
            return;
        }

        var administrator = result.Value;
        currentAdministrator.Set(
            administrator.Id, administrator.Email, administrator.MustEnrolSecondFactor);

        if (administrator.MustEnrolSecondFactor
            && !EnrolmentPaths.Contains(context.Request.Path.Value, StringComparer.OrdinalIgnoreCase))
        {
            var error = AdminErrors.SecondFactorRequired;
            await WriteProblemAsync(
                context, StatusCodes.Status403Forbidden, error.Code, error.Message);
            return;
        }

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
