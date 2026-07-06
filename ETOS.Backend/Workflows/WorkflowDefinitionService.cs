using ETOS.Backend.Agents;
using ETOS.Backend.Artifacts;
using ETOS.Backend.BusinessPolicies;
using ETOS.Backend.Classification;
using ETOS.Backend.Governance;
using ETOS.Backend.Identity;
using ETOS.Backend.Infrastructure.Persistence;
using ETOS.Backend.OptimizationModels;
using ETOS.Backend.Ontology;
using ETOS.Backend.ToolRegistry;
using Microsoft.EntityFrameworkCore;

namespace ETOS.Backend.Workflows;

public interface IWorkflowDefinitionService
{
    Task<IReadOnlyCollection<WorkflowDefinitionArtifactSummaryResponse>> ListAsync(CancellationToken cancellationToken);
    Task<WorkflowDefinitionDetailResponse> GetAsync(Guid artifactId, Guid versionId, CancellationToken cancellationToken);
    Task<WorkflowDependencySummaryResponse> GetDependenciesAsync(Guid artifactId, Guid versionId, CancellationToken cancellationToken);
    Task<CreateWorkflowDefinitionResponse> CreateAsync(CreateWorkflowDefinitionRequest request, CancellationToken cancellationToken);
    Task<CreateWorkflowDefinitionVersionResponse> CreateVersionAsync(
        Guid artifactId,
        CreateWorkflowDefinitionVersionRequest request,
        CancellationToken cancellationToken);
    Task<MarkWorkflowDefinitionReadyResponse> MarkReadyAsync(Guid artifactId, Guid versionId, CancellationToken cancellationToken);
    Task<PublishWorkflowDefinitionResponse> PublishAsync(
        Guid artifactId,
        Guid versionId,
        PublishArtifactVersionRequest request,
        CancellationToken cancellationToken);
}

public sealed class WorkflowDefinitionService(
    EnterpriseThreadDbContext dbContext,
    ITenantContextResolver tenantContextResolver,
    IAccessPermissionService permissionService,
    IAccessDenialRecorder denialRecorder,
    IAuditRecorder auditRecorder,
    IClassificationPolicyService classificationPolicyService,
    IArtifactRegistryService artifactRegistryService) : IWorkflowDefinitionService
{
    public async Task<IReadOnlyCollection<WorkflowDefinitionArtifactSummaryResponse>> ListAsync(CancellationToken cancellationToken)
    {
        await RequireReadPermissionAsync("workflows.list", cancellationToken);
        var context = await tenantContextResolver.ResolveAsync("workflows.list", cancellationToken);

        var artifacts = await dbContext.Artifacts
            .AsNoTracking()
            .Where(item => item.TenantId == context.TenantId
                && item.NormalizedArtifactType == WorkflowDefinitionArtifactTypes.WorkflowVersion.ToUpperInvariant())
            .OrderByDescending(item => item.UpdatedAt)
            .Take(100)
            .ToListAsync(cancellationToken);

        var artifactIds = artifacts.Select(item => item.Id).ToArray();
        var latestVersions = await dbContext.ArtifactVersions
            .AsNoTracking()
            .Where(version => artifactIds.Contains(version.ArtifactId))
            .GroupBy(version => version.ArtifactId)
            .Select(group => group.OrderByDescending(version => version.CreatedAt).First())
            .ToListAsync(cancellationToken);
        var versionLookup = latestVersions.ToDictionary(version => version.ArtifactId);

        return artifacts.Select(artifact =>
        {
            versionLookup.TryGetValue(artifact.Id, out var version);
            string? workflowKey = null;
            string? displayName = null;
            string? workflowScope = null;
            if (version?.PayloadJson is not null)
            {
                var payload = WorkflowDefinitionPayloadParser.Deserialize(version.PayloadJson);
                workflowKey = payload.WorkflowKey;
                displayName = payload.DisplayName;
                workflowScope = payload.WorkflowScope;
            }

            return new WorkflowDefinitionArtifactSummaryResponse(
                artifact.Id,
                artifact.TenantId,
                artifact.ArtifactType,
                artifact.Name,
                artifact.Description,
                version?.VersionLabel,
                version?.ReadinessState.ToString(),
                workflowKey,
                displayName,
                workflowScope,
                artifact.UpdatedAt);
        }).ToList();
    }

    public async Task<WorkflowDefinitionDetailResponse> GetAsync(
        Guid artifactId,
        Guid versionId,
        CancellationToken cancellationToken)
    {
        var (artifact, version) = await RequireVersionAsync(artifactId, versionId, "workflows.get", cancellationToken);
        var context = await tenantContextResolver.ResolveAsync("workflows.get", cancellationToken);
        var document = WorkflowDefinitionPayloadParser.Deserialize(version.PayloadJson ?? "{}");
        var dependencies = await ResolveDependenciesAsync(context.TenantId, document, cancellationToken);

        return WorkflowDefinitionPayloadParser.Parse(
            artifactId,
            versionId,
            version.VersionLabel,
            artifact.Name,
            artifact.Description,
            version.ReadinessState.ToString(),
            version.PayloadJson ?? "{}",
            dependencies.Agents,
            dependencies.Tools,
            dependencies.BusinessPolicies,
            dependencies.OptimizationModels,
            dependencies.ModelPackages,
            dependencies.Ontologies,
            dependencies.InputSchema,
            dependencies.OutputSchema);
    }

    public async Task<WorkflowDependencySummaryResponse> GetDependenciesAsync(
        Guid artifactId,
        Guid versionId,
        CancellationToken cancellationToken)
    {
        var (_, version) = await RequireVersionAsync(artifactId, versionId, "workflows.dependencies.get", cancellationToken);
        var context = await tenantContextResolver.ResolveAsync("workflows.dependencies.get", cancellationToken);
        var document = WorkflowDefinitionPayloadParser.Deserialize(version.PayloadJson ?? "{}");
        var dependencies = await ResolveDependenciesAsync(context.TenantId, document, cancellationToken);
        return new WorkflowDependencySummaryResponse(
            dependencies.Agents,
            dependencies.Tools,
            dependencies.BusinessPolicies,
            dependencies.OptimizationModels,
            dependencies.ModelPackages,
            dependencies.Ontologies,
            dependencies.InputSchema,
            dependencies.OutputSchema);
    }

    public async Task<CreateWorkflowDefinitionResponse> CreateAsync(
        CreateWorkflowDefinitionRequest request,
        CancellationToken cancellationToken)
    {
        var context = await RequireCreatePermissionAsync(cancellationToken);
        var payload = BuildPayload(request, context.UserId);
        WorkflowDefinitionPayloadParser.ValidateCore(payload);
        return await PersistNewWorkflowAsync(context, request.Name, request.Description, payload, cancellationToken);
    }

    public async Task<CreateWorkflowDefinitionVersionResponse> CreateVersionAsync(
        Guid artifactId,
        CreateWorkflowDefinitionVersionRequest request,
        CancellationToken cancellationToken)
    {
        var context = await RequireCreatePermissionAsync(cancellationToken);
        var artifact = await RequireArtifactAsync(artifactId, context, "workflows.versions.create", cancellationToken);

        var normalizedVersionLabel = request.VersionLabel.Trim().ToUpperInvariant();
        var exists = await dbContext.ArtifactVersions.AnyAsync(
            version => version.ArtifactId == artifactId && version.NormalizedVersionLabel == normalizedVersionLabel,
            cancellationToken);
        if (exists)
        {
            throw new RequestValidationException("Artifact version label already exists for this artifact.");
        }

        var payload = BuildPayload(request, context.UserId);
        WorkflowDefinitionPayloadParser.ValidateCore(payload);

        var version = new ArtifactVersion
        {
            Id = Guid.NewGuid(),
            TenantId = context.TenantId,
            ArtifactId = artifact.Id,
            VersionLabel = request.VersionLabel.Trim(),
            NormalizedVersionLabel = normalizedVersionLabel,
            Summary = TrimOptional(request.Summary ?? request.WorkflowDescription),
            PayloadJson = WorkflowDefinitionPayloadParser.Serialize(payload),
            ReadinessState = ArtifactReadinessState.Draft,
            CreatedByUserId = context.UserId,
            CreatedAt = DateTimeOffset.UtcNow
        };

        artifact.UpdatedAt = DateTimeOffset.UtcNow;
        dbContext.ArtifactVersions.Add(version);
        await dbContext.SaveChangesAsync(cancellationToken);

        await auditRecorder.RecordAsync(
            new AuditRecordWriteRequest(
                context.TenantId,
                context.UserId,
                "workflows.versions.create",
                AuditResult.Success,
                null,
                $"Workflow version '{version.VersionLabel}' was created for '{artifact.Name}'.",
                nameof(ArtifactVersion),
                version.Id.ToString(),
                RetentionCategory: AuditRetentionCategory.Operational),
            cancellationToken);

        return new CreateWorkflowDefinitionVersionResponse(artifact.Id, version.Id, version.VersionLabel);
    }

    public async Task<MarkWorkflowDefinitionReadyResponse> MarkReadyAsync(
        Guid artifactId,
        Guid versionId,
        CancellationToken cancellationToken)
    {
        var context = await RequireReadinessPermissionAsync("workflows.readiness.mark", cancellationToken);
        var (artifact, version) = await RequireVersionAsync(artifactId, versionId, "workflows.readiness.mark", cancellationToken);
        await RequireDraftOwnerOrAdminAsync(context, version, "workflows.readiness.mark", cancellationToken);

        if (version.ReadinessState is ArtifactReadinessState.Published or ArtifactReadinessState.Retired)
        {
            throw new RequestValidationException($"Version readiness is {version.ReadinessState} and cannot be marked ready.");
        }

        var document = WorkflowDefinitionPayloadParser.Deserialize(version.PayloadJson ?? "{}");
        var (validationNotes, derivedRisk) = await WorkflowDefinitionReadinessValidator.ValidatePublishedDependenciesAsync(
            dbContext,
            context.TenantId,
            document,
            cancellationToken);
        if (validationNotes.Count > 0)
        {
            throw new RequestValidationException(string.Join(" ", validationNotes));
        }

        if (derivedRisk is not null)
        {
            document.DerivedCapabilityRiskJson = derivedRisk;
            version.PayloadJson = WorkflowDefinitionPayloadParser.Serialize(document);
        }

        await classificationPolicyService.EvaluateArtifactPolicyRiskAsync(context.TenantId, version.Id, cancellationToken);
        await dbContext.Entry(version).ReloadAsync(cancellationToken);

        version.ReadinessState = version.PolicyRiskStatus switch
        {
            ArtifactPolicyRiskStatus.RequiresApproval => ArtifactReadinessState.RequiresApproval,
            ArtifactPolicyRiskStatus.Blocked => ArtifactReadinessState.Blocked,
            _ => ArtifactReadinessState.Ready
        };
        artifact.UpdatedAt = DateTimeOffset.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);

        await auditRecorder.RecordAsync(
            new AuditRecordWriteRequest(
                context.TenantId,
                context.UserId,
                "workflows.readiness.mark",
                AuditResult.Success,
                null,
                $"Workflow version '{version.VersionLabel}' marked {version.ReadinessState}.",
                nameof(ArtifactVersion),
                version.Id.ToString(),
                RetentionCategory: AuditRetentionCategory.Operational),
            cancellationToken);

        return new MarkWorkflowDefinitionReadyResponse(
            artifactId,
            versionId,
            version.ReadinessState.ToString(),
            validationNotes,
            WorkflowDefinitionPayloadParser.MapDerivedCapabilityRisk(derivedRisk));
    }

    public async Task<PublishWorkflowDefinitionResponse> PublishAsync(
        Guid artifactId,
        Guid versionId,
        PublishArtifactVersionRequest request,
        CancellationToken cancellationToken)
    {
        await RequireVersionAsync(artifactId, versionId, "workflows.publish", cancellationToken);
        var result = await artifactRegistryService.PublishVersionAsync(artifactId, versionId, request, cancellationToken);
        return new PublishWorkflowDefinitionResponse(
            result.Succeeded,
            result.ReadinessState.ToString(),
            result.BlockingReasons,
            artifactId,
            versionId);
    }

    private async Task<CreateWorkflowDefinitionResponse> PersistNewWorkflowAsync(
        ActiveTenantContext context,
        string name,
        string? description,
        WorkflowDefinitionPayloadParser.WorkflowDefinitionPayloadDocument payload,
        CancellationToken cancellationToken)
    {
        var versionLabel = "1.0.0";
        var artifact = new Artifact
        {
            Id = Guid.NewGuid(),
            TenantId = context.TenantId,
            ArtifactType = WorkflowDefinitionArtifactTypes.WorkflowVersion,
            NormalizedArtifactType = WorkflowDefinitionArtifactTypes.WorkflowVersion.ToUpperInvariant(),
            Name = name.Trim(),
            Description = TrimOptional(description),
            OwnerUserId = context.UserId,
            LifecycleState = ArtifactLifecycleState.Active,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        var version = new ArtifactVersion
        {
            Id = Guid.NewGuid(),
            TenantId = context.TenantId,
            ArtifactId = artifact.Id,
            VersionLabel = versionLabel,
            NormalizedVersionLabel = versionLabel.ToUpperInvariant(),
            Summary = TrimOptional(description),
            PayloadJson = WorkflowDefinitionPayloadParser.Serialize(payload),
            ReadinessState = ArtifactReadinessState.Draft,
            CreatedByUserId = context.UserId,
            CreatedAt = DateTimeOffset.UtcNow
        };

        dbContext.Artifacts.Add(artifact);
        dbContext.ArtifactVersions.Add(version);
        await dbContext.SaveChangesAsync(cancellationToken);

        await auditRecorder.RecordAsync(
            new AuditRecordWriteRequest(
                context.TenantId,
                context.UserId,
                "workflows.create",
                AuditResult.Success,
                null,
                $"Workflow '{artifact.Name}' was created.",
                nameof(Artifact),
                artifact.Id.ToString(),
                RetentionCategory: AuditRetentionCategory.Operational),
            cancellationToken);

        return new CreateWorkflowDefinitionResponse(artifact.Id, version.Id, version.VersionLabel);
    }

    private static WorkflowDefinitionPayloadParser.WorkflowDefinitionPayloadDocument BuildPayload(
        CreateWorkflowDefinitionRequest request,
        Guid createdByUserId)
        => WorkflowDefinitionPayloadParser.Create(
            request.WorkflowKey,
            request.DisplayName,
            request.WorkflowDescription,
            request.WorkflowScope,
            request.Steps,
            request.InputSchemaVersionId,
            request.OutputSchemaVersionId,
            request.ReferencedAgentVersionIds,
            request.ReferencedToolDefinitionVersionIds,
            request.ReferencedBusinessPolicyDefinitionVersionIds,
            request.ReferencedOptimizationModelVersionIds,
            request.CompatibleModelPackageVersionIds,
            request.CompatibleOntologyVersionIds,
            request.SafeModeEnabled,
            request.PreviewModeDefault,
            request.BlockedModeMessage,
            request.AllowPartialCompletion,
            request.DefaultStepSafeModeBehavior,
            request.TriggerConfig,
            request.ApprovalRequirements,
            request.CompatibilityTestNotes,
            request.CompatibilityFixtureKeys,
            createdByUserId);

    private static WorkflowDefinitionPayloadParser.WorkflowDefinitionPayloadDocument BuildPayload(
        CreateWorkflowDefinitionVersionRequest request,
        Guid createdByUserId)
        => WorkflowDefinitionPayloadParser.Create(
            request.WorkflowKey,
            request.DisplayName,
            request.WorkflowDescription,
            request.WorkflowScope,
            request.Steps,
            request.InputSchemaVersionId,
            request.OutputSchemaVersionId,
            request.ReferencedAgentVersionIds,
            request.ReferencedToolDefinitionVersionIds,
            request.ReferencedBusinessPolicyDefinitionVersionIds,
            request.ReferencedOptimizationModelVersionIds,
            request.CompatibleModelPackageVersionIds,
            request.CompatibleOntologyVersionIds,
            request.SafeModeEnabled,
            request.PreviewModeDefault,
            request.BlockedModeMessage,
            request.AllowPartialCompletion,
            request.DefaultStepSafeModeBehavior,
            request.TriggerConfig,
            request.ApprovalRequirements,
            request.CompatibilityTestNotes,
            request.CompatibilityFixtureKeys,
            createdByUserId);

    private async Task<ResolvedDependencies> ResolveDependenciesAsync(
        Guid tenantId,
        WorkflowDefinitionPayloadParser.WorkflowDefinitionPayloadDocument document,
        CancellationToken cancellationToken)
    {
        var agentVersionIds = document.ReferencedAgentVersionIds ?? [];
        var toolVersionIds = document.ReferencedToolDefinitionVersionIds ?? [];
        var policyVersionIds = document.ReferencedBusinessPolicyDefinitionVersionIds ?? [];
        var optimizationVersionIds = document.ReferencedOptimizationModelVersionIds ?? [];
        var packageIds = document.CompatibleModelPackageVersionIds ?? [];
        var ontologyIds = document.CompatibleOntologyVersionIds ?? [];

        var agents = agentVersionIds.Count == 0
            ? []
            : await (
                from version in dbContext.ArtifactVersions.AsNoTracking()
                join artifact in dbContext.Artifacts.AsNoTracking() on version.ArtifactId equals artifact.Id
                where version.TenantId == tenantId
                    && agentVersionIds.Contains(version.Id)
                    && artifact.ArtifactType == AgentDefinitionArtifactTypes.AgentVersion
                select new WorkflowAgentReferenceResponse(
                    version.Id,
                    artifact.Id,
                    artifact.Name,
                    ExtractAgentKey(version.PayloadJson),
                    version.VersionLabel,
                    version.ReadinessState.ToString()))
                .ToListAsync(cancellationToken);

        var tools = toolVersionIds.Count == 0
            ? []
            : await (
                from version in dbContext.ArtifactVersions.AsNoTracking()
                join artifact in dbContext.Artifacts.AsNoTracking() on version.ArtifactId equals artifact.Id
                where version.TenantId == tenantId
                    && toolVersionIds.Contains(version.Id)
                    && artifact.ArtifactType == ToolDefinitionArtifactTypes.ToolDefinition
                select new { version, artifact })
                .ToListAsync(cancellationToken);

        var toolResponses = tools.Select(item => new WorkflowToolReferenceResponse(
            item.version.Id,
            item.artifact.Id,
            item.artifact.Name,
            item.version.VersionLabel,
            item.version.ReadinessState.ToString(),
            ExtractToolRiskLevel(item.version.PayloadJson))).ToList();

        var businessPolicies = policyVersionIds.Count == 0
            ? []
            : await (
                from version in dbContext.ArtifactVersions.AsNoTracking()
                join artifact in dbContext.Artifacts.AsNoTracking() on version.ArtifactId equals artifact.Id
                where version.TenantId == tenantId
                    && policyVersionIds.Contains(version.Id)
                    && artifact.ArtifactType == BusinessPolicyDefinitionArtifactTypes.BusinessPolicyDefinition
                select new WorkflowBusinessPolicyReferenceResponse(
                    version.Id,
                    artifact.Id,
                    artifact.Name,
                    ExtractPolicyKey(version.PayloadJson),
                    version.VersionLabel,
                    version.ReadinessState.ToString()))
                .ToListAsync(cancellationToken);

        var optimizationModels = optimizationVersionIds.Count == 0
            ? []
            : await (
                from version in dbContext.ArtifactVersions.AsNoTracking()
                join artifact in dbContext.Artifacts.AsNoTracking() on version.ArtifactId equals artifact.Id
                where version.TenantId == tenantId
                    && optimizationVersionIds.Contains(version.Id)
                    && artifact.ArtifactType == OptimizationModelDefinitionArtifactTypes.OptimizationModel
                select new WorkflowOptimizationModelReferenceResponse(
                    version.Id,
                    artifact.Id,
                    artifact.Name,
                    ExtractOptimizationKey(version.PayloadJson),
                    version.VersionLabel,
                    version.ReadinessState.ToString()))
                .ToListAsync(cancellationToken);

        var packages = packageIds.Count == 0
            ? []
            : await dbContext.ModelPackageVersions
                .AsNoTracking()
                .Where(item => item.TenantId == tenantId && packageIds.Contains(item.Id))
                .Select(item => new WorkflowModelPackageReferenceResponse(
                    item.Id,
                    item.Key,
                    item.Name,
                    item.VersionLabel,
                    item.State.ToString()))
                .ToListAsync(cancellationToken);

        var ontologies = ontologyIds.Count == 0
            ? []
            : await dbContext.OntologyVersions
                .AsNoTracking()
                .Where(item => item.TenantId == tenantId && ontologyIds.Contains(item.Id))
                .Select(item => new WorkflowOntologyReferenceResponse(
                    item.Id,
                    item.Key,
                    item.VersionLabel,
                    item.State.ToString()))
                .ToListAsync(cancellationToken);

        var inputSchema = document.InputSchemaVersionId is Guid inputId
            ? await ResolveArtifactVersionReferenceAsync(tenantId, inputId, cancellationToken)
            : null;
        var outputSchema = document.OutputSchemaVersionId is Guid outputId
            ? await ResolveArtifactVersionReferenceAsync(tenantId, outputId, cancellationToken)
            : null;

        return new ResolvedDependencies(
            agents,
            toolResponses,
            businessPolicies,
            optimizationModels,
            packages,
            ontologies,
            inputSchema,
            outputSchema);
    }

    private async Task<WorkflowArtifactVersionReferenceResponse?> ResolveArtifactVersionReferenceAsync(
        Guid tenantId,
        Guid versionId,
        CancellationToken cancellationToken)
    {
        var version = await dbContext.ArtifactVersions
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == versionId && item.TenantId == tenantId, cancellationToken);
        if (version is null)
        {
            return null;
        }

        var artifact = await dbContext.Artifacts
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == version.ArtifactId, cancellationToken);
        if (artifact is null)
        {
            return null;
        }

        return new WorkflowArtifactVersionReferenceResponse(
            version.Id,
            artifact.Id,
            artifact.ArtifactType,
            artifact.Name,
            version.VersionLabel,
            version.ReadinessState.ToString());
    }

    private async Task<(Artifact Artifact, ArtifactVersion Version)> RequireVersionAsync(
        Guid artifactId,
        Guid versionId,
        string action,
        CancellationToken cancellationToken)
    {
        await RequireReadPermissionAsync(action, cancellationToken);
        var context = await tenantContextResolver.ResolveAsync(action, cancellationToken);
        var artifact = await dbContext.Artifacts
            .SingleOrDefaultAsync(item => item.Id == artifactId, cancellationToken)
            ?? throw new RequestValidationException("Artifact was not found.");

        if (artifact.TenantId != context.TenantId)
        {
            await RecordTenantMismatchAsync(context, action, cancellationToken);
        }

        if (!artifact.ArtifactType.Equals(
                WorkflowDefinitionArtifactTypes.WorkflowVersion,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new RequestValidationException(
                $"Artifact type '{artifact.ArtifactType}' does not match expected '{WorkflowDefinitionArtifactTypes.WorkflowVersion}'.");
        }

        var version = await dbContext.ArtifactVersions
            .SingleOrDefaultAsync(item => item.Id == versionId && item.ArtifactId == artifactId, cancellationToken)
            ?? throw new RequestValidationException("Artifact version was not found.");

        if (version.TenantId != context.TenantId)
        {
            await RecordTenantMismatchAsync(context, action, cancellationToken);
        }

        return (artifact, version);
    }

    private async Task<Artifact> RequireArtifactAsync(
        Guid artifactId,
        ActiveTenantContext context,
        string action,
        CancellationToken cancellationToken)
    {
        var artifact = await dbContext.Artifacts
            .SingleOrDefaultAsync(item => item.Id == artifactId, cancellationToken)
            ?? throw new RequestValidationException("Artifact was not found.");

        if (artifact.TenantId != context.TenantId)
        {
            await RecordTenantMismatchAsync(context, action, cancellationToken);
        }

        if (!artifact.ArtifactType.Equals(
                WorkflowDefinitionArtifactTypes.WorkflowVersion,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new RequestValidationException(
                $"Artifact type '{artifact.ArtifactType}' does not match expected '{WorkflowDefinitionArtifactTypes.WorkflowVersion}'.");
        }

        return artifact;
    }

    private async Task RequireDraftOwnerOrAdminAsync(
        ActiveTenantContext context,
        ArtifactVersion version,
        string action,
        CancellationToken cancellationToken)
    {
        var payload = WorkflowDefinitionPayloadParser.Deserialize(version.PayloadJson ?? "{}");
        if (payload.CreatedByUserId == context.UserId
            || await permissionService.HasPermissionAsync(context.TenantId, context.UserId, WorkflowPermissions.Admin, cancellationToken)
            || await permissionService.HasPermissionAsync(context.TenantId, context.UserId, ArtifactPermissions.Admin, cancellationToken)
            || await permissionService.HasPermissionAsync(context.TenantId, context.UserId, IdentityPermissions.Wildcard, cancellationToken))
        {
            return;
        }

        await denialRecorder.RecordAsync(
            context.TenantId,
            context.UserId,
            action,
            "permission_denied",
            "Only the draft creator or a workflow administrator may perform this action.",
            cancellationToken);
        throw new TenantAccessDeniedException("User lacks draft ownership or workflow administration permission.");
    }

    private async Task RequireReadPermissionAsync(string action, CancellationToken cancellationToken)
    {
        var context = await tenantContextResolver.ResolveAsync(action, cancellationToken);
        if (await HasReadPermissionAsync(context, cancellationToken))
        {
            return;
        }

        await denialRecorder.RecordAsync(
            context.TenantId,
            context.UserId,
            action,
            "permission_denied",
            $"The user lacks the {WorkflowPermissions.Read} permission.",
            cancellationToken);
        throw new TenantAccessDeniedException("User lacks workflow read permission.");
    }

    private async Task<ActiveTenantContext> RequireCreatePermissionAsync(CancellationToken cancellationToken)
    {
        var context = await tenantContextResolver.ResolveAsync("workflows.create", cancellationToken);
        if (await HasCreatePermissionAsync(context, cancellationToken))
        {
            return context;
        }

        await denialRecorder.RecordAsync(
            context.TenantId,
            context.UserId,
            "workflows.create",
            "permission_denied",
            $"The user lacks the {WorkflowPermissions.Create} permission.",
            cancellationToken);
        throw new TenantAccessDeniedException("User lacks workflow create permission.");
    }

    private async Task<ActiveTenantContext> RequireReadinessPermissionAsync(string action, CancellationToken cancellationToken)
    {
        var context = await tenantContextResolver.ResolveAsync(action, cancellationToken);
        if (await HasReadinessPermissionAsync(context, cancellationToken))
        {
            return context;
        }

        await denialRecorder.RecordAsync(
            context.TenantId,
            context.UserId,
            action,
            "permission_denied",
            $"The user lacks the {WorkflowPermissions.Readiness} permission.",
            cancellationToken);
        throw new TenantAccessDeniedException("User lacks workflow readiness permission.");
    }

    private async Task RecordTenantMismatchAsync(ActiveTenantContext context, string action, CancellationToken cancellationToken)
    {
        await denialRecorder.RecordAsync(
            context.TenantId,
            context.UserId,
            action,
            "tenant_access_denied",
            "Record belongs to a different tenant.",
            cancellationToken);
        throw new TenantAccessDeniedException("Record is not available in the active tenant.");
    }

    private async Task<bool> HasReadPermissionAsync(ActiveTenantContext context, CancellationToken cancellationToken)
        => await permissionService.HasPermissionAsync(context.TenantId, context.UserId, WorkflowPermissions.Read, cancellationToken)
            || await HasAdminPermissionAsync(context, cancellationToken);

    private async Task<bool> HasCreatePermissionAsync(ActiveTenantContext context, CancellationToken cancellationToken)
        => await permissionService.HasPermissionAsync(context.TenantId, context.UserId, WorkflowPermissions.Create, cancellationToken)
            || await HasAdminPermissionAsync(context, cancellationToken);

    private async Task<bool> HasReadinessPermissionAsync(ActiveTenantContext context, CancellationToken cancellationToken)
        => await permissionService.HasPermissionAsync(context.TenantId, context.UserId, WorkflowPermissions.Readiness, cancellationToken)
            || await HasAdminPermissionAsync(context, cancellationToken);

    private async Task<bool> HasAdminPermissionAsync(ActiveTenantContext context, CancellationToken cancellationToken)
        => await permissionService.HasPermissionAsync(context.TenantId, context.UserId, WorkflowPermissions.Admin, cancellationToken)
            || await permissionService.HasPermissionAsync(context.TenantId, context.UserId, IdentityPermissions.Wildcard, cancellationToken);

    private static string ExtractAgentKey(string? payloadJson)
    {
        if (string.IsNullOrWhiteSpace(payloadJson))
        {
            return string.Empty;
        }

        try
        {
            return AgentDefinitionPayloadParser.Deserialize(payloadJson).AgentKey ?? string.Empty;
        }
        catch (RequestValidationException)
        {
            return string.Empty;
        }
    }

    private static string ExtractPolicyKey(string? payloadJson)
    {
        if (string.IsNullOrWhiteSpace(payloadJson))
        {
            return string.Empty;
        }

        try
        {
            return BusinessPolicyDefinitionPayloadParser.Deserialize(payloadJson).PolicyKey ?? string.Empty;
        }
        catch (RequestValidationException)
        {
            return string.Empty;
        }
    }

    private static string ExtractOptimizationKey(string? payloadJson)
    {
        if (string.IsNullOrWhiteSpace(payloadJson))
        {
            return string.Empty;
        }

        try
        {
            return OptimizationModelDefinitionPayloadParser.Deserialize(payloadJson).OptimizationKey ?? string.Empty;
        }
        catch (RequestValidationException)
        {
            return string.Empty;
        }
    }

    private static string ExtractToolRiskLevel(string? payloadJson)
    {
        if (string.IsNullOrWhiteSpace(payloadJson))
        {
            return ToolRiskLevels.Low;
        }

        try
        {
            return ToolDefinitionPayloadParser.Deserialize(payloadJson).RiskLevel ?? ToolRiskLevels.Low;
        }
        catch (RequestValidationException)
        {
            return ToolRiskLevels.Low;
        }
    }

    private static string? TrimOptional(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private sealed record ResolvedDependencies(
        IReadOnlyCollection<WorkflowAgentReferenceResponse> Agents,
        IReadOnlyCollection<WorkflowToolReferenceResponse> Tools,
        IReadOnlyCollection<WorkflowBusinessPolicyReferenceResponse> BusinessPolicies,
        IReadOnlyCollection<WorkflowOptimizationModelReferenceResponse> OptimizationModels,
        IReadOnlyCollection<WorkflowModelPackageReferenceResponse> ModelPackages,
        IReadOnlyCollection<WorkflowOntologyReferenceResponse> Ontologies,
        WorkflowArtifactVersionReferenceResponse? InputSchema,
        WorkflowArtifactVersionReferenceResponse? OutputSchema);
}
