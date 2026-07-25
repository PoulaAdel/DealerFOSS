namespace OpenDealer360.Platform.Kernel;

/// <summary>
/// Strongly-typed identifiers for the tenancy hierarchy (doc 04 §1). Distinct
/// types stop a <see cref="RooftopId"/> from being passed where a
/// <see cref="LegalEntityId"/> is expected. Primary keys are application-generated
/// UUIDs; external IDs are never primary keys (doc 04 §4).
/// </summary>
public readonly record struct DealerOrganizationId(Guid Value)
{
    public static DealerOrganizationId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString();
}

public readonly record struct LegalEntityId(Guid Value)
{
    public static LegalEntityId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString();
}

public readonly record struct RooftopId(Guid Value)
{
    public static RooftopId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString();
}

public readonly record struct DepartmentId(Guid Value)
{
    public static DepartmentId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString();
}
