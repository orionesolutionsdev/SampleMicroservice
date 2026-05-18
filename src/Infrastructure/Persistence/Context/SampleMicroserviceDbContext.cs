using Microsoft.EntityFrameworkCore;
using SampleMicroservice.Domain.Common;
using SampleMicroservice.Domain.SampleItems;

namespace SampleMicroservice.Infrastructure.Persistence.Context;

public class SampleMicroserviceDbContext : DbContext
{
    public SampleMicroserviceDbContext(DbContextOptions<SampleMicroserviceDbContext> options)
        : base(options)
    {
    }

    public DbSet<SampleItem> SampleItems => Set<SampleItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<SampleItem>(entity =>
        {
            entity.ToTable("SampleItems", "sample");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(256);
            entity.Property(e => e.CreatedDate).IsRequired();
            entity.HasQueryFilter(e => e.DeletedOn == null);
        });
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        foreach (var entry in ChangeTracker.Entries<AuditableEntity<Guid>>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.CreatedOn = DateTime.UtcNow;
                    break;

                case EntityState.Modified:
                    entry.Entity.LastModifiedOn = DateTime.UtcNow;
                    break;
            }
        }

        return await base.SaveChangesAsync(cancellationToken);
    }
}
