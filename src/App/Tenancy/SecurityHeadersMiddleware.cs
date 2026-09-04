// Copyright (c) 2026 The DealerFOSS contributors.
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Overview: Purpose, File Design, and Engineering
//   SecurityHeadersMiddleware — the headers a browser needs to be told, on every
//   response.
//
// Usage:
//   Registered first in the pipeline, so a response that fails later still
//   carries them. A header only set on the happy path is not a control.
//
// Coding Instructions:
//   The content-security policy allows inline STYLES and nothing else.
//   Printed documents inline their whole stylesheet on purpose — a document
//   has to survive being saved to disk and opened next year, and a linked
//   stylesheet would not be there. Inline SCRIPT stays forbidden, which is
//   the half that matters: it is what turns a reflected value into an
//   execution.
//
//   These are set here rather than in a reverse proxy because the
//   installation targets are a Windows service, a Linux container, and
//   hosted — and only one of those reliably has a proxy in front of it.
//   That is also why HSTS is here: two of the three targets have nothing
//   else that would send it, and the one that does is the one least likely
//   to have been configured.

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;

namespace DealerFOSS.Tenancy;

public sealed class SecurityHeadersMiddleware(RequestDelegate next, IHostEnvironment environment)
{
    /// <summary>
    /// Two years, subdomains included. The session cookie is already
    /// <c>Secure</c>, so it never travels in the clear — but the FIRST request a
    /// browser makes to a hand-typed <c>dealership.example.com</c> has no cookie
    /// and no scheme, and goes out over HTTP. That request carries the tenant in
    /// a header and is answered with a redirect, and anybody on the same café
    /// wi-fi has already seen it and can answer it themselves. This header is
    /// what stops the second visit ever leaving on HTTP.
    ///
    /// No <c>preload</c>. Submitting a domain to the browsers' preload list is
    /// close to irreversible and it is the operator's domain, not ours — it must
    /// be their decision, made once they are certain every subdomain can do
    /// HTTPS. deploy/README.md says so.
    /// </summary>
    private const string StrictTransportSecurity = "max-age=63072000; includeSubDomains";

    private readonly RequestDelegate _next = next;

    // Development runs over plain HTTP. Sending HSTS from a development host
    // would pin `localhost` to HTTPS in the developer's own browser, which then
    // refuses to load the dev server and cannot be undone without digging
    // through browser internals. Same reasoning as the Secure cookie flag.
    private readonly bool _enforceHttps = !environment.IsDevelopment();

    /// <summary>
    /// Deliberately narrow. `default-src 'self'` means a page may only load from
    /// this origin; `frame-ancestors 'none'` stops it being embedded anywhere at
    /// all, which is what defeats clickjacking on a screen with a Suspend button.
    /// </summary>
    private const string ContentSecurityPolicy =
        "default-src 'self'; "
        + "img-src 'self' data:; "
        + "style-src 'self' 'unsafe-inline'; "
        + "script-src 'self'; "
        + "connect-src 'self'; "
        + "form-action 'self'; "
        + "base-uri 'self'; "
        + "frame-ancestors 'none'";

    public async Task InvokeAsync(HttpContext context)
    {
        var headers = context.Response.Headers;

        // Set before the response starts, because once it has begun writing the
        // headers are already on the wire.
        context.Response.OnStarting(() =>
        {
            // Stops a browser second-guessing a declared content type. A JSON
            // response sniffed as HTML is how a stored value becomes a page.
            headers["X-Content-Type-Options"] = "nosniff";

            // Belt and braces with frame-ancestors above, for browsers that
            // predate CSP level 2.
            headers["X-Frame-Options"] = "DENY";

            // A dealership's URLs carry record ids. Sending those to whatever
            // site somebody clicks through to is a quiet leak.
            headers["Referrer-Policy"] = "no-referrer";

            // Nothing here uses a camera, a microphone, or a location.
            headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=(), payment=()";

            headers["Content-Security-Policy"] = ContentSecurityPolicy;

            // Only over a connection that is already secure. A browser ignores
            // HSTS on a plain-HTTP response anyway, and sending it there would
            // be a header that reads as a control while doing nothing.
            if (_enforceHttps && context.Request.IsHttps)
            {
                headers["Strict-Transport-Security"] = StrictTransportSecurity;
            }

            return Task.CompletedTask;
        });

        await _next(context);
    }
}
