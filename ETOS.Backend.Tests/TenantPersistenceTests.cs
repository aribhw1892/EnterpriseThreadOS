using ETOS.Backend.Infrastructure.Persistence;
using ETOS.Backend.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace ETOS.Backend.Tests;

public sealed class TenantPersistenceTests
{
    [Fact]
    public void SampleRecordImplementsTenantScopeConvention()
    {
        var validator = new TenantScopeValidator();

        Assert.True(validator.HasTenantScope<TenantScopedSampleRecord>());
    }

    [Fact]
    public async Task DbContextPersistsTenantScopedRecord()
    {
        var options = new DbContextOptionsBuilder<EnterpriseThreadDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        var tenantId = Guid.NewGuid();

        await using (var dbContext = new EnterpriseThreadDbContext(options))
        {
            dbContext.TenantScopedSampleRecords.Add(new TenantScopedSampleRecord
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Name = "Issue 1 baseline",
                CreatedAt = DateTimeOffset.UtcNow
            });

            await dbContext.SaveChangesAsync();
        }

        await using (var dbContext = new EnterpriseThreadDbContext(options))
        {
            var record = await dbContext.TenantScopedSampleRecords.SingleAsync();

            Assert.Equal(tenantId, record.TenantId);
            Assert.Equal("Issue 1 baseline", record.Name);
        }
    }
}
