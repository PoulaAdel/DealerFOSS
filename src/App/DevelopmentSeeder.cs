// DevelopmentSeeder — creates sample dealer organizations so the system can be
// run and tested without hand-building data. Development only.
//
// Use:  runs at startup when Seed:Enabled is true. It is idempotent, so
//       repeated runs neither duplicate nor overwrite.
// Edit: the DevUsers ids are fixed on purpose — integration tests and
//       deploy/verify-e2e.ps1 both reference them. Changing one breaks both.
//       Sample data must stay synthetic; never seed real customer data.
//       Identity accounts are created through IdentitySeeder rather than here,
//       because user and role records are internal to the Identity project
//       (ADR-017).

using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using DealerFOSS.Core;
using DealerFOSS.Accounting;
using DealerFOSS.Customers;
using DealerFOSS.Data;
using DealerFOSS.Deals;
using DealerFOSS.Identity;
using DealerFOSS.Inventory;
using DealerFOSS.Leads;
using DealerFOSS.Organization;
using DealerFOSS.RepairOrders;
using DealerFOSS.Tenancy;
using DealerFOSS.Vehicles;
using CustomerAddress = DealerFOSS.Customers.Address;

namespace DealerFOSS.App;

/// <summary>
/// DEVELOPMENT ONLY. Provisions the host catalog and two sample dealer
/// organizations — one multi-rooftop, one single-rooftop (doc 08 §8) — each in
/// its own database, so tenant isolation and rooftop resolution can be exercised
/// end to end. Idempotent: safe to run repeatedly.
/// </summary>
public static class DevelopmentSeeder
{
    public static async Task RunAsync(WebApplication app, string hostConnectionString)
    {
        ArgumentNullException.ThrowIfNull(app);

        using var scope = app.Services.CreateScope();
        var services = scope.ServiceProvider;
        var hostCatalog = services.GetRequiredService<HostDb>();
        var protector = services.GetRequiredService<ISecretProtector>();
        var clock = services.GetRequiredService<IClock>();

        await hostCatalog.Database.MigrateAsync();

        // The control plane lives in the same host catalog database, under its
        // own schema. Seeded first so the deployment always has an operator, and
        // never with a second factor already set up — an administrator proves
        // they hold one before they can do anything, exactly as in production.
        await ControlPlaneSeeder.SeedDevelopmentAsync(
            hostConnectionString,
            clock,
            DevAdministrator.Id,
            DevAdministrator.Email,
            "Development Administrator",
            DevUsers.Password);

        await ControlPlaneSeeder.SeedDevelopmentAsync(
            hostConnectionString,
            clock,
            DevAdministrator.UnenrolledId,
            DevAdministrator.UnenrolledEmail,
            "Newly Created Administrator",
            DevUsers.Password);

        await SeedTenantAsync(hostCatalog, protector, clock, hostConnectionString, "northgroup", BuildNorthGroup);
        await SeedTenantAsync(hostCatalog, protector, clock, hostConnectionString, "citymotors", BuildCityMotors);

        await hostCatalog.SaveChangesAsync();
    }

    /// <summary>
    /// Names a tenant database from the host catalog's own name, so an
    /// installation called something other than <c>DealerFOSS_Host</c> keeps
    /// its databases together instead of scattering them under a fixed prefix.
    /// </summary>
    /// <remarks>
    /// The integration suite relies on this: it points the host catalog at a
    /// name unique to the run, and gets tenant databases unique to the run for
    /// free — which is what stops one run's data leaking into the next one's
    /// assertions.
    /// </remarks>
    public static string TenantDatabaseName(string hostConnectionString, string slug)
    {
        const string hostSuffix = "_Host";

        var catalog = new SqlConnectionStringBuilder(hostConnectionString).InitialCatalog;
        var prefix = catalog.EndsWith(hostSuffix, StringComparison.OrdinalIgnoreCase)
            ? catalog[..^hostSuffix.Length]
            : catalog;

        return $"{prefix}_Tenant_{slug}";
    }

    /// <summary>
    /// The well-known development administrator. Deliberately not one of the
    /// <see cref="DevUsers"/> — it is a different kind of record, in a different
    /// database, and belongs to nobody's dealership.
    /// </summary>
    public static class DevAdministrator
    {
        public static Guid Id { get; } = new("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

        /// <summary>
        /// Not a <c>@dev.local</c> address, so it never reads as one of the
        /// dealership accounts in a log or a test.
        /// </summary>
        public const string Email = "root@control.local";

        /// <summary>
        /// Reserved for the tests that observe what an administrator may do
        /// *before* enrolling a second factor. Kept separate because enrolling is
        /// one-way: sharing one account would make those tests depend on running
        /// first, which is exactly the order-dependence this suite has already
        /// been bitten by once.
        /// </summary>
        public static Guid UnenrolledId { get; } = new("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

        public const string UnenrolledEmail = "newop@control.local";
    }

    /// <summary>
    /// Well-known development users. Fixed ids so tests and manual checks can
    /// act as a specific scope without first querying for one.
    /// </summary>
    public static class DevUsers
    {
        /// <summary>Holds every permission across every rooftop.</summary>
        public static Guid OrganizationWide { get; } = new("11111111-1111-1111-1111-111111111111");

        /// <summary>Read-only, and only at the tenant's first rooftop.</summary>
        public static Guid FirstRooftopOnly { get; } = new("22222222-2222-2222-2222-222222222222");

        /// <summary>Exists and is active, but holds no assignment at all.</summary>
        public static Guid Unassigned { get; } = new("33333333-3333-3333-3333-333333333333");

        /// <summary>
        /// Does the whole sales job at the first rooftop — except approve a deal.
        /// That one gap is what makes segregation of duties testable.
        /// </summary>
        public static Guid Salesperson { get; } = new("44444444-4444-4444-4444-444444444444");

        /// <summary>
        /// Writes up workshop findings at the first rooftop but cannot record that
        /// the customer agreed to pay for them. The workshop's equivalent of the
        /// salesperson's missing approval, and what makes that split testable.
        /// </summary>
        public static Guid Technician { get; } = new("66666666-6666-6666-6666-666666666666");

        /// <summary>Shared password for every development account.</summary>
        public const string Password = "Dev@Pass1!";

        public const string OrganizationWideEmail = "gm@dev.local";
        public const string FirstRooftopOnlyEmail = "advisor@dev.local";
        /// <summary>
        /// Reserved for second-factor tests, which switch MFA on and off. Kept
        /// separate so enrolling it cannot stop every other test signing in.
        /// </summary>
        public static Guid SecondFactor { get; } = new("55555555-5555-5555-5555-555555555555");

        public const string UnassignedEmail = "nobody@dev.local";
        public const string SalespersonEmail = "sales@dev.local";
        public const string SecondFactorEmail = "mfa@dev.local";
        public const string TechnicianEmail = "tech@dev.local";
    }

    private static async Task SeedTenantAsync(
        HostDb hostCatalog,
        ISecretProtector protector,
        IClock clock,
        string hostConnectionString,
        string slug,
        Func<DealerOrganization> build)
    {
        var tenantConnection = new SqlConnectionStringBuilder(hostConnectionString)
        {
            InitialCatalog = TenantDatabaseName(hostConnectionString, slug),
        }.ConnectionString;

        var options = new DbContextOptionsBuilder<TenantDb>()
            .UseSqlServer(tenantConnection)
            .Options;

        await using var tenantDb = new TenantDb(options, clock);
        await tenantDb.Database.MigrateAsync();

        if (!await tenantDb.Organizations.AnyAsync())
        {
            tenantDb.Organizations.Add(build());
            await tenantDb.SaveChangesAsync();
        }

        var organizationId = await tenantDb.Organizations.Select(o => o.Id).FirstAsync();
        var name = await tenantDb.Organizations.Select(o => o.Name).FirstAsync();

        var firstRooftop = await tenantDb.Rooftops
            .OrderBy(r => r.Code)
            .Select(r => r.Id)
            .FirstAsync();

        await IdentitySeeder.SeedDevelopmentAsync(
            tenantConnection,
            clock,
            firstRooftop,
            DevUsers.Password,
            new DevelopmentAccount(DevUsers.OrganizationWide, DevUsers.OrganizationWideEmail, "Organization Manager"),
            new DevelopmentAccount(DevUsers.FirstRooftopOnly, DevUsers.FirstRooftopOnlyEmail, "Single Rooftop Advisor"),
            new DevelopmentAccount(DevUsers.Unassigned, DevUsers.UnassignedEmail, "Unassigned User"),
            new DevelopmentAccount(DevUsers.Salesperson, DevUsers.SalespersonEmail, "Rooftop Salesperson"),
            new DevelopmentAccount(DevUsers.SecondFactor, DevUsers.SecondFactorEmail, "Second Factor Test"),
            new DevelopmentAccount(DevUsers.Technician, DevUsers.TechnicianEmail, "Workshop Technician"));

        await SeedCustomersAsync(tenantDb);
        await SeedStockAsync(tenantDb, clock);
        await SeedChartOfAccountsAsync(tenantDb);
        await OpenTheBooksAsync(tenantDb, clock);
        await SeedLeadsAsync(tenantDb, clock);
        await SeedDealsAsync(tenantDb, clock);
        await SeedRepairOrdersAsync(tenantDb, clock);
        await SeedAppointmentsAsync(tenantDb, clock);

        var record = await hostCatalog.Tenants.SingleOrDefaultAsync(t => t.Slug == slug);
        if (record is null)
        {
            hostCatalog.Tenants.Add(new TenantRecord
            {
                Id = organizationId.Value,
                Name = name,
                Slug = slug,
                Status = TenantStatus.Active,
                ProtectedConnectionString = protector.Protect(tenantConnection),
                DatabaseVersion = 1,
                CreatedAt = clock.UtcNow,
            });
        }
    }

    /// <summary>
    /// A handful of invented customers, so search returns something on a fresh
    /// install. Names are obviously fictional; never seed real people.
    /// </summary>
    private static async Task SeedCustomersAsync(TenantDb db)
    {
        if (await db.Customers.AnyAsync())
        {
            return;
        }

        var alvarez = Customer.Person(Guid.NewGuid(), "Marisol", "Alvarez");
        alvarez.AddContactPoint(Guid.NewGuid(), ContactKind.Email, "marisol.alvarez@example.test");
        alvarez.AddContactPoint(Guid.NewGuid(), ContactKind.Phone, "(555) 010-2030");
        alvarez.SetAddress(CustomerAddress.Create(
            "18 Kestrel Way", null, "Springfield", "IL", "62704", "US"));

        var okafor = Customer.Person(Guid.NewGuid(), "Daniel", "Okafor");
        okafor.AddContactPoint(Guid.NewGuid(), ContactKind.Mobile, "+15550117788");

        var fleet = Customer.Business(Guid.NewGuid(), "Brightline Facilities Ltd");
        fleet.AddContactPoint(Guid.NewGuid(), ContactKind.Email, "fleet@brightline.example.test");

        db.Customers.AddRange(alvarez, okafor, fleet);
        await db.SaveChangesAsync();
    }

    /// <summary>
    /// A few invented vehicles and some stock on each lot. The units are spread
    /// across rooftops on purpose: a group with stock at only one location would
    /// not show that one location cannot see another's.
    /// </summary>
    private static async Task SeedStockAsync(TenantDb db, IClock clock)
    {
        if (await db.Vehicles.AnyAsync())
        {
            return;
        }

        var rav4 = Vehicle.Record(Guid.NewGuid(), "JT2BF22K1W0123456", 2021, "Toyota", "RAV4", "XLE",
            bodyStyle: "SUV", exteriorColor: "Silver");
        var civic = Vehicle.Record(Guid.NewGuid(), "1HGCM82633A004352", 2019, "Honda", "Civic", "EX",
            bodyStyle: "Sedan", exteriorColor: "Blue");
        var f150 = Vehicle.Record(Guid.NewGuid(), "WBA3A5C55DF123456", 2022, "Ford", "F-150", "Lariat",
            bodyStyle: "Pickup", exteriorColor: "White");

        // Deliberately not a standard VIN: the documented-exception path has to be
        // exercised by real data, not only by a test (doc 04 §4).
        var trailer = Vehicle.Record(Guid.NewGuid(), "TRAILER-1975-A", 1975, "Wells Cargo", "Utility Trailer",
            vinExceptionReason: "Pre-1981 trailer; number read from the frame plate.");

        db.Vehicles.AddRange(rav4, civic, f150, trailer);

        var rooftops = await db.Rooftops
            .OrderBy(r => r.Code)
            .Select(r => r.Id)
            .ToListAsync();

        var now = clock.UtcNow;
        var acquired = DateOnly.FromDateTime(now.UtcDateTime).AddDays(-30);

        var first = rooftops[0];
        var second = rooftops.Count > 1 ? rooftops[1] : rooftops[0];

        var onLot = InventoryUnit.Receive(Guid.NewGuid(), rav4.Id, first, "A1001", now,
            cost: new Money(24500m, "USD"), acquiredOn: acquired, note: "Auction purchase.");
        onLot.ChangeStatus(InventoryStatus.Available, now, note: "Passed inspection.");

        var inShop = InventoryUnit.Receive(Guid.NewGuid(), civic.Id, first, "A1002", now,
            cost: new Money(15750m, "USD"), acquiredOn: acquired, note: "Trade-in.");
        inShop.ChangeStatus(InventoryStatus.Reconditioning, now, note: "Awaiting tyres.");

        // At the second rooftop, so a rooftop-scoped user must not see it.
        var otherLot = InventoryUnit.Receive(Guid.NewGuid(), f150.Id, second, "B2001", now,
            cost: new Money(38900m, "USD"), acquiredOn: acquired);
        otherLot.ChangeStatus(InventoryStatus.Available, now);

        db.InventoryUnits.AddRange(onLot, inShop, otherLot);
        await db.SaveChangesAsync();
    }

    /// <summary>
    /// A few enquiries in different states, spread across rooftops so the scope
    /// rules have something to actually hide. One is deliberately lost and
    /// reopened, because that is the path most likely to be broken by a careless
    /// change to the status rules.
    /// </summary>
    private static async Task SeedLeadsAsync(TenantDb db, IClock clock)
    {
        if (await db.Leads.AnyAsync())
        {
            return;
        }

        var rooftops = await db.Rooftops.OrderBy(r => r.Code).Select(r => r.Id).ToListAsync();
        var first = rooftops[0];
        var second = rooftops.Count > 1 ? rooftops[1] : rooftops[0];

        var customers = await db.Customers.OrderBy(c => c.LastName).Select(c => c.Id).ToListAsync();
        if (customers.Count == 0)
        {
            return;
        }

        var vehicle = await db.Vehicles.OrderBy(v => v.Vin).Select(v => (Guid?)v.Id).FirstOrDefaultAsync();
        var now = clock.UtcNow;

        var walkIn = Lead.Capture(
            Guid.NewGuid(), first, customers[0], LeadSource.WalkIn, now.AddDays(-6),
            vehicleOfInterestId: vehicle, enquiry: "Wants something around 25k, part-exchanging a hatchback.");
        walkIn.ChangeStatus(LeadStatus.Working, now.AddDays(-5), note: "Called back, sending options.");
        walkIn.ChangeStatus(LeadStatus.Appointment, now.AddDays(-2), note: "Coming in Saturday at 10.");

        var website = Lead.Capture(
            Guid.NewGuid(), first, customers[Math.Min(1, customers.Count - 1)], LeadSource.Website, now.AddDays(-3),
            enquiry: "Enquiry from the website contact form.");

        // Lost in the spring, back in the summer — the path that proves a closed
        // lead can legitimately reopen instead of becoming a duplicate record.
        var revived = Lead.Capture(
            Guid.NewGuid(), second, customers[^1], LeadSource.Phone, now.AddDays(-40),
            enquiry: "Asked about finance on a pickup.");
        revived.ChangeStatus(LeadStatus.Working, now.AddDays(-39));
        revived.ChangeStatus(LeadStatus.Lost, now.AddDays(-30), note: "Went quiet.");
        revived.ChangeStatus(LeadStatus.Working, now.AddDays(-1), note: "Rang back, still looking.");

        db.Leads.AddRange(walkIn, website, revived);
        await db.SaveChangesAsync();
    }

    /// <summary>
    /// Two jobs in the workshop: one just booked in, and one under way with a
    /// piece of work the technician found and nobody has put to the customer yet.
    /// The second is the interesting one — it is a job that CANNOT be invoiced,
    /// which is the control the capability exists to hold, visible without anybody
    /// having to construct it.
    /// </summary>
    private static async Task SeedRepairOrdersAsync(TenantDb db, IClock clock)
    {
        if (await db.RepairOrders.AnyAsync())
        {
            return;
        }

        var rooftop = await db.Rooftops.OrderBy(r => r.Code).Select(r => r.Id).FirstOrDefaultAsync();
        var customers = await db.Customers.OrderBy(c => c.LastName).Select(c => c.Id).Take(2).ToListAsync();
        var vehicles = await db.Vehicles.OrderBy(v => v.Vin).Select(v => v.Id).Take(2).ToListAsync();

        if (rooftop == default || customers.Count == 0 || vehicles.Count == 0)
        {
            return;
        }

        var now = clock.UtcNow;

        var booked = RepairOrder.Open(
            Guid.NewGuid(), rooftop, customers[0], vehicles[0], "RO-1001",
            "Squealing from the front when braking.", "USD", now.AddDays(-1),
            odometerReading: 48_210);

        var underWay = RepairOrder.Open(
            Guid.NewGuid(), rooftop, customers[^1], vehicles[^1], "RO-1002",
            "Service due, and a warning light on the dash.", "USD", now.AddDays(-2),
            odometerReading: 71_455);

        // Booked in for a service, so this is authorized on arrival.
        underWay.AddLine(ServiceLineKind.Labour, "Full service", 1.5m, 120m, 0m, now.AddDays(-2), null);
        underWay.AddLine(ServiceLineKind.Part, "Oil and filter kit", null, null, 68.40m, now.AddDays(-2), null);

        underWay.ChangeStatus(RepairOrderStatus.InProgress, now.AddDays(-1), note: "On the ramp.");

        // Found once the wheels were off. Nobody has rung the customer, so this
        // line is Pending and the job cannot be invoiced until somebody does.
        underWay.AddLine(
            ServiceLineKind.Part, "Front discs and pads — worn beyond limit",
            null, null, 284.00m, now.AddHours(-4), null);

        db.RepairOrders.AddRange(booked, underWay);
        await db.SaveChangesAsync();
    }

    /// <summary>
    /// A few cars expected but not yet here, so the diary has a shape on a fresh
    /// clone. Dated forward from now rather than fixed, because a booking in the
    /// past is refused — and a seed that only worked in August would be a puzzle
    /// for whoever cloned this in September.
    /// </summary>
    private static async Task SeedAppointmentsAsync(TenantDb db, IClock clock)
    {
        if (await db.Appointments.AnyAsync())
        {
            return;
        }

        var rooftop = await db.Rooftops.OrderBy(r => r.Code).Select(r => r.Id).FirstOrDefaultAsync();
        var customers = await db.Customers.OrderBy(c => c.LastName).Select(c => c.Id).Take(2).ToListAsync();
        var vehicles = await db.Vehicles.OrderBy(v => v.Vin).Select(v => v.Id).Take(2).ToListAsync();

        if (rooftop == default || customers.Count == 0 || vehicles.Count == 0)
        {
            return;
        }

        var now = clock.UtcNow;
        var tomorrow = now.Date.AddDays(1);

        // Two on one morning, so the day carries 4.5 hours and the load figure
        // has something to say.
        var first = Appointment.Book(
            Guid.NewGuid(), rooftop, customers[0], vehicles[0],
            new DateTimeOffset(tomorrow.AddHours(9), TimeSpan.Zero),
            "Annual service.", now, estimatedHours: 2m);

        var second = Appointment.Book(
            Guid.NewGuid(), rooftop, customers[^1], vehicles[^1],
            new DateTimeOffset(tomorrow.AddHours(11), TimeSpan.Zero),
            "Judders under braking above 50.", now, estimatedHours: 2.5m);

        // Nobody estimated this one, which is honest and different from
        // estimating zero — the screen shows it as unestimated.
        var later = Appointment.Book(
            Guid.NewGuid(), rooftop, customers[0], vehicles[^1],
            new DateTimeOffset(tomorrow.AddDays(3).AddHours(8).AddMinutes(30), TimeSpan.Zero),
            "Air conditioning not cold.", now);

        db.Appointments.AddRange(first, second, later);
        await db.SaveChangesAsync();
    }

    /// <summary>
    /// One deal waiting for a manager, on a car that is consequently held rather
    /// than available. Seeded directly rather than through DealService, because
    /// the seeder has no signed-in user to authorize — the reservation is done by
    /// hand here to match what the service would have produced.
    /// </summary>
    private static async Task SeedDealsAsync(TenantDb db, IClock clock)
    {
        if (await db.Deals.AnyAsync())
        {
            return;
        }

        var unit = await db.InventoryUnits
            .Where(u => u.Status == InventoryStatus.Available)
            .OrderBy(u => u.StockNumber)
            .FirstOrDefaultAsync();

        var customerId = await db.Customers.OrderBy(c => c.LastName).Select(c => c.Id).FirstOrDefaultAsync();

        if (unit is null || customerId == Guid.Empty)
        {
            return;
        }

        var now = clock.UtcNow;

        var deal = Deal.Start(
            Guid.NewGuid(), unit.RooftopId, customerId, unit.Id, "USD", now.AddDays(-1),
            salespersonUserId: DevUsers.Salesperson);

        deal.SetTerms(
            [
                (ChargeKind.VehiclePrice, "2021 Toyota RAV4 XLE", 26995m),
                (ChargeKind.Fee, "Documentation fee", 399m),
                (ChargeKind.Discount, "Manager discount", -500m),
            ],
            TradeIn.Create("2014 Honda Civic, 96,000 miles", 4500m, 1200m));

        deal.ChangeStatus(DealStatus.Submitted, now, DevUsers.Salesperson, "Ready for review.");

        // The car is spoken for while the deal is live.
        unit.ChangeStatus(InventoryStatus.OnHold, now, DevUsers.Salesperson, "Held for a deal.");

        db.Deals.Add(deal);
        await db.SaveChangesAsync();
    }

    /// <summary>
    /// The minimum chart of accounts a retail sale needs. Reconciled every run
    /// rather than created once, so a database seeded before accounting existed
    /// picks it up instead of failing to post.
    /// </summary>
    /// <summary>
    /// Opens the books for this month, and the two before it.
    ///
    /// Nothing can post into a month that has not been opened — the maintainer
    /// chose an explicit start over one inferred from the first thing anybody
    /// typed. That makes opening the books part of setting a dealership up, not
    /// something a developer discovers when the first sale is refused. Three
    /// months because the seeded deals and jobs are backdated.
    ///
    /// Reconciled every run rather than created once, for the same reason as the
    /// chart of accounts above.
    /// </summary>
    private static async Task OpenTheBooksAsync(TenantDb db, IClock clock)
    {
        var today = DateOnly.FromDateTime(clock.UtcNow.UtcDateTime);

        for (var back = 2; back >= 0; back--)
        {
            var month = today.AddMonths(-back);

            var exists = await db.AccountingPeriods
                .AnyAsync(p => p.Year == month.Year && p.Month == month.Month);

            if (exists)
            {
                continue;
            }

            db.AccountingPeriods.Add(AccountingPeriod.Open(
                Guid.NewGuid(), month.Year, month.Month, clock.UtcNow, null,
                "Opened when the dealership was set up."));
        }

        await db.SaveChangesAsync();
    }

    private static async Task SeedChartOfAccountsAsync(TenantDb db)
    {
        (string Code, string Name, AccountKind Kind)[] chart =
        [
            (AccountCodes.Cash, "Cash", AccountKind.Asset),
            (AccountCodes.VehicleInventory, "Vehicle inventory", AccountKind.Asset),
            (AccountCodes.TradeInventory, "Trade-in inventory", AccountKind.Asset),
            (AccountCodes.VehicleSalesRevenue, "Vehicle sales", AccountKind.Revenue),
            (AccountCodes.FeeRevenue, "Fee income", AccountKind.Revenue),
            (AccountCodes.LabourRevenue, "Labour sales", AccountKind.Revenue),
            (AccountCodes.PartsRevenue, "Parts sales", AccountKind.Revenue),
            (AccountCodes.SubletRevenue, "Sublet sales", AccountKind.Revenue),
            (AccountCodes.SalesDiscounts, "Sales discounts", AccountKind.Revenue),
            (AccountCodes.CostOfVehicleSales, "Cost of vehicle sales", AccountKind.Expense),
            (AccountCodes.PartsInventory, "Parts inventory", AccountKind.Asset),
            (AccountCodes.CostOfPartsSales, "Cost of parts sales", AccountKind.Expense),
            (AccountCodes.FinanceProductRevenue, "Finance product sales", AccountKind.Revenue),
            (AccountCodes.CostOfFinanceProducts, "Cost of finance products", AccountKind.Expense),
        ];

        var existing = await db.Accounts.Select(a => a.Code).ToListAsync();
        var missing = chart.Where(a => !existing.Contains(a.Code)).ToList();

        if (missing.Count == 0)
        {
            return;
        }

        db.Accounts.AddRange(missing.Select(a => new Account(Guid.NewGuid(), a.Code, a.Name, a.Kind)));
        await db.SaveChangesAsync();
    }

    private static DealerOrganization BuildNorthGroup()
    {
        var org = new DealerOrganization(DealerOrganizationId.New(), "North Auto Group", "northgroup");

        var entity = new LegalEntity(LegalEntityId.New(), org.Id, "North Auto LLC");
        entity.SetRegistration("North Auto Group, LLC", "82-1234567");

        var downtown = new Rooftop(RooftopId.New(), entity.Id, "North Auto Downtown", "NAG-01", "America/New_York");
        AddCoreDepartments(downtown);

        var uptown = new Rooftop(RooftopId.New(), entity.Id, "North Auto Uptown", "NAG-02", "America/New_York");
        AddCoreDepartments(uptown);

        entity.Rooftops.Add(downtown);
        entity.Rooftops.Add(uptown);
        org.LegalEntities.Add(entity);
        return org;
    }

    private static DealerOrganization BuildCityMotors()
    {
        var org = new DealerOrganization(DealerOrganizationId.New(), "City Motors", "citymotors");
        var entity = new LegalEntity(LegalEntityId.New(), org.Id, "City Motors Inc");

        var main = new Rooftop(RooftopId.New(), entity.Id, "City Motors Main", "CM-01", "America/Chicago");
        AddCoreDepartments(main);

        entity.Rooftops.Add(main);
        org.LegalEntities.Add(entity);
        return org;
    }

    private static void AddCoreDepartments(Rooftop rooftop)
    {
        rooftop.Departments.Add(new Department(DepartmentId.New(), rooftop.Id, "Sales", DepartmentKind.Sales));
        rooftop.Departments.Add(new Department(DepartmentId.New(), rooftop.Id, "Finance & Insurance", DepartmentKind.FinanceAndInsurance));
        rooftop.Departments.Add(new Department(DepartmentId.New(), rooftop.Id, "Service", DepartmentKind.Service));
        rooftop.Departments.Add(new Department(DepartmentId.New(), rooftop.Id, "Parts", DepartmentKind.Parts));
    }
}
