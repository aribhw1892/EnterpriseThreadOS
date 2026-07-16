using ETOS.Backend.Artifacts;
using ETOS.Backend.Identity;
using ETOS.Backend.Infrastructure.Persistence;
using ETOS.Backend.Learning;
using Microsoft.EntityFrameworkCore;

namespace ETOS.Backend.Tests;

public sealed class LearningSignalTests
{
    [Fact]
    public async Task ListAsync_returns_tenant_scoped_signals()
    {
        await using var dbContext = CreateDbContext();
        var context = SeedTenant(dbContext);
        var otherTenant = SeedTenant(dbContext);

        await SeedSignalAsync(dbContext, context, "manual:accept:data-quality-review", 3);
        await SeedSignalAsync(dbContext, otherTenant, "manual:reject:data-quality-review", 4);

        var service = CreateService(dbContext, context);
        var list = await service.ListAsync(null, null, CancellationToken.None);

        Assert.Single(list);
        Assert.Equal("manual:accept:data-quality-review", list.First().PatternKey);
        Assert.Equal(3, list.First().OccurrenceCount);
        Assert.Equal("active", list.First().Status);
    }

    [Fact]
    public async Task ListAsync_filters_by_status_and_pattern()
    {
        await using var dbContext = CreateDbContext();
        var context = SeedTenant(dbContext);
        await SeedSignalAsync(dbContext, context, "manual:accept:data-quality-review", 3, "active");
        await SeedSignalAsync(dbContext, context, "security:reject:governance-security-review", 5, "active");

        var service = CreateService(dbContext, context);
        var filtered = await service.ListAsync("active", "security", CancellationToken.None);

        Assert.Single(filtered);
        Assert.Contains("security", filtered.First().PatternKey, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetAsync_includes_related_evidence()
    {
        await using var dbContext = CreateDbContext();
        var context = SeedTenant(dbContext);
        var patternKey = "manual:accept:data-quality-review";
        var decisionId = Guid.NewGuid();
        var signal = await SeedSignalAsync(dbContext, context, patternKey, 3, sourceDecisionIds: [decisionId]);

        dbContext.DecisionLearningEvidence.Add(new DecisionLearningEvidence
        {
            Id = Guid.NewGuid(),
            TenantId = context.TenantId,
            DecisionArtifactId = decisionId,
            PatternKey = patternKey,
            SourceType = "manual",
            OutcomeKey = "accept",
            EvidenceSummary = "Decision finalized.",
            CreatedAt = DateTimeOffset.UtcNow
        });
        await dbContext.SaveChangesAsync();

        var detail = await CreateService(dbContext, context).GetAsync(signal.ArtifactId, CancellationToken.None);

        Assert.Equal(signal.ArtifactId, detail.ArtifactId);
        Assert.Equal(patternKey, detail.PatternKey);
        Assert.Contains(decisionId, detail.SourceDecisionIds);
        Assert.NotEmpty(detail.RelatedEvidence);
    }

    [Fact]
    public async Task ListAsync_denied_without_permission()
    {
        await using var dbContext = CreateDbContext();
        var context = SeedTenant(dbContext);
        var service = new LearningSignalService(
            dbContext,
            new StaticTenantContextResolver(context),
            new DenyAllPermissionService());

        await Assert.ThrowsAsync<TenantAccessDeniedException>(() =>
            service.ListAsync(null, null, CancellationToken.None));
    }

    private static LearningSignalService CreateService(EnterpriseThreadDbContext dbContext, TestContext context)
        => new(
            dbContext,
            new StaticTenantContextResolver(context),
            new AllowAllPermissionService());

    private static async Task<LearningSignalSummaryResponse> SeedSignalAsync(
        EnterpriseThreadDbContext dbContext,
        TestContext context,
        string patternKey,
        int occurrenceCount,
        string status = "active",
        IReadOnlyCollection<Guid>? sourceDecisionIds = null)
    {
        var artifactId = Guid.NewGuid();
        var versionId = Guid.NewGuid();
        var payload = System.Text.Json.JsonSerializer.Serialize(new
        {
            patternKey,
            occurrenceCount,
            sourceDecisionIds = sourceDecisionIds ?? [],
            summary = $"Repeated governance pattern detected for '{patternKey}'.",
            status
        });

        dbContext.Artifacts.Add(new Artifact
        {
            Id = artifactId,
            TenantId = context.TenantId,
            ArtifactType = LearningArtifactTypes.LearningSignal,
            NormalizedArtifactType = LearningArtifactTypes.LearningSignal.ToUpperInvariant(),
            Name = $"Learning signal: {patternKey}",
            Description = "Rollup learning signal.",
            OwnerUserId = context.UserId,
            LifecycleState = ArtifactLifecycleState.Active,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });
        dbContext.ArtifactVersions.Add(new ArtifactVersion
        {
            Id = versionId,
            TenantId = context.TenantId,
            ArtifactId = artifactId,
            VersionLabel = "1.0.0",
            NormalizedVersionLabel = "1.0.0",
            Summary = patternKey,
            PayloadJson = payload,
            ReadinessState = ArtifactReadinessState.Published,
            CreatedByUserId = context.UserId,
            CreatedAt = DateTimeOffset.UtcNow
        });
        await dbContext.SaveChangesAsync();

        return new LearningSignalSummaryResponse(
            artifactId,
            versionId,
            $"Learning signal: {patternKey}",
            patternKey,
            occurrenceCount,
            $"Repeated governance pattern detected for '{patternKey}'.",
            status,
            sourceDecisionIds ?? [],
            DateTimeOffset.UtcNow);
    }

    private static EnterpriseThreadDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<EnterpriseThreadDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new EnterpriseThreadDbContext(options);
    }

    private static TestContext SeedTenant(EnterpriseThreadDbContext dbContext)
    {
        var context = new TestContext(Guid.NewGuid(), Guid.NewGuid());
        dbContext.Tenants.Add(new Tenant
        {
            Id = context.TenantId,
            Identifier = $"demo-{context.TenantId:N}"[..16],
            NormalizedIdentifier = $"DEMO-{context.TenantId:N}"[..16].ToUpperInvariant(),
            Name = "Demo Tenant",
            CreatedAt = DateTimeOffset.UtcNow
        });
        dbContext.Users.Add(new EtosUser
        {
            Id = context.UserId,
            UserName = $"{context.UserId:N}@example.test",
            NormalizedUserName = $"{context.UserId:N}@EXAMPLE.TEST",
            Email = $"{context.UserId:N}@example.test",
            NormalizedEmail = $"{context.UserId:N}@EXAMPLE.TEST",
            DisplayName = "Demo User",
            CreatedAt = DateTimeOffset.UtcNow
        });
        var roleId = Guid.NewGuid();
        dbContext.TenantRoles.Add(new TenantRole
        {
            Id = roleId,
            TenantId = context.TenantId,
            Name = "Admin",
            NormalizedName = "ADMIN",
            CreatedAt = DateTimeOffset.UtcNow
        });
        dbContext.TenantMemberships.Add(new TenantMembership
        {
            Id = Guid.NewGuid(),
            TenantId = context.TenantId,
            UserId = context.UserId,
            TenantRoleId = roleId,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow
        });
        dbContext.SaveChanges();
        return context;
    }

    private sealed record TestContext(Guid TenantId, Guid UserId);

    private sealed class StaticTenantContextResolver(TestContext context) : ITenantContextResolver
    {
        public Task<ActiveTenantContext> ResolveAsync(string action, CancellationToken cancellationToken)
            => Task.FromResult(new ActiveTenantContext(context.TenantId, "demo", "Demo", context.UserId));
    }

    private sealed class AllowAllPermissionService : IAccessPermissionService
    {
        public Task<bool> HasTenantAccessAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken)
            => Task.FromResult(true);

        public Task<bool> HasPermissionAsync(Guid tenantId, Guid userId, string permissionKey, CancellationToken cancellationToken)
            => Task.FromResult(true);
    }

    private sealed class DenyAllPermissionService : IAccessPermissionService
    {
        public Task<bool> HasTenantAccessAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken)
            => Task.FromResult(true);

        public Task<bool> HasPermissionAsync(Guid tenantId, Guid userId, string permissionKey, CancellationToken cancellationToken)
            => Task.FromResult(false);
    }
}
