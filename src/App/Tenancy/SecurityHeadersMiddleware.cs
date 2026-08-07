// SecurityHeadersMiddleware — the headers a browser needs to be told, on every
// response.
//
// Use:  registered first in the pipeline, so a response that fails later still
//       carries them. A header only set on the happy path is not a control.
// Edit: the content-security policy allows inline STYLES and nothing else.
//       Printed documents inline their whole stylesheet on purpose — a document
//       has to survive being saved to disk and opened next year, and a linked
//       stylesheet would not be there. Inline SCRIPT stays forbidden, which is
//       the half that matters: it is what turns a reflected value into an
//       execution.
//
//       These are set here rather than in a reverse proxy because the
//       installation targets are a Windows service, a Linux container, and
//       hosted — and only one of those reliably has a proxy in front of it.

using Microsoft.AspNetCore.Http;

namespace DealerFOSS.Tenancy;

public sealed class SecurityHeadersMiddleware(RequestDelegate next)
{
    private readonly RequestDelegate _next = next;

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

            return Task.CompletedTask;
        });

        await _next(context);
    }
}
