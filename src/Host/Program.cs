using System.Globalization;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using OpenDealer360.Host.Development;
using OpenDealer360.Host.Tenancy;
using OpenDealer360.Identity;
using OpenDealer360.Organization;
using OpenDealer360.Core;
using OpenDealer360.Tenancy;
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

// --- Tenancy + modules. Enabled when a host-catalog connection is configured,
//     so the Host still runs health-only with zero configuration (doc 07 §1). ---
var hostConnection = builder.Configuration.GetConnectionString("HostCatalog");
var tenancyEnabled = !string.IsNullOrWhiteSpace(hostConnection);
if (tenancyEnabled)
{
    builder.Services.AddHostCatalog(hostConnection!);
    builder.Services.AddScoped<ICurrentUser, CurrentUser>();
    builder.Services.AddIdentityModule();
    builder.Services.AddOrganizationModule();
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
        serviceName: "OpenDealer360.Host",
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
// non-Development environment (doc 06 §4).
if (!app.Environment.IsDevelopment()
    && app.Services.GetService<ISecretProtector>() is DevSecretProtector)
{
    throw new InvalidOperationException(
        "The development pass-through secret protector must not be used outside Development. "
        + "Register a DPAPI/certificate/KMS-backed ISecretProtector.");
}

app.UseSerilogRequestLogging();

if (tenancyEnabled)
{
    // Order matters: the tenant is resolved first, then the caller within it.
    app.UseMiddleware<TenantMiddleware>();
    app.UseMiddleware<CurrentUserMiddleware>();
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
    app.MapOrganizationModule();
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

/// <summary>Exposed so integration tests can drive the Host via WebApplicationFactory.</summary>
public partial class Program;
