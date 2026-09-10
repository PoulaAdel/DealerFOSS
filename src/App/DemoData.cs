// Copyright (c) 2026 The DealerFOSS contributors.
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Overview: Purpose, File Design, and Engineering
//   DemoData — a dealership with enough in it to look like one. Hundreds of
//   cars, customers, enquiries, deals and repair orders spread over three
//   months, so every screen has real volume and the month-on-month figures on
//   the dashboard have something to compare.
//
// Usage:
//   Set Seed:Demo to true in appsettings.Development.json and start the
//   application. Runs after DevelopmentSeeder, once, and does nothing on a
//   second run.
//
// Coding Instructions:
//   OFF BY DEFAULT, AND THAT IS NOT TIMIDITY. The integration suite and
//   verify-e2e.ps1 both assert against the small set DevelopmentSeeder makes —
//   counts, the single Draft deal, the three stock numbers. Turning this on for
//   everybody would break dozens of tests for no gain, and slow every run.
//
//   IT GOES THROUGH THE SERVICES, NOT THROUGH TenantDb. That is the whole
//   design. Delivering a deal posts to the ledger and invoicing a repair order
//   posts to the ledger; writing rows directly would mean re-implementing both
//   postings here, where they would drift from the real ones and quietly stop
//   reconciling. Going through IDeals and IRepairOrders means the trial balance
//   balances because the application balanced it, not because this file agreed
//   with itself.
//
//   ONE EXCEPTION, AND IT IS NAMED HERE SO THE RULE ABOVE STAYS TRUE:
//   PostStockPurchasesAsync writes its two ledger lines directly. IInventory
//   receives and posts at the current instant, and this seeder spreads stock
//   across ninety-five days precisely so the ageing bands and the month-on-month
//   figures have something to show — going through the service would flatten
//   every car onto today. The lines are the same two the service posts, and the
//   arithmetic is asserted by a regression test rather than by the route taken.
//   If you add a second exception, argue for it in the same way or do not add it.
//
//   DETERMINISTIC ON PURPOSE. One fixed seed, so two people running this see
//   the same dealership and can talk about "the Okafor deal" and mean the same
//   record. Never use Random() unseeded or DateTime.Now here.
//
//   Synthetic names only, and obviously so. Never seed anything resembling a
//   real person (doc 08 §8).

using Microsoft.EntityFrameworkCore;
using DealerFOSS.Accounting;
using DealerFOSS.Core;
using DealerFOSS.Customers;
using DealerFOSS.Data;
using DealerFOSS.Deals;
using DealerFOSS.Inventory;
using DealerFOSS.Leads;
using DealerFOSS.RepairOrders;
using DealerFOSS.Tenancy;
using DealerFOSS.Vehicles;

namespace DealerFOSS.App;

/// <summary>
/// DEVELOPMENT ONLY. Fills a seeded tenant with enough data to look like a
/// working dealership, so screens can be judged against real volume rather than
/// against three rows.
/// </summary>
public static class DemoData
{
    /// <summary>How far back the history runs, so "last month" is not empty.</summary>
    private const int DaysOfHistory = 95;

    /// <summary>
    /// Written onto every customer this file creates, so a second run can tell
    /// its own work from the base seeder's and leave both alone.
    /// </summary>
    private const string Marker = "DEMO";

    public static async Task PopulateAsync(WebApplication app, params string[] slugs)
    {
        ArgumentNullException.ThrowIfNull(app);
        ArgumentNullException.ThrowIfNull(slugs);

        var scopes = app.Services.GetRequiredService<ITenantScopeFactory>();
        var clock = app.Services.GetRequiredService<IClock>();

        foreach (var slug in slugs)
        {
            // Named as the development manager rather than unattended: these are
            // records a person would have created, and attributing hundreds of
            // rows to "system" would teach the wrong thing about the audit trail.
            var job = JobContext.RequestedBy(
                slug, DevelopmentSeeder.DevUsers.OrganizationWide, "demo data");

            await using var scope = await scopes.OpenAsync(job, CancellationToken.None);
            if (scope is null)
            {
                continue;
            }

            await PopulateTenantAsync(scope, clock);
        }
    }

    private static async Task PopulateTenantAsync(TenantScope scope, IClock clock)
    {
        var db = scope.Services.GetRequiredService<TenantDb>();

        // One seed, fixed. Two people running this get the same dealership.
        var rng = new Random(20260909);
        var today = clock.UtcNow;

        var rooftops = await db.Rooftops.OrderBy(r => r.Code).Select(r => r.Id).ToListAsync();
        if (rooftops.Count == 0)
        {
            return;
        }

        // EACH SECTION GUARDS ITSELF, rather than one check at the top. The
        // first draft asked "are there any demo customers?" and skipped the lot
        // if so — which meant a run that failed partway (the first one did, on a
        // VIN) left the tenant permanently half full, because customers had
        // already been written. Section guards let a second run finish the job.
        var customers = await ExistingCustomersAsync(db);

        if (customers.Count == 0)
        {
            customers = await AddCustomersAsync(db, rng, 480);
        }

        var units = await ExistingStockAsync(db);

        if (units.Count == 0)
        {
            units = await AddStockAsync(db, rng, rooftops, today, 210);
        }

        // Unconditional, and after the stock section rather than inside it: this
        // back-fills the dealerships seeded before the purchase posting existed,
        // which is every one of them created before 2026-09-10. Units that
        // already have an entry are skipped, so a second run is free.
        await PostStockPurchasesAsync(db, today);

        if (!await db.Leads.AnyAsync(l => l.Enquiry == LeadNote))
        {
            await AddLeadsAsync(db, rng, rooftops, customers, today, 140);
        }

        if (await db.Deals.CountAsync() < 20)
        {
            await AddDealsAsync(scope, db, rng, rooftops, customers, units, today, 90);
        }

        await DeliverApprovedAsync(scope, db);

        if (await db.RepairOrders.CountAsync() < 20)
        {
            await AddRepairOrdersAsync(scope, db, rng, rooftops, customers, today, 160);
        }
    }

    /// <summary>Marks the enquiries this file made, so a resumed run can tell.</summary>
    private const string LeadNote = "Demo enquiry.";

    /// <summary>
    /// Finishes deals an earlier run approved but could not deliver, and leaves
    /// roughly a fifth of them sitting at Approved because a real desk always has
    /// some.
    /// </summary>
    /// <remarks>
    /// This exists because of a real failure rather than a hypothetical one. The
    /// first full run left 53 deals stuck at Approved: delivery posts to the
    /// ledger, tax had just been added to the amount due, and the posting
    /// credited nothing against it — so every delivery was refused for not
    /// balancing. With that fixed, the deals were still stranded, and the
    /// alternative to this method was dropping the database.
    /// </remarks>
    private static async Task DeliverApprovedAsync(TenantScope scope, TenantDb db)
    {
        var deals = scope.Services.GetRequiredService<IDeals>();

        var approved = await db.Deals
            .AsNoTracking()
            .Where(d => d.Status == DealStatus.Approved)
            .OrderBy(d => d.CreatedAt)
            .Select(d => d.Id)
            .ToListAsync();

        var toDeliver = approved.Take(approved.Count * 4 / 5).ToList();

        foreach (var dealId in toDeliver)
        {
            var moved = await deals.ChangeStatusAsync(
                dealId, new DealStatusChangeRequest("Delivered"), CancellationToken.None);

            // A delivery that will not post is a defect worth stopping for, not
            // one to leave in a dealership somebody is about to be shown.
            if (moved.IsFailure)
            {
                throw new InvalidOperationException(
                    $"Demo delivery failed: {moved.Error.Code} — {moved.Error.Message}");
            }
        }
    }

    private static async Task<List<Guid>> ExistingCustomersAsync(TenantDb db) =>
        await db.Customers
            .AsNoTracking()
            .Where(c => c.ExternalReference != null && c.ExternalReference.StartsWith(Marker))
            .Select(c => c.Id)
            .ToListAsync();

    private static async Task<List<Stocked>> ExistingStockAsync(TenantDb db) =>
        await db.InventoryUnits
            .AsNoTracking()
            .Where(u => u.StockNumber.StartsWith('D'))
            .Select(u => new Stocked(u.Id, u.RooftopId, u.Status, u.CostAmount ?? 0m))
            .ToListAsync();

    // --- people ---------------------------------------------------------------

    private static readonly string[] Given =
    [
        "Marisol", "Daniel", "Priya", "Tomas", "Aisha", "Callum", "Ingrid", "Mateo", "Nadia", "Ove",
        "Ruth", "Kwame", "Lena", "Hiroshi", "Fatima", "Owen", "Sofia", "Dmitri", "Amara", "Nils",
        "Cira", "Bram", "Yara", "Otto", "Leila", "Rune", "Talia", "Emeka", "Vera", "Janusz",
    ];

    private static readonly string[] Family =
    [
        "Alvarez", "Okafor", "Raman", "Ferreira", "Bekele", "Doyle", "Halvorsen", "Cruz", "Haddad",
        "Lindqvist", "Mbeki", "Novak", "Oyelaran", "Petrov", "Quintero", "Rasmussen", "Sandoval",
        "Tarrant", "Ustinov", "Vaeth", "Whitlock", "Xu", "Yilmaz", "Zabala", "Ashworth", "Bissett",
    ];

    private static readonly string[] Trading =
    [
        "Brightline Facilities", "Kestrel Logistics", "Ravenwood Catering", "Fenmoor Plant Hire",
        "Ashgrove Couriers", "Tolliver Groundworks", "Marnock Security", "Deepdale Utilities",
    ];

    /// <summary>Real US county names, paired with their state, so tax has something true to read.</summary>
    private static readonly (string City, string State, string County, string Postcode)[] Places =
    [
        ("Springfield", "IL", "Sangamon", "62704"),
        ("Peoria", "IL", "Peoria", "61602"),
        ("Naperville", "IL", "DuPage", "60540"),
        ("Boston", "MA", "Suffolk", "02108"),
        ("Worcester", "MA", "Worcester", "01608"),
        ("Austin", "TX", "Travis", "78701"),
        ("Plano", "TX", "Collin", "75024"),
        ("Fresno", "CA", "Fresno", "93721"),
        ("Pasadena", "CA", "Los Angeles", "91101"),
        ("Boulder", "CO", "Boulder", "80302"),
        ("Tacoma", "WA", "Pierce", "98402"),
        ("Akron", "OH", "Summit", "44308"),
    ];

    private static async Task<List<Guid>> AddCustomersAsync(TenantDb db, Random rng, int count)
    {
        var made = new List<Customer>(count);

        for (var i = 0; i < count; i++)
        {
            // Roughly one in nine is a business, which is about the mix a mixed
            // retail and fleet dealership actually sees.
            var isBusiness = i % 9 == 8;
            var reference = $"{Marker}-C{i + 1:D4}";

            var customer = isBusiness
                ? Customer.Business(Guid.NewGuid(), $"{Pick(rng, Trading)} Ltd")
                : Customer.Person(Guid.NewGuid(), Pick(rng, Given), Pick(rng, Family));

            var place = Places[rng.Next(Places.Length)];

            customer.SetAddress(Address.Create(
                $"{rng.Next(1, 400)} {Pick(rng, ["Kestrel Way", "Maple Row", "Dover Street", "Ashfield Lane",
                    "Union Terrace", "Granby Road", "Pinewood Drive", "Harbour Approach"])}",
                rng.Next(6) == 0 ? $"Unit {rng.Next(1, 40)}" : null,
                place.City,
                place.State,
                place.County,
                place.Postcode,
                "US"));

            customer.AddContactPoint(
                Guid.NewGuid(), ContactKind.Email,
                $"{reference.ToLowerInvariant()}@example.invalid");
            customer.AddContactPoint(
                Guid.NewGuid(), ContactKind.Phone, $"555{rng.Next(1000000, 9999999)}");

            customer.SetExternalReference(reference);
            made.Add(customer);
        }

        db.Customers.AddRange(made);
        await db.SaveChangesAsync();

        return made.Select(c => c.Id).ToList();
    }

    // --- cars -----------------------------------------------------------------

    private static readonly (string Make, string Model, string Trim, string Body)[] Models =
    [
        ("Toyota", "RAV4", "XLE", "SUV"),
        ("Toyota", "Corolla", "LE", "Sedan"),
        ("Honda", "Civic", "EX", "Sedan"),
        ("Honda", "CR-V", "Touring", "SUV"),
        ("Ford", "F-150", "Lariat", "Pickup"),
        ("Ford", "Escape", "SE", "SUV"),
        ("Volkswagen", "Golf", "Style", "Hatchback"),
        ("Volkswagen", "Passat", "Elegance", "Estate"),
        ("Nissan", "Qashqai", "Acenta", "SUV"),
        ("Kia", "Sportage", "GT-Line", "SUV"),
        ("Hyundai", "Tucson", "Premium", "SUV"),
        ("BMW", "3 Series", "M Sport", "Saloon"),
        ("Mercedes-Benz", "C-Class", "AMG Line", "Saloon"),
        ("Skoda", "Octavia", "SE L", "Estate"),
    ];

    private static readonly string[] Colours =
    [
        "Silver", "White", "Black", "Blue", "Grey", "Red", "Green", "Bronze",
    ];

    /// <summary>A car on the lot, and where it is in its life.</summary>
    private sealed record Stocked(Guid UnitId, RooftopId Rooftop, InventoryStatus Status, decimal Cost);

    private static async Task<List<Stocked>> AddStockAsync(
        TenantDb db,
        Random rng,
        List<RooftopId> rooftops,
        DateTimeOffset today,
        int count)
    {
        var vehicles = new List<Vehicle>(count);
        var units = new List<InventoryUnit>(count);
        var stocked = new List<Stocked>(count);

        for (var i = 0; i < count; i++)
        {
            var (make, model, trim, body) = Models[rng.Next(Models.Length)];
            var year = 2017 + rng.Next(9);

            // A real 17-character VIN, which means no I, O or Q — the domain
            // rejects those, and "DEMO" contains one. DFS plus digits keeps it
            // obviously synthetic and still valid, so these cars exercise the
            // ordinary path rather than the documented-exception one.
            var vehicle = Vehicle.Record(
                Guid.NewGuid(),
                $"DFS{i:D6}HZ{rng.Next(100000, 999999):D6}",
                year, make, model, trim,
                bodyStyle: body,
                exteriorColor: Colours[rng.Next(Colours.Length)]);

            vehicles.Add(vehicle);

            var rooftop = rooftops[rng.Next(rooftops.Count)];
            var age = rng.Next(3, DaysOfHistory);
            var received = today.AddDays(-age);
            var cost = 6_000m + rng.Next(0, 44) * 500m;

            var unit = InventoryUnit.Receive(
                Guid.NewGuid(), vehicle.Id, rooftop, $"D{i + 1:D4}", received,
                cost: new Money(cost, "USD"),
                acquiredOn: DateOnly.FromDateTime(received.UtcDateTime));

            // A real lot is mostly available, with a tail in preparation and a
            // few still to arrive. The mix is what makes the stock-age bands and
            // the status filter worth looking at.
            var roll = rng.Next(100);
            var status = roll switch
            {
                < 8 => InventoryStatus.Incoming,
                < 26 => InventoryStatus.Reconditioning,
                _ => InventoryStatus.Available,
            };

            if (status != InventoryStatus.Incoming)
            {
                unit.ChangeStatus(status, received.AddDays(1));
            }

            units.Add(unit);
            stocked.Add(new Stocked(unit.Id, rooftop, status, cost));
        }

        db.Vehicles.AddRange(vehicles);
        db.InventoryUnits.AddRange(units);
        await db.SaveChangesAsync();

        // Purchases are posted from PopulateTenantAsync, which back-fills stock
        // seeded before this posting existed as well as the cars just written.

        return stocked;
    }

    /// <summary>
    /// Puts the cars this seeder placed onto the balance sheet, and back-fills
    /// any that are already there without one.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Written straight into the ledger rather than through
    /// <c>IInventory.ReceiveAsync</c>, which is the exception to this file's rule
    /// of going through the services. The service receives and posts at
    /// <c>_clock.UtcNow</c>, and this seeder deliberately spreads stock across
    /// ninety-five days so the ageing bands and the month-on-month figures are
    /// worth looking at. Routing through the service would flatten every car onto
    /// today and destroy the one thing this data exists to show.
    /// </para>
    /// <para>
    /// <b>It back-fills on purpose.</b> This runs on every populate, not only
    /// when stock is created, because the dealerships seeded before 2026-09-10
    /// have two hundred cars and no purchase entries at all — dropping the
    /// database was the alternative, and a demo that has to be rebuilt to show a
    /// fix is a demo nobody looks at twice. Units that already have an entry are
    /// skipped, so running it again is free.
    /// </para>
    /// <para>
    /// Without this the seeded dealership showed vehicle inventory at minus
    /// $993,190 — an asset account nearly a million dollars negative, because
    /// every delivery relieved 1300 and nothing had ever put a car there. That is
    /// what the dealer-day walk found.
    /// </para>
    /// </remarks>
    private static async Task PostStockPurchasesAsync(TenantDb db, DateTimeOffset today)
    {
        var accounts = await db.Accounts
            .Where(a => a.Code == AccountCodes.VehicleInventory || a.Code == AccountCodes.Cash)
            .ToDictionaryAsync(a => a.Code);

        if (accounts.Count < 2)
        {
            return;
        }

        // A stock number is unique per rooftop and not per organization, so what
        // identifies an entry is the pair. Comparing on the number alone would
        // decide that the second lot's A1001 had already been bought.
        var posted = (await db.JournalEntries
            .AsNoTracking()
            .Where(e => e.Source == JournalSource.StockPurchase)
            .Select(e => new { e.RooftopId, e.Reference })
            .ToListAsync())
            .Select(e => (e.RooftopId, e.Reference))
            .ToHashSet();

        var units = await db.InventoryUnits.AsNoTracking().ToListAsync();
        var entities = await db.Rooftops.ToDictionaryAsync(r => r.Id, r => r.LegalEntityId);

        var inventory = accounts[AccountCodes.VehicleInventory];
        var cash = accounts[AccountCodes.Cash];
        var entries = new List<JournalEntry>();

        foreach (var unit in units)
        {
            if (unit.CostAmount is not { } amount || amount <= 0m)
            {
                continue;
            }

            if (posted.Contains((unit.RooftopId, unit.StockNumber)))
            {
                continue;
            }

            if (!entities.TryGetValue(unit.RooftopId, out var legalEntityId))
            {
                continue;
            }

            entries.Add(JournalEntry.Post(
                Guid.NewGuid(),
                legalEntityId,
                unit.RooftopId,
                unit.AcquiredOn ?? DateOnly.FromDateTime(today.UtcDateTime),
                JournalSource.StockPurchase,
                unit.StockNumber,
                $"Stock {unit.StockNumber}",
                unit.CostCurrency ?? "USD",
                [
                    (inventory.Code, inventory.Id, amount, 0m, "Car onto the lot"),
                    (cash.Code, cash.Id, 0m, amount, "Paid for the car"),
                ],
                today,
                DevelopmentSeeder.DevUsers.OrganizationWide));
        }

        if (entries.Count == 0)
        {
            return;
        }

        db.JournalEntries.AddRange(entries);
        await db.SaveChangesAsync();
    }

    // --- enquiries ------------------------------------------------------------

    private static async Task AddLeadsAsync(
        TenantDb db,
        Random rng,
        List<RooftopId> rooftops,
        List<Guid> customers,
        DateTimeOffset today,
        int count)
    {
        var sources = Enum.GetValues<LeadSource>();
        var leads = new List<Lead>(count);

        for (var i = 0; i < count; i++)
        {
            var raised = today.AddDays(-rng.Next(1, DaysOfHistory));

            var lead = Lead.Capture(
                Guid.NewGuid(),
                rooftops[rng.Next(rooftops.Count)],
                customers[rng.Next(customers.Count)],
                sources[rng.Next(sources.Length)],
                raised,
                enquiry: LeadNote);

            // A funnel, not a flat distribution: most enquiries are still new or
            // being worked, a few reach an appointment, fewer still convert.
            var roll = rng.Next(100);

            if (roll >= 40)
            {
                lead.ChangeStatus(LeadStatus.Working, raised.AddDays(1));
            }

            if (roll >= 70)
            {
                lead.ChangeStatus(LeadStatus.Appointment, raised.AddDays(3));
            }

            if (roll >= 88)
            {
                lead.ChangeStatus(LeadStatus.Won, raised.AddDays(6));
            }
            else if (roll is >= 78 and < 88)
            {
                lead.ChangeStatus(LeadStatus.Lost, raised.AddDays(5), note: "Bought elsewhere.");
            }

            leads.Add(lead);
        }

        db.Leads.AddRange(leads);
        await db.SaveChangesAsync();
    }

    // --- deals ----------------------------------------------------------------

    private static async Task AddDealsAsync(
        TenantScope scope,
        TenantDb db,
        Random rng,
        List<RooftopId> rooftops,
        List<Guid> customers,
        List<Stocked> stock,
        DateTimeOffset today,
        int count)
    {
        var deals = scope.Services.GetRequiredService<IDeals>();

        // Only cars that are actually on the lot can be sold, and each one only
        // once — the inventory hold enforces it, so taking a car twice here would
        // simply fail half the time.
        var sellable = stock
            .Where(s => s.Status == InventoryStatus.Available)
            .OrderBy(_ => rng.Next())
            .Take(count)
            .ToList();

        foreach (var (unit, index) in sellable.Select((u, i) => (u, i)))
        {
            var started = await deals.StartAsync(
                new NewDeal(
                    unit.Rooftop,
                    customers[rng.Next(customers.Count)],
                    unit.UnitId,
                    "USD",
                    DevelopmentSeeder.DevUsers.Salesperson,
                    null),
                CancellationToken.None);

            if (started.IsFailure)
            {
                continue;
            }

            var dealId = started.Value.Id;
            // To the nearest ten. Math.Round with negative digits is not a thing in
            // .NET, which is how the first run of this died.
            var price = Math.Round(unit.Cost * (1.14m + rng.Next(0, 9) / 100m) / 10m) * 10m;
            var hasTrade = rng.Next(100) < 45;

            var charges = new List<NewCharge>
            {
                new("VehiclePrice", "Vehicle", price),
                new("DocumentationFee", "Documentation", 499m),
                new("Fee", "Registration and title", 145m),
            };

            if (rng.Next(100) < 30)
            {
                charges.Add(new NewCharge("Accessory", "Protection pack", 250m + rng.Next(0, 6) * 50m));
            }

            if (rng.Next(100) < 35)
            {
                charges.Add(new NewCharge("Discount", "Negotiated", -(200m + rng.Next(0, 12) * 50m)));
            }

            var priced = await deals.SetTermsAsync(
                dealId,
                new DealTerms(
                    charges,
                    hasTrade
                        ? new NewTradeIn(
                            $"{2012 + rng.Next(10)} {Models[rng.Next(Models.Length)].Model}",
                            2_000m + rng.Next(0, 24) * 500m,
                            rng.Next(100) < 40 ? 1_000m + rng.Next(0, 16) * 500m : 0m)
                        : null),
                CancellationToken.None);

            if (priced.IsFailure)
            {
                continue;
            }

            await AddTaxAsync(deals, db, dealId, priced.Value, CancellationToken.None);

            // A pipeline with a shape: most deals move on, some are still being
            // worked, a few are lost. Only delivered ones post to the ledger.
            var roll = rng.Next(100);
            var when = today.AddDays(-rng.Next(1, DaysOfHistory));

            if (roll < 18)
            {
                continue; // stays Draft
            }

            await Move(deals, dealId, "Submitted", when);

            if (roll < 32)
            {
                continue;
            }

            if (roll < 40)
            {
                await Move(deals, dealId, "Cancelled", when.AddDays(1), "Finance declined.");
                continue;
            }

            await Move(deals, dealId, "Approved", when.AddDays(1));

            if (roll < 52)
            {
                continue;
            }

            await Move(deals, dealId, "Delivered", when.AddDays(2));
            _ = index;
        }
    }

    /// <summary>
    /// Tax on the deal, entered the way a dealership without a rate pack would
    /// enter it — by a person, and recorded as such (ADR-024 R5). The rate is a
    /// plausible combined state and county figure for the customer's address,
    /// not a computed one, and the record says so.
    /// </summary>
    private static async Task AddTaxAsync(
        IDeals deals,
        TenantDb db,
        Guid dealId,
        DealDetail detail,
        CancellationToken cancellationToken)
    {
        var address = await db.Customers
            .AsNoTracking()
            .Where(c => c.Id == detail.CustomerId)
            .Select(c => c.Address)
            .SingleOrDefaultAsync(cancellationToken);

        if (address is null)
        {
            return;
        }

        // Taxed on the car and its extras, less the trade — the common US rule.
        // The doc fee is in; the registration fee is not.
        var basis = detail.Charges
            .Where(c => c.Kind is "VehiclePrice" or "Accessory" or "Discount" or "DocumentationFee")
            .Sum(c => c.Amount) - (detail.TradeIn?.Allowance ?? 0m);

        basis = Math.Max(0m, basis);

        var rate = address.AdministrativeArea switch
        {
            "IL" => 0.0725m,
            "MA" => 0.0625m,
            "TX" => 0.0825m,
            "CA" => 0.0775m,
            "CO" => 0.0485m,
            "WA" => 0.0930m,
            _ => 0.0700m,
        };

        await deals.SetTaxAsync(
            dealId,
            new DealTaxEntry(
                [
                    new NewTaxLine(
                        "Sales tax",
                        $"US-{address.AdministrativeArea}",
                        basis,
                        rate,
                        Math.Round(basis * rate, 2),
                        nameof(TaxProvenance.EnteredByPerson)),
                ],
                new TaxAddressView(
                    address.AdministrativeArea, address.County, address.PostalCode, address.Country)),
            cancellationToken);
    }

    private static async Task Move(IDeals deals, Guid dealId, string status, DateTimeOffset _, string? note = null) =>
        await deals.ChangeStatusAsync(
            dealId, new DealStatusChangeRequest(status, note), CancellationToken.None);

    // --- the workshop ---------------------------------------------------------

    private static readonly (string Concern, string Operation, decimal Hours, decimal Rate)[] Jobs =
    [
        ("Grinding noise when braking", "Front pads and discs", 1.8m, 120m),
        ("Service due", "Interim service", 1.2m, 120m),
        ("Service due", "Full service", 2.5m, 120m),
        ("Warning light on dash", "Diagnostic read and report", 1.0m, 130m),
        ("Air conditioning not cold", "Regas and leak check", 1.5m, 120m),
        ("Pulls to the left", "Four wheel alignment", 1.0m, 110m),
        ("Battery flat overnight", "Parasitic drain test", 2.0m, 130m),
        ("MOT preparation", "Pre-test inspection", 0.8m, 110m),
        ("Clutch slipping", "Clutch replacement", 5.5m, 120m),
        ("Coolant loss", "Pressure test and hose replacement", 2.2m, 120m),
    ];

    private static async Task AddRepairOrdersAsync(
        TenantScope scope,
        TenantDb db,
        Random rng,
        List<RooftopId> rooftops,
        List<Guid> customers,
        DateTimeOffset today,
        int count)
    {
        var orders = scope.Services.GetRequiredService<IRepairOrders>();

        var vehicles = await db.Vehicles
            .AsNoTracking()
            .OrderBy(v => v.Vin)
            .Select(v => v.Id)
            .Take(count)
            .ToListAsync();

        if (vehicles.Count == 0)
        {
            return;
        }

        for (var i = 0; i < count; i++)
        {
            var (concern, operation, hours, rate) = Jobs[rng.Next(Jobs.Length)];
            var opened = today.AddDays(-rng.Next(1, DaysOfHistory));

            var created = await orders.OpenAsync(
                new NewRepairOrder(
                    rooftops[rng.Next(rooftops.Count)],
                    customers[rng.Next(customers.Count)],
                    vehicles[i % vehicles.Count],
                    concern,
                    OdometerReading: rng.Next(1000, 180000)),
                CancellationToken.None);

            if (created.IsFailure)
            {
                continue;
            }

            var orderId = created.Value.Id;

            // Most work is the customer's to pay for; some is warranty, and a
            // little is the dealership preparing its own stock. That mix is the
            // whole reason pay type exists, so the labour report has something
            // to separate.
            var payType = rng.Next(100) switch
            {
                < 72 => "CustomerPay",
                < 90 => "Warranty",
                _ => "Internal",
            };

            var lined = await orders.AddLineAsync(
                orderId,
                new NewServiceLine("Labour", operation, hours, rate, PayType: payType),
                CancellationToken.None);

            if (lined.IsFailure)
            {
                continue;
            }

            var roll = rng.Next(100);

            if (roll < 22)
            {
                continue; // booked in, nobody has agreed to anything yet
            }

            // Work nobody agreed to is not billed — the control the workshop
            // exists to enforce — so every line is answered before the order can
            // move on. That is the same path a service advisor walks.
            foreach (var line in lined.Value.Lines)
            {
                await orders.AnswerLineAsync(
                    orderId, line.Id,
                    new LineAnswerRequest(true, "Customer agreed by phone."),
                    CancellationToken.None);
            }

            await orders.ChangeStatusAsync(
                orderId, new RepairOrderStatusChangeRequest("InProgress"), CancellationToken.None);

            if (roll < 40)
            {
                continue; // on a ramp
            }

            await orders.ChangeStatusAsync(
                orderId, new RepairOrderStatusChangeRequest("Completed"), CancellationToken.None);

            await orders.ChangeStatusAsync(
                orderId, new RepairOrderStatusChangeRequest("Invoiced"), CancellationToken.None);
            _ = opened;
        }
    }

    private static string Pick(Random rng, string[] from) => from[rng.Next(from.Length)];
}
