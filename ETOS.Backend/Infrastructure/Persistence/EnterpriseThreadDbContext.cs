using ETOS.Backend.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace ETOS.Backend.Infrastructure.Persistence;

public sealed class EnterpriseThreadDbContext(DbContextOptions<EnterpriseThreadDbContext> options) : DbContext(options)
{
    public DbSet<TenantScopedSampleRecord> TenantScopedSampleRecords => Set<TenantScopedSampleRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TenantScopedSampleRecord>(entity =>
        {
            entity.ToTable("tenant_scoped_sample_records");
            entity.HasKey(record => record.Id);
            entity.Property(record => record.TenantId).IsRequired();
            entity.Property(record => record.Name).HasMaxLength(200).IsRequired();
            entity.Property(record => record.CreatedAt).IsRequired();
            entity.HasIndex(record => record.TenantId);
        });
    }
}
