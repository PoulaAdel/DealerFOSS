// Copyright (c) 2026 The DealerFOSS contributors.
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Overview: Purpose, File Design, and Engineering
//   ShippedPacks — the jurisdiction packs built into this assembly, as bytes.
//
// Usage:
//   ShippedPacks.All() for every pack that ships; the operator loads them
//   through POST /api/v1/admin/jurisdiction-packs/shipped, and the development
//   seeder loads them directly.
//
// Coding Instructions:
//   EMBEDDED RATHER THAN LOOSE FILES, and the reason is the content hash. A
//   reviewer's approval is tied to the exact bytes they approved (ADR-024); a
//   pack sitting on disk beside the binary could be edited afterwards and would
//   still be the file the product reads. Embedding makes the build the thing
//   that fixes the content.
//
//   LOADING A SHIPPED PACK IS STILL AN OPERATOR'S ACT, not something startup
//   does on its own. The packs arrive Draft and resolve nothing, so loading them
//   automatically would be harmless — but it would put rows in a deployment's
//   database that nobody asked for, and the habit is the problem rather than
//   these particular rows. The one exception is the development seeder, which
//   exists precisely to produce a database somebody can look at.
//
//   Nothing here parses. Returning bytes keeps the hash honest: a round trip
//   through a model and back out would hash a re-serialisation of the pack
//   rather than the pack.

using System.Reflection;

namespace DealerFOSS.Compliance;

internal static class ShippedPacks
{
    /// <summary>
    /// Every pack embedded in this assembly, as (resource name, bytes). The name
    /// is for reporting which file failed, not for identity — a pack's identity
    /// is the packId and version inside it.
    /// </summary>
    public static IReadOnlyList<(string Name, byte[] Content)> All()
    {
        var assembly = typeof(ShippedPacks).Assembly;

        return
        [
            .. assembly.GetManifestResourceNames()
                .Where(name => name.Contains(".Compliance.Packs.", StringComparison.Ordinal)
                    && name.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                .OrderBy(name => name, StringComparer.Ordinal)
                .Select(name => (Name: Shorten(name), Content: Read(assembly, name))),
        ];
    }

    private static byte[] Read(Assembly assembly, string resourceName)
    {
        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException(
                $"The embedded pack '{resourceName}' is listed but cannot be opened.");

        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        return buffer.ToArray();
    }

    /// <summary>"DealerFOSS.Compliance.Packs.us-in.json" reads as "us-in.json".</summary>
    private static string Shorten(string resourceName)
    {
        const string Marker = ".Compliance.Packs.";
        var at = resourceName.IndexOf(Marker, StringComparison.Ordinal);
        return at < 0 ? resourceName : resourceName[(at + Marker.Length)..];
    }
}
