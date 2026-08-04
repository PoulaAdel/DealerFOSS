// AntiForgeryMiddleware — refuses a state-changing request that does not prove it
// came from this application rather than from another site.
//
// Use:  automatic for every /api/v1 path; runs after CurrentUserMiddleware, so a
//       request that reaches it already has a session.
// Edit: the rule is deliberately blunt — if the request carries a session cookie
//       and changes something, it must also carry the matching anti-forgery
//       token in a header. Exemptions are the two paths where no session can
//       exist yet, and nothing else. Adding an endpoint to that list is a
//       security decision, not a convenience.
//
//       Why a header and not a second cookie comparison: a cross-site form post
//       can carry cookies but cannot set a custom header, and a cross-origin
//       fetch that tries is stopped by the browser's preflight. Why the token is
//       bound to the session rather than merely echoed: otherwise anything able
//       to write a cookie for this site could supply both halves of the pair.

using DealerFOSS.Administration;
using DealerFOSS.App;
using DealerFOSS.Identity;

namespace DealerFOSS.Tenancy;

/// <summary>
/// Enforces ADR-009's CSRF protection on every write. The session cookie is
/// <c>SameSite=Strict</c>, which stops the common cases on its own; this is the
/// explicit check that does not depend on the browser honouring that attribute.
/// </summary>
public sealed class AntiForgeryMiddleware(RequestDelegate next)
{
    private const string ApiPrefix = "/api/v1";

    /// <summary>
    /// The only writes allowed without a token: the two that create a session,
    /// which by definition cannot present one issued by a session that does not
    /// exist. Both are protected by credentials instead.
    /// </summary>
    private static readonly string[] ExemptPaths =
    [
        "/api/v1/auth/login",
        "/api/v1/auth/login/second-factor",
        // The control-plane equivalent, for the identical reason: no
        // administrator session exists yet to have issued a token.
        "/api/v1/admin/login",
    ];

    private readonly RequestDelegate _next = next;

    public async Task InvokeAsync(HttpContext context)
    {
        // Which pair of secrets applies is decided by the path, not by which
        // cookies happen to be present. After support access is granted a browser
        // holds both, and a control-plane write must not be satisfiable with a
        // token minted for a dealership session — or the two worlds this
        // middleware sits between would be one world again.
        var isControlPlane = context.Request.Path.StartsWithSegments(
            AdministratorMiddleware.AdminPrefix);

        var cookieName = isControlPlane
            ? AdminEndpoints.AdminSessionCookie
            : AuthEndpoints.SessionCookie;

        var headerName = isControlPlane
            ? AdminEndpoints.AdminAntiForgeryHeader
            : AuthEndpoints.AntiForgeryHeader;

        if (!RequiresAntiForgery(context.Request, cookieName))
        {
            await _next(context);
            return;
        }

        var sessionToken = context.Request.Cookies[cookieName]!;
        var presented = context.Request.Headers[headerName].ToString();

        var verified = isControlPlane
            ? await context.RequestServices.GetRequiredService<IGlobalAdministration>()
                .VerifyAntiForgeryAsync(sessionToken, presented, context.RequestAborted)
            : await context.RequestServices.GetRequiredService<IAuthenticator>()
                .VerifyAntiForgeryAsync(sessionToken, presented, context.RequestAborted);

        if (verified)
        {
            await _next(context);
            return;
        }

        var error = isControlPlane ? AdminErrors.AntiForgeryFailed : AuthErrors.AntiForgeryFailed;
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        await context.Response.WriteAsJsonAsync(new
        {
            type = "about:blank",
            title = error.Code,
            status = StatusCodes.Status403Forbidden,
            code = error.Code,
            detail = error.Message,
        });
    }

    /// <summary>
    /// A request needs a token when it changes something, is addressed to the
    /// API, and arrives with a session cookie. Without a session there is nothing
    /// to ride — such a request is unauthenticated and was already refused.
    /// </summary>
    private static bool RequiresAntiForgery(HttpRequest request, string cookieName) =>
        request.Path.StartsWithSegments(ApiPrefix)
        && !HttpMethods.IsGet(request.Method)
        && !HttpMethods.IsHead(request.Method)
        && !HttpMethods.IsOptions(request.Method)
        && !HttpMethods.IsTrace(request.Method)
        && !ExemptPaths.Contains(request.Path.Value, StringComparer.OrdinalIgnoreCase)
        && !string.IsNullOrWhiteSpace(request.Cookies[cookieName]);
}
