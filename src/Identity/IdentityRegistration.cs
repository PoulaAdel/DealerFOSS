// IdentityRegistration — the only way the application switches this project on.
//
// Use:  services.AddIdentity() from Program.cs.
// Edit: this and the two contracts are almost the whole public surface of the
//       project. Everything else — the context, the services, the tables — is
//       internal on purpose, so no feature can write a user row or an audit row
//       except through IAccessDirectory and IAuthenticator (ADR-017). Adding a
//       public type here is a security decision, not a convenience.

using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using DealerFOSS.Core;

namespace DealerFOSS.Identity;

public static class IdentityRegistration
{
    /// <summary>
    /// Registers identity services. The context binds to the tenant resolved for
    /// the current request, so it can only be constructed after tenant middleware
    /// has run.
    /// </summary>
    public static IServiceCollection AddIdentity(this IServiceCollection services)
    {
        services.AddDbContext<IdentityDb>((serviceProvider, options) =>
        {
            var tenant = serviceProvider.GetRequiredService<ITenantContext>();
            options.UseSqlServer(tenant.Current.ConnectionString);
        });

        services.AddScoped<IAuditSink, SqlAuditSink>();
        services.AddScoped<IAccessDirectory, AccessService>();
        services.AddScoped<ISecurityPolicy, SecurityPolicyService>();
        services.AddScoped<IStaffDirectory, StaffDirectoryService>();

        // Password hashing algorithm and parameters live here, so upgrading them
        // is one change rather than a search through call sites.
        services.AddSingleton<IPasswordHasher<User>, PasswordHasher<User>>();
        services.AddScoped<IAuthenticator, Authenticator>();

        return services;
    }

    /// <summary>
    /// Registers control-plane identity — the people who operate the deployment.
    /// Its context binds to the host catalog, not to any tenant, so an
    /// administrator row and a dealership's business tables are never open in the
    /// same unit of work (doc 06 §2).
    /// </summary>
    /// <remarks>
    /// This lives in the Identity project rather than in an application folder
    /// because password verification, TOTP, and session issuance must exist in
    /// exactly one place — the one the rest of the application cannot reach. A
    /// second implementation in <c>src/App</c> would be visible to every feature.
    /// </remarks>
    public static IServiceCollection AddControlPlane(
        this IServiceCollection services,
        string hostConnectionString)
    {
        services.AddDbContext<ControlPlaneDb>(options =>
            options.UseSqlServer(hostConnectionString, sql =>
                sql.MigrationsAssembly(typeof(ControlPlaneDb).Assembly.FullName)));

        services.AddSingleton<IPasswordHasher<Administrator>, PasswordHasher<Administrator>>();
        services.AddScoped<IGlobalAdministration, GlobalAdministrationService>();

        return services;
    }
}
