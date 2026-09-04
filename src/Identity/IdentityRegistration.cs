// Copyright (c) 2026 The DealerFOSS contributors.
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Overview: Purpose, File Design, and Engineering
//   IdentityRegistration — the only way the application switches this project on.
//
// Usage:
//   Services.AddIdentity() from Program.cs.
//
// Coding Instructions:
//   This and the two contracts are almost the whole public surface of the
//   project. Everything else — the context, the services, the tables — is
//   internal on purpose, so no feature can write a user row or an audit row
//   except through IAccessDirectory and IAuthenticator (ADR-017). Adding a
//   public type here is a security decision, not a convenience.

using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
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
        services.AddScoped<IAccountRecovery, AccountRecoveryService>();

        // Password hashing algorithm and parameters live here, so upgrading them
        // is one change rather than a search through call sites.
        services.AddSingleton<IPasswordHasher<User>, PasswordHasher<User>>();
        services.AddScoped<IAuthenticator, Authenticator>();

        // Passkeys. ISessionIssuer resolves to the SAME Authenticator instance
        // as IAuthenticator — registered by forwarding rather than as a second
        // AddScoped, because two instances would mean two change trackers and a
        // session written by one while the other holds the transaction.
        services.AddScoped<ISessionIssuer>(sp => (Authenticator)sp.GetRequiredService<IAuthenticator>());
        services.AddScoped<IPasskeys, PasskeyDirectory>();

        // Bound from configuration where it exists, so a real deployment sets
        // its own domain. The default is localhost, which is right for
        // development and wrong everywhere else — a passkey is bound to the
        // relying-party id for ever, so shipping with this unset would orphan
        // every credential the day it was corrected.
        services.AddSingleton(sp =>
        {
            var options = new PasskeyOptions();
            sp.GetRequiredService<IConfiguration>().GetSection("Passkeys").Bind(options);
            return options;
        });

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
