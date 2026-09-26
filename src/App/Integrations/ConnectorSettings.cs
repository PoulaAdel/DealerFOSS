// Copyright (c) 2026 The DealerFOSS contributors.
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Overview: Purpose, File Design, and Engineering
//   ConnectorSettings — turning a dealership's connector configuration into
//   something that can sit in a table, and back again.
//
//   Storing a schedule meant storing an ApiSecret, which is why this exists as
//   its own file rather than two lines inside the entity. ISecretProtector
//   names connector credentials as one of the two things it is for; this is the
//   first code to use it for that.
//
// Usage:
//   Protect(manifest, supplied) before saving a schedule. Reveal(manifest,
//   stored) in the worker, immediately before handing the settings to the
//   connector. Describe(manifest, stored) for anything a screen will see.
//
// Coding Instructions:
//   WHICH VALUES ARE PROTECTED IS DECIDED BY THE MANIFEST, NOT BY THE NAME OF
//   THE KEY. A rule like "protect anything called secret or password or token"
//   is the one that misses `ApiKey`, and it misses it silently — the value
//   stores and reads back perfectly, so nothing ever fails. The connector
//   already declares SettingKind.Secret per field, so that declaration is the
//   only input.
//
//   AN UNKNOWN KEY IS REFUSED, NOT DROPPED. ValidateSettings already refuses
//   one at the door for exactly the reason stated there — a misspelled key that
//   silently does nothing is the same failure as a missing one, found much
//   later. Protect runs after that check and asserts it again rather than
//   trusting the caller, because storing a key the manifest does not declare
//   would mean nothing knows whether it holds a secret.
//
//   DESCRIBE NEVER RETURNS A SECRET'S VALUE, not even the protected form. The
//   protected form is a ciphertext, so returning it looks safe and is not: it
//   travels to the browser, into logs, and into whatever the browser stores —
//   and an attacker who later obtains the key gets every one of them. A screen
//   needs to know a secret IS SET, which is a boolean, and the same rule the
//   quarantine list follows for payloads (ADR-022).
//
//   Reveal is the only thing that decrypts, it is called once per run in the
//   worker, and what it returns is never stored, returned or logged.

using System.Text.Json;
using DealerFOSS.Core;

namespace DealerFOSS.Integrations;

/// <summary>
/// One setting as a screen may see it: what it is, and whether it has a value —
/// never the value itself when it is a secret.
/// </summary>
public sealed record ConnectorSettingState(
    string Name,

    /// <summary>The declared kind, as the enum name, so the screen draws the right control.</summary>
    string Kind,
    bool Required,
    string Description,

    /// <summary>
    /// The stored value, or null for a secret — which reports only whether it is
    /// set, through <paramref name="IsSet"/>.
    /// </summary>
    string? Value,
    bool IsSet);

/// <summary>Protects and reveals a dealership's connector settings.</summary>
public static class ConnectorSettings
{
    private static readonly JsonSerializerOptions Format = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// Prepare supplied settings for storage, protecting every value the
    /// manifest declares as a secret.
    /// </summary>
    /// <remarks>
    /// Call <see cref="ConnectorManifest.ValidateSettings"/> first — this
    /// re-checks that every key is declared, but it reports a storage problem
    /// rather than the useful per-setting refusal that validation gives.
    /// </remarks>
    public static Result<string> Protect(
        ConnectorManifest manifest,
        IReadOnlyDictionary<string, string?> supplied,
        ISecretProtector protector)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentNullException.ThrowIfNull(supplied);
        ArgumentNullException.ThrowIfNull(protector);

        var stored = new Dictionary<string, string?>(StringComparer.Ordinal);

        foreach (var (name, value) in supplied)
        {
            var declared = manifest.Settings.FirstOrDefault(s =>
                string.Equals(s.Name, name, StringComparison.Ordinal));

            if (declared is null)
            {
                // Asserted rather than assumed. Without the declaration nothing
                // knows whether this value needs protecting.
                return Result.Failure<string>(IntegrationErrors.SettingUnknown(name));
            }

            stored[name] = declared.Kind == SettingKind.Secret && !string.IsNullOrWhiteSpace(value)
                ? protector.Protect(value)
                : value;
        }

        return Result.Success(JsonSerializer.Serialize(stored, Format));
    }

    /// <summary>
    /// Merge a change into what is already stored, leaving a secret alone when
    /// the caller supplies nothing for it.
    /// </summary>
    /// <remarks>
    /// This is why a secret's absence and its blank have to stay different.
    /// <see cref="Describe"/> never returns a secret's value, so a screen
    /// editing a schedule has nothing to send back for one — and if an absent
    /// secret meant "clear it", every edit of the interval would wipe the
    /// credential. An explicitly empty string still clears it, which is how
    /// somebody removes one on purpose.
    /// </remarks>
    public static Result<string> Merge(
        ConnectorManifest manifest,
        string stored,
        IReadOnlyDictionary<string, string?> supplied,
        ISecretProtector protector)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentNullException.ThrowIfNull(supplied);

        var existing = Read(stored);
        var merged = new Dictionary<string, string?>(existing, StringComparer.Ordinal);

        foreach (var (name, value) in supplied)
        {
            merged[name] = value;
        }

        foreach (var secret in manifest.Settings.Where(s => s.Kind == SettingKind.Secret))
        {
            if (!supplied.ContainsKey(secret.Name) && existing.TryGetValue(secret.Name, out var kept))
            {
                // Already protected, so it is carried across as it stands
                // rather than re-protected — which would work, and would also
                // mean every edit rewrote a ciphertext for no reason.
                merged[secret.Name] = kept;
            }
        }

        var reprotect = new Dictionary<string, string?>(StringComparer.Ordinal);

        foreach (var (name, value) in merged)
        {
            var declared = manifest.Settings.FirstOrDefault(s =>
                string.Equals(s.Name, name, StringComparison.Ordinal));

            if (declared is null)
            {
                return Result.Failure<string>(IntegrationErrors.SettingUnknown(name));
            }

            // A supplied secret is plaintext and needs protecting; a carried-over
            // one is already protected. They are told apart by whether the
            // caller supplied the key, which is the only thing that knows.
            var needsProtecting = declared.Kind == SettingKind.Secret
                && supplied.ContainsKey(name)
                && !string.IsNullOrWhiteSpace(value);

            reprotect[name] = needsProtecting ? protector.Protect(value!) : value;
        }

        return Result.Success(JsonSerializer.Serialize(reprotect, Format));
    }

    /// <summary>
    /// The plaintext settings, for the one moment they are handed to a
    /// connector. Never store, return or log what this produces.
    /// </summary>
    public static IReadOnlyDictionary<string, string?> Reveal(
        ConnectorManifest manifest,
        string stored,
        ISecretProtector protector)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentNullException.ThrowIfNull(protector);

        var read = Read(stored);
        var plain = new Dictionary<string, string?>(StringComparer.Ordinal);

        foreach (var (name, value) in read)
        {
            var declared = manifest.Settings.FirstOrDefault(s =>
                string.Equals(s.Name, name, StringComparison.Ordinal));

            plain[name] = declared?.Kind == SettingKind.Secret && !string.IsNullOrWhiteSpace(value)
                ? protector.Unprotect(value)
                : value;
        }

        return plain;
    }

    /// <summary>
    /// What is configured, as a screen may see it. A secret reports that it is
    /// set and never what it is.
    /// </summary>
    public static IReadOnlyList<ConnectorSettingState> Describe(
        ConnectorManifest manifest,
        string stored)
    {
        ArgumentNullException.ThrowIfNull(manifest);

        var read = Read(stored);

        return
        [
            .. manifest.Settings.Select(setting =>
            {
                read.TryGetValue(setting.Name, out var value);
                var isSet = !string.IsNullOrWhiteSpace(value);

                return new ConnectorSettingState(
                    setting.Name,
                    setting.Kind.ToString(),
                    setting.Required,
                    setting.Description,
                    setting.Kind == SettingKind.Secret ? null : value,
                    isSet);
            }),
        ];
    }

    private static Dictionary<string, string?> Read(string stored) =>
        string.IsNullOrWhiteSpace(stored)
            ? new Dictionary<string, string?>(StringComparer.Ordinal)
            : JsonSerializer.Deserialize<Dictionary<string, string?>>(stored, Format)
                ?? new Dictionary<string, string?>(StringComparer.Ordinal);
}
