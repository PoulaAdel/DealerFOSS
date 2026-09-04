// Copyright (c) 2026 The DealerFOSS contributors.
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Overview: Purpose, File Design, and Engineering
//   DevSecretProtector — a no-op ISecretProtector for local development.
//
// Usage:
//   Nowhere but Development. Program.cs refuses to start if this is
//   registered in any other environment.
//
// Coding Instructions:
//   Do not "improve" this into a real implementation. Add a separate
//   DPAPI/certificate/KMS class and register that instead.

using DealerFOSS.Core;

namespace DealerFOSS.Tenancy;

/// <summary>
/// DEVELOPMENT ONLY. A no-op protector so the resolution seam works end to end
/// before real key management lands. A production deployment must register a
/// DPAPI/certificate/KMS-backed <see cref="ISecretProtector"/> instead
/// (doc 06 §4). The Host refuses to start with this protector outside Development.
/// </summary>
public sealed class DevSecretProtector : ISecretProtector
{
    public string Protect(string plaintext) => plaintext;

    public string Unprotect(string protectedValue) => protectedValue;
}
