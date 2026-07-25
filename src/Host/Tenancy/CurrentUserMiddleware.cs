using OpenDealer360.Platform.Security;

namespace OpenDealer360.Host.Tenancy;

/// <summary>
/// Resolves the calling user for the request, after the tenant is known.
/// Tenant-scoped API calls without an identified user are refused here rather
/// than deep in a service.
/// </summary>
/// <remarks>
/// Provisional source: the <c>X-User</c> header. When durable sessions land
/// (ADR-009), the user comes from the authenticated session cookie and this
/// header path is removed. It is accepted only in the Development environment
/// so a deployed instance can never be driven by a client-supplied identity.
/// </remarks>
public sealed class CurrentUserMiddleware(RequestDelegate next, IWebHostEnvironment environment)
{
    public const string UserHeader = "X-User";

    private const string ApiPrefix = "/api/v1";

    private readonly RequestDelegate _next = next;
    private readonly IWebHostEnvironment _environment = environment;

    public async Task InvokeAsync(HttpContext context, ICurrentUser currentUser)
    {
        if (!context.Request.Path.StartsWithSegments(ApiPrefix))
        {
            await _next(context);
            return;
        }

        if (!_environment.IsDevelopment())
        {
            // No authentication mechanism exists yet, so outside Development
            // there is no safe way to identify a caller. Refuse rather than
            // silently treating requests as anonymous-but-allowed.
            await WriteProblemAsync(
                context,
                StatusCodes.Status401Unauthorized,
                "authentication_unavailable",
                "Session authentication is not yet enabled in this environment.");
            return;
        }

        var header = context.Request.Headers[UserHeader].ToString();
        if (!Guid.TryParse(header, out var userId))
        {
            await WriteProblemAsync(
                context,
                StatusCodes.Status401Unauthorized,
                "user_required",
                $"Provide the {UserHeader} header with the calling user's id.");
            return;
        }

        currentUser.Set(userId);
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
