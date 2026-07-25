using OpenDealer360.Platform.Kernel;

namespace OpenDealer360.Modules.Organization.Contracts;

/// <summary>
/// The Organization module's public contract — the only surface other modules
/// may call (ADR-008). Other modules validate rooftop scope through this rather
/// than reading Organization's tables.
/// </summary>
public interface IOrganizationDirectory
{
    /// <summary>The full structure of the current tenant's organization, or null if not provisioned.</summary>
    Task<OrganizationView?> GetStructureAsync(CancellationToken cancellationToken);

    /// <summary>Whether a rooftop exists in the current tenant.</summary>
    Task<bool> RooftopExistsAsync(RooftopId rooftopId, CancellationToken cancellationToken);
}
