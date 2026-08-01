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

using OpenDealer360.App;
using OpenDealer360.Identity;

namespace OpenDealer360.Tenancy;

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
    ];

    private readonly RequestDelegate _next = next;

    public async Task InvokeAsync(HttpContext context)
    {
        if (!RequiresAntiForgery(context.Request))
        {
            await _next(context);
            return;
        }

        var authenticator = context.RequestServices.GetRequiredService<IAuthenticator>();
        var sessionToken = context.Request.Cookies[AuthEndpoints.SessionCookie]!;
        var presented = context.Request.Headers[AuthEndpoints.AntiForgeryHeader].ToString();

        if (await authenticator.VerifyAntiForgeryAsync(sessionToken, presented, context.RequestAborted))
        {
            await _next(context);
            return;
        }

        var error = AuthErrors.AntiForgeryFailed;
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
    private static bool RequiresAntiForgery(HttpRequest request) =>
        request.Path.StartsWithSegments(ApiPrefix)
        && !HttpMethods.IsGet(request.Method)
        && !HttpMethods.IsHead(request.Method)
        && !HttpMethods.IsOptions(request.Method)
        && !HttpMethods.IsTrace(request.Method)
        && !ExemptPaths.Contains(request.Path.Value, StringComparer.OrdinalIgnoreCase)
        && !string.IsNullOrWhiteSpace(request.Cookies[AuthEndpoints.SessionCookie]);
}
