// Copyright (c) 2026 The DealerFOSS contributors.
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Overview: Purpose, File Design, and Engineering
//   AuthEndpoints — signing in, signing out, and reporting who you are.
//
// Usage:
//   POST /api/v1/auth/login sets the session cookie; POST .../logout clears
//   and revokes it; GET .../me confirms the caller. All require X-Tenant,
//   because a user belongs to one dealer organization.
//
// Coding Instructions:
//   The session cookie is HttpOnly so script cannot read it, SameSite=Strict
//   so another site cannot cause a request with it, and Secure outside
//   Development. Do not relax any of the three to make a client easier to
//   write. The token is returned only in the cookie, never in the body.
//
//   Sign-in sets a second cookie carrying the anti-forgery token. That one
//   is deliberately readable by script — the client has to read it to put it
//   in a header, and a header is the thing a cross-site form cannot forge.
//   It is not a credential: on its own it opens nothing.

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using DealerFOSS.Core;
using DealerFOSS.Identity;

namespace DealerFOSS.App;

internal static class AuthEndpoints
{
    /// <summary>Name of the cookie carrying the session token.</summary>
    public const string SessionCookie = "dfoss_session";

    /// <summary>
    /// Name of the cookie carrying this session's anti-forgery token. Readable by
    /// script on purpose, so the client can copy it into <see cref="AntiForgeryHeader"/>.
    /// </summary>
    public const string AntiForgeryCookie = "dfoss_csrf";

    /// <summary>
    /// The header a write must carry. "CSRF" rather than "anti-forgery" because
    /// every proxy, browser tool, and developer already recognises the name.
    /// </summary>
    public const string AntiForgeryHeader = "X-CSRF-Token";

    public static void MapAuth(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/auth").WithTags("Auth");

        // Rate limited: both are places where a wrong answer can simply be tried
        // again. The second-factor challenge already dies after five wrong codes,
        // which is per-secret; this is per-caller, and each covers what the other
        // cannot.
        group.MapPost("/login", LoginAsync).RequireRateLimiting(RateLimits.Credentials);
        group.MapPost("/login/second-factor", SecondFactorAsync)
            .RequireRateLimiting(RateLimits.Credentials);
        group.MapPost("/logout", LogoutAsync);
        group.MapGet("/me", Me);

        group.MapPost("/mfa/enrol", EnrolMfaAsync);

        // Also rate limited, and for a reason the sign-in limiter does not
        // cover. These two take a six-digit code and, unlike the sign-in
        // challenge, there is no per-secret attempt counter behind them to die
        // after five wrong answers — the challenge row that counts failures is
        // only created by a password sign-in. Left unlimited, somebody holding a
        // stolen cookie jar could walk the whole million codes at whatever rate
        // the server would answer: `confirm` to bind an authenticator they
        // control, `disable` to take the second factor off the account
        // altogether. Both are persistence, not access, which is exactly the
        // step worth making expensive.
        group.MapPost("/mfa/confirm", ConfirmMfaAsync)
            .RequireRateLimiting(RateLimits.Credentials);
        group.MapPost("/mfa/disable", DisableMfaAsync)
            .RequireRateLimiting(RateLimits.Credentials);

        // Passkeys live in this group rather than their own, because they are a
        // way of signing in and a sign-in ends with the same cookie pair as
        // every other. Mapping them elsewhere would mean a second copy of
        // CompleteSession, and two places that write a session cookie is how the
        // two quietly stop agreeing about SameSite or expiry.
        //
        // Rate limited on the same allowance as a password, and for the same
        // reason: finishing a ceremony is a place where a wrong answer can
        // simply be tried again.
        group.MapPost("/passkeys/register/begin", BeginPasskeyRegistrationAsync);
        group.MapPost("/passkeys/register/finish", FinishPasskeyRegistrationAsync);
        group.MapPost("/passkeys/sign-in/begin", BeginPasskeySignInAsync)
            .RequireRateLimiting(RateLimits.Credentials);
        group.MapPost("/passkeys/sign-in/finish", FinishPasskeySignInAsync)
            .RequireRateLimiting(RateLimits.Credentials);
        group.MapGet("/passkeys", ListPasskeysAsync);
        group.MapDelete("/passkeys/{passkeyId:guid}", ForgetPasskeyAsync);
    }

    private static async Task<IResult> BeginPasskeyRegistrationAsync(
        IPasskeys passkeys,
        CancellationToken cancellationToken)
    {
        var result = await passkeys.BeginRegistrationAsync(cancellationToken);
        return result.IsSuccess ? Results.Ok(result.Value) : Problem(result.Error);
    }

    private static async Task<IResult> FinishPasskeyRegistrationAsync(
        PasskeyRegistrationResponse request,
        IPasskeys passkeys,
        CancellationToken cancellationToken)
    {
        var result = await passkeys.FinishRegistrationAsync(request, cancellationToken);
        return result.IsSuccess ? Results.Ok(result.Value) : Problem(result.Error);
    }

    private static async Task<IResult> BeginPasskeySignInAsync(
        IPasskeys passkeys,
        CancellationToken cancellationToken)
    {
        var result = await passkeys.BeginSignInAsync(cancellationToken);
        return result.IsSuccess ? Results.Ok(result.Value) : Problem(result.Error);
    }

    /// <summary>
    /// The one that hands out a session. Ends at <see cref="CompleteSession"/>,
    /// the same call a password sign-in makes, so the cookie, the anti-forgery
    /// token and the expiry are identical by construction rather than by
    /// somebody remembering to keep them the same.
    /// </summary>
    private static async Task<IResult> FinishPasskeySignInAsync(
        PasskeySignInResponse request,
        IPasskeys passkeys,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var result = await passkeys.FinishSignInAsync(request, cancellationToken);

        // 401 rather than 400: the caller may try again with another passkey, or
        // fall back to a password.
        return result.IsSuccess
            ? CompleteSession(context, result.Value)
            : Unauthorized(result.Error);
    }

    private static async Task<IResult> ListPasskeysAsync(
        IPasskeys passkeys,
        CancellationToken cancellationToken)
    {
        var result = await passkeys.ListMineAsync(cancellationToken);
        return result.IsSuccess ? Results.Ok(result.Value) : Problem(result.Error);
    }

    private static async Task<IResult> ForgetPasskeyAsync(
        Guid passkeyId,
        IPasskeys passkeys,
        CancellationToken cancellationToken)
    {
        var result = await passkeys.ForgetAsync(passkeyId, cancellationToken);
        return result.IsSuccess ? Results.NoContent() : Problem(result.Error);
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
    /// <remarks>
    /// <para>
    /// <c>permissions</c> is what the caller holds somewhere, and it is a
    /// <b>hint for drawing a screen, not a decision</b>. Until 2026-09-18 the
    /// browser had no idea what anybody held, so every signed-in person was
    /// shown every module and discovered the truth by being refused — a
    /// technician saw Books and Staff on the navigation.
    /// </para>
    /// <para>
    /// It stays a hint. Every endpoint enforces for itself, exactly as before,
    /// and an integration test forbids a caller from reaching accounting on the
    /// strength of a list that said they could. Nothing here is a second copy
    /// of the authorization rule: this endpoint asks the same
    /// <see cref="IAccessDirectory"/> every service asks.
    /// </para>
    /// <para>
    /// A caller who owes a second factor gets an EMPTY list. Their session can
    /// reach enrolment and nothing else, so listing what they will hold once
    /// they enrol would describe an application they cannot use yet.
    /// </para>
    /// </remarks>
    private static async Task<IResult> Me(
        ICurrentUser currentUser,
        IAccessDirectory access,
        CancellationToken cancellationToken)
    {
        var permissions = currentUser.MustEnrolSecondFactor
            ? new HashSet<string>()
            : (ISet<string>)new HashSet<string>(
                await access.GetHeldPermissionsAsync(currentUser.Id, cancellationToken),
                StringComparer.Ordinal);

        return Results.Ok(new
        {
            userId = currentUser.Id,
            mustEnrolSecondFactor = currentUser.MustEnrolSecondFactor,

            // Ordered so the payload is stable between calls. An unordered set
            // would make this response differ byte for byte on every request,
            // which is a nuisance to anything that caches or diffs it.
            permissions = permissions.OrderBy(p => p, StringComparer.Ordinal).ToArray(),
        });
    }

    /// <summary>
    /// Internal rather than private so the control plane sets its own cookies
    /// with exactly these attributes. Two copies of this would eventually differ,
    /// and the difference would be a security bug nobody was looking for.
    /// </summary>
    internal static CookieOptions BuildCookieOptions(
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
