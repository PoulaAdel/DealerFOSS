// Copyright (c) 2026 The DealerFOSS contributors.
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Overview: Purpose, File Design, and Engineering
//   Program — the composition root. Everything the application is made of is
//   registered and ordered here, and nowhere else.
//
// Usage:
//   Dotnet run --project src/App. With no HostCatalog connection string it
//   still starts and serves health only, which keeps a bare checkout usable.
//
// Coding Instructions:
//   A new feature is one AddScoped line and one Map line. Middleware order
//   is behaviour, not style: the tenant must be resolved before the caller,
//   and both before any endpoint runs.

using System.Globalization;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting.WindowsServices;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using DealerFOSS.Accounting;
using DealerFOSS.Administration;
using DealerFOSS.App;
using DealerFOSS.Core;
using DealerFOSS.Customers;
using DealerFOSS.Data;
using DealerFOSS.DataMigration;
using DealerFOSS.Deals;
using DealerFOSS.Documents;
using DealerFOSS.Finance;
using DealerFOSS.RepairOrders;
using DealerFOSS.Reporting;
using DealerFOSS.Identity;
using DealerFOSS.Integrations;
using DealerFOSS.Inventory;
using DealerFOSS.Leads;
using DealerFOSS.Organization;
using DealerFOSS.Parts;
using DealerFOSS.Tenancy;
using System.Threading.RateLimiting;
using DealerFOSS.Vehicles;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;

// A Windows service starts in whatever directory the service control manager
// chose — typically system32 — not in the folder holding the executable. So
// appsettings.json and wwwroot are both looked for in the wrong place and
// neither is found: the service starts, serves the API from defaults, and shows
// a blank page.
//
// This has to be passed at construction. Assigning builder.Environment
// afterwards is too late — the configuration sources have already been bound
// against the original content root.
//
// Gated on actually being a service, so a `dotnet run`, the container, and the
// test host keep the content root each of them expects. Null means "work it out
// as usual".
var serviceContentRoot = WindowsServiceHelpers.IsWindowsService()
    ? AppContext.BaseDirectory
    : null;

var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args,
    ContentRootPath = serviceContentRoot,
});

// Lets the service control manager start and stop this process properly. Without
// it an installed service never reports "running" and `sc start` times out after
// thirty seconds — with the application actually up and serving, which makes for
// a confusing half hour for whoever installed it.
//
// A no-op when the process is not running under the SCM.
builder.Host.UseWindowsService(options => options.ServiceName = "DealerFOSS");

// --- Structured logging (doc 02 stack: Serilog) ---
builder.Host.UseSerilog((context, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console(formatProvider: CultureInfo.InvariantCulture));

// --- Core services ---
builder.Services.AddSingleton<IClock, SystemClock>();

// --- Tenancy, identity, and features. Enabled when a host-catalog connection is
//     configured, so the app still runs health-only with zero configuration
//     (doc 07 §1). ---
var hostConnection = builder.Configuration.GetConnectionString("HostCatalog");
var tenancyEnabled = !string.IsNullOrWhiteSpace(hostConnection);
if (tenancyEnabled)
{
    builder.Services.AddHostCatalog(hostConnection!);

    // Replaces the development pass-through with real AES-256-GCM envelope
    // encryption whenever keys are configured — in every environment, so what
    // runs in production is what developers exercise.
    builder.Services.AddSecretProtection(builder.Configuration);

    builder.Services.AddScoped<ICurrentUser, CurrentUser>();
    builder.Services.AddScoped<ICurrentAdministrator, CurrentAdministrator>();

    // Identity is a separate project so its tables and services are physically
    // unreachable from here — only IAccessDirectory and IAuthenticator are
    // public (ADR-017).
    builder.Services.AddIdentity();

    // Control-plane identity, bound to the host catalog rather than to any
    // tenant. Separate context, separate tables, separate cookie: an
    // administrator cannot become a dealership caller by any route (doc 06 §2).
    builder.Services.AddControlPlane(hostConnection!);

    // One business database for this tenant, bound to the connection resolved
    // for the current request.
    builder.Services.AddDbContext<TenantDb>((serviceProvider, options) =>
    {
        var tenant = serviceProvider.GetRequiredService<ITenantContext>();
        options.UseSqlServer(tenant.Current.ConnectionString);
    });

    builder.Services.AddScoped<IOrganization, OrganizationService>();
    builder.Services.AddScoped<ICustomers, CustomerService>();
    builder.Services.AddScoped<IVehicles, VehicleService>();
    builder.Services.AddScoped<IInventory, InventoryService>();
    builder.Services.AddScoped<ILeads, LeadService>();
    builder.Services.AddScoped<IDeals, DealService>();
    builder.Services.AddScoped<IFinanceProducts, FinanceProductService>();
    builder.Services.AddScoped<IDocuments, DocumentService>();
    builder.Services.AddScoped<ITenantProvisioning, TenantProvisioning>();
    builder.Services.AddScoped<IParts, PartsService>();
    builder.Services.AddScoped<IRepairOrders, RepairOrderService>();
    builder.Services.AddScoped<IAppointments, AppointmentService>();
    builder.Services.AddScoped<IAccounting, AccountingService>();
    builder.Services.AddScoped<IReporting, ReportingService>();
    builder.Services.AddScoped<IMigration, MigrationService>();

    // The only outbound call in the product, and the only integration on the
    // manager's list that needs no contract and no approval: the US road-safety
    // regulator publishes recall campaigns free and unauthenticated (doc 11 §3.4).
    //
    // The timeout is here rather than inside the adapter so the deadline is
    // visible where the dependency is declared. Ten seconds is generous for a
    // read and short enough that a person waiting on a screen is told something
    // went wrong rather than left watching a spinner.
    builder.Services.AddHttpClient<ISafetyRecalls, NhtsaSafetyRecalls>(client =>
    {
        client.BaseAddress = new Uri(
            builder.Configuration["SafetyRecalls:BaseAddress"] ?? "https://api.nhtsa.gov/");
        client.Timeout = TimeSpan.FromSeconds(10);
    });

    // The integration edge. A capability that can receive records from a
    // connector registers an IRecordSink here; the runtime finds it by contract
    // name and version. A contract with no sink is refused as Misconfigured
    // before the provider is called.
    builder.Services.AddScoped<ConnectorRuntime>();
    builder.Services.AddScoped<IRecordSink, CustomerRecordSink>();

    // Work that is not a request. It names the dealership it is working on
    // rather than inheriting one, because outside a request there is no
    // "current" tenant and there must never be one (see TenantScope).
    builder.Services.AddSingleton<ITenantScopeFactory, TenantScopeFactory>();
    builder.Services.AddHostedService<ImportWorker>();
}

// --- Health: liveness, readiness, and degraded dependencies are separated
//     (doc 07 §6). Readiness checks are tagged "ready". ---
builder.Services
    .AddHealthChecks()
    .AddCheck("self", () => HealthCheckResult.Healthy("Host process is running."), tags: ["ready"]);

// --- Telemetry (doc 02 stack: OpenTelemetry). The OTLP exporter is added only
//     when an endpoint is configured, so a small install runs without a
//     collector present. ---
var otlpEndpoint = builder.Configuration["OpenTelemetry:OtlpEndpoint"];
builder.Services
    .AddOpenTelemetry()
    .ConfigureResource(resource => resource.AddService(
        serviceName: "DealerFOSS.App",
        serviceVersion: ThisAssembly.Version))
    .WithTracing(tracing =>
    {
        tracing.AddAspNetCoreInstrumentation();
        if (!string.IsNullOrWhiteSpace(otlpEndpoint))
        {
            tracing.AddOtlpExporter(options => options.Endpoint = new Uri(otlpEndpoint));
        }
    })
    .WithMetrics(metrics =>
    {
        metrics.AddAspNetCoreInstrumentation();
        if (!string.IsNullOrWhiteSpace(otlpEndpoint))
        {
            metrics.AddOtlpExporter(options => options.Endpoint = new Uri(otlpEndpoint));
        }
    });

// Rate limiting on the endpoints where somebody guesses a secret.
//
// Partitioned by client address, so one attacker cannot lock out a whole
// dealership by exhausting a shared bucket — which is what a global limiter on a
// sign-in page amounts to. A fixed window rather than a token bucket because the
// threat is sustained volume, not a burst.
//
// Note what this does NOT replace: the second-factor challenge already dies
// after five wrong codes, and an enrolment code after five. Those are per-secret
// and this is per-caller; each covers what the other cannot.
// Whether anything in front of this process may be believed about who the
// caller is. Empty unless the operator listed their reverse proxies, and the
// limiter below partitions on the direct peer when it is — see TrustedProxies
// for why trusting X-Forwarded-For unconditionally is worse than not trusting
// it at all.
var trustedProxies = builder.Services.AddTrustedProxies(builder.Configuration);

var credentialAttemptsPerMinute =
    builder.Configuration.GetValue("RateLimiting:CredentialAttemptsPerMinute", 20);

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.AddPolicy(RateLimits.Credentials, context =>
        RateLimitPartition.GetFixedWindowLimiter(
            // Falls back to the connection id rather than a constant when there
            // is no remote address. A shared constant would put every caller in
            // ONE bucket — so twenty sign-ins from anywhere would lock out
            // everybody, which is what happened the first time this was written
            // and the whole test suite started failing. A real deployment always
            // has a remote address (the client's, or the proxy's), so this
            // fallback is effectively in-process callers only.
            //
            // Behind a reverse proxy this is the PROXY's address for every
            // caller — the same one-bucket failure — unless the operator has
            // listed their proxies under Network:TrustedProxies, in which case
            // UseForwardedHeaders below has already replaced it with the real
            // client. It is opt-in because an unconditionally trusted
            // X-Forwarded-For is spoofable, and a limiter an attacker can step
            // around at will is worse than one that is merely shared.
            context.Connection.RemoteIpAddress?.ToString() ?? context.Connection.Id,
            _ => new FixedWindowRateLimiterOptions
            {
                // Twenty a minute is far beyond a person typing and far below
                // anything worth calling an attack.
                //
                // Configurable because the in-process test host is not a
                // realistic caller: it makes hundreds of sign-ins in seconds down
                // one connection, and no partitioning scheme distinguishes that
                // from an attack without also failing to catch a real one. The
                // integration suite raises it and the limiter is proven instead
                // by verify-e2e.ps1, against a real host over a real socket —
                // which is the more honest test anyway.
                PermitLimit = credentialAttemptsPerMinute,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
            }));
});

var app = builder.Build();

// Refuse to run with the development pass-through secret protector in any
// non-Development environment (doc 06 §4). Tenant connection strings would be
// stored in plaintext, which is not a degraded mode — it is a breach waiting to
// be found.
if (!app.Environment.IsDevelopment()
    && app.Services.GetService<ISecretProtector>() is DevSecretProtector)
{
    throw new InvalidOperationException(
        $"No secret-protection keys are configured, so tenant connection strings would be stored "
        + $"in plaintext. Set {SecretProtection.SectionName}:CurrentKeyId and at least one "
        + $"{SecretProtection.SectionName}:Keys entry — a base64 32-byte key. "
        + "deploy/README.md explains how to generate one and where to put it on each "
        + "deployment target.");
}

// A maintenance verb, not a server. Restoring the host catalog under new
// database names leaves every tenant row still naming the originals, and only
// something holding the deployment's keys can rewrite them — so the restore
// script calls this rather than being handed the keys itself.
if (args.Contains(RepointTenants.Verb, StringComparer.OrdinalIgnoreCase))
{
    if (!tenancyEnabled)
    {
        Console.Error.WriteLine(
            "No HostCatalog connection string, so there is no catalog to re-point.");
        return 1;
    }

    var (prefix, server, dryRun) = RepointTenants.Parse(args);

    using var maintenance = app.Services.CreateScope();
    return await RepointTenants.RunAsync(
        maintenance.ServiceProvider.GetRequiredService<HostDb>(),
        maintenance.ServiceProvider.GetRequiredService<ISecretProtector>(),
        maintenance.ServiceProvider.GetRequiredService<IClock>(),
        prefix,
        server,
        dryRun,
        Console.Out);
}

// Before everything, because it changes the answer to "who is calling" and "is
// this HTTPS" — and both the security headers and the rate limiter below are
// wrong if they run first. Registered only when the operator named their
// proxies; with none configured this is not in the pipeline at all and the
// direct peer is used, exactly as before.
if (trustedProxies.Count > 0)
{
    app.UseForwardedHeaders();
    TrustedProxyLog.Trusting(app.Logger, trustedProxies.Count, trustedProxies);
}

// First of the real pipeline, so a response that fails anywhere below still
// carries them. A security header set only on the happy path is not a control.
app.UseMiddleware<SecurityHeadersMiddleware>();

app.UseSerilogRequestLogging();

// Guessing a password, a six-digit code, or an enrolment code is a volume game,
// and volume is the one thing a limiter takes away. Applied to the credential
// endpoints only — throttling the whole API would punish a busy dealership for
// being busy.
app.UseRateLimiter();

// --- the application itself ---
//
// The built frontend is served from wwwroot when it is there. It is not there in
// a bare checkout — in development the Vite dev server serves it and proxies
// /api here — so this is guarded rather than assumed. `dotnet run` on a fresh
// clone must still start and serve the API.
//
// Being served by the application, rather than by a separate web server, is what
// makes an installation ONE thing to install. It is also what keeps the session
// cookie working without configuration: the cookie is SameSite=Strict, so the
// page and the API have to be the same origin or the browser silently drops it.
var shellFile = Path.Combine(app.Environment.WebRootPath ?? string.Empty, "index.html");
var shellIsPublished = !string.IsNullOrEmpty(app.Environment.WebRootPath) && File.Exists(shellFile);

// One options object, used by BOTH the static-file middleware and the shell
// fallback below. They are two different ways of sending the same files, and each
// carries its own copy of these options — so a policy set on only one of them
// applies to some requests and not others.
//
// That is not hypothetical: index.html reached the browser with no cache header
// at all until this was shared, because "/" is served by the fallback ENDPOINT
// rather than by the middleware. Routing runs first, selects the endpoint, and
// UseStaticFiles then stands aside for it.
var staticFiles = new StaticFileOptions
{
    OnPrepareResponse = served =>
    {
        // Vite fingerprints everything under /assets, so a changed file has a
        // changed name and caching it for a year is safe. index.html must never
        // be cached: it is the file that names the current fingerprints, and a
        // stale copy points a browser at assets that no longer exist — which
        // presents as a white page after an upgrade, fixed by a hard refresh
        // nobody knows to perform.
        var path = served.Context.Request.Path.Value ?? string.Empty;

        served.Context.Response.Headers.CacheControl =
            path.StartsWith("/assets/", StringComparison.Ordinal)
                ? "public, max-age=31536000, immutable"
                : "no-store";
    },
};

if (shellIsPublished)
{
    app.UseStaticFiles(staticFiles);
}

if (tenancyEnabled)
{
    // Order matters: the tenant is resolved first, then the caller within it,
    // and only then is a write allowed to prove it was not forged. The
    // administrator step sits between the first two because opening support
    // access needs the dealership already resolved — and because a control-plane
    // request must never reach the tenant caller step at all.
    app.UseMiddleware<TenantMiddleware>();
    app.UseMiddleware<AdministratorMiddleware>();
    app.UseMiddleware<CurrentUserMiddleware>();
    app.UseMiddleware<AntiForgeryMiddleware>();
}

// Liveness: is the process up at all? Runs no dependency checks.
app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });

// Readiness: are the dependencies this node needs available?
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = registration => registration.Tags.Contains("ready"),
});

// The identity document, for a bare API installation with no frontend built.
//
// Guarded, because it and the shell fallback both answer "/" and this one wins:
// routing runs before the static-file middleware, and once an endpoint is
// selected UseStaticFiles stands aside for it. Unguarded, an operator opening
// http://their-server:8080 for the first time is shown `{"name":"DealerFOSS"…}`
// and reasonably concludes the install is broken. UseDefaultFiles does not fix
// that — it is the same collision — so the route simply is not mapped when there
// is a frontend to serve instead.
if (!shellIsPublished)
{
    app.MapGet("/", () => Results.Ok(new
    {
        name = "DealerFOSS",
        description = "Open-source Dealer Management System",
        status = "ok",
    }));
}

if (tenancyEnabled)
{
    app.MapAuth();
    app.MapSecurity();
    app.MapAdministration();
    app.MapMigration();
    app.MapOrganization();
    app.MapCustomers();
    app.MapVehicles();
    app.MapInventory();
    app.MapLeads();
    app.MapDeals();
    app.MapParts();
    app.MapRepairOrders();
    app.MapAppointments();
    app.MapStaff();
    app.MapFinance();
    app.MapDocuments();
    app.MapAccounting();
    app.MapReporting();
}

if (shellIsPublished)
{
    // An unknown API route is a 404, not the application shell. Without this the
    // catch-all below would answer /api/v1/typo with a page and a 200, and a
    // caller would parse HTML looking for JSON — the kind of failure that costs
    // an afternoon because nothing reports an error.
    // Rehearsed 2026-08-07: with this line removed, a signed-in caller asking for
    // /api/v1/organisation gets the application shell and HTTP 200. The
    // MapFallbackToFile below matches any path without a dot in it, which every
    // mistyped API route is.
    app.MapFallback("/api/{**path}", () => Results.NotFound());

    // Everything else is the frontend's own routing. /dashboard and /inventory
    // are not server routes; the browser asks for them on a reload and has to be
    // handed the shell. This also answers "/", which is why the JSON identity
    // route above is guarded.
    //
    // The default route pattern here is {*path:nonfile} — it deliberately does
    // NOT match a path containing a dot, so a request for an asset that no longer
    // exists fails as a missing script rather than returning HTML where
    // JavaScript was expected.
    app.MapFallbackToFile("index.html", staticFiles);
}

// Development-only sample data (doc 08 §8), gated behind an explicit flag.
if (tenancyEnabled
    && app.Environment.IsDevelopment()
    && app.Configuration.GetValue<bool>("Seed:Enabled"))
{
    await DevelopmentSeeder.RunAsync(app, hostConnection!);
}

app.Run();

// Serving is the ordinary path, and it only returns when the host stops. The
// maintenance verb above returns its own code before reaching here.
return 0;

/// <summary>Assembly version constant surfaced to telemetry.</summary>
internal static class ThisAssembly
{
    public static readonly string Version =
        typeof(ThisAssembly).Assembly.GetName().Version?.ToString() ?? "0.0.0";
}

/// <summary>Exposed so integration tests can drive the app via WebApplicationFactory.</summary>
public partial class Program;
