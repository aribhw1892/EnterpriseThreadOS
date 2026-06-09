using ETOS.Backend.Infrastructure.Persistence;
using Finbuckle.MultiTenant.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace ETOS.Backend.Identity;

public sealed class EtosTenantStore(EnterpriseThreadDbContext dbContext) : IMultiTenantStore<EtosTenantInfo>
{
    public Task<bool> AddAsync(EtosTenantInfo tenantInfo)
    {
        return Task.FromResult(false);
    }

    public Task<bool> UpdateAsync(EtosTenantInfo tenantInfo)
    {
        return Task.FromResult(false);
    }

    public Task<bool> RemoveAsync(string identifier)
    {
        return Task.FromResult(false);
    }

    public async Task<EtosTenantInfo?> GetByIdentifierAsync(string identifier)
    {
        var normalizedIdentifier = Normalize(identifier);
        var tenantId = Guid.TryParse(identifier, out var parsedTenantId) ? parsedTenantId : (Guid?)null;

        var tenant = await dbContext.Tenants
            .AsNoTracking()
            .SingleOrDefaultAsync(candidate =>
                candidate.IsActive
                && (candidate.NormalizedIdentifier == normalizedIdentifier || candidate.Id == tenantId));

        return tenant is null ? null : ToTenantInfo(tenant);
    }

    public async Task<EtosTenantInfo?> GetAsync(string id)
    {
        if (!Guid.TryParse(id, out var tenantId))
        {
            return null;
        }

        var tenant = await dbContext.Tenants
            .AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.IsActive && candidate.Id == tenantId);

        return tenant is null ? null : ToTenantInfo(tenant);
    }

    public async Task<IEnumerable<EtosTenantInfo>> GetAllAsync()
    {
        var tenants = await dbContext.Tenants
            .AsNoTracking()
            .Where(tenant => tenant.IsActive)
            .OrderBy(tenant => tenant.Identifier)
            .ToListAsync();

        return tenants.Select(ToTenantInfo);
    }

    public async Task<IEnumerable<EtosTenantInfo>> GetAllAsync(int take, int skip)
    {
        var tenants = await dbContext.Tenants
            .AsNoTracking()
            .Where(tenant => tenant.IsActive)
            .OrderBy(tenant => tenant.Identifier)
            .Skip(skip)
            .Take(take)
            .ToListAsync();

        return tenants.Select(ToTenantInfo);
    }

    private static EtosTenantInfo ToTenantInfo(Tenant tenant)
    {
        return new EtosTenantInfo
        {
            Id = tenant.Id.ToString(),
            Identifier = tenant.Identifier,
            Name = tenant.Name,
            TenantId = tenant.Id
        };
    }

    private static string Normalize(string value) => value.Trim().ToUpperInvariant();
}
