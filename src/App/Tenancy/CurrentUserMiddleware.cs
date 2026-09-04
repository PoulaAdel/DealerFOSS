// Copyright (c) 2026 The DealerFOSS contributors.
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Overview: Purpose, File Design, and Engineering
//   CurrentUserMiddleware — identifies the caller from their session cookie, after
//   the tenant is known.
//
// Usage:
//   Automatic for every /api/v1 path; runs after TenantMiddleware. Login and
//   logout are exempt, since those are where a session is obtained or ended.
//
// Coding Instructions:
//   The session is checked against the database on every request, so
//   revoking one takes effect immediately rather than whenever a token would
//   have expired. Do not cache that lookup without also solving revocation —
//   that trade is the whole reason sessions are durable rather than stateless.
//
//   The same check reports whether the caller owes their organization a
//   second factor. If they do, this middleware lets them reach the enrolment
//   path and nothing else. Enforcing it here rather than in each endpoint is
//   the point: a capability added next year is covered without being told.

using DealerFOSS.Administration;
using DealerFOSS.Core;
using DealerFOSS.App;
using DealerFOSS.Identity;

namespace DealerFOSS.Tenancy;

/// <summary>
/// Resolves the calling user for the request. Tenant-scoped API calls without a
/// valid session are refused here rather than deep in a service.
/// </summary>
public sealed class CurrentUserMiddleware(RequestDelegate next)
{
    private const string ApiPrefix = "/api/v1";

    /// <summary>
    /// The only endpoints reachable without a session: the ones where a session
    /// is obtained or ended. Completing a second factor belongs here for the
    /// obvious reason — the whole point is that the password did not produce a
    /// session, so there is nothing to authenticate with yet. It is not
    /// unprotected: it demands a challenge token that only a correct password
    /// produces, and a code on top of that.
    /// </summary>
    private static readonly string[] AnonymousPaths =
    [
        "/api/v1/auth/login",
        "/api/v1/auth/login/second-factor",
        "/api/v1/auth/logout",
        // Setting a first password. The caller cannot possibly have a session —
        // not having one is the state this endpoint exists to fix.
        "/api/v1/auth/enrol",
        // Recovering a forgotten one, for exactly the same reason (ADR-018).
        // Neither is unprotected: each demands a single-use proof the caller
        // could only hold legitimately — a live code from their authenticator, or
        // a hashed, expiring code a manager issued — and both are rate limited
        // with the other credential endpoints.
        "/api/v1/auth/recover",
        "/api/v1/auth/recover/authenticator",
        "/api/v1/auth/recover/code",
        // Signing in with a passkey, for the same reason as a password: there is
        // no session yet, and producing one is what these two are for. Note the
        // REGISTRATION routes are deliberately absent — adding a credential to
        // an account requires already being signed in to it, so those stay
        // behind the normal check.
        "/api/v1/auth/passkeys/sign-in/begin",
        "/api/v1/auth/passkeys/sign-in/finish",
    ];

    /// <summary>
    /// All a caller who owes a second factor may reach. Enrolling and confirming
    /// are the way out; <c>me</c> is how a client knows to show that screen; and
    /// signing out must always be possible. Disabling is deliberately absent —
    /// it would be a way to answer the policy by removing the thing it asks for.
    /// </summary>
    private static readonly string[] EnrolmentPaths =
    [
        "/api/v1/auth/me",
        "/api/v1/auth/mfa/enrol",
        "/api/v1/auth/mfa/confirm",
        "/api/v1/auth/logout",
    ];

    private readonly RequestDelegate _next = next;

    // IAuthenticator is resolved inside the method, not as a parameter. Injected
    // middleware parameters are constructed before the method body runs, and
    // building it reaches the tenant-bound DbContext — which throws on paths
    // like /health where no tenant was ever resolved.
    public async Task InvokeAsync(HttpContext context, ICurrentUser currentUser)
    {
        // The control plane is not a tenant caller and never becomes one. It has
        // its own middleware, its own cookie, and its own store; an administrator
        // never reaches ICurrentUser.Set below. That is why an administrator
        // session presented to a business endpoint resolves to nobody and is
        // refused — the separation is structural rather than a check each
        // capability has to remember (doc 06 §3).
        if (!context.Request.Path.StartsWithSegments(ApiPrefix)
            || context.Request.Path.StartsWithSegments(AdministratorMiddleware.AdminPrefix)
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

        var caller = result.Value;
        currentUser.Set(caller.UserId, caller.MustEnrolSecondFactor);

        if (caller.MustEnrolSecondFactor
            && !EnrolmentPaths.Contains(context.Request.Path.Value, StringComparer.OrdinalIgnoreCase))
        {
            // 403 and not 401: the session is genuinely valid, and signing in
            // again would change nothing. The message names the way out.
            var error = AuthErrors.SecondFactorRequiredByPolicy;
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
