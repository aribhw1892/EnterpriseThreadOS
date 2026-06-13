using ETOS.Backend.AiTrace;
using ETOS.Backend.Artifacts;
using ETOS.Backend.Documents;
using ETOS.Backend.Governance;
using ETOS.Backend.Identity;
using ETOS.Backend.Infrastructure.Persistence;
using ETOS.Backend.Recommendations;
using Microsoft.EntityFrameworkCore;

namespace ETOS.Backend.Explorers;

public interface IGovernanceFlowService
{
    Task<GovernanceFlowResponse> BuildFlowAsync(
        ContextViewAnchorKind anchorKind,
        string anchorId,
        CancellationToken cancellationToken);
}

public sealed class GovernanceFlowService(
    EnterpriseThreadDbContext dbContext,
    ITenantContextResolver tenantContextResolver,
    IAccessPermissionService permissionService,
    IAccessDenialRecorder denialRecorder,
    IArtifactRegistryService artifactRegistryService,
    IAiTraceService aiTraceService) : IGovernanceFlowService
{
    public async Task<GovernanceFlowResponse> BuildFlowAsync(
        ContextViewAnchorKind anchorKind,
        string anchorId,
        CancellationToken cancellationToken)
    {
        var context = await RequirePermissionAsync(
            "explorers.governance_flow",
            ExplorerPermissions.GovernanceFlow,
            cancellationToken);

        var nodes = new List<GovernanceFlowNodeResponse>();
        var edges = new List<GovernanceFlowEdgeResponse>();
        var anchorNodeId = $"anchor:{anchorKind}:{anchorId}";
        nodes.Add(new GovernanceFlowNodeResponse(
            anchorNodeId,
            GovernanceFlowNodeKind.Anchor,
            $"{anchorKind} anchor",
            $"Governance flow anchor for {anchorKind} '{anchorId}'.",
            "active",
            BuildContextViewRoute(anchorKind, anchorId)));

        if (anchorKind == ContextViewAnchorKind.Artifact && Guid.TryParse(anchorId, out var artifactId))
        {
            await AddArtifactFlowAsync(context, artifactId, anchorNodeId, nodes, edges, cancellationToken);
        }
        else if (anchorKind == ContextViewAnchorKind.Document && Guid.TryParse(anchorId, out var documentId))
        {
            await AddDocumentFlowAsync(context, documentId, anchorNodeId, nodes, edges, cancellationToken);
        }
        else if (anchorKind == ContextViewAnchorKind.GraphNode && Guid.TryParse(anchorId, out var graphNodeId))
        {
            await AddGraphNodeFlowAsync(context, graphNodeId, anchorNodeId, nodes, edges, cancellationToken);
        }
        else if (anchorKind == ContextViewAnchorKind.ContextPackage && Guid.TryParse(anchorId, out var packageId))
        {
            await AddContextPackageFlowAsync(context, packageId, anchorNodeId, nodes, edges, cancellationToken);
        }
        else if (anchorKind == ContextViewAnchorKind.AiTrace && Guid.TryParse(anchorId, out var traceId))
        {
            await AddAiTraceFlowAsync(context, traceId, anchorNodeId, nodes, edges, cancellationToken);
        }

        var placeholders = BuildReviewChainPlaceholders(nodes);
        return new GovernanceFlowResponse(nodes, edges, placeholders);
    }

    private async Task AddArtifactFlowAsync(
        ActiveTenantContext context,
        Guid artifactId,
        string anchorNodeId,
        List<GovernanceFlowNodeResponse> nodes,
        List<GovernanceFlowEdgeResponse> edges,
        CancellationToken cancellationToken)
    {
        if (!await HasPermissionAsync(context, ArtifactPermissions.Read, cancellationToken))
        {
            return;
        }

        var artifact = await dbContext.Artifacts
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == artifactId && item.TenantId == context.TenantId, cancellationToken);
        if (artifact is null)
        {
            return;
        }

        var artifactNodeId = artifact.Id.ToString();
        var artifactRoute = artifact.ArtifactType.Equals(RecommendationArtifactTypes.Recommendation, StringComparison.OrdinalIgnoreCase)
            ? $"/recommendations/{artifact.Id}"
            : $"/artifacts/{artifact.Id}";
        nodes.Add(new GovernanceFlowNodeResponse(
            artifactNodeId,
            GovernanceFlowNodeKind.Artifact,
            artifact.Name,
            $"Artifact '{artifact.Name}' ({artifact.ArtifactType}).",
            artifact.LifecycleState.ToString(),
            artifactRoute));

        edges.Add(new GovernanceFlowEdgeResponse(
            Guid.NewGuid().ToString(),
            anchorNodeId,
            artifactNodeId,
            GovernanceFlowEdgeKind.Relationship,
            "anchor"));

        var relationships = await dbContext.ArtifactRelationships
            .AsNoTracking()
            .Where(relationship => relationship.TenantId == context.TenantId
                && (relationship.SourceArtifactId == artifactId || relationship.TargetArtifactId == artifactId))
            .Join(
                dbContext.Artifacts,
                relationship => relationship.SourceArtifactId == artifactId
                    ? relationship.TargetArtifactId
                    : relationship.SourceArtifactId,
                related => related.Id,
                (relationship, related) => new { relationship, related })
            .ToListAsync(cancellationToken);

        foreach (var pair in relationships)
        {
            var relatedNodeId = pair.related.Id.ToString();
            if (nodes.All(node => node.NodeId != relatedNodeId))
            {
                nodes.Add(new GovernanceFlowNodeResponse(
                    relatedNodeId,
                    GovernanceFlowNodeKind.Artifact,
                    pair.related.Name,
                    $"Related artifact '{pair.related.Name}'.",
                    pair.related.LifecycleState.ToString(),
                    $"/artifacts/{pair.related.Id}"));
            }

            edges.Add(new GovernanceFlowEdgeResponse(
                pair.relationship.Id.ToString(),
                artifactNodeId,
                relatedNodeId,
                GovernanceFlowEdgeKind.Relationship,
                pair.relationship.RelationshipType.ToString()));
        }

        var latestVersion = await dbContext.ArtifactVersions
            .AsNoTracking()
            .Where(version => version.TenantId == context.TenantId && version.ArtifactId == artifactId)
            .OrderByDescending(version => version.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (latestVersion is not null)
        {
            var versionNodeId = latestVersion.Id.ToString();
            var versionSummary = latestVersion.Summary ?? $"Version {latestVersion.VersionLabel}.";
            if (artifact.ArtifactType.Equals(RecommendationArtifactTypes.Recommendation, StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(latestVersion.PayloadJson))
            {
                var payload = RecommendationPayloadParser.Deserialize(latestVersion.PayloadJson);
                versionSummary = $"{payload.LifecycleStatus} · trust {payload.TrustState} · {payload.Summary}";
            }

            nodes.Add(new GovernanceFlowNodeResponse(
                versionNodeId,
                GovernanceFlowNodeKind.ArtifactVersion,
                latestVersion.VersionLabel,
                versionSummary,
                latestVersion.ReadinessState.ToString(),
                artifact.ArtifactType.Equals(RecommendationArtifactTypes.Recommendation, StringComparison.OrdinalIgnoreCase)
                    ? $"/recommendations/{artifact.Id}"
                    : null));

            edges.Add(new GovernanceFlowEdgeResponse(
                Guid.NewGuid().ToString(),
                artifactNodeId,
                versionNodeId,
                GovernanceFlowEdgeKind.Dependency,
                "latest-version"));

            if (await HasPermissionAsync(context, ArtifactPermissions.Read, cancellationToken))
            {
                var impact = await artifactRegistryService.GetImpactAsync(artifactId, latestVersion.Id, cancellationToken);
                foreach (var dependency in impact.Dependencies)
                {
                    var dependencyNodeId = $"dependency:{dependency.Id}";
                    nodes.Add(new GovernanceFlowNodeResponse(
                        dependencyNodeId,
                        GovernanceFlowNodeKind.Dependency,
                        dependency.RequiredArtifactName,
                        $"Requires {dependency.RequiredArtifactName} {dependency.RequiredVersionLabel}.",
                        dependency.RequiredReadinessState.ToString(),
                        $"/artifacts/{dependency.RequiredArtifactId}"));

                    edges.Add(new GovernanceFlowEdgeResponse(
                        dependency.Id.ToString(),
                        versionNodeId,
                        dependencyNodeId,
                        GovernanceFlowEdgeKind.Dependency,
                        dependency.DependencyKind.ToString()));
                }

                foreach (var dependent in impact.Dependents)
                {
                    var dependentNodeId = dependent.DependentArtifactId.ToString();
                    if (nodes.All(node => node.NodeId != dependentNodeId))
                    {
                        nodes.Add(new GovernanceFlowNodeResponse(
                            dependentNodeId,
                            GovernanceFlowNodeKind.Artifact,
                            dependent.DependentArtifactName,
                            $"Dependent artifact '{dependent.DependentArtifactName}'.",
                            "active",
                            $"/artifacts/{dependent.DependentArtifactId}"));
                    }

                    edges.Add(new GovernanceFlowEdgeResponse(
                        dependent.DependencyId.ToString(),
                        dependentNodeId,
                        versionNodeId,
                        GovernanceFlowEdgeKind.Dependency,
                        dependent.DependencyKind.ToString()));
                }
            }
        }

        await AddTraceLinksForObjectAsync(context, artifactId.ToString(), artifactNodeId, nodes, edges, cancellationToken);
        await AddAuditLinksAsync(context, nameof(Artifact), artifactId.ToString(), artifactNodeId, nodes, edges, cancellationToken);
    }

    private async Task AddDocumentFlowAsync(
        ActiveTenantContext context,
        Guid documentId,
        string anchorNodeId,
        List<GovernanceFlowNodeResponse> nodes,
        List<GovernanceFlowEdgeResponse> edges,
        CancellationToken cancellationToken)
    {
        var document = await dbContext.DocumentArtifacts
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == documentId && item.TenantId == context.TenantId, cancellationToken);
        if (document is null)
        {
            return;
        }

        var documentNodeId = document.Id.ToString();
        nodes.Add(new GovernanceFlowNodeResponse(
            documentNodeId,
            GovernanceFlowNodeKind.Document,
            document.Title,
            $"Document '{document.Title}'.",
            "active",
            $"/documents/{document.Id}"));

        edges.Add(new GovernanceFlowEdgeResponse(
            Guid.NewGuid().ToString(),
            anchorNodeId,
            documentNodeId,
            GovernanceFlowEdgeKind.EvidenceLink,
            "anchor"));

        var links = await dbContext.DocumentObjectLinks
            .AsNoTracking()
            .Where(link => link.TenantId == context.TenantId && link.DocumentArtifactId == documentId)
            .ToListAsync(cancellationToken);

        foreach (var link in links.Where(item => item.GraphNodeId.HasValue))
        {
            var graphNodeId = link.GraphNodeId!.Value.ToString();
            nodes.Add(new GovernanceFlowNodeResponse(
                graphNodeId,
                GovernanceFlowNodeKind.GraphNode,
                "Graph node",
                link.EvidenceSummary,
                link.ExtractionStatus.ToString(),
                $"/graph/{graphNodeId}"));

            edges.Add(new GovernanceFlowEdgeResponse(
                link.Id.ToString(),
                documentNodeId,
                graphNodeId,
                GovernanceFlowEdgeKind.EvidenceLink,
                "document-link"));
        }

        await AddTraceLinksForObjectAsync(context, documentId.ToString(), documentNodeId, nodes, edges, cancellationToken);
        await AddAuditLinksAsync(context, nameof(DocumentArtifact), documentId.ToString(), documentNodeId, nodes, edges, cancellationToken);
    }

    private async Task AddGraphNodeFlowAsync(
        ActiveTenantContext context,
        Guid graphNodeId,
        string anchorNodeId,
        List<GovernanceFlowNodeResponse> nodes,
        List<GovernanceFlowEdgeResponse> edges,
        CancellationToken cancellationToken)
    {
        var graphNode = $"graph:{graphNodeId}";
        nodes.Add(new GovernanceFlowNodeResponse(
            graphNode,
            GovernanceFlowNodeKind.GraphNode,
            "Graph node",
            $"Graph node {graphNodeId}.",
            "active",
            $"/graph/{graphNodeId}"));

        edges.Add(new GovernanceFlowEdgeResponse(
            Guid.NewGuid().ToString(),
            anchorNodeId,
            graphNode,
            GovernanceFlowEdgeKind.EvidenceLink,
            "anchor"));

        await AddTraceLinksForObjectAsync(context, graphNodeId.ToString(), graphNode, nodes, edges, cancellationToken);
    }

    private async Task AddContextPackageFlowAsync(
        ActiveTenantContext context,
        Guid packageId,
        string anchorNodeId,
        List<GovernanceFlowNodeResponse> nodes,
        List<GovernanceFlowEdgeResponse> edges,
        CancellationToken cancellationToken)
    {
        var package = await dbContext.ContextPackages
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == packageId && item.TenantId == context.TenantId, cancellationToken);
        if (package is null)
        {
            return;
        }

        var packageNodeId = package.Id.ToString();
        nodes.Add(new GovernanceFlowNodeResponse(
            packageNodeId,
            GovernanceFlowNodeKind.Anchor,
            "Context package",
            package.SafeSummary,
            "completed",
            $"/context-packages/{package.Id}"));

        edges.Add(new GovernanceFlowEdgeResponse(
            Guid.NewGuid().ToString(),
            anchorNodeId,
            packageNodeId,
            GovernanceFlowEdgeKind.TraceLink,
            "context-package"));

        if (await HasPermissionAsync(context, AiTracePermissions.Read, cancellationToken))
        {
            var trace = await aiTraceService.GetTraceByRetrievalRunAsync(package.RetrievalRunId, cancellationToken);
            if (trace is not null)
            {
                var traceNodeId = trace.Id.ToString();
                nodes.Add(new GovernanceFlowNodeResponse(
                    traceNodeId,
                    GovernanceFlowNodeKind.AiTrace,
                    trace.IntentKey,
                    trace.SafeSummary,
                    trace.Status,
                    $"/ai-traces/{trace.Id}"));

                edges.Add(new GovernanceFlowEdgeResponse(
                    Guid.NewGuid().ToString(),
                    packageNodeId,
                    traceNodeId,
                    GovernanceFlowEdgeKind.TraceLink,
                    "retrieval-run"));
            }
        }
    }

    private async Task AddAiTraceFlowAsync(
        ActiveTenantContext context,
        Guid traceId,
        string anchorNodeId,
        List<GovernanceFlowNodeResponse> nodes,
        List<GovernanceFlowEdgeResponse> edges,
        CancellationToken cancellationToken)
    {
        if (!await HasPermissionAsync(context, AiTracePermissions.Read, cancellationToken))
        {
            return;
        }

        var trace = await aiTraceService.GetTraceAsync(traceId, cancellationToken);
        var traceNodeId = trace.Id.ToString();
        nodes.Add(new GovernanceFlowNodeResponse(
            traceNodeId,
            GovernanceFlowNodeKind.AiTrace,
            trace.IntentKey,
            trace.SafeSummary,
            trace.Status,
            $"/ai-traces/{trace.Id}"));

        edges.Add(new GovernanceFlowEdgeResponse(
            Guid.NewGuid().ToString(),
            anchorNodeId,
            traceNodeId,
            GovernanceFlowEdgeKind.TraceLink,
            "anchor"));

        foreach (var link in trace.ArtifactLinks)
        {
            var linkedNodeId = $"{link.LinkKind}:{link.ObjectId}";
            nodes.Add(new GovernanceFlowNodeResponse(
                linkedNodeId,
                MapLinkKind(link.LinkKind),
                link.ObjectType,
                $"{link.LinkKind} link to {link.ObjectType}.",
                "linked",
                BuildLinkRoute(link.LinkKind, link.ObjectId)));

            edges.Add(new GovernanceFlowEdgeResponse(
                link.Id.ToString(),
                traceNodeId,
                linkedNodeId,
                GovernanceFlowEdgeKind.TraceLink,
                link.LinkKind.ToString()));
        }
    }

    private async Task AddTraceLinksForObjectAsync(
        ActiveTenantContext context,
        string objectId,
        string fromNodeId,
        List<GovernanceFlowNodeResponse> nodes,
        List<GovernanceFlowEdgeResponse> edges,
        CancellationToken cancellationToken)
    {
        if (!await HasPermissionAsync(context, AiTracePermissions.Read, cancellationToken))
        {
            return;
        }

        var links = await dbContext.AiTraceArtifactLinks
            .AsNoTracking()
            .Where(link => link.TenantId == context.TenantId && link.ObjectId == objectId)
            .Join(
                dbContext.AiTraceRecords,
                link => link.AiTraceRecordId,
                trace => trace.Id,
                (link, trace) => new { link, trace })
            .ToListAsync(cancellationToken);

        foreach (var pair in links)
        {
            var traceNodeId = pair.trace.Id.ToString();
            if (nodes.All(node => node.NodeId != traceNodeId))
            {
                nodes.Add(new GovernanceFlowNodeResponse(
                    traceNodeId,
                    GovernanceFlowNodeKind.AiTrace,
                    pair.trace.IntentKey,
                    pair.trace.SafeSummary,
                    pair.trace.Status,
                    $"/ai-traces/{pair.trace.Id}"));
            }

            edges.Add(new GovernanceFlowEdgeResponse(
                pair.link.Id.ToString(),
                fromNodeId,
                traceNodeId,
                GovernanceFlowEdgeKind.TraceLink,
                pair.link.LinkKind.ToString()));
        }
    }

    private async Task AddAuditLinksAsync(
        ActiveTenantContext context,
        string sourceObjectType,
        string sourceObjectId,
        string fromNodeId,
        List<GovernanceFlowNodeResponse> nodes,
        List<GovernanceFlowEdgeResponse> edges,
        CancellationToken cancellationToken)
    {
        if (!await HasPermissionAsync(context, IdentityPermissions.IdentityAdmin, cancellationToken))
        {
            return;
        }

        var auditRecords = await dbContext.AuditRecords
            .AsNoTracking()
            .Where(record => record.TenantId == context.TenantId
                && record.SourceObjectType == sourceObjectType
                && record.SourceObjectId == sourceObjectId)
            .OrderByDescending(record => record.CreatedAt)
            .Take(10)
            .ToListAsync(cancellationToken);

        foreach (var record in auditRecords)
        {
            var auditNodeId = record.Id.ToString();
            nodes.Add(new GovernanceFlowNodeResponse(
                auditNodeId,
                GovernanceFlowNodeKind.AuditRecord,
                record.Action,
                record.SafeSummary,
                record.Result.ToString(),
                null));

            edges.Add(new GovernanceFlowEdgeResponse(
                record.Id.ToString(),
                fromNodeId,
                auditNodeId,
                GovernanceFlowEdgeKind.AuditLink,
                record.Action));
        }
    }

    private static IReadOnlyCollection<GovernanceFlowPlaceholderResponse> BuildReviewChainPlaceholders(
        IReadOnlyCollection<GovernanceFlowNodeResponse> nodes)
    {
        var hasRecommendationNode = nodes.Any(node =>
            node.Title.Contains("Recommendation", StringComparison.OrdinalIgnoreCase)
            || node.SafeSummary.Contains(RecommendationArtifactTypes.Recommendation, StringComparison.OrdinalIgnoreCase));

        var placeholders = new List<GovernanceFlowPlaceholderResponse>();
        if (!hasRecommendationNode)
        {
            placeholders.Add(new GovernanceFlowPlaceholderResponse(
                GovernanceFlowPlaceholderKind.Recommendation,
                "Recommendation",
                "available",
                "Milestone 4",
                "Create a recommendation artifact from evidence to begin the review chain."));
        }

        placeholders.AddRange(
        [
            new GovernanceFlowPlaceholderResponse(
                GovernanceFlowPlaceholderKind.ReviewTask,
                "Review task",
                "not_implemented",
                "Milestone 4",
                "Review task workflow is planned for Milestone 4."),
            new GovernanceFlowPlaceholderResponse(
                GovernanceFlowPlaceholderKind.Decision,
                "Decision",
                "not_implemented",
                "Milestone 4",
                "Decision artifact lifecycle is planned for Milestone 4."),
            new GovernanceFlowPlaceholderResponse(
                GovernanceFlowPlaceholderKind.OutcomeCheck,
                "Outcome check",
                "not_implemented",
                "Milestone 4",
                "Outcome verification is planned for Milestone 4."),
            new GovernanceFlowPlaceholderResponse(
                GovernanceFlowPlaceholderKind.LearningSignal,
                "Learning signal",
                "not_implemented",
                "Milestone 4",
                "Learning signal capture is planned for Milestone 4.")
        ]);

        return placeholders;
    }

    private static GovernanceFlowNodeKind MapLinkKind(AiTraceArtifactLinkKind linkKind)
    {
        return linkKind switch
        {
            AiTraceArtifactLinkKind.GraphNode => GovernanceFlowNodeKind.GraphNode,
            AiTraceArtifactLinkKind.DocumentArtifact => GovernanceFlowNodeKind.Document,
            AiTraceArtifactLinkKind.ContextPackage => GovernanceFlowNodeKind.Anchor,
            AiTraceArtifactLinkKind.DraftArtifact => GovernanceFlowNodeKind.Artifact,
            _ => GovernanceFlowNodeKind.Artifact
        };
    }

    private static string? BuildLinkRoute(AiTraceArtifactLinkKind linkKind, string objectId)
    {
        return linkKind switch
        {
            AiTraceArtifactLinkKind.GraphNode => $"/graph/{objectId}",
            AiTraceArtifactLinkKind.DocumentArtifact => $"/documents/{objectId}",
            AiTraceArtifactLinkKind.ContextPackage => $"/context-packages/{objectId}",
            AiTraceArtifactLinkKind.DraftArtifact => $"/artifacts/{objectId}",
            _ => null
        };
    }

    private static string BuildContextViewRoute(ContextViewAnchorKind anchorKind, string anchorId)
    {
        return anchorKind switch
        {
            ContextViewAnchorKind.Artifact => $"/artifacts/{anchorId}",
            ContextViewAnchorKind.Document => $"/documents/{anchorId}",
            ContextViewAnchorKind.GraphNode => $"/graph/{anchorId}",
            ContextViewAnchorKind.ContextPackage => $"/context-packages/{anchorId}",
            ContextViewAnchorKind.AiTrace => $"/ai-traces/{anchorId}",
            _ => $"/explorers?anchorKind={anchorKind}&anchorId={anchorId}"
        };
    }

    private async Task<bool> HasPermissionAsync(
        ActiveTenantContext context,
        string permissionKey,
        CancellationToken cancellationToken)
    {
        return await permissionService.HasPermissionAsync(context.TenantId, context.UserId, permissionKey, cancellationToken);
    }

    private async Task<ActiveTenantContext> RequirePermissionAsync(
        string action,
        string permissionKey,
        CancellationToken cancellationToken)
    {
        var context = await tenantContextResolver.ResolveAsync(action, cancellationToken);
        if (!await permissionService.HasPermissionAsync(context.TenantId, context.UserId, permissionKey, cancellationToken))
        {
            await denialRecorder.RecordAsync(
                context.TenantId,
                context.UserId,
                action,
                "permission_denied",
                $"The user lacks the {permissionKey} permission.",
                cancellationToken);
            throw new TenantAccessDeniedException("User lacks governance flow permission.");
        }

        return context;
    }
}
