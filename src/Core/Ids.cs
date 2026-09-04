// Copyright (c) 2026 The DealerFOSS contributors.
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Overview: Purpose, File Design, and Engineering
//   Typed identifiers for the tenancy hierarchy — dealer organization, legal
//   entity, rooftop, department — so that a RooftopId cannot be passed where a
//   LegalEntityId is expected. Every one of them is a Guid underneath, which is
//   precisely the problem: bare Guids are mutually assignable, and a scope
//   confusion in this system is a permission bug.
//
//   The compiler catching it is worth the wrapping. This is the cheapest place
//   in the product to prevent a whole class of authorization mistake.
//
// Usage:
//   new RooftopId(guid)
//   rooftopId.Value   → the Guid back
//
// Coding Instructions:
//   ADDING AN ID MEANS ADDING ITS JsonConverter TOO. Without one it serializes
//   as {"value":"..."} rather than a plain GUID, which breaks route binding and
//   every client that reads the field — and it fails at runtime, not at build.

using System.Text.Json;
using System.Text.Json.Serialization;

namespace DealerFOSS.Core;

/// <summary>
/// Strongly-typed identifiers for the tenancy hierarchy (doc 04 §1). Distinct
/// types stop a <see cref="RooftopId"/> from being passed where a
/// <see cref="LegalEntityId"/> is expected. Primary keys are application-generated
/// UUIDs; external IDs are never primary keys (doc 04 §4).
/// </summary>
/// <remarks>
/// Each carries a JSON converter so the wrapper serializes as a plain GUID. The
/// type safety is for our code; the API contract stays an ordinary identifier.
/// </remarks>
[JsonConverter(typeof(DealerOrganizationIdJsonConverter))]
public readonly record struct DealerOrganizationId(Guid Value)
{
    public static DealerOrganizationId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString();
}

[JsonConverter(typeof(LegalEntityIdJsonConverter))]
public readonly record struct LegalEntityId(Guid Value)
{
    public static LegalEntityId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString();
}

[JsonConverter(typeof(RooftopIdJsonConverter))]
public readonly record struct RooftopId(Guid Value)
{
    public static RooftopId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString();
}

[JsonConverter(typeof(DepartmentIdJsonConverter))]
public readonly record struct DepartmentId(Guid Value)
{
    public static DepartmentId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString();
}

public sealed class DealerOrganizationIdJsonConverter : JsonConverter<DealerOrganizationId>
{
    public override DealerOrganizationId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        new(reader.GetGuid());

    public override void Write(Utf8JsonWriter writer, DealerOrganizationId value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);
        writer.WriteStringValue(value.Value);
    }
}

public sealed class LegalEntityIdJsonConverter : JsonConverter<LegalEntityId>
{
    public override LegalEntityId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        new(reader.GetGuid());

    public override void Write(Utf8JsonWriter writer, LegalEntityId value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);
        writer.WriteStringValue(value.Value);
    }
}

public sealed class RooftopIdJsonConverter : JsonConverter<RooftopId>
{
    public override RooftopId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        new(reader.GetGuid());

    public override void Write(Utf8JsonWriter writer, RooftopId value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);
        writer.WriteStringValue(value.Value);
    }
}

public sealed class DepartmentIdJsonConverter : JsonConverter<DepartmentId>
{
    public override DepartmentId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        new(reader.GetGuid());

    public override void Write(Utf8JsonWriter writer, DepartmentId value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);
        writer.WriteStringValue(value.Value);
    }
}
