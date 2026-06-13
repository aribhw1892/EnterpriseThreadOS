using ETOS.Backend.AiTrace;
using ETOS.Backend.Artifacts;
using ETOS.Backend.DataQuality;
using ETOS.Backend.Documents;
using ETOS.Backend.GovernedQuery;
using ETOS.Backend.Identity;
using ETOS.Backend.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ETOS.Backend.Explorers;

public interface IContextViewService
{
    Task<ContextView360Response> GetContextViewAsync(
        ContextViewAnchorKind anchorKind,
        string anchorId,
        string? policyKey,
        CancellationToken cancellationToken);
}

public sealed class ContextViewService(
    EnterpriseThreadDbContext dbContext,
    ITenantContextResolver tenantContextResolver,
    IAccessPermissionService permissionService,
    IAccessDenialRecorder denialRecorder,
    IArtifactRegistryService artifactRegistryService,
    IDocumentService documentService,
    IGovernedQueryService governedQueryService,
    IAiTraceService aiTraceService,
    IGovernanceFlowService governanceFlowService) : IContextViewService
{
    public async Task<ContextView360Response> GetContextViewAsync(
        ContextViewAnchorKind anchorKind,
        string anchorId,
        string? policyKey,
        CancellationToken cancellationToken)
    {
        var context = await RequirePermissionAsync(
            "explorers.context_view",
            ExplorerPermissions.ContextView,
            cancellationToken);

        var sections = anchorKind switch
        {
            ContextViewAnchorKind.Artifact when Guid.TryParse(anchorId, out var artifactId)
                => await BuildArtifactSectionsAsync(context, artifactId, policyKey, cancellationToken),
            ContextViewAnchorKind.Document when Guid.TryParse(anchorId, out var documentId)
                => await BuildDocumentSectionsAsync(context, documentId, policyKey, cancellationToken),
            ContextViewAnchorKind.GraphNode when Guid.TryParse(anchorId, out var graphNodeId)
                => await BuildGraphNodeSectionsAsync(context, graphNodeId, policyKey, cancellationToken),
            ContextViewAnchorKind.ContextPackage when Guid.TryParse(anchorId, out var packageId)
                => await BuildContextPackageSectionsAsync(context, packageId, policyKey, cancellationToken),
            ContextViewAnchorKind.AiTrace when Guid.TryParse(anchorId, out var traceId)
                => await BuildAiTraceSectionsAsync(context, traceId, policyKey, cancellationToken),
            _ => throw new RequestValidationException("Anchor id is invalid.")
        };

        GovernanceFlowResponse? governanceFlow = null;
        if (await HasPermissionAsync(context, ExplorerPermissions.GovernanceFlow, cancellationToken))
        {
            governanceFlow = await governanceFlowService.BuildFlowAsync(anchorKind, anchorId, cancellationToken);
        }

        var title = sections.FirstOrDefault(section => section.SectionKey == "overview")?.Items.FirstOrDefault()?.Title
            ?? $"{anchorKind} context";
        var summary = sections.FirstOrDefault(section => section.SectionKey == "overview")?.Items.FirstOrDefault()?.SafeSummary
            ?? $"360° context view for {anchorKind} '{anchorId}'.";

        return new ContextView360Response(
            anchorKind,
            anchorId,
            title,
            summary,
            sections,
            governanceFlow,
            BuildFilterSummary(sections, policyKey));
    }

    private async Task<IReadOnlyCollection<ContextViewSectionResponse>> BuildArtifactSectionsAsync(
        ActiveTenantContext context,
        Guid artifactId,
        string? policyKey,
        CancellationToken cancellationToken)
    {
        var artifact = await dbContext.Artifacts
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == artifactId, cancellationToken)
            ?? throw new RequestValidationException("Artifact was not found.");

        await EnsureSameTenantAsync(artifact.TenantId, context, cancellationToken);

        var sections = new List<ContextViewSectionResponse>
        {
            await BuildOverviewSectionAsync(
                context,
                ArtifactPermissions.Read,
                "overview",
                "Overview",
                async () =>
                {
                    var detail = await artifactRegistryService.GetArtifactAsync(artifactId, cancellationToken);
                    var latest = detail.Versions.OrderByDescending(version => version.CreatedAt).FirstOrDefault();
                    return
                    [
                        new ContextViewItemResponse(
                            artifact.Id.ToString(),
                            detail.ArtifactType,
                            detail.Name,
                            detail.Description ?? $"Artifact '{detail.Name}' ({detail.ArtifactType}).",
                            $"/artifacts/{artifact.Id}",
                            new Dictionary<string, string>
                            {
                                ["lifecycleState"] = detail.LifecycleState.ToString(),
                                ["ownerUserId"] = detail.OwnerUserId.ToString(),
                                ["latestVersion"] = latest?.VersionLabel ?? "none",
                                ["readinessState"] = latest?.ReadinessState.ToString() ?? "unknown"
                            })
                    ];
                },
                cancellationToken)
        };

        sections.Add(await BuildSectionAsync(
            context,
            ArtifactPermissions.Read,
            "relationships",
            "Relationships",
            async () =>
            {
                var relationships = await artifactRegistryService.ListRelationshipsAsync(artifactId, cancellationToken);
                return relationships
                    .GroupBy(relationship => relationship.RelationshipType.ToString())
                    .SelectMany(group => group.Select(relationship => new ContextViewItemResponse(
                        relationship.Id.ToString(),
                        group.Key,
                        relationship.TargetArtifactName,
                        relationship.Description ?? $"{group.Key} relationship to {relationship.TargetArtifactName}.",
                        $"/artifacts/{relationship.TargetArtifactId}",
                        new Dictionary<string, string> { ["relationshipType"] = group.Key })))
                    .ToList();
            },
            cancellationToken));

        sections.Add(await BuildSectionAsync(
            context,
            DocumentPermissions.Read,
            "evidence",
            "Evidence",
            async () =>
            {
                var document = await dbContext.DocumentArtifacts
                    .AsNoTracking()
                    .SingleOrDefaultAsync(item => item.TenantId == context.TenantId && item.ArtifactId == artifactId, cancellationToken);
                if (document is null)
                {
                    return [];
                }

                var detail = await documentService.GetDocumentAsync(document.Id, policyKey, cancellationToken);
                return detail.ObjectLinks.Select(link => new ContextViewItemResponse(
                    link.Id.ToString(),
                    "document-link",
                    document.Title,
                    link.EvidenceSummary,
                    link.GraphNodeId.HasValue ? $"/graph/{link.GraphNodeId}" : $"/documents/{document.Id}",
                    new Dictionary<string, string>
                    {
                        ["confidenceScore"] = link.ConfidenceScore.ToString("0.##"),
                        ["extractionStatus"] = link.ExtractionStatus.ToString()
                    })).ToList();
            },
            cancellationToken));

        sections.Add(await BuildAiTraceSectionAsync(context, artifactId.ToString(), cancellationToken));
        sections.Add(await BuildAuditSectionAsync(context, nameof(Artifact), artifactId.ToString(), cancellationToken));

        sections.Add(await BuildSectionAsync(
            context,
            ArtifactPermissions.Read,
            "versions",
            "Versions",
            async () =>
            {
                var versions = await artifactRegistryService.ListVersionsAsync(artifactId, cancellationToken);
                return versions.Select(version => new ContextViewItemResponse(
                    version.Id.ToString(),
                    "artifact-version",
                    version.VersionLabel,
                    version.Summary ?? $"Version {version.VersionLabel}.",
                    null,
                    new Dictionary<string, string>
                    {
                        ["readinessState"] = version.ReadinessState.ToString(),
                        ["compatibilityStatus"] = version.CompatibilityStatus.ToString()
                    })).ToList();
            },
            cancellationToken));

        sections.Add(await BuildSectionAsync(
            context,
            ArtifactPermissions.Read,
            "dependencies",
            "Dependencies",
            async () =>
            {
                var latestVersion = await dbContext.ArtifactVersions
                    .AsNoTracking()
                    .Where(version => version.TenantId == context.TenantId && version.ArtifactId == artifactId)
                    .OrderByDescending(version => version.CreatedAt)
                    .FirstOrDefaultAsync(cancellationToken);
                if (latestVersion is null)
                {
                    return [];
                }

                var impact = await artifactRegistryService.GetImpactAsync(artifactId, latestVersion.Id, cancellationToken);
                var items = impact.Dependencies.Select(dependency => new ContextViewItemResponse(
                    dependency.Id.ToString(),
                    "dependency",
                    dependency.RequiredArtifactName,
                    $"Requires {dependency.RequiredArtifactName} {dependency.RequiredVersionLabel}.",
                    $"/artifacts/{dependency.RequiredArtifactId}",
                    new Dictionary<string, string> { ["dependencyKind"] = dependency.DependencyKind.ToString() })).ToList();
                items.AddRange(impact.Dependents.Select(dependent => new ContextViewItemResponse(
                    dependent.DependencyId.ToString(),
                    "dependent",
                    dependent.DependentArtifactName,
                    $"Dependent version {dependent.DependentVersionLabel}.",
                    $"/artifacts/{dependent.DependentArtifactId}",
                    new Dictionary<string, string> { ["dependencyKind"] = dependent.DependencyKind.ToString() })));
                return items;
            },
            cancellationToken));

        sections.Add(await BuildIssuesSectionAsync(context, artifactId.ToString(), null, cancellationToken));
        sections.Add(await BuildContextPackagesSectionAsync(context, null, artifactId, cancellationToken));

        return sections;
    }

    private async Task<IReadOnlyCollection<ContextViewSectionResponse>> BuildDocumentSectionsAsync(
        ActiveTenantContext context,
        Guid documentId,
        string? policyKey,
        CancellationToken cancellationToken)
    {
        var document = await dbContext.DocumentArtifacts
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == documentId, cancellationToken)
            ?? throw new RequestValidationException("Document was not found.");

        await EnsureSameTenantAsync(document.TenantId, context, cancellationToken);

        var detail = await documentService.GetDocumentAsync(documentId, policyKey, cancellationToken);
        var sections = new List<ContextViewSectionResponse>
        {
            await BuildOverviewSectionAsync(
                context,
                DocumentPermissions.Read,
                "overview",
                "Overview",
                () => Task.FromResult<IReadOnlyCollection<ContextViewItemResponse>>(
                [
                    new ContextViewItemResponse(
                        detail.Id.ToString(),
                        detail.DocumentType,
                        detail.Title,
                        detail.Description ?? $"Document '{detail.Title}'.",
                        $"/documents/{detail.Id}",
                        new Dictionary<string, string>
                        {
                            ["classificationKey"] = detail.ClassificationKey,
                            ["ownerUserId"] = detail.OwnerUserId.ToString(),
                            ["versionCount"] = detail.Versions.Count.ToString()
                        })
                ]),
                cancellationToken),
            await BuildSectionAsync(
                context,
                DocumentPermissions.Read,
                "relationships",
                "Relationships",
                () => Task.FromResult<IReadOnlyCollection<ContextViewItemResponse>>(detail.ObjectLinks.Select(link => new ContextViewItemResponse(
                    link.Id.ToString(),
                    "document-object-link",
                    link.GraphNodeId.HasValue ? "Graph node link" : "Import link",
                    link.EvidenceSummary,
                    link.GraphNodeId.HasValue ? $"/graph/{link.GraphNodeId}" : null,
                    new Dictionary<string, string>
                    {
                        ["confidenceScore"] = link.ConfidenceScore.ToString("0.##"),
                        ["extractionStatus"] = link.ExtractionStatus.ToString()
                    })).ToList()),
                cancellationToken),
            await BuildSectionAsync(
                context,
                DocumentPermissions.Read,
                "evidence",
                "Evidence",
                () => Task.FromResult<IReadOnlyCollection<ContextViewItemResponse>>(detail.ObjectLinks.Select(link => new ContextViewItemResponse(
                    link.Id.ToString(),
                    "evidence",
                    detail.Title,
                    link.EvidenceSummary,
                    link.GraphNodeId.HasValue ? $"/graph/{link.GraphNodeId}" : $"/documents/{detail.Id}",
                    null)).ToList()),
                cancellationToken),
            await BuildAiTraceSectionAsync(context, documentId.ToString(), cancellationToken),
            await BuildAuditSectionAsync(context, nameof(DocumentArtifact), documentId.ToString(), cancellationToken),
            await BuildSectionAsync(
                context,
                DocumentPermissions.Read,
                "versions",
                "Versions",
                () => Task.FromResult<IReadOnlyCollection<ContextViewItemResponse>>(detail.Versions.Select(version => new ContextViewItemResponse(
                    version.Id.ToString(),
                    "document-version",
                    version.VersionLabel,
                    version.ExtractionStatus.ToString(),
                    null,
                    new Dictionary<string, string>
                    {
                        ["contentType"] = version.ContentType,
                        ["sizeBytes"] = version.SizeBytes.ToString()
                    })).ToList()),
                cancellationToken),
            await BuildIssuesSectionAsync(context, documentId.ToString(), null, cancellationToken),
            await BuildContextPackagesSectionAsync(context, documentId, null, cancellationToken)
        };

        return sections;
    }

    private async Task<IReadOnlyCollection<ContextViewSectionResponse>> BuildGraphNodeSectionsAsync(
        ActiveTenantContext context,
        Guid graphNodeId,
        string? policyKey,
        CancellationToken cancellationToken)
    {
        ContextPackageResponse? package = null;
        RetrievalRunResponse? run = null;

        if (await HasPermissionAsync(context, GovernedQueryPermissions.Run, cancellationToken))
        {
            run = await governedQueryService.RunAsync(
                new RunGovernedQueryRequest("object-360-context", graphNodeId, null, policyKey, null, 2, CreateAiTrace: false),
                cancellationToken);
            package = run.ContextPackage;
        }
        else if (await HasPermissionAsync(context, GovernedQueryPermissions.Read, cancellationToken))
        {
            var latestRun = await dbContext.RetrievalRuns
                .AsNoTracking()
                .Where(item => item.TenantId == context.TenantId && item.StartGraphNodeId == graphNodeId)
                .OrderByDescending(item => item.CreatedAt)
                .FirstOrDefaultAsync(cancellationToken);
            if (latestRun is not null)
            {
                run = await governedQueryService.GetRunAsync(latestRun.Id, cancellationToken);
                package = run.ContextPackage;
            }
        }

        var sections = new List<ContextViewSectionResponse>
        {
            new(
                "overview",
                "Overview",
                ContextViewSectionVisibility.Visible,
                null,
                [
                    new ContextViewItemResponse(
                        graphNodeId.ToString(),
                        "graph-node",
                        "Graph node",
                        package?.SafeSummary ?? $"Graph node {graphNodeId}.",
                        $"/graph/{graphNodeId}",
                        new Dictionary<string, string>
                        {
                            ["intentKey"] = run?.QueryIntent.IntentKey ?? "object-360-context",
                            ["retrievedCount"] = (run?.RetrievedCount ?? 0).ToString()
                        })
                ],
                null)
        };

        sections.Add(BuildGovernedContextSection(
            context,
            GovernedQueryPermissions.Read,
            "relationships",
            "Relationships",
            package?.LlmVisibleContext.Where(item => item.SourceKind == "Graph").ToList() ?? []));

        sections.Add(BuildGovernedContextSection(
            context,
            GovernedQueryPermissions.Read,
            "evidence",
            "Evidence",
            package?.LlmVisibleContext ?? []));

        sections.Add(await BuildAiTraceSectionForRunAsync(context, run?.Id, cancellationToken));
        sections.Add(await BuildAuditSectionAsync(context, "GraphNode", graphNodeId.ToString(), cancellationToken));
        sections.Add(package is null
            ? EmptySection("context-packages", "Context packages")
            : new ContextViewSectionResponse(
                "context-packages",
                "Context packages",
                ContextViewSectionVisibility.Visible,
                null,
                [
                    new ContextViewItemResponse(
                        package.Id.ToString(),
                        "context-package",
                        run?.QueryIntent.IntentKey ?? "object-360-context",
                        package.SafeSummary,
                        $"/context-packages/{package.Id}",
                        new Dictionary<string, string>
                        {
                            ["allowedCount"] = package.AllowedCount.ToString(),
                            ["deniedCount"] = package.DeniedCount.ToString()
                        })
                ],
                null));

        sections.Add(await BuildIssuesSectionAsync(context, null, graphNodeId, cancellationToken));

        return sections;
    }

    private async Task<IReadOnlyCollection<ContextViewSectionResponse>> BuildContextPackageSectionsAsync(
        ActiveTenantContext context,
        Guid packageId,
        string? policyKey,
        CancellationToken cancellationToken)
    {
        if (!await HasPermissionAsync(context, GovernedQueryPermissions.Read, cancellationToken))
        {
            return [DeniedSection("overview", "Overview", GovernedQueryPermissions.Read)];
        }

        var package = await governedQueryService.GetContextPackageAsync(packageId, cancellationToken);
        var run = await dbContext.RetrievalRuns
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == package.RetrievalRunId && item.TenantId == context.TenantId, cancellationToken)
            ?? throw new RequestValidationException("Retrieval run was not found.");

        return
        [
            new ContextViewSectionResponse(
                "overview",
                "Overview",
                ContextViewSectionVisibility.Visible,
                null,
                [
                    new ContextViewItemResponse(
                        package.Id.ToString(),
                        "context-package",
                        run.QueryText,
                        package.SafeSummary,
                        $"/context-packages/{package.Id}",
                        new Dictionary<string, string>
                        {
                            ["allowedCount"] = package.AllowedCount.ToString(),
                            ["deniedCount"] = package.DeniedCount.ToString(),
                            ["policyKey"] = package.PolicyKey ?? "default"
                        })
                ],
                null),
            BuildGovernedContextSection(context, GovernedQueryPermissions.Read, "evidence", "Evidence", package.LlmVisibleContext),
            BuildGovernedContextSection(context, GovernedQueryPermissions.Read, "relationships", "Relationships", package.FilteredContext),
            await BuildAiTraceSectionForRunAsync(context, run.Id, cancellationToken),
            new ContextViewSectionResponse(
                "context-packages",
                "Context packages",
                ContextViewSectionVisibility.Visible,
                null,
                [
                    new ContextViewItemResponse(
                        package.Id.ToString(),
                        "context-package",
                        "Retrieval package",
                        package.SafeSummary,
                        $"/context-packages/{package.Id}",
                        new Dictionary<string, string> { ["deniedCount"] = package.DeniedCount.ToString() })
                ],
                null)
        ];
    }

    private async Task<IReadOnlyCollection<ContextViewSectionResponse>> BuildAiTraceSectionsAsync(
        ActiveTenantContext context,
        Guid traceId,
        string? policyKey,
        CancellationToken cancellationToken)
    {
        if (!await HasPermissionAsync(context, AiTracePermissions.Read, cancellationToken))
        {
            return [DeniedSection("overview", "Overview", AiTracePermissions.Read)];
        }

        var trace = await aiTraceService.GetTraceAsync(traceId, cancellationToken);
        return
        [
            new ContextViewSectionResponse(
                "overview",
                "Overview",
                ContextViewSectionVisibility.Visible,
                null,
                [
                    new ContextViewItemResponse(
                        trace.Id.ToString(),
                        trace.TraceKind.ToString(),
                        trace.IntentKey,
                        trace.SafeSummary,
                        $"/ai-traces/{trace.Id}",
                        new Dictionary<string, string>
                        {
                            ["strategyKey"] = trace.StrategyKey,
                            ["status"] = trace.Status
                        })
                ],
                null),
            new ContextViewSectionResponse(
                "ai-trace",
                "AI trace",
                ContextViewSectionVisibility.Visible,
                null,
                trace.ArtifactLinks.Select(link => new ContextViewItemResponse(
                    link.Id.ToString(),
                    link.LinkKind.ToString(),
                    link.ObjectType,
                    $"{link.LinkKind} link.",
                    BuildTraceLinkRoute(link.LinkKind, link.ObjectId),
                    new Dictionary<string, string> { ["objectId"] = link.ObjectId })).ToList(),
                null),
            new ContextViewSectionResponse(
                "evidence",
                "Evidence",
                ContextViewSectionVisibility.Visible,
                null,
                trace.FilteredSummaries.Select(item => new ContextViewItemResponse(
                    item.ContextId,
                    item.ContextType,
                    item.SourceKind,
                    item.SafeSummary,
                    null,
                    null)).ToList(),
                null),
            await BuildAuditSectionAsync(context, nameof(AiTraceRecord), traceId.ToString(), cancellationToken)
        ];
    }

    private async Task<ContextViewSectionResponse> BuildOverviewSectionAsync(
        ActiveTenantContext context,
        string permissionKey,
        string sectionKey,
        string title,
        Func<Task<IReadOnlyCollection<ContextViewItemResponse>>> buildItems,
        CancellationToken cancellationToken)
    {
        return await BuildSectionAsync(context, permissionKey, sectionKey, title, buildItems, cancellationToken);
    }

    private async Task<ContextViewSectionResponse> BuildSectionAsync(
        ActiveTenantContext context,
        string permissionKey,
        string sectionKey,
        string title,
        Func<Task<IReadOnlyCollection<ContextViewItemResponse>>> buildItems,
        CancellationToken cancellationToken)
    {
        if (!await HasPermissionAsync(context, permissionKey, cancellationToken))
        {
            return DeniedSection(sectionKey, title, permissionKey);
        }

        var items = await buildItems();
        return new ContextViewSectionResponse(
            sectionKey,
            title,
            items.Count == 0 ? ContextViewSectionVisibility.Empty : ContextViewSectionVisibility.Visible,
            null,
            items,
            null);
    }

    private static ContextViewSectionResponse BuildGovernedContextSection(
        ActiveTenantContext context,
        string permissionKey,
        string sectionKey,
        string title,
        IReadOnlyCollection<ContextItemResponse> items)
    {
        _ = context;
        _ = permissionKey;
        return new ContextViewSectionResponse(
            sectionKey,
            title,
            items.Count == 0 ? ContextViewSectionVisibility.Empty : ContextViewSectionVisibility.Visible,
            null,
            items.Select(item => new ContextViewItemResponse(
                item.ContextId,
                item.ContextType,
                item.SourceKind,
                item.SafeSummary,
                item.SourceKind == "Graph" ? $"/graph/{item.ContextId}" : null,
                new Dictionary<string, string>
                {
                    ["classificationKey"] = item.ClassificationKey,
                    ["sourceKind"] = item.SourceKind
                })).ToList(),
            null);
    }

    private async Task<ContextViewSectionResponse> BuildAiTraceSectionAsync(
        ActiveTenantContext context,
        string objectId,
        CancellationToken cancellationToken)
    {
        if (!await HasPermissionAsync(context, AiTracePermissions.Read, cancellationToken))
        {
            return DeniedSection("ai-trace", "AI trace", AiTracePermissions.Read);
        }

        var links = await dbContext.AiTraceArtifactLinks
            .AsNoTracking()
            .Where(link => link.TenantId == context.TenantId && link.ObjectId == objectId)
            .Join(
                dbContext.AiTraceRecords,
                link => link.AiTraceRecordId,
                trace => trace.Id,
                (link, trace) => new { link, trace })
            .OrderByDescending(pair => pair.trace.CreatedAt)
            .Take(10)
            .ToListAsync(cancellationToken);

        var items = links.Select(pair => new ContextViewItemResponse(
            pair.trace.Id.ToString(),
            pair.trace.TraceKind.ToString(),
            pair.trace.IntentKey,
            pair.trace.SafeSummary,
            $"/ai-traces/{pair.trace.Id}",
            new Dictionary<string, string> { ["linkKind"] = pair.link.LinkKind.ToString() })).ToList();

        return new ContextViewSectionResponse(
            "ai-trace",
            "AI trace",
            items.Count == 0 ? ContextViewSectionVisibility.Empty : ContextViewSectionVisibility.Visible,
            null,
            items,
            null);
    }

    private async Task<ContextViewSectionResponse> BuildAiTraceSectionForRunAsync(
        ActiveTenantContext context,
        Guid? runId,
        CancellationToken cancellationToken)
    {
        if (!await HasPermissionAsync(context, AiTracePermissions.Read, cancellationToken))
        {
            return DeniedSection("ai-trace", "AI trace", AiTracePermissions.Read);
        }

        if (runId is null)
        {
            return EmptySection("ai-trace", "AI trace");
        }

        var trace = await aiTraceService.GetTraceByRetrievalRunAsync(runId.Value, cancellationToken);
        if (trace is null)
        {
            return EmptySection("ai-trace", "AI trace");
        }

        return new ContextViewSectionResponse(
            "ai-trace",
            "AI trace",
            ContextViewSectionVisibility.Visible,
            null,
            [
                new ContextViewItemResponse(
                    trace.Id.ToString(),
                    trace.TraceKind.ToString(),
                    trace.IntentKey,
                    trace.SafeSummary,
                    $"/ai-traces/{trace.Id}",
                    new Dictionary<string, string> { ["retrievalRunId"] = runId.Value.ToString() })
            ],
            null);
    }

    private async Task<ContextViewSectionResponse> BuildAuditSectionAsync(
        ActiveTenantContext context,
        string sourceObjectType,
        string sourceObjectId,
        CancellationToken cancellationToken)
    {
        if (!await HasPermissionAsync(context, IdentityPermissions.IdentityAdmin, cancellationToken))
        {
            return DeniedSection("audit", "Audit", IdentityPermissions.IdentityAdmin);
        }

        var records = await dbContext.AuditRecords
            .AsNoTracking()
            .Where(record => record.TenantId == context.TenantId
                && record.SourceObjectType == sourceObjectType
                && record.SourceObjectId == sourceObjectId)
            .OrderByDescending(record => record.CreatedAt)
            .Take(10)
            .ToListAsync(cancellationToken);

        return new ContextViewSectionResponse(
            "audit",
            "Audit",
            records.Count == 0 ? ContextViewSectionVisibility.Empty : ContextViewSectionVisibility.Visible,
            null,
            records.Select(record => new ContextViewItemResponse(
                record.Id.ToString(),
                record.Result.ToString(),
                record.Action,
                record.SafeSummary,
                null,
                new Dictionary<string, string> { ["createdAt"] = record.CreatedAt.ToString("O") })).ToList(),
            null);
    }

    private async Task<ContextViewSectionResponse> BuildIssuesSectionAsync(
        ActiveTenantContext context,
        string? sourceObjectId,
        Guid? graphNodeId,
        CancellationToken cancellationToken)
    {
        if (!await HasPermissionAsync(context, DataQualityPermissions.Read, cancellationToken))
        {
            return DeniedSection("issues", "Issues", DataQualityPermissions.Read);
        }

        var query = dbContext.DataQualityIssues
            .AsNoTracking()
            .Include(issue => issue.SourceLinks)
            .Where(issue => issue.TenantId == context.TenantId);

        if (graphNodeId.HasValue)
        {
            query = query.Where(issue => issue.GraphNodeId == graphNodeId);
        }
        else if (!string.IsNullOrWhiteSpace(sourceObjectId))
        {
            query = query.Where(issue =>
                issue.SourceLinks.Any(link => link.SourceId == sourceObjectId)
                || issue.ImportBatchId.ToString() == sourceObjectId);
        }

        var issues = await query.OrderByDescending(issue => issue.CreatedAt).Take(10).ToListAsync(cancellationToken);
        return new ContextViewSectionResponse(
            "issues",
            "Issues",
            issues.Count == 0 ? ContextViewSectionVisibility.Empty : ContextViewSectionVisibility.Visible,
            null,
            issues.Select(issue => new ContextViewItemResponse(
                issue.Id.ToString(),
                issue.IssueCode,
                issue.Title,
                issue.EvidenceSummary,
                null,
                new Dictionary<string, string>
                {
                    ["severity"] = issue.Severity.ToString(),
                    ["status"] = issue.Status.ToString()
                })).ToList(),
            null);
    }

    private async Task<ContextViewSectionResponse> BuildContextPackagesSectionAsync(
        ActiveTenantContext context,
        Guid? documentArtifactId,
        Guid? artifactId,
        CancellationToken cancellationToken)
    {
        if (!await HasPermissionAsync(context, GovernedQueryPermissions.Read, cancellationToken))
        {
            return DeniedSection("context-packages", "Context packages", GovernedQueryPermissions.Read);
        }

        Guid? resolvedDocumentId = documentArtifactId;
        if (resolvedDocumentId is null && artifactId.HasValue)
        {
            resolvedDocumentId = await dbContext.DocumentArtifacts
                .AsNoTracking()
                .Where(document => document.TenantId == context.TenantId && document.ArtifactId == artifactId)
                .Select(document => (Guid?)document.Id)
                .FirstOrDefaultAsync(cancellationToken);
        }

        var runsQuery = dbContext.RetrievalRuns.AsNoTracking().Where(run => run.TenantId == context.TenantId);
        if (resolvedDocumentId.HasValue)
        {
            runsQuery = runsQuery.Where(run => run.DocumentArtifactId == resolvedDocumentId);
        }
        else
        {
            runsQuery = runsQuery.Where(run => false);
        }

        var runs = await runsQuery
            .Join(
                dbContext.ContextPackages,
                run => run.Id,
                package => package.RetrievalRunId,
                (run, package) => new { run, package })
            .OrderByDescending(pair => pair.run.CreatedAt)
            .Take(10)
            .ToListAsync(cancellationToken);

        return new ContextViewSectionResponse(
            "context-packages",
            "Context packages",
            runs.Count == 0 ? ContextViewSectionVisibility.Empty : ContextViewSectionVisibility.Visible,
            null,
            runs.Select(pair => new ContextViewItemResponse(
                pair.package.Id.ToString(),
                "context-package",
                pair.run.QueryText,
                pair.package.SafeSummary,
                $"/context-packages/{pair.package.Id}",
                new Dictionary<string, string>
                {
                    ["allowedCount"] = pair.package.AllowedCount.ToString(),
                    ["deniedCount"] = pair.package.DeniedCount.ToString()
                })).ToList(),
            null);
    }

    private static ContextViewSectionResponse DeniedSection(string sectionKey, string title, string permissionKey)
    {
        return new ContextViewSectionResponse(
            sectionKey,
            title,
            ContextViewSectionVisibility.Denied,
            $"Requires {permissionKey}.",
            [],
            null);
    }

    private static ContextViewSectionResponse EmptySection(string sectionKey, string title)
    {
        return new ContextViewSectionResponse(sectionKey, title, ContextViewSectionVisibility.Empty, null, [], null);
    }

    private static ContextViewFilterSummaryResponse BuildFilterSummary(
        IReadOnlyCollection<ContextViewSectionResponse> sections,
        string? policyKey)
    {
        return new ContextViewFilterSummaryResponse(
            sections.Count(section => section.Visibility == ContextViewSectionVisibility.Visible),
            sections.Count(section => section.Visibility == ContextViewSectionVisibility.Denied),
            sections.Count(section => section.Visibility == ContextViewSectionVisibility.Empty),
            0,
            policyKey);
    }

    private static string? BuildTraceLinkRoute(AiTraceArtifactLinkKind linkKind, string objectId)
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

    private async Task EnsureSameTenantAsync(
        Guid resourceTenantId,
        ActiveTenantContext context,
        CancellationToken cancellationToken)
    {
        if (resourceTenantId != context.TenantId)
        {
            await denialRecorder.RecordAsync(
                context.TenantId,
                context.UserId,
                "explorers.context_view",
                "tenant_mismatch",
                "The requested anchor belongs to a different tenant.",
                cancellationToken);
            throw new TenantAccessDeniedException("Anchor belongs to a different tenant.");
        }
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
            throw new TenantAccessDeniedException("User lacks context view permission.");
        }

        return context;
    }
}
