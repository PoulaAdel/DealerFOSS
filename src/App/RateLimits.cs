// RateLimits — the named limiter policies, in one place.
//
// Use:  .RequireRateLimiting(RateLimits.Credentials) on an endpoint where
//       somebody could guess a secret.
// Edit: adding a policy here is cheap; applying one to a business endpoint is
//       not. A limiter on "list the stock" punishes a busy dealership for being
//       busy, and the volume that matters is guessing, not working.

namespace DealerFOSS.App;

internal static class RateLimits
{
    /// <summary>
    /// Signing in, completing a second factor, redeeming an enrolment code, and
    /// the control-plane equivalent. Everything where a wrong answer can simply
    /// be tried again.
    /// </summary>
    public const string Credentials = "credentials";
}
