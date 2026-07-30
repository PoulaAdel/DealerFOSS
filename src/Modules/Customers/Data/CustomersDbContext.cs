// CustomersDbContext — persistence for this module. Owns the "customers" schema
// and no other (ADR-014).
//
// Use:  injected into this module's own services; nothing outside Customers may
//       depend on it.
// Edit: there is deliberately no unique index on a contact value. Two customers
//       can share an email or a phone — a couple, a family business — and
//       enforcing uniqueness would block legitimate records and push staff into
//       creating bad data to get around it (doc 04 §4).

using Microsoft.EntityFrameworkCore;
using OpenDealer360.Core;
using OpenDealer360.Customers.Domain;

namespace OpenDealer360.Customers.Data;

public sealed class CustomersDbContext(DbContextOptions<CustomersDbContext> options, IClock clock)
    : DbContext(options)
{
    public const string Schema = "customers";

    private readonly IClock _clock = clock;

    public DbSet<Customer> Customers => Set<Customer>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);

        modelBuilder.Entity<Customer>(builder =>
        {
            builder.ToTable("Customers");
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Id).ValueGeneratedNever();
            builder.Property(x => x.Kind).HasConversion<string>().HasMaxLength(20);
            builder.Property(x => x.FirstName).HasMaxLength(120).IsRequired();
            builder.Property(x => x.LastName).HasMaxLength(200).IsRequired();
            builder.Property(x => x.HomeRooftopId)
                .HasConversion(id => id!.Value.Value, value => new RooftopId(value));

            // Search hits these two constantly; archived customers are filtered
            // out of every list, so it belongs in the index rather than beside it.
            builder.HasIndex(x => new { x.IsArchived, x.LastName });
            builder.HasIndex(x => x.HomeRooftopId);

            builder.OwnsOne(x => x.Address, address =>
            {
                address.Property(a => a.Line1).HasColumnName("AddressLine1").HasMaxLength(200);
                address.Property(a => a.Line2).HasColumnName("AddressLine2").HasMaxLength(200);
                address.Property(a => a.City).HasColumnName("City").HasMaxLength(120);
                address.Property(a => a.AdministrativeArea).HasColumnName("AdministrativeArea").HasMaxLength(120);
                address.Property(a => a.PostalCode).HasColumnName("PostalCode").HasMaxLength(20);
                address.Property(a => a.Country).HasColumnName("Country").HasMaxLength(2);
            });

            builder.HasMany(x => x.ContactPoints)
                .WithOne()
                .HasForeignKey(c => c.CustomerId)
                .OnDelete(DeleteBehavior.Cascade);
            builder.Navigation(x => x.ContactPoints).AutoInclude();

            ConfigureAudit(builder);
        });

        modelBuilder.Entity<ContactPoint>(builder =>
        {
            builder.ToTable("ContactPoints");
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Id).ValueGeneratedNever();
            builder.Property(x => x.Kind).HasConversion<string>().HasMaxLength(20);
            builder.Property(x => x.Value).HasMaxLength(320).IsRequired();
            // Searching by email or phone looks the value up directly — but not
            // uniquely; see the note at the top of this file.
            builder.HasIndex(x => x.Value);
            builder.HasIndex(x => new { x.CustomerId, x.Kind });
        });
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var now = _clock.UtcNow;

        foreach (var entry in ChangeTracker.Entries<AuditableEntity>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.CreatedAt = now;
                    entry.Entity.ConcurrencyStamp = Guid.NewGuid();
                    break;
                case EntityState.Modified:
                    entry.Entity.ModifiedAt = now;
                    entry.Entity.ConcurrencyStamp = Guid.NewGuid();
                    break;
                default:
                    break;
            }
        }

        return base.SaveChangesAsync(cancellationToken);
    }

    private static void ConfigureAudit<TEntity>(
        Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<TEntity> builder)
        where TEntity : AuditableEntity
    {
        builder.Property(x => x.CreatedBy).HasMaxLength(120).IsRequired();
        builder.Property(x => x.ModifiedBy).HasMaxLength(120);
        builder.Property(x => x.ConcurrencyStamp).IsConcurrencyToken();
    }
}
