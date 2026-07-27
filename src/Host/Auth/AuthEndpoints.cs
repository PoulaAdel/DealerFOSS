// AuthEndpoints — signing in, signing out, and reporting who you are.
//
// Use:  POST /api/v1/auth/login sets the session cookie; POST .../logout clears
//       and revokes it; GET .../me confirms the caller. All require X-Tenant,
//       because a user belongs to one dealer organization.
// Edit: the cookie is HttpOnly so script cannot read it, SameSite=Strict so
//       another site cannot cause a request with it, and Secure outside
//       Development. Do not relax any of the three to make a client easier to
//       write. The token is returned only in the cookie, never in the body.

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using OpenDealer360.Core;
using OpenDealer360.Identity.Contracts;

namespace OpenDealer360.Host.Auth;

internal static class AuthEndpoints
{
    /// <summary>Name of the cookie carrying the session token.</summary>
    public const string SessionCookie = "odms_session";

    public static void MapAuth(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/auth").WithTags("Auth");

        group.MapPost("/login", LoginAsync);
        group.MapPost("/logout", LogoutAsync);
        group.MapGet("/me", Me);
    }

    private static async Task<IResult> LoginAsync(
        LoginRequest request,
        HttpContext context,
        IAuthenticator authenticator,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var result = await authenticator.SignInAsync(
            request.Email,
            request.Password,
            DescribeDevice(context),
            cancellationToken);

        if (result.IsFailure)
        {
            // 401, not 403: the caller may retry with different credentials.
            return Results.Problem(
                title: result.Error.Code,
                detail: result.Error.Message,
                statusCode: StatusCodes.Status401Unauthorized);
        }

        context.Response.Cookies.Append(
            SessionCookie,
            result.Value.Token,
            BuildCookieOptions(context, result.Value.AbsoluteExpiresAt));

        return Results.Ok(new { expiresAt = result.Value.AbsoluteExpiresAt });
    }

    private static async Task<IResult> LogoutAsync(
        HttpContext context,
        IAuthenticator authenticator,
        CancellationToken cancellationToken)
    {
        if (context.Request.Cookies.TryGetValue(SessionCookie, out var token))
        {
            await authenticator.RevokeAsync(token, cancellationToken);
        }

        // Clear the cookie with the same attributes it was set with, or the
        // browser keeps it.
        context.Response.Cookies.Append(
            SessionCookie, string.Empty, BuildCookieOptions(context, DateTimeOffset.UnixEpoch));

        return Results.NoContent();
    }

    private static IResult Me(ICurrentUser currentUser) =>
        Results.Ok(new { userId = currentUser.Id });

    private static CookieOptions BuildCookieOptions(HttpContext context, DateTimeOffset expiresAt) =>
        new()
        {
            HttpOnly = true,
            SameSite = SameSiteMode.Strict,
            // Development runs over plain HTTP; everywhere else the cookie must
            // never travel unencrypted.
            Secure = !context.RequestServices
                .GetRequiredService<IWebHostEnvironment>()
                .IsDevelopment(),
            Path = "/",
            Expires = expiresAt,
        };

    /// <summary>
    /// A coarse device label for the user's own session list. Deliberately not
    /// the full user-agent, which is unnecessarily identifying.
    /// </summary>
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

/// <summary>Credentials posted to the login endpoint.</summary>
internal sealed record LoginRequest(string Email, string Password);
