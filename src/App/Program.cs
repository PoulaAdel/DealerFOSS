// Program — the composition root. Everything the application is made of is
// registered and ordered here, and nowhere else.
//
// Use:  dotnet run --project src/App. With no HostCatalog connection string it
//       still starts and serves health only, which keeps a bare checkout usable.
// Edit: a new feature is one AddScoped line and one Map line. Middleware order
//       is behaviour, not style: the tenant must be resolved before the caller,
//       and both before any endpoint runs.

using System.Globalization;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
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

var builder = WebApplication.CreateBuilder(args);

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
    builder.Services.AddScoped<IAccounting, AccountingService>();
    builder.Services.AddScoped<IReporting, ReportingService>();
    builder.Services.AddScoped<IMigration, MigrationService>();

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

// First, so a response that fails anywhere below still carries them. A security
// header set only on the happy path is not a control.
app.UseMiddleware<SecurityHeadersMiddleware>();

app.UseSerilogRequestLogging();

// Guessing a password, a six-digit code, or an enrolment code is a volume game,
// and volume is the one thing a limiter takes away. Applied to the credential
// endpoints only — throttling the whole API would punish a busy dealership for
// being busy.
app.UseRateLimiter();

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

app.MapGet("/", () => Results.Ok(new
{
    name = "DealerFOSS",
    description = "Open-source Dealer Management System",
    status = "ok",
}));

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
    app.MapStaff();
    app.MapFinance();
    app.MapDocuments();
    app.MapAccounting();
    app.MapReporting();
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
