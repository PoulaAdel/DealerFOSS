// Copyright (c) 2026 The DealerFOSS contributors.
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Overview: Purpose, File Design, and Engineering
//   load.cs - measures normal interactive API latency under a stated load, so
//   the p95 promised in doc 07 and required by doc 09's exit criteria is a
//   number somebody measured rather than a number somebody hoped for.
//
//   A .NET 10 file-based app on purpose. It is not a project, it is not in
//   DealerFOSS.slnx, and it takes no NuGet package - so it adds nothing to the
//   build, nothing for NuGetAudit to police, and cannot slow the gates. It sits
//   beside verify-e2e.ps1 because that is where this project keeps hand-rolled
//   verification tooling, and it reuses that script's sign-in shape deliberately
//   rather than inventing a second one.
//
//   TWO LATENCIES ARE REPORTED AND THE DIFFERENCE IS THE WHOLE POINT.
//   "Service" is how long a request took once sent. "Response" is measured from
//   the moment the request was DUE to be sent. When the driver falls behind,
//   service time stays flat and response time grows - and response time is the
//   one a person would have felt. Reporting only service time is coordinated
//   omission: a server that slows down receives less load in a naive closed
//   loop, so the percentile improves as the system gets worse. That is how most
//   homegrown load tests report a number that is not true, and it is the single
//   easiest way to make this whole exercise worthless.
//
//   THE DRIVER MUST PROVE IT WAS NOT THE BOTTLENECK. Achieved rate is printed
//   against requested rate every run, measured against the wall clock. A
//   measurement taken while the harness was saturated describes the harness,
//   and is not citable.
//
//   WHEN THE OFFERED RATE IS NOT SUSTAINED, RESPONSE TIME STOPS BEING LATENCY.
//   Each user awaits its own request before scheduling the next, so while
//   service time stays under the think interval this is a true open model - and
//   once service time exceeds it, the model degrades to a closed loop and
//   "time since due" accumulates backlog without bound. The run that taught us
//   this reported a 181 SECOND p95 response against an 11 s service time. Both
//   are printed, and the banner says which one to read. Do not quote response
//   time from a run whose banner says the load was not sustained.
//
// Usage:
//   Needs a host already running and seeded (Seed:Demo on - a p95 measured
//   against a handful of rows is meaningless). Start one with:
//     dotnet run --project src/App -c Release --no-build -- --urls http://localhost:5080
//
//   Then, the documented load - 50 concurrent users per organization:
//     dotnet run deploy/load.cs
//
//   Saturation instead, to find where it actually breaks:
//     dotnet run deploy/load.cs -- --think 0 --seconds 30
//
//   Arguments: --url --tenants --users --think --seconds --warmup --email
//   --password. Defaults match the seeded development data.
//
// Coding Instructions:
//   DO NOT replace the fixed cadence with "loop as fast as you can and divide".
//   The cadence is what makes the arrival rate independent of how the server is
//   coping, and independence is the property being bought.
//
//   Read every response body. An unread body measures time-to-headers, not
//   time-to-answer, and the difference is exactly where a large list hurts.
//
//   Warmup samples are discarded, not averaged in. The first call to an
//   endpoint pays JIT and EF query-plan compilation; left in, it lands in the
//   tail and gets reported as a latency problem that does not exist.
//
//   Percentiles interpolate. Picking sorted[(int)(n * 0.95)] silently reports a
//   different statistic on small samples, and small samples are exactly when
//   somebody eyeballs the number and trusts it.

using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;

string? Arg(string name)
{
    var i = Array.IndexOf(args, name);
    return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
}

var baseUrl = Arg("--url") ?? "http://localhost:5080";
var tenants = (Arg("--tenants") ?? "northgroup,citymotors").Split(',', StringSplitOptions.RemoveEmptyEntries);
var users = int.Parse(Arg("--users") ?? "50", CultureInfo.InvariantCulture);
var thinkMs = int.Parse(Arg("--think") ?? "5000", CultureInfo.InvariantCulture);
var seconds = int.Parse(Arg("--seconds") ?? "60", CultureInfo.InvariantCulture);
var warmupSeconds = int.Parse(Arg("--warmup") ?? "10", CultureInfo.InvariantCulture);
var email = Arg("--email") ?? "gm@dev.local";
var password = Arg("--password") ?? "Dev@Pass1!";

// Weighted toward the lists a person actually sits on all day. reporting/month
// is the heaviest call and the rarest, so over-weighting it would report a
// pessimistic figure nobody experiences.
(string Path, int Weight)[] mix =
[
    ("/api/v1/inventory?limit=50", 25),
    ("/api/v1/customers?limit=50", 20),
    ("/api/v1/leads?limit=50", 20),
    ("/api/v1/deals?limit=50", 15),
    ("/api/v1/repair-orders?limit=50", 12),
    ("/api/v1/reporting/month", 8),
];

var wheel = mix.SelectMany(m => Enumerable.Repeat(m.Path, m.Weight)).ToArray();
var totalUsers = users * tenants.Length;
var requestedRate = thinkMs > 0 ? totalUsers * 1000.0 / thinkMs : double.NaN;

Console.WriteLine("DealerFOSS load baseline");
Console.WriteLine($"  target       {baseUrl}");
Console.WriteLine($"  tenants      {string.Join(", ", tenants)}");
Console.WriteLine($"  users        {users} per organization ({totalUsers} total)");
Console.WriteLine(thinkMs > 0
    ? $"  think time   {thinkMs} ms  ->  requested {requestedRate:N1} req/s"
    : "  think time   0 ms  ->  SATURATION (closed loop, no requested rate)");
Console.WriteLine($"  duration     {seconds} s measured, {warmupSeconds} s warmup discarded");
Console.WriteLine();

async Task<HttpClient> SignInAsync(string tenant)
{
    var handler = new SocketsHttpHandler
    {
        CookieContainer = new CookieContainer(),
        UseCookies = true,
        // High on purpose: the driver must never be the thing that queues.
        MaxConnectionsPerServer = 2000,
    };

    var client = new HttpClient(handler)
    {
        BaseAddress = new Uri(baseUrl),
        Timeout = TimeSpan.FromSeconds(30),
    };
    client.DefaultRequestHeaders.Add("X-Tenant", tenant);

    // Built with JsonEncodedText rather than a serializer: the reflection-based
    // Serialize overload is neither trim- nor AOT-safe, which this file is
    // compiled to be, and a two-field login body does not need a serializer to
    // be correct. Encode still escapes, so a password with a quote in it works.
    var json =
        $"{{\"email\":\"{JsonEncodedText.Encode(email)}\",\"password\":\"{JsonEncodedText.Encode(password)}\"}}";
    var body = new StringContent(json, Encoding.UTF8, "application/json");

    HttpResponseMessage response;
    try
    {
        response = await client.PostAsync("/api/v1/auth/login", body);
    }
    catch (HttpRequestException ex)
    {
        // A refused connection is the overwhelmingly common way to run this
        // wrong: the host was never started, or was stopped between rungs of a
        // ladder. A forty-line socket stack trace buries that behind something
        // that looks like a driver defect, and the next person spends an
        // afternoon on it. Say it in one line and name the fix.
        throw new InvalidOperationException(
            $"Could not reach {baseUrl} - nothing is listening there. Start the host first:"
            + "\n  dotnet run --project src/App -c Release --no-build -- --urls http://localhost:5080",
            ex);
    }

    if (!response.IsSuccessStatusCode)
    {
        throw new InvalidOperationException(
            $"Sign-in failed for {tenant} as {email}: {(int)response.StatusCode}. "
            + "Is the host running and seeded?");
    }

    return client;
}

var clients = new Dictionary<string, HttpClient>();
try
{
    foreach (var tenant in tenants)
    {
        clients[tenant] = await SignInAsync(tenant);
        Console.WriteLine($"signed in to {tenant}");
    }
}
catch (InvalidOperationException ex)
{
    // Exit 2 rather than throwing: this is a setup mistake, not a failed
    // measurement, and the two must not look the same to a script.
    Console.Error.WriteLine(ex.Message);
    return 2;
}

var samples = new List<Sample>[totalUsers];
var clock = Stopwatch.StartNew();
var warmupMs = warmupSeconds * 1000.0;
var stopMs = warmupMs + seconds * 1000.0;

async Task RunUserAsync(int index, string tenant, HttpClient client)
{
    var mine = new List<Sample>(4096);
    samples[index] = mine;

    // Each user is offset inside one think interval so fifty of them do not all
    // fire on the same tick and manufacture a burst the model never asked for.
    var random = new Random(index * 7919);
    var offsetMs = thinkMs > 0 ? random.NextDouble() * thinkMs : 0.0;
    var pick = random.Next(wheel.Length);
    long issued = 0;

    while (true)
    {
        var scheduledMs = thinkMs > 0
            ? offsetMs + thinkMs * issued
            : clock.Elapsed.TotalMilliseconds;

        if (scheduledMs >= stopMs)
        {
            return;
        }

        var wait = scheduledMs - clock.Elapsed.TotalMilliseconds;
        if (wait > 1)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(wait));
        }

        var path = wheel[pick % wheel.Length];
        pick++;
        issued++;

        var sentMs = clock.Elapsed.TotalMilliseconds;
        var status = 0;
        try
        {
            using var response = await client.GetAsync(path);
            status = (int)response.StatusCode;
            // Read it. Time-to-headers is not the number anybody waits for.
            _ = await response.Content.ReadAsByteArrayAsync();
        }
        catch (Exception)
        {
            status = -1;
        }

        var doneMs = clock.Elapsed.TotalMilliseconds;

        // Warmup is discarded by when the request was DUE, not when it finished,
        // so a slow call straddling the boundary is not half-counted.
        if (scheduledMs >= warmupMs)
        {
            mine.Add(new Sample(path, doneMs - sentMs, doneMs - scheduledMs, status));
        }
    }
}

var runners = new List<Task>();
for (var t = 0; t < tenants.Length; t++)
{
    for (var u = 0; u < users; u++)
    {
        runners.Add(RunUserAsync(t * users + u, tenants[t], clients[tenants[t]]));
    }
}

Console.WriteLine($"running {totalUsers} users for {warmupSeconds + seconds} s ...");
Console.WriteLine();
await Task.WhenAll(runners);

var all = samples.Where(s => s is not null).SelectMany(s => s).ToList();
var ok = all.Where(s => s.Status == 200).ToList();
var bad = all.Count - ok.Count;

// Measured against the clock, NOT against the requested duration. Dividing by
// the duration that was ASKED for reports the requested rate back as though it
// had been achieved, which is worse than useless: it prints "kept up: yes" in
// exactly the overloaded runs where the answer is no. Found 2026-09-29, when a
// 40 s run took over 180 s and still claimed 200 req/s.
var elapsedSeconds = Math.Max(0.001, (clock.Elapsed.TotalMilliseconds - warmupMs) / 1000.0);
var achieved = all.Count / elapsedSeconds;
var overran = elapsedSeconds > seconds * 1.1;

static double Pct(List<double> sorted, double p)
{
    if (sorted.Count == 0)
    {
        return double.NaN;
    }

    var rank = p / 100.0 * (sorted.Count - 1);
    var lo = (int)Math.Floor(rank);
    var hi = (int)Math.Ceiling(rank);
    return lo == hi ? sorted[lo] : sorted[lo] + (rank - lo) * (sorted[hi] - sorted[lo]);
}

void Report(string title, Func<Sample, double> pick)
{
    Console.WriteLine(title);
    Console.WriteLine($"  {"endpoint",-34}{"n",7}{"p50",7}{"p75",7}{"p90",7}{"p95",7}{"p99",7}{"max",7}");
    foreach (var group in mix.Select(m => m.Path))
    {
        var values = ok.Where(s => s.Path == group).Select(pick).OrderBy(v => v).ToList();
        if (values.Count == 0)
        {
            continue;
        }

        Console.WriteLine($"  {group,-34}{values.Count,7}{Pct(values, 50),7:N0}{Pct(values, 75),7:N0}"
            + $"{Pct(values, 90),7:N0}{Pct(values, 95),7:N0}{Pct(values, 99),7:N0}{values[^1],7:N0}");
    }

    var allValues = ok.Select(pick).OrderBy(v => v).ToList();
    Console.WriteLine($"  {"ALL",-34}{allValues.Count,7}{Pct(allValues, 50),7:N0}{Pct(allValues, 75),7:N0}"
        + $"{Pct(allValues, 90),7:N0}{Pct(allValues, 95),7:N0}{Pct(allValues, 99),7:N0}{allValues[^1],7:N0}");
    Console.WriteLine();
}

if (thinkMs > 0)
{
    var kept = achieved >= requestedRate * 0.95;
    Console.WriteLine($"requested {requestedRate:N1} req/s   achieved {achieved:N1} req/s"
        + $"   over {elapsedSeconds:N1} s of wall clock");
    Console.WriteLine(kept
        ? "offered load was sustained: the figures below describe the API."
        : "OFFERED LOAD WAS NOT SUSTAINED. The server could not absorb the requested"
          + "\nrate, so users fell behind their schedule and the arrival model degraded"
          + "\nfrom open to closed. Read SERVICE time below; RESPONSE time is backlog,"
          + "\nnot latency, and grows without bound once behind.");
}
else
{
    Console.WriteLine($"saturation: achieved {achieved:N1} req/s with {totalUsers} users in flight");
}

Console.WriteLine($"samples {all.Count}   non-200 {bad}");

// What the failures WERE, not just how many. A timeout and a 500 point at
// different causes, and "525 errors" sends the next person guessing.
foreach (var group in all.Where(s => s.Status != 200)
    .GroupBy(s => s.Status)
    .OrderByDescending(g => g.Count()))
{
    var label = group.Key == -1 ? "timeout or socket failure" : $"HTTP {group.Key}";
    Console.WriteLine($"  {group.Count(),6} x {label}");
}

Console.WriteLine();

Report("service time (once sent)", s => s.ServiceMs);
Report("response time (from when the request was due - this is the honest one)", s => s.ResponseMs);

var verdict = ok.Select(s => s.ResponseMs).OrderBy(v => v).ToList();
var p95 = Pct(verdict, 95);
Console.WriteLine($"p95 response {p95:N0} ms   target 500 ms   {(p95 < 500 ? "PASS" : "FAIL")}");
Console.WriteLine("Target is doc 07's coexistence release figure. The standalone");
Console.WriteLine("financial core column asks for 300 ms and is a later release shape.");

return bad == 0 && p95 < 500 ? 0 : 1;

internal readonly record struct Sample(string Path, double ServiceMs, double ResponseMs, int Status);
