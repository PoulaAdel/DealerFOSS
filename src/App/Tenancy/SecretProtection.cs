// Copyright (c) 2026 The DealerFOSS contributors.
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Overview: Purpose, File Design, and Engineering
//   SecretProtection — decides which ISecretProtector the application runs with.
//
// Usage:
//   Services.AddSecretProtection(configuration) from Program.cs, after
//   AddHostCatalog. Registering it later is what lets it replace the
//   development default.
//
// Coding Instructions:
//   The rule is deliberately blunt. Keys configured means real encryption,
//   in every environment including Development — so the thing that runs in
//   production is the thing developers exercise. No keys means the
//   development pass-through, and Program.cs refuses to start with that
//   outside Development.

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using DealerFOSS.Core;

namespace DealerFOSS.Tenancy;

public static class SecretProtection
{
    /// <summary>Configuration section holding the keys.</summary>
    public const string SectionName = "Secrets";

    /// <summary>
    /// Registers real envelope encryption when keys are configured, and leaves
    /// the development pass-through in place when they are not.
    /// </summary>
    public static IServiceCollection AddSecretProtection(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var section = configuration.GetSection(SectionName);
        var currentKeyId = section["CurrentKeyId"];

        var keys = section.GetSection("Keys")
            .GetChildren()
            .Where(child => !string.IsNullOrWhiteSpace(child.Value))
            .ToDictionary(child => child.Key, child => child.Value!, StringComparer.Ordinal);

        if (keys.Count == 0 || string.IsNullOrWhiteSpace(currentKeyId))
        {
            // Nothing configured: whatever AddHostCatalog registered stands.
            return services;
        }

        // Constructed eagerly so a bad key is a startup failure with a readable
        // message, rather than a surprise on the first request that needs it.
        var protector = new EnvelopeSecretProtector(keys, currentKeyId);
        services.AddSingleton<ISecretProtector>(protector);

        return services;
    }
}
