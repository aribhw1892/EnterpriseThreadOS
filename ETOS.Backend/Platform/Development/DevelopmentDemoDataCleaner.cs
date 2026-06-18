using ETOS.Backend.Documents;
using ETOS.Backend.Governance;
using ETOS.Backend.Identity;
using ETOS.Backend.Imports;
using ETOS.Backend.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Neo4j.Driver;

namespace ETOS.Backend.Platform.Development;

public interface IDevelopmentDemoDataCleaner
{
    Task<CleanDevelopmentDemoDataResponse> CleanTenantDemoDataAsync(CancellationToken cancellationToken);
}

public sealed record CleanDevelopmentDemoDataResponse(
    Guid TenantId,
    IReadOnlyDictionary<string, int> DeletedCounts,
    bool GraphMemoryCleared,
    bool ImportFilesCleared,
    bool DocumentFilesCleared,
    string Summary);

public sealed class DevelopmentDemoDataCleaner(
    EnterpriseThreadDbContext dbContext,
    ITenantContextResolver tenantContextResolver,
    IAccessPermissionService permissionService,
    IAccessDenialRecorder denialRecorder,
    IDriver graphDriver,
    IOptions<ImportFileStorageOptions> importFileStorageOptions,
    IOptions<DocumentFileStorageOptions> documentFileStorageOptions,
    ILogger<DevelopmentDemoDataCleaner> logger) : IDevelopmentDemoDataCleaner
{
    private const string CleanAction = "development.clean-demo-data";

    public async Task<CleanDevelopmentDemoDataResponse> CleanTenantDemoDataAsync(CancellationToken cancellationToken)
    {
        var context = await tenantContextResolver.ResolveAsync(CleanAction, cancellationToken);
        var hasPermission = await permissionService.HasPermissionAsync(
            context.TenantId,
            context.UserId,
            IdentityPermissions.Wildcard,
            cancellationToken);

        if (!hasPermission)
        {
            await denialRecorder.RecordAsync(
                context.TenantId,
                context.UserId,
                CleanAction,
                "permission_denied",
                "Development demo cleanup requires tenant administrator wildcard permission.",
                cancellationToken);
            throw new TenantAccessDeniedException("Development demo cleanup requires tenant administrator access.");
        }

        var deletedCounts = new Dictionary<string, int>();
        var ownsTransaction = dbContext.Database.IsRelational();
        await using var transaction = ownsTransaction
            ? await dbContext.Database.BeginTransactionAsync(cancellationToken)
            : null;

        deletedCounts[nameof(dbContext.AiTraceExportRecords)] =
            await DeleteTenantRowsAsync(dbContext.AiTraceExportRecords, context.TenantId, cancellationToken);
        deletedCounts[nameof(dbContext.AiTraceArtifactLinks)] =
            await DeleteTenantRowsAsync(dbContext.AiTraceArtifactLinks, context.TenantId, cancellationToken);
        deletedCounts[nameof(dbContext.AiTraceRecords)] =
            await DeleteTenantRowsAsync(dbContext.AiTraceRecords, context.TenantId, cancellationToken);
        deletedCounts[nameof(dbContext.DashboardReportExportRecords)] =
            await DeleteTenantRowsAsync(dbContext.DashboardReportExportRecords, context.TenantId, cancellationToken);
        deletedCounts[nameof(dbContext.GovernedChatTurns)] =
            await DeleteTenantRowsAsync(dbContext.GovernedChatTurns, context.TenantId, cancellationToken);
        deletedCounts[nameof(dbContext.GovernedChatSessions)] =
            await DeleteTenantRowsAsync(dbContext.GovernedChatSessions, context.TenantId, cancellationToken);
        deletedCounts[nameof(dbContext.ContextAccessDecisions)] =
            await DeleteTenantRowsAsync(dbContext.ContextAccessDecisions, context.TenantId, cancellationToken);
        deletedCounts[nameof(dbContext.ContextPackages)] =
            await DeleteTenantRowsAsync(dbContext.ContextPackages, context.TenantId, cancellationToken);
        deletedCounts[nameof(dbContext.RetrievalRuns)] =
            await DeleteTenantRowsAsync(dbContext.RetrievalRuns, context.TenantId, cancellationToken);
        deletedCounts[nameof(dbContext.DocumentVectorIndexRecords)] =
            await DeleteTenantRowsAsync(dbContext.DocumentVectorIndexRecords, context.TenantId, cancellationToken);
        deletedCounts[nameof(dbContext.DocumentObjectLinks)] =
            await DeleteTenantRowsAsync(dbContext.DocumentObjectLinks, context.TenantId, cancellationToken);
        deletedCounts[nameof(dbContext.DocumentVersions)] =
            await DeleteTenantRowsAsync(dbContext.DocumentVersions, context.TenantId, cancellationToken);
        deletedCounts[nameof(dbContext.DocumentArtifacts)] =
            await DeleteTenantRowsAsync(dbContext.DocumentArtifacts, context.TenantId, cancellationToken);
        deletedCounts[nameof(dbContext.DataQualityTrustImpacts)] =
            await DeleteTenantRowsAsync(dbContext.DataQualityTrustImpacts, context.TenantId, cancellationToken);
        deletedCounts[nameof(dbContext.DataQualityIssueSourceLinks)] =
            await DeleteTenantRowsAsync(dbContext.DataQualityIssueSourceLinks, context.TenantId, cancellationToken);
        deletedCounts[nameof(dbContext.DataQualityIssues)] =
            await DeleteTenantRowsAsync(dbContext.DataQualityIssues, context.TenantId, cancellationToken);
        deletedCounts[nameof(dbContext.MonitoringIssueTypeDefinitions)] =
            await DeleteTenantRowsAsync(dbContext.MonitoringIssueTypeDefinitions, context.TenantId, cancellationToken);
        deletedCounts[nameof(dbContext.IdentityLearningEvidence)] =
            await DeleteTenantRowsAsync(dbContext.IdentityLearningEvidence, context.TenantId, cancellationToken);
        deletedCounts[nameof(dbContext.IdentityResolutionDecisions)] =
            await DeleteTenantRowsAsync(dbContext.IdentityResolutionDecisions, context.TenantId, cancellationToken);
        deletedCounts[nameof(dbContext.TrustScoreRecords)] =
            await DeleteTenantRowsAsync(dbContext.TrustScoreRecords, context.TenantId, cancellationToken);
        deletedCounts[nameof(dbContext.IdentityCandidateLinks)] =
            await DeleteTenantRowsAsync(dbContext.IdentityCandidateLinks, context.TenantId, cancellationToken);
        deletedCounts[nameof(dbContext.IdentityResolutionRules)] =
            await DeleteTenantRowsAsync(dbContext.IdentityResolutionRules, context.TenantId, cancellationToken);
        deletedCounts[nameof(dbContext.BomComparisonRuns)] =
            await DeleteTenantRowsAsync(dbContext.BomComparisonRuns, context.TenantId, cancellationToken);
        deletedCounts[nameof(dbContext.RejectedStagingSummaries)] =
            await DeleteTenantRowsAsync(dbContext.RejectedStagingSummaries, context.TenantId, cancellationToken);
        deletedCounts[nameof(dbContext.ImportPromotionRuns)] =
            await DeleteTenantRowsAsync(dbContext.ImportPromotionRuns, context.TenantId, cancellationToken);
        deletedCounts[nameof(dbContext.ImportStagingGraphRuns)] =
            await DeleteTenantRowsAsync(dbContext.ImportStagingGraphRuns, context.TenantId, cancellationToken);
        deletedCounts[nameof(dbContext.ImportValidationIssues)] =
            await DeleteTenantRowsAsync(dbContext.ImportValidationIssues, context.TenantId, cancellationToken);
        deletedCounts[nameof(dbContext.ImportColumnMappings)] =
            await DeleteTenantRowsAsync(dbContext.ImportColumnMappings, context.TenantId, cancellationToken);
        deletedCounts[nameof(dbContext.ImportLifecycleMappings)] =
            await DeleteTenantRowsAsync(dbContext.ImportLifecycleMappings, context.TenantId, cancellationToken);
        deletedCounts[nameof(dbContext.ImportMappingVersions)] =
            await DeleteTenantRowsAsync(dbContext.ImportMappingVersions, context.TenantId, cancellationToken);
        deletedCounts[nameof(dbContext.ImportFileEvidence)] =
            await DeleteTenantRowsAsync(dbContext.ImportFileEvidence, context.TenantId, cancellationToken);
        deletedCounts[nameof(dbContext.ImportBatches)] =
            await DeleteTenantRowsAsync(dbContext.ImportBatches, context.TenantId, cancellationToken);
        deletedCounts[nameof(dbContext.GraphDiffs)] =
            await DeleteTenantRowsAsync(dbContext.GraphDiffs, context.TenantId, cancellationToken);
        deletedCounts[nameof(dbContext.GraphSnapshots)] =
            await DeleteTenantRowsAsync(dbContext.GraphSnapshots, context.TenantId, cancellationToken);
        deletedCounts[nameof(dbContext.PolicyEvaluationRecords)] =
            await DeleteTenantRowsAsync(dbContext.PolicyEvaluationRecords, context.TenantId, cancellationToken);
        deletedCounts[nameof(dbContext.RestrictedContextRules)] =
            await DeleteTenantRowsAsync(dbContext.RestrictedContextRules, context.TenantId, cancellationToken);
        deletedCounts[nameof(dbContext.PolicyVersions)] =
            await DeleteTenantRowsAsync(dbContext.PolicyVersions, context.TenantId, cancellationToken);
        deletedCounts[nameof(dbContext.ClassificationSchemeVersions)] =
            await DeleteTenantRowsAsync(dbContext.ClassificationSchemeVersions, context.TenantId, cancellationToken);
        deletedCounts[nameof(dbContext.ClassificationSchemes)] =
            await DeleteTenantRowsAsync(dbContext.ClassificationSchemes, context.TenantId, cancellationToken);
        deletedCounts[nameof(dbContext.ArtifactDependencies)] =
            await DeleteTenantRowsAsync(dbContext.ArtifactDependencies, context.TenantId, cancellationToken);
        deletedCounts[nameof(dbContext.ArtifactRelationships)] =
            await DeleteTenantRowsAsync(dbContext.ArtifactRelationships, context.TenantId, cancellationToken);
        deletedCounts[nameof(dbContext.ArtifactVersions)] =
            await DeleteTenantRowsAsync(dbContext.ArtifactVersions, context.TenantId, cancellationToken);
        deletedCounts[nameof(dbContext.Artifacts)] =
            await DeleteTenantRowsAsync(dbContext.Artifacts, context.TenantId, cancellationToken);
        deletedCounts[nameof(dbContext.ModelPackageVersions)] =
            await DeleteTenantRowsAsync(dbContext.ModelPackageVersions, context.TenantId, cancellationToken);
        deletedCounts[nameof(dbContext.AttributeDefinitions)] =
            await DeleteTenantRowsAsync(dbContext.AttributeDefinitions, context.TenantId, cancellationToken);
        deletedCounts[nameof(dbContext.AttributeSchemaVersions)] =
            await DeleteTenantRowsAsync(dbContext.AttributeSchemaVersions, context.TenantId, cancellationToken);
        deletedCounts[nameof(dbContext.LifecycleTransitionDefinitions)] =
            await DeleteTenantRowsAsync(dbContext.LifecycleTransitionDefinitions, context.TenantId, cancellationToken);
        deletedCounts[nameof(dbContext.LifecycleStateDefinitions)] =
            await DeleteTenantRowsAsync(dbContext.LifecycleStateDefinitions, context.TenantId, cancellationToken);
        deletedCounts[nameof(dbContext.LifecycleVocabularyVersions)] =
            await DeleteTenantRowsAsync(dbContext.LifecycleVocabularyVersions, context.TenantId, cancellationToken);
        deletedCounts[nameof(dbContext.SemanticLayerVersions)] =
            await DeleteTenantRowsAsync(dbContext.SemanticLayerVersions, context.TenantId, cancellationToken);
        deletedCounts[nameof(dbContext.BomRelationshipDefinitions)] =
            await DeleteTenantRowsAsync(dbContext.BomRelationshipDefinitions, context.TenantId, cancellationToken);
        deletedCounts[nameof(dbContext.SemanticRelationshipDefinitions)] =
            await DeleteTenantRowsAsync(dbContext.SemanticRelationshipDefinitions, context.TenantId, cancellationToken);
        deletedCounts[nameof(dbContext.OntologyObjectTypeDefinitions)] =
            await DeleteTenantRowsAsync(dbContext.OntologyObjectTypeDefinitions, context.TenantId, cancellationToken);
        deletedCounts[nameof(dbContext.OntologyVersions)] =
            await DeleteTenantRowsAsync(dbContext.OntologyVersions, context.TenantId, cancellationToken);
        deletedCounts[nameof(dbContext.QueryIntentVersions)] =
            await DeleteTenantRowsAsync(dbContext.QueryIntentVersions, context.TenantId, cancellationToken);
        deletedCounts[nameof(dbContext.RetrievalStrategyVersions)] =
            await DeleteTenantRowsAsync(dbContext.RetrievalStrategyVersions, context.TenantId, cancellationToken);
        deletedCounts[nameof(dbContext.SecurityEvents)] =
            await DeleteTenantRowsAsync(dbContext.SecurityEvents, context.TenantId, cancellationToken);
        deletedCounts[nameof(dbContext.AuditRecords)] =
            await DeleteTenantRowsAsync(dbContext.AuditRecords, context.TenantId, cancellationToken);
        deletedCounts[nameof(dbContext.AccessDenialRecords)] =
            await DeleteTenantRowsAsync(dbContext.AccessDenialRecords, context.TenantId, cancellationToken);
        deletedCounts[nameof(dbContext.AccessRequests)] =
            await DeleteTenantRowsAsync(dbContext.AccessRequests, context.TenantId, cancellationToken);
        deletedCounts[nameof(dbContext.TenantScopedSampleRecords)] =
            await DeleteTenantRowsAsync(dbContext.TenantScopedSampleRecords, context.TenantId, cancellationToken);

        if (transaction is not null)
        {
            await transaction.CommitAsync(cancellationToken);
        }

        var graphMemoryCleared = await TryClearTenantGraphAsync(context.TenantId, cancellationToken);
        var importFilesCleared = TryDeleteTenantStorageDirectory(importFileStorageOptions.Value.RootPath, context.TenantId);
        var documentFilesCleared = TryDeleteTenantStorageDirectory(documentFileStorageOptions.Value.RootPath, context.TenantId);

        var totalDeleted = deletedCounts.Values.Sum();
        var summary =
            $"Removed {totalDeleted} tenant-scoped demo records for '{context.Identifier}'. Identity, roles, and memberships were preserved.";

        logger.LogInformation(
            "Development demo cleanup completed for tenant {TenantId}. DeletedRows={DeletedRows} Graph={GraphCleared} ImportFiles={ImportFilesCleared} DocumentFiles={DocumentFilesCleared}",
            context.TenantId,
            totalDeleted,
            graphMemoryCleared,
            importFilesCleared,
            documentFilesCleared);

        return new CleanDevelopmentDemoDataResponse(
            context.TenantId,
            deletedCounts,
            graphMemoryCleared,
            importFilesCleared,
            documentFilesCleared,
            summary);
    }

    private async Task<int> DeleteTenantRowsAsync<TEntity>(
        DbSet<TEntity> dbSet,
        Guid tenantId,
        CancellationToken cancellationToken)
        where TEntity : class
    {
        var query = dbSet.Where(entity => EF.Property<Guid>(entity, "TenantId") == tenantId);

        if (dbContext.Database.IsRelational())
        {
            return await query.ExecuteDeleteAsync(cancellationToken);
        }

        var rows = await query.ToListAsync(cancellationToken);
        if (rows.Count == 0)
        {
            return 0;
        }

        dbSet.RemoveRange(rows);
        await dbContext.SaveChangesAsync(cancellationToken);
        return rows.Count;
    }

    private async Task<bool> TryClearTenantGraphAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        try
        {
            await using var session = graphDriver.AsyncSession();
            await session.ExecuteWriteAsync(async transaction =>
            {
                await transaction.RunAsync(
                    """
                    MATCH (node:BaseNode { tenantId: $tenantId })
                    DETACH DELETE node
                    """,
                    new { tenantId = tenantId.ToString() });
            });

            cancellationToken.ThrowIfCancellationRequested();
            return true;
        }
        catch (Exception exception)
        {
            logger.LogWarning(
                exception,
                "Development demo cleanup could not clear graph memory for tenant {TenantId}. PostgreSQL cleanup still completed.",
                tenantId);
            return false;
        }
    }

    private static bool TryDeleteTenantStorageDirectory(string rootPath, Guid tenantId)
    {
        if (string.IsNullOrWhiteSpace(rootPath))
        {
            return false;
        }

        var root = Path.GetFullPath(rootPath);
        var tenantDirectory = Path.GetFullPath(Path.Combine(root, tenantId.ToString("N")));
        if (!tenantDirectory.StartsWith(root, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!Directory.Exists(tenantDirectory))
        {
            return false;
        }

        Directory.Delete(tenantDirectory, recursive: true);
        return true;
    }
}
