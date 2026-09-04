// Copyright (c) 2026 The DealerFOSS contributors.
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Overview: Purpose, File Design, and Engineering
//   TenantCache — in-process routing cache, so every request does not re-read the
//   host catalog. A single node needs nothing more (ADR-005).
//
// Usage:
//   Through TenantResolver.
//
// Coding Instructions:
//   Entries expire after Ttl. That is also the lag before a suspended tenant
//   actually stops being served — call Invalidate on status changes.

using System.Collections.Concurrent;
using DealerFOSS.Core;

namespace DealerFOSS.Tenancy;

/// <summary>
/// In-process routing cache shared across requests (registered as a singleton).
/// A single-node deployment needs no external cache for this (ADR-005). Entries
/// expire so a re-provisioned or suspended tenant is picked up without a restart.
/// </summary>
public sealed class TenantCache(IClock clock)
{
    private readonly ConcurrentDictionary<string, Entry> _entries = new(StringComparer.OrdinalIgnoreCase);
    private readonly IClock _clock = clock;

    public static TimeSpan Ttl { get; } = TimeSpan.FromMinutes(10);

    public bool TryGet(string key, out ResolvedTenant tenant)
    {
        if (_entries.TryGetValue(key, out var entry) && entry.ExpiresAt > _clock.UtcNow)
        {
            tenant = entry.Tenant;
            return true;
        }

        tenant = null!;
        return false;
    }

    public void Set(string key, ResolvedTenant tenant) =>
        _entries[key] = new Entry(tenant, _clock.UtcNow.Add(Ttl));

    public void Remove(string key) => _entries.TryRemove(key, out _);

    private readonly record struct Entry(ResolvedTenant Tenant, DateTimeOffset ExpiresAt);
}
