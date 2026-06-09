namespace ETOS.Backend.Tenancy;

public interface ITenantScoped
{
    Guid TenantId { get; }
}
