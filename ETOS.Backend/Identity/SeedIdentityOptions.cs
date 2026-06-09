using System.ComponentModel.DataAnnotations;

namespace ETOS.Backend.Identity;

public sealed class SeedIdentityOptions
{
    public const string SectionName = "SeedIdentity";

    public bool Enabled { get; set; }

    public Guid AdminUserId { get; set; } = Guid.Parse("11111111-1111-1111-1111-111111111111");

    [EmailAddress]
    public string AdminEmail { get; set; } = "admin@etos.com";

    [MinLength(8)]
    public string AdminPassword { get; set; } = "admin-password";

    public Guid TenantId { get; set; } = Guid.Parse("22222222-2222-2222-2222-222222222222");

    [MinLength(1)]
    public string TenantIdentifier { get; set; } = "local";

    [MinLength(1)]
    public string TenantName { get; set; } = "Local Tenant";
}
