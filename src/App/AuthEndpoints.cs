// AuthEndpoints — signing in, signing out, and reporting who you are.
//
// Use:  POST /api/v1/auth/login sets the session cookie; POST .../logout clears
//       and revokes it; GET .../me confirms the caller. All require X-Tenant,
//       because a user belongs to one dealer organization.
// Edit: the session cookie is HttpOnly so script cannot read it, SameSite=Strict
//       so another site cannot cause a request with it, and Secure outside
//       Development. Do not relax any of the three to make a client easier to
//       write. The token is returned only in the cookie, never in the body.
//
//       Sign-in sets a second cookie carrying the anti-forgery token. That one
//       is deliberately readable by script — the client has to read it to put it
//       in a header, and a header is the thing a cross-site form cannot forge.
//       It is not a credential: on its own it opens nothing.

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using OpenDealer360.Core;
using OpenDealer360.Identity;

namespace OpenDealer360.App;

internal static class AuthEndpoints
{
    /// <summary>Name of the cookie carrying the session token.</summary>
    public const string SessionCookie = "odms_session";

    /// <summary>
    /// Name of the cookie carrying this session's anti-forgery token. Readable by
    /// script on purpose, so the client can copy it into <see cref="AntiForgeryHeader"/>.
    /// </summary>
    public const string AntiForgeryCookie = "odms_csrf";

    /// <summary>
    /// The header a write must carry. "CSRF" rather than "anti-forgery" because
    /// every proxy, browser tool, and developer already recognises the name.
    /// </summary>
    public const string AntiForgeryHeader = "X-CSRF-Token";

    public static void MapAuth(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/auth").WithTags("Auth");

        group.MapPost("/login", LoginAsync);
        group.MapPost("/login/second-factor", SecondFactorAsync);
        group.MapPost("/logout", LogoutAsync);
        group.MapGet("/me", Me);

        group.MapPost("/mfa/enrol", EnrolMfaAsync);
        group.MapPost("/mfa/confirm", ConfirmMfaAsync);
        group.MapPost("/mfa/disable", DisableMfaAsync);
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
            return Unauthorized(result.Error);
        }

        // An account with a second factor gets a challenge, not a cookie. The
        // challenge is returned in the body rather than set as a cookie so it
        // cannot be mistaken for a session by anything downstream.
        if (!result.Value.IsComplete)
        {
            var challenge = result.Value.Challenge!;
            return Results.Ok(new
            {
                secondFactorRequired = true,
                challengeToken = challenge.Token,
                expiresAt = challenge.ExpiresAt,
            });
        }

        return CompleteSession(context, result.Value.Session!);
    }

    private static async Task<IResult> SecondFactorAsync(
        SecondFactorRequest request,
        HttpContext context,
        IAuthenticator authenticator,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var result = await authenticator.CompleteSignInAsync(
            request.ChallengeToken, request.Code, cancellationToken);

        return result.IsFailure
            ? Unauthorized(result.Error)
            : CompleteSession(context, result.Value);
    }

    private static async Task<IResult> EnrolMfaAsync(
        ICurrentUser currentUser,
        IAuthenticator authenticator,
        CancellationToken cancellationToken)
    {
        var result = await authenticator.BeginMfaEnrolmentAsync(currentUser.Id, cancellationToken);

        // The secret is returned once, to be shown as a QR code and then
        // forgotten by the client.
        return result.IsSuccess ? Results.Ok(result.Value) : Problem(result.Error);
    }

    private static async Task<IResult> ConfirmMfaAsync(
        SecondFactorCodeRequest request,
        ICurrentUser currentUser,
        IAuthenticator authenticator,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var result = await authenticator.ConfirmMfaAsync(currentUser.Id, request.Code, cancellationToken);

        return result.IsSuccess
            ? Results.Ok(new { recoveryCodes = result.Value })
            : Problem(result.Error);
    }

    private static async Task<IResult> DisableMfaAsync(
        SecondFactorCodeRequest request,
        ICurrentUser currentUser,
        IAuthenticator authenticator,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var result = await authenticator.DisableMfaAsync(currentUser.Id, request.Code, cancellationToken);

        return result.IsSuccess ? Results.NoContent() : Problem(result.Error);
    }

    private static IResult CompleteSession(HttpContext context, IssuedSession session)
    {
        context.Response.Cookies.Append(
            SessionCookie,
            session.Token,
            BuildCookieOptions(context, session.AbsoluteExpiresAt));

        // Same lifetime and same SameSite rule as the session, so the pair can
        // never drift apart. HttpOnly is off only for this one: the client must
        // read it to echo it back.
        context.Response.Cookies.Append(
            AntiForgeryCookie,
            session.AntiForgeryToken,
            BuildCookieOptions(context, session.AbsoluteExpiresAt, readableByScript: true));

        return Results.Ok(new { expiresAt = session.AbsoluteExpiresAt });
    }

    /// <summary>401, not 403: the caller may retry with different credentials.</summary>
    private static IResult Unauthorized(Error error) =>
        Results.Problem(
            title: error.Code,
            detail: error.Message,
            statusCode: StatusCodes.Status401Unauthorized);

    private static IResult Problem(Error error)
    {
        var status = error.Type switch
        {
            ErrorType.Validation => StatusCodes.Status400BadRequest,
            ErrorType.Conflict => StatusCodes.Status409Conflict,
            _ => StatusCodes.Status401Unauthorized,
        };

        return Results.Problem(title: error.Code, detail: error.Message, statusCode: status);
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

        // Clear both cookies with the same attributes they were set with, or the
        // browser keeps them.
        context.Response.Cookies.Append(
            SessionCookie, string.Empty, BuildCookieOptions(context, DateTimeOffset.UnixEpoch));
        context.Response.Cookies.Append(
            AntiForgeryCookie,
            string.Empty,
            BuildCookieOptions(context, DateTimeOffset.UnixEpoch, readableByScript: true));

        return Results.NoContent();
    }

    /// <summary>
    /// Reachable even by a caller who owes a second factor — it is how a client
    /// finds out that it must show the enrolment screen rather than the
    /// application.
    /// </summary>
    private static IResult Me(ICurrentUser currentUser) =>
        Results.Ok(new
        {
            userId = currentUser.Id,
            mustEnrolSecondFactor = currentUser.MustEnrolSecondFactor,
        });

    private static CookieOptions BuildCookieOptions(
        HttpContext context,
        DateTimeOffset expiresAt,
        bool readableByScript = false) =>
        new()
        {
            HttpOnly = !readableByScript,
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

/// <summary>The challenge from a password sign-in, plus the code from the app.</summary>
internal sealed record SecondFactorRequest(string ChallengeToken, string Code);

/// <summary>A code from the authenticator app, or a recovery code.</summary>
internal sealed record SecondFactorCodeRequest(string Code);
