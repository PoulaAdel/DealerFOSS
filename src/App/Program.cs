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
using OpenDealer360.Accounting;
using OpenDealer360.Administration;
using OpenDealer360.App;
using OpenDealer360.Core;
using OpenDealer360.Customers;
using OpenDealer360.Data;
using OpenDealer360.Deals;
using OpenDealer360.Identity;
using OpenDealer360.Inventory;
using OpenDealer360.Leads;
using OpenDealer360.Organization;
using OpenDealer360.Tenancy;
using OpenDealer360.Vehicles;
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
    builder.Services.AddScoped<IAccounting, AccountingService>();
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
        serviceName: "OpenDealer360.App",
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

app.UseSerilogRequestLogging();

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
    name = "OpenDealer360",
    description = "Open-source Dealer Management System",
    status = "ok",
}));

if (tenancyEnabled)
{
    app.MapAuth();
    app.MapSecurity();
    app.MapAdministration();
    app.MapOrganization();
    app.MapCustomers();
    app.MapVehicles();
    app.MapInventory();
    app.MapLeads();
    app.MapDeals();
    app.MapAccounting();
}

// Development-only sample data (doc 08 §8), gated behind an explicit flag.
if (tenancyEnabled
    && app.Environment.IsDevelopment()
    && app.Configuration.GetValue<bool>("Seed:Enabled"))
{
    await DevelopmentSeeder.RunAsync(app, hostConnection!);
}

app.Run();

/// <summary>Assembly version constant surfaced to telemetry.</summary>
internal static class ThisAssembly
{
    public static readonly string Version =
        typeof(ThisAssembly).Assembly.GetName().Version?.ToString() ?? "0.0.0";
}

/// <summary>Exposed so integration tests can drive the app via WebApplicationFactory.</summary>
public partial class Program;
