namespace ETOS.Backend.Tenancy;

public interface ITenantScopeValidator
{
    bool HasTenantScope<TRecord>()
        where TRecord : ITenantScoped;

    bool HasTenantScope(Type recordType);
}

public sealed class TenantScopeValidator : ITenantScopeValidator
{
    public bool HasTenantScope<TRecord>()
        where TRecord : ITenantScoped
    {
        return HasTenantScope(typeof(TRecord));
    }

    public bool HasTenantScope(Type recordType)
    {
        return typeof(ITenantScoped).IsAssignableFrom(recordType)
            && recordType.GetProperty(nameof(ITenantScoped.TenantId))?.PropertyType == typeof(Guid);
    }
}
