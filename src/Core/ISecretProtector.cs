// Copyright (c) 2026 The DealerFOSS contributors.
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Overview: Purpose, File Design, and Engineering
//   Protects sensitive configuration at rest — chiefly tenant connection
//   references and connector credentials, and the TOTP shared secret.
//
//   An interface in Core with implementations outside it, because how a secret
//   is protected is an operational choice that differs per deployment (DPAPI, a
//   certificate, a cloud key manager) while the code that stores secrets should
//   not care. Storage code therefore never sees a plaintext key.
//
// Usage:
//   Protect(value) before storing; Unprotect(value) after reading.
//
// Coding Instructions:
//   src/App/Tenancy carries a DEVELOPMENT no-op implementation. It is not
//   encryption and is not meant to be — a real one is required before any
//   deployment holds real data, and doc 06 §4 says what "real" means here:
//   authenticated encryption with a key id, a rotation story, and a recovery
//   story. "AES-256" on its own is not a design.

namespace DealerFOSS.Core;

/// <summary>
/// Protects sensitive configuration at rest — most importantly tenant connection
/// references and connector credentials (doc 06 §4). Real implementations use
/// authenticated envelope encryption with a key id and rotation; the interface
/// keeps that concern behind a seam so storage code never sees plaintext keys.
/// </summary>
public interface ISecretProtector
{
    string Protect(string plaintext);

    string Unprotect(string protectedValue);
}
