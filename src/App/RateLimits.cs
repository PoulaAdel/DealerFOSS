// Copyright (c) 2026 The DealerFOSS contributors.
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Overview: Purpose, File Design, and Engineering
//   RateLimits — the named limiter policies, in one place.
//
// Usage:
//   .RequireRateLimiting(RateLimits.Credentials) on an endpoint where
//   somebody could guess a secret.
//
// Coding Instructions:
//   Adding a policy here is cheap; applying one to a business endpoint is
//   not. A limiter on "list the stock" punishes a busy dealership for being
//   busy, and the volume that matters is guessing, not working.

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
