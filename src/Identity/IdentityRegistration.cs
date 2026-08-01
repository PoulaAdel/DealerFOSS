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
using OpenDealer360.Core;

namespace OpenDealer360.Identity;

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

        // Password hashing algorithm and parameters live here, so upgrading them
        // is one change rather than a search through call sites.
        services.AddSingleton<IPasswordHasher<User>, PasswordHasher<User>>();
        services.AddScoped<IAuthenticator, Authenticator>();

        return services;
    }
}
