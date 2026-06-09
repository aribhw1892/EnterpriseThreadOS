using ETOS.Backend.Identity;
using ETOS.Backend.Governance;
using ETOS.Backend.Tenancy;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace ETOS.Backend.Infrastructure.Persistence;

public sealed class EnterpriseThreadDbContext(DbContextOptions<EnterpriseThreadDbContext> options)
    : IdentityDbContext<EtosUser, EtosIdentityRole, Guid>(options)
{
    public DbSet<TenantScopedSampleRecord> TenantScopedSampleRecords => Set<TenantScopedSampleRecord>();

    public DbSet<Tenant> Tenants => Set<Tenant>();

    public DbSet<TenantRole> TenantRoles => Set<TenantRole>();

    public DbSet<TenantMembership> TenantMemberships => Set<TenantMembership>();

    public DbSet<Permission> Permissions => Set<Permission>();

    public DbSet<TenantRolePermission> TenantRolePermissions => Set<TenantRolePermission>();

    public DbSet<AccessGrant> AccessGrants => Set<AccessGrant>();

    public DbSet<AccessRequest> AccessRequests => Set<AccessRequest>();

    public DbSet<AccessDenialRecord> AccessDenialRecords => Set<AccessDenialRecord>();

    public DbSet<AuditRecord> AuditRecords => Set<AuditRecord>();

    public DbSet<SecurityEvent> SecurityEvents => Set<SecurityEvent>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        ConfigureIdentityTables(modelBuilder);

        modelBuilder.Entity<TenantScopedSampleRecord>(entity =>
        {
            entity.ToTable("tenant_scoped_sample_records");
            entity.HasKey(record => record.Id);
            entity.Property(record => record.TenantId).IsRequired();
            entity.Property(record => record.Name).HasMaxLength(200).IsRequired();
            entity.Property(record => record.CreatedAt).IsRequired();
            entity.HasIndex(record => record.TenantId);
        });

        modelBuilder.Entity<Tenant>(entity =>
        {
            entity.ToTable("tenants");
            entity.HasKey(tenant => tenant.Id);
            entity.Property(tenant => tenant.Identifier).HasMaxLength(128).IsRequired();
            entity.Property(tenant => tenant.NormalizedIdentifier).HasMaxLength(128).IsRequired();
            entity.Property(tenant => tenant.Name).HasMaxLength(200).IsRequired();
            entity.Property(tenant => tenant.Description).HasMaxLength(1000);
            entity.Property(tenant => tenant.CreatedAt).IsRequired();
            entity.HasIndex(tenant => tenant.NormalizedIdentifier).IsUnique();
        });

        modelBuilder.Entity<TenantRole>(entity =>
        {
            entity.ToTable("tenant_roles");
            entity.HasKey(role => role.Id);
            entity.Property(role => role.TenantId).IsRequired();
            entity.Property(role => role.Name).HasMaxLength(120).IsRequired();
            entity.Property(role => role.NormalizedName).HasMaxLength(120).IsRequired();
            entity.Property(role => role.Description).HasMaxLength(1000);
            entity.Property(role => role.CreatedAt).IsRequired();
            entity.HasIndex(role => new { role.TenantId, role.NormalizedName }).IsUnique();
            entity.HasOne(role => role.Tenant)
                .WithMany(tenant => tenant.Roles)
                .HasForeignKey(role => role.TenantId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<TenantMembership>(entity =>
        {
            entity.ToTable("tenant_memberships");
            entity.HasKey(membership => membership.Id);
            entity.Property(membership => membership.TenantId).IsRequired();
            entity.Property(membership => membership.UserId).IsRequired();
            entity.Property(membership => membership.TenantRoleId).IsRequired();
            entity.Property(membership => membership.CreatedAt).IsRequired();
            entity.HasIndex(membership => new { membership.TenantId, membership.UserId, membership.TenantRoleId }).IsUnique();
            entity.HasOne(membership => membership.Tenant)
                .WithMany(tenant => tenant.Memberships)
                .HasForeignKey(membership => membership.TenantId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(membership => membership.User)
                .WithMany()
                .HasForeignKey(membership => membership.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(membership => membership.TenantRole)
                .WithMany(role => role.Memberships)
                .HasForeignKey(membership => membership.TenantRoleId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Permission>(entity =>
        {
            entity.ToTable("permissions");
            entity.HasKey(permission => permission.Id);
            entity.Property(permission => permission.Key).HasMaxLength(160).IsRequired();
            entity.Property(permission => permission.NormalizedKey).HasMaxLength(160).IsRequired();
            entity.Property(permission => permission.Description).HasMaxLength(1000);
            entity.Property(permission => permission.CreatedAt).IsRequired();
            entity.HasIndex(permission => permission.NormalizedKey).IsUnique();
        });

        modelBuilder.Entity<TenantRolePermission>(entity =>
        {
            entity.ToTable("tenant_role_permissions");
            entity.HasKey(rolePermission => rolePermission.Id);
            entity.Property(rolePermission => rolePermission.TenantId).IsRequired();
            entity.Property(rolePermission => rolePermission.CreatedAt).IsRequired();
            entity.HasIndex(rolePermission => new { rolePermission.TenantId, rolePermission.TenantRoleId, rolePermission.PermissionId }).IsUnique();
            entity.HasOne(rolePermission => rolePermission.TenantRole)
                .WithMany(role => role.RolePermissions)
                .HasForeignKey(rolePermission => rolePermission.TenantRoleId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(rolePermission => rolePermission.Permission)
                .WithMany(permission => permission.RolePermissions)
                .HasForeignKey(rolePermission => rolePermission.PermissionId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<AccessGrant>(entity =>
        {
            entity.ToTable("access_grants");
            entity.HasKey(grant => grant.Id);
            entity.Property(grant => grant.TenantId).IsRequired();
            entity.Property(grant => grant.PermissionKey).HasMaxLength(160).IsRequired();
            entity.Property(grant => grant.NormalizedPermissionKey).HasMaxLength(160).IsRequired();
            entity.Property(grant => grant.Kind).HasConversion<string>().HasMaxLength(32).IsRequired();
            entity.Property(grant => grant.Justification).HasMaxLength(1000).IsRequired();
            entity.Property(grant => grant.CreatedAt).IsRequired();
            entity.HasIndex(grant => new { grant.TenantId, grant.UserId, grant.NormalizedPermissionKey });
            entity.HasOne(grant => grant.Tenant)
                .WithMany()
                .HasForeignKey(grant => grant.TenantId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(grant => grant.User)
                .WithMany()
                .HasForeignKey(grant => grant.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<AccessRequest>(entity =>
        {
            entity.ToTable("access_requests");
            entity.HasKey(request => request.Id);
            entity.Property(request => request.TenantId).IsRequired();
            entity.Property(request => request.PermissionKey).HasMaxLength(160).IsRequired();
            entity.Property(request => request.NormalizedPermissionKey).HasMaxLength(160).IsRequired();
            entity.Property(request => request.Reason).HasMaxLength(1000).IsRequired();
            entity.Property(request => request.Status).HasConversion<string>().HasMaxLength(32).IsRequired();
            entity.Property(request => request.CreatedAt).IsRequired();
            entity.HasIndex(request => new { request.TenantId, request.UserId, request.NormalizedPermissionKey });
            entity.HasOne(request => request.Tenant)
                .WithMany()
                .HasForeignKey(request => request.TenantId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(request => request.User)
                .WithMany()
                .HasForeignKey(request => request.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<AccessDenialRecord>(entity =>
        {
            entity.ToTable("access_denial_records");
            entity.HasKey(record => record.Id);
            entity.Property(record => record.Action).HasMaxLength(200).IsRequired();
            entity.Property(record => record.Reason).HasMaxLength(500).IsRequired();
            entity.Property(record => record.SafeSummary).HasMaxLength(1000).IsRequired();
            entity.Property(record => record.CreatedAt).IsRequired();
            entity.HasIndex(record => new { record.TenantId, record.UserId, record.CreatedAt });
        });

        modelBuilder.Entity<AuditRecord>(entity =>
        {
            entity.ToTable("audit_records");
            entity.HasKey(record => record.Id);
            entity.Property(record => record.Action).HasMaxLength(200).IsRequired();
            entity.Property(record => record.Result).HasConversion<string>().HasMaxLength(32).IsRequired();
            entity.Property(record => record.Reason).HasMaxLength(500);
            entity.Property(record => record.SourceObjectType).HasMaxLength(120);
            entity.Property(record => record.SourceObjectId).HasMaxLength(200);
            entity.Property(record => record.PolicyName).HasMaxLength(160);
            entity.Property(record => record.PolicyVersion).HasMaxLength(80);
            entity.Property(record => record.CorrelationId).HasMaxLength(120);
            entity.Property(record => record.SafeSummary).HasMaxLength(1000).IsRequired();
            entity.Property(record => record.RetentionCategory).HasConversion<string>().HasMaxLength(32).IsRequired();
            entity.Property(record => record.CreatedAt).IsRequired();
            entity.HasIndex(record => new { record.TenantId, record.CreatedAt });
            entity.HasIndex(record => new { record.TenantId, record.Result, record.CreatedAt });
            entity.HasIndex(record => new { record.TenantId, record.Action, record.CreatedAt });
        });

        modelBuilder.Entity<SecurityEvent>(entity =>
        {
            entity.ToTable("security_events");
            entity.HasKey(securityEvent => securityEvent.Id);
            entity.Property(securityEvent => securityEvent.EventType).HasConversion<string>().HasMaxLength(64).IsRequired();
            entity.Property(securityEvent => securityEvent.Severity).HasConversion<string>().HasMaxLength(32).IsRequired();
            entity.Property(securityEvent => securityEvent.SourceAction).HasMaxLength(200).IsRequired();
            entity.Property(securityEvent => securityEvent.Reason).HasMaxLength(500);
            entity.Property(securityEvent => securityEvent.SafeSummary).HasMaxLength(1000).IsRequired();
            entity.Property(securityEvent => securityEvent.ReviewTaskHint).HasMaxLength(500);
            entity.Property(securityEvent => securityEvent.CreatedAt).IsRequired();
            entity.HasIndex(securityEvent => new { securityEvent.TenantId, securityEvent.CreatedAt });
            entity.HasIndex(securityEvent => new { securityEvent.TenantId, securityEvent.EventType, securityEvent.CreatedAt });
            entity.HasIndex(securityEvent => new { securityEvent.TenantId, securityEvent.Severity, securityEvent.CreatedAt });
            entity.HasOne(securityEvent => securityEvent.RelatedAuditRecord)
                .WithMany(record => record.SecurityEvents)
                .HasForeignKey(securityEvent => securityEvent.RelatedAuditRecordId)
                .OnDelete(DeleteBehavior.SetNull);
        });
    }

    private static void ConfigureIdentityTables(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<EtosUser>(entity =>
        {
            entity.ToTable("identity_users");
            entity.Property(user => user.DisplayName).HasMaxLength(200);
            entity.Property(user => user.CreatedAt).IsRequired();
        });

        modelBuilder.Entity<EtosIdentityRole>(entity =>
        {
            entity.ToTable("identity_roles");
            entity.Property(role => role.Description).HasMaxLength(1000);
            entity.Property(role => role.CreatedAt).IsRequired();
        });

        modelBuilder.Entity<IdentityUserClaim<Guid>>().ToTable("identity_user_claims");
        modelBuilder.Entity<IdentityUserLogin<Guid>>().ToTable("identity_user_logins");
        modelBuilder.Entity<IdentityUserToken<Guid>>().ToTable("identity_user_tokens");
        modelBuilder.Entity<IdentityRoleClaim<Guid>>().ToTable("identity_role_claims");
        modelBuilder.Entity<IdentityUserRole<Guid>>().ToTable("identity_user_roles");
    }
}
