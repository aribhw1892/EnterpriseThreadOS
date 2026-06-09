using ETOS.Backend.Infrastructure.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ETOS.Backend.Infrastructure.Persistence;

public sealed class EnterpriseThreadDbContextFactory : IDesignTimeDbContextFactory<EnterpriseThreadDbContext>
{
    public EnterpriseThreadDbContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var connectionString = configuration
            .GetSection(OperationalStoreOptions.SectionName)
            .Get<OperationalStoreOptions>()?.ConnectionString;

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("OperationalStore:ConnectionString must be configured for EF Core design-time commands.");
        }

        var options = new DbContextOptionsBuilder<EnterpriseThreadDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        return new EnterpriseThreadDbContext(options);
    }
}
