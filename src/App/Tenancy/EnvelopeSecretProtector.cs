// Copyright (c) 2026 The DealerFOSS contributors.
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Overview: Purpose, File Design, and Engineering
//   EnvelopeSecretProtector — the real ISecretProtector: AES-256-GCM with a key id
//   so keys can be rotated without rewriting what was already stored.
//
// Usage:
//   Registered by AddSecretProtection when keys are configured. Nothing
//   constructs it directly.
//
// Coding Instructions:
//   Three things here are not preferences.
//
//   GCM is authenticated encryption, so a tampered value fails to decrypt
//   rather than quietly returning wrong bytes. Do not swap it for CBC.
//
//   The nonce is random per call and never reused with the same key. Reusing
//   a nonce under GCM does not merely weaken it — it leaks the key stream.
//
//   The key id travels with the value. That is what makes rotation possible:
//   new values use the current key, old values still decrypt with the key
//   they were written under.
//
//   Works identically on Windows, Linux, and a hosted platform, which is why
//   it is this and not DPAPI (ADR-015, doc 06 §4).

using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using DealerFOSS.Core;

namespace DealerFOSS.Tenancy;

public sealed class EnvelopeSecretProtector : ISecretProtector
{
    /// <summary>Marks the format so a later scheme can be told apart from this one.</summary>
    private const string Version = "dfoss.v1";

    private const int KeyBytes = 32;   // AES-256
    private const int NonceBytes = 12; // GCM standard
    private const int TagBytes = 16;

    private readonly Dictionary<string, byte[]> _keys;
    private readonly string _currentKeyId;

    public EnvelopeSecretProtector(IReadOnlyDictionary<string, string> base64Keys, string currentKeyId)
    {
        ArgumentNullException.ThrowIfNull(base64Keys);

        if (base64Keys.Count == 0)
        {
            throw new ArgumentException(
                "No secret-protection keys are configured. See deploy/README.md.", nameof(base64Keys));
        }

        if (string.IsNullOrWhiteSpace(currentKeyId))
        {
            throw new ArgumentException(
                "Secrets:CurrentKeyId must name the key new values are written with.", nameof(currentKeyId));
        }

        _keys = new Dictionary<string, byte[]>(StringComparer.Ordinal);

        foreach (var (id, base64) in base64Keys)
        {
            if (id.Contains(Separator, StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    $"Key id '{id}' cannot contain '{Separator}' — it is the field separator.",
                    nameof(base64Keys));
            }

            byte[] key;
            try
            {
                key = Convert.FromBase64String(base64);
            }
            catch (FormatException ex)
            {
                throw new ArgumentException($"Key '{id}' is not valid base64.", nameof(base64Keys), ex);
            }

            if (key.Length != KeyBytes)
            {
                throw new ArgumentException(
                    $"Key '{id}' is {key.Length} bytes; AES-256 needs exactly {KeyBytes}.",
                    nameof(base64Keys));
            }

            _keys[id] = key;
        }

        if (!_keys.ContainsKey(currentKeyId))
        {
            throw new ArgumentException(
                $"Secrets:CurrentKeyId is '{currentKeyId}', which is not one of the configured keys "
                + $"({string.Join(", ", _keys.Keys)}).",
                nameof(currentKeyId));
        }

        _currentKeyId = currentKeyId;
    }

    private const char Separator = ':';

    public string Protect(string plaintext)
    {
        ArgumentNullException.ThrowIfNull(plaintext);

        var key = _keys[_currentKeyId];
        var bytes = Encoding.UTF8.GetBytes(plaintext);

        var nonce = RandomNumberGenerator.GetBytes(NonceBytes);
        var ciphertext = new byte[bytes.Length];
        var tag = new byte[TagBytes];

        using (var gcm = new AesGcm(key, TagBytes))
        {
            gcm.Encrypt(nonce, bytes, ciphertext, tag);
        }

        // nonce | tag | ciphertext, so the fixed-length parts come first and the
        // reader never has to be told how long the payload is.
        var packed = new byte[NonceBytes + TagBytes + ciphertext.Length];
        nonce.CopyTo(packed, 0);
        tag.CopyTo(packed, NonceBytes);
        ciphertext.CopyTo(packed, NonceBytes + TagBytes);

        return string.Join(Separator, Version, _currentKeyId, Convert.ToBase64String(packed));
    }

    public string Unprotect(string protectedValue)
    {
        ArgumentNullException.ThrowIfNull(protectedValue);

        var parts = protectedValue.Split(Separator, 3);

        if (parts.Length != 3 || !string.Equals(parts[0], Version, StringComparison.Ordinal))
        {
            throw new CryptographicException(
                "This value was not written by this protector. A store encrypted with a different "
                + "scheme has to be migrated, not simply read.");
        }

        var keyId = parts[1];

        if (!_keys.TryGetValue(keyId, out var key))
        {
            throw new CryptographicException(
                $"This value was written with key '{keyId}', which is not configured. Retiring a key "
                + "before re-encrypting everything written under it makes that data unreadable.");
        }

        byte[] packed;
        try
        {
            packed = Convert.FromBase64String(parts[2]);
        }
        catch (FormatException ex)
        {
            throw new CryptographicException("The protected value is malformed.", ex);
        }

        if (packed.Length < NonceBytes + TagBytes)
        {
            throw new CryptographicException("The protected value is truncated.");
        }

        var nonce = packed.AsSpan(0, NonceBytes);
        var tag = packed.AsSpan(NonceBytes, TagBytes);
        var ciphertext = packed.AsSpan(NonceBytes + TagBytes);
        var plaintext = new byte[ciphertext.Length];

        using (var gcm = new AesGcm(key, TagBytes))
        {
            // Throws if anything was altered — that is the point of the tag.
            gcm.Decrypt(nonce, ciphertext, tag, plaintext);
        }

        return Encoding.UTF8.GetString(plaintext);
    }

    /// <summary>
    /// Whether a stored value was written by this protector. Lets a migration tell
    /// an already-encrypted value from a plaintext one left by development.
    /// </summary>
    public static bool IsProtected(string? value) =>
        value is not null && value.StartsWith(Version + Separator, StringComparison.Ordinal);
}
