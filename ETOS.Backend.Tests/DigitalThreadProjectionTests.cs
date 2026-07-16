using ETOS.Backend.Artifacts;
using ETOS.Backend.DataQuality;
using ETOS.Backend.DigitalThread;
using ETOS.Backend.Identity;
using ETOS.Backend.Imports;
using ETOS.Backend.Infrastructure.Persistence;
using ETOS.Backend.ToolRegistry;
using Microsoft.EntityFrameworkCore;

namespace ETOS.Backend.Tests;

public sealed class DigitalThreadProjectionTests
{
    [Fact]
    public async Task ListEventsAsync_returns_tenant_scoped_ordered_events()
    {
        await using var dbContext = CreateDbContext();
        var context = SeedTenant(dbContext);
        var otherTenant = SeedTenant(dbContext);
        var older = DateTimeOffset.UtcNow.AddHours(-2);
        var newer = DateTimeOffset.UtcNow.AddHours(-1);

        SeedImportBatch(dbContext, context, "SolidWorks PDM", older);
        SeedToolRun(dbContext, context, newer, ToolRunStatuses.Succeeded);
        SeedImportBatch(dbContext, otherTenant, "Other ERP", newer);

        var events = await CreateService(dbContext, context).ListEventsAsync(
            DateTimeOffset.UtcNow.AddHours(-24),
            DateTimeOffset.UtcNow,
            null,
            50,
            CancellationToken.None);

        Assert.DoesNotContain(events, item => item.SourceSystemName.Contains("Other", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(events, item => item.EventType == "ToolRun");
        Assert.Contains(events, item => item.EventType == "ImportBatchCreated");
        Assert.True(events.First().TimestampUtc >= events.Last().TimestampUtc);
    }

    [Fact]
    public async Task GetSummaryAsync_counts_seeded_imports_and_runs()
    {
        await using var dbContext = CreateDbContext();
        var context = SeedTenant(dbContext);
        SeedImportBatch(dbContext, context, "SAP S/4HANA", DateTimeOffset.UtcNow.AddMinutes(-10));
        SeedToolRun(dbContext, context, DateTimeOffset.UtcNow.AddMinutes(-2), ToolRunStatuses.Failed);
        SeedDataQualityIssue(dbContext, context);

        var summary = await CreateService(dbContext, context).GetSummaryAsync(24, CancellationToken.None);

        Assert.True(summary.ConnectedSystemCount >= 1);
        Assert.True(summary.OpenAlertCounts.DataQualityOpen >= 1);
        Assert.True(summary.OpenAlertCounts.FailedRuns >= 1);
        Assert.NotEmpty(summary.TopThreads);
        Assert.Equal(24, summary.WindowHours);
    }

    [Fact]
    public async Task ListEventsAsync_filters_by_system_id()
    {
        await using var dbContext = CreateDbContext();
        var context = SeedTenant(dbContext);
        SeedImportBatch(dbContext, context, "SolidWorks PDM", DateTimeOffset.UtcNow.AddHours(-1));
        SeedToolRun(dbContext, context, DateTimeOffset.UtcNow.AddMinutes(-30), ToolRunStatuses.Succeeded);

        var events = await CreateService(dbContext, context).ListEventsAsync(
            null,
            null,
            "tool-runtime",
            20,
            CancellationToken.None);

        Assert.NotEmpty(events);
        Assert.All(events, item => Assert.Equal("tool-runtime", item.SourceSystemId));
    }

    [Fact]
    public async Task ListSystemsAsync_denied_without_permission()
    {
        await using var dbContext = CreateDbContext();
        var context = SeedTenant(dbContext);
        var service = new DigitalThreadProjectionService(
            dbContext,
            new StaticTenantContextResolver(context),
            new DenyAllPermissionService());

        await Assert.ThrowsAsync<TenantAccessDeniedException>(() =>
            service.ListSystemsAsync(CancellationToken.None));
    }

    [Fact]
    public async Task GetSummaryAsync_empty_tenant_returns_zero_kpis()
    {
        await using var dbContext = CreateDbContext();
        var context = SeedTenant(dbContext);

        var summary = await CreateService(dbContext, context).GetSummaryAsync(24, CancellationToken.None);

        Assert.Equal(0, summary.ConnectedSystemCount);
        Assert.Equal(0, summary.OpenAlertCounts.Total);
        Assert.Empty(summary.TopThreads);
        Assert.Empty(summary.HeatmapBuckets);
        Assert.Equal(0, summary.EventsLastMinute);
    }

    [Fact]
    public async Task ListBranchesAsync_empty_tenant_returns_empty()
    {
        await using var dbContext = CreateDbContext();
        var context = SeedTenant(dbContext);

        var branches = await CreateService(dbContext, context).ListBranchesAsync(24, null, null, CancellationToken.None);

        Assert.Empty(branches);
    }

    [Fact]
    public async Task ListBranchesAsync_returns_projection_points_for_seeded_events()
    {
        await using var dbContext = CreateDbContext();
        var context = SeedTenant(dbContext);
        SeedImportBatch(dbContext, context, "SolidWorks PDM", DateTimeOffset.UtcNow.AddHours(-2));
        SeedToolRun(dbContext, context, DateTimeOffset.UtcNow.AddMinutes(-20), ToolRunStatuses.Succeeded);

        var branches = await CreateService(dbContext, context).ListBranchesAsync(24, null, null, CancellationToken.None);

        Assert.NotEmpty(branches);
        Assert.Contains(branches, branch => branch.EventCount > 0 && branch.ProjectionPoints.Count > 0);
        Assert.All(branches, branch => Assert.False(string.IsNullOrWhiteSpace(branch.BranchId)));
    }

    [Fact]
    public async Task GetLineageAsync_returns_hops_and_isolates_tenants()
    {
        await using var dbContext = CreateDbContext();
        var context = SeedTenant(dbContext);
        var otherTenant = SeedTenant(dbContext);
        var (sourceId, targetId) = SeedArtifactRelationship(dbContext, context);
        SeedArtifactRelationship(dbContext, otherTenant);

        var lineage = await CreateService(dbContext, context).GetLineageAsync(sourceId, CancellationToken.None);
        var missing = await CreateService(dbContext, context).GetLineageAsync(Guid.NewGuid(), CancellationToken.None);
        var otherLineage = await CreateService(dbContext, otherTenant).GetLineageAsync(sourceId, CancellationToken.None);

        Assert.NotNull(lineage);
        Assert.Equal(sourceId, lineage!.ArtifactId);
        Assert.Contains(lineage.Hops, hop => hop.FromArtifactId == sourceId && hop.ToArtifactId == targetId);
        Assert.Null(missing);
        Assert.Null(otherLineage);
    }

    [Fact]
    public async Task GetEventDetailAsync_found_and_missing()
    {
        await using var dbContext = CreateDbContext();
        var context = SeedTenant(dbContext);
        var runId = Guid.NewGuid();
        SeedToolRun(dbContext, context, DateTimeOffset.UtcNow.AddMinutes(-5), ToolRunStatuses.Succeeded, runId);

        var detail = await CreateService(dbContext, context).GetEventDetailAsync(
            $"tool-run:{runId}",
            CancellationToken.None);
        var missing = await CreateService(dbContext, context).GetEventDetailAsync(
            $"tool-run:{Guid.NewGuid()}",
            CancellationToken.None);

        Assert.NotNull(detail);
        Assert.Equal($"tool-run:{runId}", detail!.EventId);
        Assert.Equal("ToolRun", detail.EventType);
        Assert.Null(missing);
    }

    [Fact]
    public async Task GetMinimapAsync_returns_window_and_systems_shape()
    {
        await using var dbContext = CreateDbContext();
        var context = SeedTenant(dbContext);
        SeedImportBatch(dbContext, context, "SAP S/4HANA", DateTimeOffset.UtcNow.AddMinutes(-30));

        var minimap = await CreateService(dbContext, context).GetMinimapAsync(24, CancellationToken.None);

        Assert.Equal(24, minimap.WindowHours);
        Assert.True(minimap.WindowEndUtc >= minimap.WindowStartUtc);
        Assert.NotEmpty(minimap.Systems);
        Assert.NotEmpty(minimap.CoarsePoints);
    }

    [Fact]
    public async Task StreamEventsAsync_emits_seeded_event_then_heartbeat()
    {
        await using var dbContext = CreateDbContext();
        var context = SeedTenant(dbContext);
        var createdAt = DateTimeOffset.UtcNow.AddSeconds(-2);
        SeedToolRun(dbContext, context, createdAt, ToolRunStatuses.Succeeded);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(8));
        var service = CreateService(dbContext, context);
        DigitalThreadStreamEnvelope? eventEnvelope = null;
        DigitalThreadStreamEnvelope? heartbeat = null;

        await foreach (var envelope in service.StreamEventsAsync(createdAt.AddSeconds(-5), null, cts.Token))
        {
            if (envelope.Event is not null && eventEnvelope is null)
            {
                eventEnvelope = envelope;
            }

            if (envelope.Heartbeat)
            {
                heartbeat = envelope;
            }

            if (eventEnvelope is not null && heartbeat is not null)
            {
                cts.Cancel();
                break;
            }
        }

        Assert.NotNull(eventEnvelope);
        Assert.Equal("ToolRun", eventEnvelope!.Event!.EventType);
        Assert.NotNull(heartbeat);
        Assert.True(heartbeat!.Heartbeat);
    }

    private static DigitalThreadProjectionService CreateService(EnterpriseThreadDbContext dbContext, TestContext context)
        => new(
            dbContext,
            new StaticTenantContextResolver(context),
            new AllowAllPermissionService());

    private static void SeedImportBatch(
        EnterpriseThreadDbContext dbContext,
        TestContext context,
        string sourceSystem,
        DateTimeOffset createdAt)
    {
        dbContext.ImportBatches.Add(new ImportBatch
        {
            Id = Guid.NewGuid(),
            TenantId = context.TenantId,
            SourceSystem = sourceSystem,
            NormalizedSourceSystem = sourceSystem.ToUpperInvariant(),
            Status = ImportBatchStatus.Created,
            ActiveModelPackageVersionId = Guid.NewGuid(),
            CreatedByUserId = context.UserId,
            CreatedAt = createdAt
        });
        dbContext.SaveChanges();
    }

    private static void SeedToolRun(
        EnterpriseThreadDbContext dbContext,
        TestContext context,
        DateTimeOffset createdAt,
        string status,
        Guid? runId = null)
    {
        dbContext.ToolRuns.Add(new ToolRun
        {
            Id = runId ?? Guid.NewGuid(),
            TenantId = context.TenantId,
            ToolDefinitionVersionId = Guid.NewGuid(),
            RequestedByUserId = context.UserId,
            Status = status,
            IsDryRun = false,
            InputSafeSummaryJson = "{}",
            CreatedAt = createdAt
        });
        dbContext.SaveChanges();
    }

    private static void SeedDataQualityIssue(EnterpriseThreadDbContext dbContext, TestContext context)
    {
        dbContext.DataQualityIssues.Add(new DataQualityIssue
        {
            Id = Guid.NewGuid(),
            TenantId = context.TenantId,
            Title = "Missing required attribute",
            IssueCode = "MISSING_ATTR",
            NormalizedIssueCode = "MISSING_ATTR",
            Severity = DataQualitySeverity.High,
            Status = DataQualityIssueStatus.Open,
            Origin = DataQualityIssueOrigin.Manual,
            AffectedEntityType = DataQualityAffectedEntityType.ImportBatch,
            EvidenceSummary = "Attribute missing on staged node.",
            CreatedByUserId = context.UserId,
            CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-5),
            UpdatedAt = DateTimeOffset.UtcNow.AddMinutes(-5)
        });
        dbContext.SaveChanges();
    }

    private static (Guid SourceId, Guid TargetId) SeedArtifactRelationship(
        EnterpriseThreadDbContext dbContext,
        TestContext context)
    {
        var sourceId = Guid.NewGuid();
        var targetId = Guid.NewGuid();
        dbContext.Artifacts.AddRange(
            new Artifact
            {
                Id = sourceId,
                TenantId = context.TenantId,
                ArtifactType = "Part",
                NormalizedArtifactType = "PART",
                Name = "Source Part",
                OwnerUserId = context.UserId,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            },
            new Artifact
            {
                Id = targetId,
                TenantId = context.TenantId,
                ArtifactType = "Drawing",
                NormalizedArtifactType = "DRAWING",
                Name = "Target Drawing",
                OwnerUserId = context.UserId,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            });
        dbContext.ArtifactRelationships.Add(new ArtifactRelationship
        {
            Id = Guid.NewGuid(),
            TenantId = context.TenantId,
            SourceArtifactId = sourceId,
            TargetArtifactId = targetId,
            RelationshipType = ArtifactRelationshipType.References,
            CreatedAt = DateTimeOffset.UtcNow
        });
        dbContext.SaveChanges();
        return (sourceId, targetId);
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
