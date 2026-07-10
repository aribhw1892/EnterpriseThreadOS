using System.Text.Json;
using ETOS.Backend.AgentRuntime;
using ETOS.Backend.AgentRuns;
using ETOS.Backend.AgentTemplates;
using ETOS.Backend.AgentTypes;
using ETOS.Backend.Artifacts;
using ETOS.Backend.BusinessPolicies;
using ETOS.Backend.Capabilities;
using ETOS.Backend.Classification;
using ETOS.Backend.Governance;
using ETOS.Backend.GovernedChat.Llm;
using ETOS.Backend.GovernedQuery;
using ETOS.Backend.Identity;
using ETOS.Backend.Infrastructure.Persistence;
using ETOS.Backend.OptimizationModels;
using ETOS.Backend.Ontology;
using ETOS.Backend.ToolRegistry;
using Microsoft.EntityFrameworkCore;

namespace ETOS.Backend.Agents;

public interface IAgentDefinitionService
{
    Task<IReadOnlyCollection<AgentDefinitionArtifactSummaryResponse>> ListAsync(CancellationToken cancellationToken);
    Task<AgentDefinitionDetailResponse> GetAsync(Guid artifactId, Guid versionId, CancellationToken cancellationToken);
    Task<AgentDependencySummaryResponse> GetDependenciesAsync(Guid artifactId, Guid versionId, CancellationToken cancellationToken);
    Task<CreateAgentDefinitionResponse> CreateAsync(CreateAgentDefinitionRequest request, CancellationToken cancellationToken);
    Task<CreateAgentDefinitionVersionResponse> CreateVersionAsync(
        Guid artifactId,
        CreateAgentDefinitionVersionRequest request,
        CancellationToken cancellationToken);
    Task<CreateAgentDefinitionResponse> CreateFromTemplateAsync(CreateAgentFromTemplateRequest request, CancellationToken cancellationToken);
    Task<CreateAgentDefinitionResponse> CreateFromPromptAsync(CreateAgentFromPromptRequest request, CancellationToken cancellationToken);
    Task<MarkAgentDefinitionReadyResponse> MarkReadyAsync(Guid artifactId, Guid versionId, CancellationToken cancellationToken);
    Task<PublishAgentDefinitionResponse> PublishAsync(
        Guid artifactId,
        Guid versionId,
        PublishArtifactVersionRequest request,
        CancellationToken cancellationToken);
    Task<UpdateAgentModelConfigResponse> UpdateModelConfigAsync(
        Guid artifactId,
        Guid versionId,
        UpdateAgentModelConfigRequest request,
        CancellationToken cancellationToken);
}

public sealed class AgentDefinitionService(
    EnterpriseThreadDbContext dbContext,
    ITenantContextResolver tenantContextResolver,
    IAccessPermissionService permissionService,
    IAccessDenialRecorder denialRecorder,
    IAuditRecorder auditRecorder,
    IClassificationPolicyService classificationPolicyService,
    IArtifactRegistryService artifactRegistryService,
    ILlmCompletionService llmCompletionService,
    IGovernedQueryService governedQueryService,
    IDirectResponseArtifactSeeder directResponseArtifactSeeder) : IAgentDefinitionService
{
    private const string AgentDraftPromptSchema = """
        {
          "type": "object",
          "required": ["agentKey", "displayName", "description", "patternSummary"],
          "properties": {
            "agentKey": { "type": "string" },
            "displayName": { "type": "string" },
            "description": { "type": "string" },
            "patternSummary": { "type": "string" }
          }
        }
        """;

    public async Task<IReadOnlyCollection<AgentDefinitionArtifactSummaryResponse>> ListAsync(CancellationToken cancellationToken)
    {
        await RequireReadPermissionAsync("agents.list", cancellationToken);
        var context = await tenantContextResolver.ResolveAsync("agents.list", cancellationToken);

        var artifacts = await dbContext.Artifacts
            .AsNoTracking()
            .Where(item => item.TenantId == context.TenantId
                && item.NormalizedArtifactType == AgentDefinitionArtifactTypes.AgentVersion.ToUpperInvariant())
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
            string? agentKey = null;
            string? displayName = null;
            string? preferredRuntimeAdapterKey = null;
            if (version?.PayloadJson is not null)
            {
                var payload = AgentDefinitionPayloadParser.Deserialize(version.PayloadJson);
                agentKey = payload.AgentKey;
                displayName = payload.DisplayName;
                preferredRuntimeAdapterKey = payload.PreferredRuntimeAdapterKey;
            }

            return new AgentDefinitionArtifactSummaryResponse(
                artifact.Id,
                artifact.TenantId,
                artifact.ArtifactType,
                artifact.Name,
                artifact.Description,
                version?.VersionLabel,
                version?.ReadinessState.ToString(),
                agentKey,
                displayName,
                preferredRuntimeAdapterKey,
                artifact.UpdatedAt);
        }).ToList();
    }

    public async Task<AgentDefinitionDetailResponse> GetAsync(
        Guid artifactId,
        Guid versionId,
        CancellationToken cancellationToken)
    {
        var (artifact, version) = await RequireVersionAsync(artifactId, versionId, "agents.get", cancellationToken);
        var context = await tenantContextResolver.ResolveAsync("agents.get", cancellationToken);
        var document = AgentDefinitionPayloadParser.Deserialize(version.PayloadJson ?? "{}");
        var dependencies = await ResolveDependenciesAsync(context.TenantId, document, cancellationToken);

        return AgentDefinitionPayloadParser.Parse(
            artifactId,
            versionId,
            version.VersionLabel,
            artifact.Name,
            artifact.Description,
            version.ReadinessState.ToString(),
            version.PayloadJson ?? "{}",
            dependencies.AgentType,
            dependencies.Capabilities,
            dependencies.BusinessPolicies,
            dependencies.OptimizationModels,
            dependencies.ModelPackages,
            dependencies.Ontologies,
            dependencies.PromptTemplate,
            dependencies.OutputSchema,
            dependencies.QueryIntent,
            dependencies.RetrievalStrategy,
            dependencies.Tools,
            dependencies.Skills);
    }

    public async Task<AgentDependencySummaryResponse> GetDependenciesAsync(
        Guid artifactId,
        Guid versionId,
        CancellationToken cancellationToken)
    {
        var (_, version) = await RequireVersionAsync(artifactId, versionId, "agents.dependencies.get", cancellationToken);
        var context = await tenantContextResolver.ResolveAsync("agents.dependencies.get", cancellationToken);
        var document = AgentDefinitionPayloadParser.Deserialize(version.PayloadJson ?? "{}");
        var dependencies = await ResolveDependenciesAsync(context.TenantId, document, cancellationToken);
        return new AgentDependencySummaryResponse(
            dependencies.AgentType,
            dependencies.Capabilities,
            dependencies.BusinessPolicies,
            dependencies.OptimizationModels,
            dependencies.ModelPackages,
            dependencies.Ontologies,
            dependencies.PromptTemplate,
            dependencies.OutputSchema,
            dependencies.QueryIntent,
            dependencies.RetrievalStrategy,
            dependencies.Tools,
            dependencies.Skills);
    }

    public async Task<CreateAgentDefinitionResponse> CreateAsync(
        CreateAgentDefinitionRequest request,
        CancellationToken cancellationToken)
    {
        var context = await RequireCreatePermissionAsync(cancellationToken);
        var payload = BuildPayload(request, context.UserId);
        AgentDefinitionPayloadParser.ValidateCore(payload);
        return await PersistNewAgentAsync(context, request.Name, request.Description, payload, cancellationToken);
    }

    public async Task<CreateAgentDefinitionVersionResponse> CreateVersionAsync(
        Guid artifactId,
        CreateAgentDefinitionVersionRequest request,
        CancellationToken cancellationToken)
    {
        var context = await RequireCreatePermissionAsync(cancellationToken);
        var artifact = await RequireArtifactAsync(artifactId, context, "agents.versions.create", cancellationToken);

        var normalizedVersionLabel = request.VersionLabel.Trim().ToUpperInvariant();
        var exists = await dbContext.ArtifactVersions.AnyAsync(
            version => version.ArtifactId == artifactId && version.NormalizedVersionLabel == normalizedVersionLabel,
            cancellationToken);
        if (exists)
        {
            throw new RequestValidationException("Artifact version label already exists for this artifact.");
        }

        var payload = BuildPayload(request, context.UserId);
        AgentDefinitionPayloadParser.ValidateCore(payload);

        var version = new ArtifactVersion
        {
            Id = Guid.NewGuid(),
            TenantId = context.TenantId,
            ArtifactId = artifact.Id,
            VersionLabel = request.VersionLabel.Trim(),
            NormalizedVersionLabel = normalizedVersionLabel,
            Summary = TrimOptional(request.Summary ?? request.AgentDescription),
            PayloadJson = AgentDefinitionPayloadParser.Serialize(payload),
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
                "agents.versions.create",
                AuditResult.Success,
                null,
                $"Agent version '{version.VersionLabel}' was created for '{artifact.Name}'.",
                nameof(ArtifactVersion),
                version.Id.ToString(),
                RetentionCategory: AuditRetentionCategory.Operational),
            cancellationToken);

        return new CreateAgentDefinitionVersionResponse(artifact.Id, version.Id, version.VersionLabel);
    }

    public async Task<CreateAgentDefinitionResponse> CreateFromTemplateAsync(
        CreateAgentFromTemplateRequest request,
        CancellationToken cancellationToken)
    {
        var context = await RequireCreatePermissionAsync(cancellationToken);

        var templateVersion = await dbContext.ArtifactVersions
            .AsNoTracking()
            .Include(item => item.Artifact)
            .SingleOrDefaultAsync(item => item.Id == request.SourceAgentTemplateVersionId, cancellationToken)
            ?? throw new RequestValidationException("Source agent template version was not found.");

        if (templateVersion.TenantId != context.TenantId)
        {
            throw new RequestValidationException("Source agent template version belongs to a different tenant.");
        }

        if (!templateVersion.Artifact!.ArtifactType.Equals(
                AgentTemplateDefinitionArtifactTypes.AgentTemplate,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new RequestValidationException("Source artifact is not an agent template version.");
        }

        if (templateVersion.ReadinessState != ArtifactReadinessState.Published)
        {
            throw new RequestValidationException("Source agent template version must be published.");
        }

        var templatePayload = AgentTemplateDefinitionPayloadParser.Deserialize(templateVersion.PayloadJson ?? "{}");
        var agentTypeVersionId = request.AgentTypeDefinitionVersionId
            ?? await ResolveDefaultAgentTypeVersionIdAsync(context.TenantId, cancellationToken);

        var agentKey = string.IsNullOrWhiteSpace(request.AgentKey)
            ? Slugify(templatePayload.TemplateKey ?? templateVersion.Artifact.Name)
            : request.AgentKey.Trim();
        var displayName = string.IsNullOrWhiteSpace(request.DisplayName)
            ? templateVersion.Artifact.Name
            : request.DisplayName.Trim();

        var createRequest = new CreateAgentDefinitionRequest(
            displayName,
            request.Description ?? templateVersion.Artifact.Description,
            agentKey,
            displayName,
            request.Description ?? templatePayload.PatternSummary,
            agentTypeVersionId,
            templateVersion.Id,
            templatePayload.PreferredRuntimeAdapterKey,
            templatePayload.CompatibleModelPackageVersionIds,
            templatePayload.CompatibleOntologyVersionIds,
            templatePayload.ReferencedCapabilityDefinitionVersionIds,
            templatePayload.ReferencedBusinessPolicyDefinitionVersionIds,
            templatePayload.ReferencedOptimizationModelVersionIds,
            templatePayload.PromptTemplateVersionId,
            templatePayload.OutputSchemaVersionId,
            templatePayload.QueryIntentVersionId,
            templatePayload.RetrievalStrategyVersionId,
            templatePayload.ReferencedToolDefinitionVersionIds,
            [],
            request.PrimaryModelProviderKey,
            request.PrimaryModelId,
            [],
            SafeModeEnabled: false,
            PreviewModeDefault: true,
            BlockedModeMessage: null,
            CompatibilityTestNotes: [],
            CompatibilityFixtureKeys: [],
            templatePayload.CompositionMetadata);

        var payload = BuildPayload(createRequest, context.UserId);
        AgentDefinitionPayloadParser.ValidateCore(payload);
        return await PersistNewAgentAsync(context, displayName, createRequest.Description, payload, cancellationToken);
    }

    public async Task<CreateAgentDefinitionResponse> CreateFromPromptAsync(
        CreateAgentFromPromptRequest request,
        CancellationToken cancellationToken)
    {
        var context = await RequireCreatePermissionAsync(cancellationToken);

        if (string.IsNullOrWhiteSpace(request.Prompt))
        {
            throw new RequestValidationException("prompt is required.");
        }

        var agentTypeVersionId = request.AgentTypeDefinitionVersionId
            ?? await ResolveDefaultAgentTypeVersionIdAsync(context.TenantId, cancellationToken);

        var prompt = $"""
            Create a draft tenant agent definition from this user prompt.
            User prompt:
            {request.Prompt.Trim()}
            """;

        var structuredJson = await llmCompletionService.CompleteStructuredAsync(
            prompt,
            AgentDraftPromptSchema,
            cancellationToken);

        using var draftDocument = JsonDocument.Parse(structuredJson);
        var root = draftDocument.RootElement;
        var agentKey = root.TryGetProperty("agentKey", out var agentKeyElement)
            ? agentKeyElement.GetString()?.Trim()
            : null;
        var displayName = root.TryGetProperty("displayName", out var displayNameElement)
            ? displayNameElement.GetString()?.Trim()
            : null;
        var description = root.TryGetProperty("description", out var descriptionElement)
            ? descriptionElement.GetString()?.Trim()
            : null;
        var patternSummary = root.TryGetProperty("patternSummary", out var patternSummaryElement)
            ? patternSummaryElement.GetString()?.Trim()
            : null;

        agentKey ??= AgentPromptDraftDeriver.DeriveAgentKey(request.Prompt);
        displayName ??= AgentPromptDraftDeriver.DeriveDisplayName(request.Prompt);
        description ??= AgentPromptDraftDeriver.DeriveDescription(request.Prompt);
        patternSummary ??= AgentPromptDraftDeriver.DerivePatternSummary(request.Prompt);

        if (string.IsNullOrWhiteSpace(agentKey) || string.IsNullOrWhiteSpace(displayName))
        {
            throw new RequestValidationException("LLM draft did not produce required agentKey and displayName.");
        }

        var templateResolution = await TryResolvePublishedTemplateForPromptAsync(
            context.TenantId,
            request.Prompt,
            cancellationToken);
        var infrastructureTemplate = templateResolution.Match
            ?? await TryResolveInfrastructureTemplateAsync(context.TenantId, cancellationToken);

        var compositionMetadata = new Dictionary<string, string> { ["createdFromPrompt"] = "true" };
        if (templateResolution.Match?.Payload.TemplateKey is { Length: > 0 } templateKey)
        {
            compositionMetadata["seededFromTemplateKey"] = templateKey;
        }

        Guid? queryIntentVersionId;
        Guid? retrievalStrategyVersionId;
        IReadOnlyCollection<Guid>? referencedToolDefinitionVersionIds;
        Guid? promptTemplateVersionId;
        Guid? outputSchemaVersionId;
        if (templateResolution.Match is not null)
        {
            queryIntentVersionId = templateResolution.Match.Payload.QueryIntentVersionId;
            retrievalStrategyVersionId = templateResolution.Match.Payload.RetrievalStrategyVersionId;
            referencedToolDefinitionVersionIds = templateResolution.Match.Payload.ReferencedToolDefinitionVersionIds;
            promptTemplateVersionId = templateResolution.Match.Payload.PromptTemplateVersionId;
            outputSchemaVersionId = templateResolution.Match.Payload.OutputSchemaVersionId;
        }
        else
        {
            var directResponse = await governedQueryService.EnsurePlatformFixedIntentVersionsAsync(
                "direct-response-v1",
                cancellationToken);
            var directResponseArtifacts = await directResponseArtifactSeeder.EnsurePlatformArtifactsAsync(
                context,
                cancellationToken);
            queryIntentVersionId = directResponse.IntentVersionId;
            retrievalStrategyVersionId = directResponse.RetrievalStrategyVersionId;
            referencedToolDefinitionVersionIds = [];
            promptTemplateVersionId = directResponseArtifacts.PromptTemplate.VersionId;
            outputSchemaVersionId = directResponseArtifacts.OutputSchema.VersionId;
            compositionMetadata["seededQueryIntentKey"] = "direct-response-v1";
        }

        var payload = AgentDefinitionPayloadParser.Create(
            agentKey,
            displayName,
            description ?? patternSummary,
            agentTypeVersionId,
            templateResolution.Match?.VersionId ?? infrastructureTemplate?.VersionId,
            templateResolution.Match?.Payload.PreferredRuntimeAdapterKey
                ?? infrastructureTemplate?.Payload.PreferredRuntimeAdapterKey,
            templateResolution.Match?.Payload.CompatibleModelPackageVersionIds
                ?? infrastructureTemplate?.Payload.CompatibleModelPackageVersionIds,
            templateResolution.Match?.Payload.CompatibleOntologyVersionIds
                ?? infrastructureTemplate?.Payload.CompatibleOntologyVersionIds,
            templateResolution.Match?.Payload.ReferencedCapabilityDefinitionVersionIds
                ?? infrastructureTemplate?.Payload.ReferencedCapabilityDefinitionVersionIds,
            templateResolution.Match?.Payload.ReferencedBusinessPolicyDefinitionVersionIds
                ?? infrastructureTemplate?.Payload.ReferencedBusinessPolicyDefinitionVersionIds,
            templateResolution.Match?.Payload.ReferencedOptimizationModelVersionIds
                ?? infrastructureTemplate?.Payload.ReferencedOptimizationModelVersionIds,
            promptTemplateVersionId ?? infrastructureTemplate?.Payload.PromptTemplateVersionId,
            outputSchemaVersionId ?? infrastructureTemplate?.Payload.OutputSchemaVersionId,
            queryIntentVersionId,
            retrievalStrategyVersionId,
            referencedToolDefinitionVersionIds,
            [],
            request.PrimaryModelProviderKey,
            request.PrimaryModelId,
            [],
            false,
            true,
            null,
            patternSummary is null ? [] : [patternSummary],
            [],
            context.UserId,
            compositionMetadata);

        return await PersistNewAgentAsync(context, displayName, description ?? patternSummary, payload, cancellationToken);
    }

    private sealed record PromptTemplateDefaults(
        Guid VersionId,
        AgentTemplateDefinitionPayloadParser.AgentTemplateDefinitionPayloadDocument Payload);

    private sealed record PromptTemplateResolution(
        PromptTemplateDefaults? Match,
        int Score);

    private async Task<PromptTemplateResolution> TryResolvePublishedTemplateForPromptAsync(
        Guid tenantId,
        string userPrompt,
        CancellationToken cancellationToken)
    {
        var templateVersions = await dbContext.ArtifactVersions
            .AsNoTracking()
            .Include(item => item.Artifact)
            .Where(item => item.TenantId == tenantId
                && item.ReadinessState == ArtifactReadinessState.Published
                && item.Artifact!.ArtifactType == AgentTemplateDefinitionArtifactTypes.AgentTemplate)
            .OrderByDescending(item => item.PublishedAt ?? item.CreatedAt)
            .ToListAsync(cancellationToken);

        if (templateVersions.Count == 0)
        {
            return new PromptTemplateResolution(null, 0);
        }

        PromptTemplateDefaults? bestMatch = null;
        var bestScore = 0;
        foreach (var version in templateVersions)
        {
            var payload = AgentTemplateDefinitionPayloadParser.Deserialize(version.PayloadJson ?? "{}");
            var score = ScoreTemplateForPrompt(userPrompt, payload.TemplateKey, version.Artifact!.Name);
            if (score > bestScore)
            {
                bestScore = score;
                bestMatch = new PromptTemplateDefaults(version.Id, payload);
            }
        }

        return bestScore > 0
            ? new PromptTemplateResolution(bestMatch, bestScore)
            : new PromptTemplateResolution(null, 0);
    }

    private async Task<PromptTemplateDefaults?> TryResolveInfrastructureTemplateAsync(
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        var version = await dbContext.ArtifactVersions
            .AsNoTracking()
            .Include(item => item.Artifact)
            .Where(item => item.TenantId == tenantId
                && item.ReadinessState == ArtifactReadinessState.Published
                && item.Artifact!.ArtifactType == AgentTemplateDefinitionArtifactTypes.AgentTemplate)
            .OrderByDescending(item => item.PublishedAt ?? item.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (version is null)
        {
            return null;
        }

        return new PromptTemplateDefaults(version.Id, AgentTemplateDefinitionPayloadParser.Deserialize(version.PayloadJson ?? "{}"));
    }

    private static int ScoreTemplateForPrompt(string userPrompt, string? templateKey, string artifactName)
    {
        var promptLower = userPrompt.ToLowerInvariant();
        var score = 0;

        foreach (var token in CollectTemplateMatchTokens(templateKey, artifactName))
        {
            if (token.Length >= 4 && promptLower.Contains(token, StringComparison.Ordinal))
            {
                score += token.Length;
            }
        }

        if (promptLower.Contains("investig", StringComparison.Ordinal)
            && (templateKey?.Contains("investig", StringComparison.OrdinalIgnoreCase) ?? false))
        {
            score += 8;
        }

        if ((promptLower.Contains("bom", StringComparison.Ordinal) || promptLower.Contains("bill of material", StringComparison.Ordinal))
            && (templateKey?.Contains("manufacturing", StringComparison.OrdinalIgnoreCase) ?? false))
        {
            score += 8;
        }

        if ((promptLower.Contains("import", StringComparison.Ordinal) || promptLower.Contains("mapping", StringComparison.Ordinal))
            && (templateKey?.Contains("mapping", StringComparison.OrdinalIgnoreCase) ?? false))
        {
            score += 8;
        }

        return score;
    }

    private static IEnumerable<string> CollectTemplateMatchTokens(string? templateKey, string artifactName)
    {
        if (!string.IsNullOrWhiteSpace(templateKey))
        {
            foreach (var token in templateKey.Split('-', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                yield return token.ToLowerInvariant();
            }
        }

        foreach (var token in artifactName.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            yield return token.ToLowerInvariant();
        }
    }

    public async Task<MarkAgentDefinitionReadyResponse> MarkReadyAsync(
        Guid artifactId,
        Guid versionId,
        CancellationToken cancellationToken)
    {
        var context = await RequireReadinessPermissionAsync("agents.readiness.mark", cancellationToken);
        var (artifact, version) = await RequireVersionAsync(artifactId, versionId, "agents.readiness.mark", cancellationToken);
        await RequireDraftOwnerOrAdminAsync(context, version, "agents.readiness.mark", cancellationToken);

        if (version.ReadinessState is ArtifactReadinessState.Published or ArtifactReadinessState.Retired)
        {
            throw new RequestValidationException($"Version readiness is {version.ReadinessState} and cannot be marked ready.");
        }

        var document = AgentDefinitionPayloadParser.Deserialize(version.PayloadJson ?? "{}");
        var (validationNotes, derivedRisk) = await AgentDefinitionReadinessValidator.ValidatePublishedDependenciesAsync(
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
            version.PayloadJson = AgentDefinitionPayloadParser.Serialize(document);
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
                "agents.readiness.mark",
                AuditResult.Success,
                null,
                $"Agent version '{version.VersionLabel}' marked {version.ReadinessState}.",
                nameof(ArtifactVersion),
                version.Id.ToString(),
                RetentionCategory: AuditRetentionCategory.Operational),
            cancellationToken);

        return new MarkAgentDefinitionReadyResponse(
            artifactId,
            versionId,
            version.ReadinessState.ToString(),
            validationNotes,
            AgentDefinitionPayloadParser.MapDerivedCapabilityRisk(derivedRisk));
    }

    public async Task<PublishAgentDefinitionResponse> PublishAsync(
        Guid artifactId,
        Guid versionId,
        PublishArtifactVersionRequest request,
        CancellationToken cancellationToken)
    {
        await RequireVersionAsync(artifactId, versionId, "agents.publish", cancellationToken);
        var result = await artifactRegistryService.PublishVersionAsync(artifactId, versionId, request, cancellationToken);
        return new PublishAgentDefinitionResponse(
            result.Succeeded,
            result.ReadinessState.ToString(),
            result.BlockingReasons,
            artifactId,
            versionId);
    }

    public async Task<UpdateAgentModelConfigResponse> UpdateModelConfigAsync(
        Guid artifactId,
        Guid versionId,
        UpdateAgentModelConfigRequest request,
        CancellationToken cancellationToken)
    {
        var context = await RequireCreatePermissionAsync(cancellationToken);
        var (artifact, version) = await RequireVersionAsync(artifactId, versionId, "agents.model-config.update", cancellationToken);
        ValidateModelConfigRequest(request);

        var document = AgentDefinitionPayloadParser.Deserialize(version.PayloadJson ?? "{}");
        ApplyModelConfig(document, request);
        AgentDefinitionPayloadParser.ValidateCore(document);

        if (version.ReadinessState == ArtifactReadinessState.Draft)
        {
            await RequireDraftOwnerOrAdminAsync(context, version, "agents.model-config.update", cancellationToken);
            version.PayloadJson = AgentDefinitionPayloadParser.Serialize(document);
            artifact.UpdatedAt = DateTimeOffset.UtcNow;
            await dbContext.SaveChangesAsync(cancellationToken);

            await auditRecorder.RecordAsync(
                new AuditRecordWriteRequest(
                    context.TenantId,
                    context.UserId,
                    "agents.model-config.update",
                    AuditResult.Success,
                    null,
                    $"Agent version '{version.VersionLabel}' model config was updated in place.",
                    nameof(ArtifactVersion),
                    version.Id.ToString(),
                    RetentionCategory: AuditRetentionCategory.Operational),
                cancellationToken);

            return new UpdateAgentModelConfigResponse(
                artifact.Id,
                version.Id,
                version.VersionLabel,
                version.ReadinessState.ToString(),
                CreatedNewVersion: false);
        }

        if (version.ReadinessState is ArtifactReadinessState.Published
            or ArtifactReadinessState.Ready
            or ArtifactReadinessState.Blocked)
        {
            document.CreatedByUserId = context.UserId;
            document.DerivedCapabilityRiskJson = null;

            var existingLabels = await dbContext.ArtifactVersions
                .AsNoTracking()
                .Where(item => item.ArtifactId == artifactId)
                .Select(item => item.VersionLabel)
                .ToListAsync(cancellationToken);
            var versionLabel = AgentVersionLabelBuilder.NextVersionLabel(version.VersionLabel, existingLabels);

            var newVersion = new ArtifactVersion
            {
                Id = Guid.NewGuid(),
                TenantId = context.TenantId,
                ArtifactId = artifact.Id,
                VersionLabel = versionLabel,
                NormalizedVersionLabel = versionLabel.ToUpperInvariant(),
                Summary = version.Summary,
                PayloadJson = AgentDefinitionPayloadParser.Serialize(document),
                ReadinessState = ArtifactReadinessState.Draft,
                CreatedByUserId = context.UserId,
                CreatedAt = DateTimeOffset.UtcNow
            };

            artifact.UpdatedAt = DateTimeOffset.UtcNow;
            dbContext.ArtifactVersions.Add(newVersion);
            await dbContext.SaveChangesAsync(cancellationToken);

            await auditRecorder.RecordAsync(
                new AuditRecordWriteRequest(
                    context.TenantId,
                    context.UserId,
                    "agents.model-config.update",
                    AuditResult.Success,
                    null,
                    $"Agent version '{newVersion.VersionLabel}' was created as a draft with updated model config.",
                    nameof(ArtifactVersion),
                    newVersion.Id.ToString(),
                    RetentionCategory: AuditRetentionCategory.Operational),
                cancellationToken);

            return new UpdateAgentModelConfigResponse(
                artifact.Id,
                newVersion.Id,
                newVersion.VersionLabel,
                newVersion.ReadinessState.ToString(),
                CreatedNewVersion: true);
        }

        throw new RequestValidationException(
            $"Version readiness is {version.ReadinessState} and cannot receive model config updates.");
    }

    private static void ValidateModelConfigRequest(UpdateAgentModelConfigRequest request)
    {
        AgentModelProviderKeys.Validate(request.PrimaryModelProviderKey);

        if (string.IsNullOrWhiteSpace(request.PrimaryModelId))
        {
            throw new RequestValidationException("primaryModelId is required.");
        }

        foreach (var fallback in request.FallbackModels ?? [])
        {
            AgentModelProviderKeys.Validate(fallback.ProviderKey);
            if (string.IsNullOrWhiteSpace(fallback.ModelId))
            {
                throw new RequestValidationException("fallback modelId is required.");
            }

            if (string.IsNullOrWhiteSpace(fallback.TriggerReason))
            {
                throw new RequestValidationException("fallback triggerReason is required.");
            }
        }
    }

    private static void ApplyModelConfig(
        AgentDefinitionPayloadParser.AgentDefinitionPayloadDocument document,
        UpdateAgentModelConfigRequest request)
    {
        document.PrimaryModelProviderKey = request.PrimaryModelProviderKey.Trim();
        document.PrimaryModelId = request.PrimaryModelId.Trim();
        document.FallbackModels = (request.FallbackModels ?? [])
            .Select(item => new AgentDefinitionPayloadParser.FallbackModelDocument
            {
                ProviderKey = item.ProviderKey.Trim(),
                ModelId = item.ModelId.Trim(),
                TriggerReason = item.TriggerReason.Trim()
            })
            .ToList();
    }

    private async Task<CreateAgentDefinitionResponse> PersistNewAgentAsync(
        ActiveTenantContext context,
        string name,
        string? description,
        AgentDefinitionPayloadParser.AgentDefinitionPayloadDocument payload,
        CancellationToken cancellationToken)
    {
        var versionLabel = "1.0.0";
        var artifact = new Artifact
        {
            Id = Guid.NewGuid(),
            TenantId = context.TenantId,
            ArtifactType = AgentDefinitionArtifactTypes.AgentVersion,
            NormalizedArtifactType = AgentDefinitionArtifactTypes.AgentVersion.ToUpperInvariant(),
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
            PayloadJson = AgentDefinitionPayloadParser.Serialize(payload),
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
                "agents.create",
                AuditResult.Success,
                null,
                $"Agent '{artifact.Name}' was created.",
                nameof(Artifact),
                artifact.Id.ToString(),
                RetentionCategory: AuditRetentionCategory.Operational),
            cancellationToken);

        return new CreateAgentDefinitionResponse(artifact.Id, version.Id, version.VersionLabel);
    }

    private async Task<Guid> ResolveDefaultAgentTypeVersionIdAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var version = await (
            from artifact in dbContext.Artifacts.AsNoTracking()
            join artifactVersion in dbContext.ArtifactVersions.AsNoTracking() on artifact.Id equals artifactVersion.ArtifactId
            where artifact.TenantId == tenantId
                && artifact.NormalizedArtifactType == AgentTypeDefinitionArtifactTypes.AgentTypeDefinition.ToUpperInvariant()
                && artifactVersion.ReadinessState == ArtifactReadinessState.Published
            orderby artifactVersion.PublishedAt descending, artifactVersion.CreatedAt descending
            select artifactVersion)
            .FirstOrDefaultAsync(cancellationToken);

        if (version is null)
        {
            throw new RequestValidationException(
                "No published agent type definition is available. Provide agentTypeDefinitionVersionId or publish an agent type first.");
        }

        return version.Id;
    }

    private static AgentDefinitionPayloadParser.AgentDefinitionPayloadDocument BuildPayload(
        CreateAgentDefinitionRequest request,
        Guid createdByUserId)
        => AgentDefinitionPayloadParser.Create(
            request.AgentKey,
            request.DisplayName,
            request.AgentDescription,
            request.AgentTypeDefinitionVersionId,
            request.SourceAgentTemplateVersionId,
            request.PreferredRuntimeAdapterKey,
            request.CompatibleModelPackageVersionIds,
            request.CompatibleOntologyVersionIds,
            request.ReferencedCapabilityDefinitionVersionIds,
            request.ReferencedBusinessPolicyDefinitionVersionIds,
            request.ReferencedOptimizationModelVersionIds,
            request.PromptTemplateVersionId,
            request.OutputSchemaVersionId,
            request.QueryIntentVersionId,
            request.RetrievalStrategyVersionId,
            request.ReferencedToolDefinitionVersionIds,
            request.ReferencedSkillDefinitionVersionIds,
            request.PrimaryModelProviderKey,
            request.PrimaryModelId,
            request.FallbackModels,
            request.SafeModeEnabled,
            request.PreviewModeDefault,
            request.BlockedModeMessage,
            request.CompatibilityTestNotes,
            request.CompatibilityFixtureKeys,
            createdByUserId,
            request.CompositionMetadata);

    private static AgentDefinitionPayloadParser.AgentDefinitionPayloadDocument BuildPayload(
        CreateAgentDefinitionVersionRequest request,
        Guid createdByUserId)
        => AgentDefinitionPayloadParser.Create(
            request.AgentKey,
            request.DisplayName,
            request.AgentDescription,
            request.AgentTypeDefinitionVersionId,
            request.SourceAgentTemplateVersionId,
            request.PreferredRuntimeAdapterKey,
            request.CompatibleModelPackageVersionIds,
            request.CompatibleOntologyVersionIds,
            request.ReferencedCapabilityDefinitionVersionIds,
            request.ReferencedBusinessPolicyDefinitionVersionIds,
            request.ReferencedOptimizationModelVersionIds,
            request.PromptTemplateVersionId,
            request.OutputSchemaVersionId,
            request.QueryIntentVersionId,
            request.RetrievalStrategyVersionId,
            request.ReferencedToolDefinitionVersionIds,
            request.ReferencedSkillDefinitionVersionIds,
            request.PrimaryModelProviderKey,
            request.PrimaryModelId,
            request.FallbackModels,
            request.SafeModeEnabled,
            request.PreviewModeDefault,
            request.BlockedModeMessage,
            request.CompatibilityTestNotes,
            request.CompatibilityFixtureKeys,
            createdByUserId,
            request.CompositionMetadata);

    private async Task<ResolvedDependencies> ResolveDependenciesAsync(
        Guid tenantId,
        AgentDefinitionPayloadParser.AgentDefinitionPayloadDocument document,
        CancellationToken cancellationToken)
    {
        var capabilityVersionIds = document.ReferencedCapabilityDefinitionVersionIds ?? [];
        var policyVersionIds = document.ReferencedBusinessPolicyDefinitionVersionIds ?? [];
        var optimizationVersionIds = document.ReferencedOptimizationModelVersionIds ?? [];
        var toolVersionIds = document.ReferencedToolDefinitionVersionIds ?? [];
        var skillVersionIds = document.ReferencedSkillDefinitionVersionIds ?? [];
        var packageIds = document.CompatibleModelPackageVersionIds ?? [];
        var ontologyIds = document.CompatibleOntologyVersionIds ?? [];

        var capabilities = capabilityVersionIds.Count == 0
            ? []
            : await (
                from version in dbContext.ArtifactVersions.AsNoTracking()
                join artifact in dbContext.Artifacts.AsNoTracking() on version.ArtifactId equals artifact.Id
                where version.TenantId == tenantId
                    && capabilityVersionIds.Contains(version.Id)
                    && artifact.ArtifactType == CapabilityDefinitionArtifactTypes.CapabilityDefinition
                select new AgentCapabilityReferenceResponse(
                    version.Id,
                    artifact.Id,
                    artifact.Name,
                    ExtractCapabilityKey(version.PayloadJson),
                    version.VersionLabel,
                    version.ReadinessState.ToString()))
                .ToListAsync(cancellationToken);

        var businessPolicies = policyVersionIds.Count == 0
            ? []
            : await (
                from version in dbContext.ArtifactVersions.AsNoTracking()
                join artifact in dbContext.Artifacts.AsNoTracking() on version.ArtifactId equals artifact.Id
                where version.TenantId == tenantId
                    && policyVersionIds.Contains(version.Id)
                    && artifact.ArtifactType == BusinessPolicyDefinitionArtifactTypes.BusinessPolicyDefinition
                select new AgentBusinessPolicyReferenceResponse(
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
                select new AgentOptimizationModelReferenceResponse(
                    version.Id,
                    artifact.Id,
                    artifact.Name,
                    ExtractOptimizationKey(version.PayloadJson),
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

        var toolResponses = tools.Select(item => new AgentToolReferenceResponse(
            item.version.Id,
            item.artifact.Id,
            item.artifact.Name,
            item.version.VersionLabel,
            item.version.ReadinessState.ToString(),
            ExtractToolRiskLevel(item.version.PayloadJson))).ToList();

        var skills = skillVersionIds.Count == 0
            ? []
            : await (
                from version in dbContext.ArtifactVersions.AsNoTracking()
                join artifact in dbContext.Artifacts.AsNoTracking() on version.ArtifactId equals artifact.Id
                where version.TenantId == tenantId
                    && skillVersionIds.Contains(version.Id)
                    && artifact.ArtifactType == SkillDefinitionArtifactTypes.SkillDefinition
                select new AgentSkillReferenceResponse(
                    version.Id,
                    artifact.Id,
                    artifact.Name,
                    ExtractSkillKey(version.PayloadJson),
                    version.VersionLabel,
                    version.ReadinessState.ToString()))
                .ToListAsync(cancellationToken);

        var packages = packageIds.Count == 0
            ? []
            : await dbContext.ModelPackageVersions
                .AsNoTracking()
                .Where(item => item.TenantId == tenantId && packageIds.Contains(item.Id))
                .Select(item => new AgentModelPackageReferenceResponse(
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
                .Select(item => new AgentOntologyReferenceResponse(
                    item.Id,
                    item.Key,
                    item.VersionLabel,
                    item.State.ToString()))
                .ToListAsync(cancellationToken);

        var promptTemplate = document.PromptTemplateVersionId is Guid promptId
            ? await ResolveArtifactVersionReferenceAsync(tenantId, promptId, cancellationToken)
            : null;
        var outputSchema = document.OutputSchemaVersionId is Guid outputId
            ? await ResolveArtifactVersionReferenceAsync(tenantId, outputId, cancellationToken)
            : null;

        AgentQueryIntentReferenceResponse? queryIntent = null;
        if (document.QueryIntentVersionId is Guid queryIntentId)
        {
            var intent = await dbContext.QueryIntentVersions
                .AsNoTracking()
                .SingleOrDefaultAsync(item => item.Id == queryIntentId && item.TenantId == tenantId, cancellationToken);
            if (intent is not null)
            {
                queryIntent = new AgentQueryIntentReferenceResponse(
                    intent.Id,
                    intent.IntentKey,
                    intent.VersionLabel,
                    intent.IsEnabled);
            }
        }

        AgentRetrievalStrategyReferenceResponse? retrievalStrategy = null;
        if (document.RetrievalStrategyVersionId is Guid retrievalStrategyId)
        {
            var strategy = await dbContext.RetrievalStrategyVersions
                .AsNoTracking()
                .SingleOrDefaultAsync(item => item.Id == retrievalStrategyId && item.TenantId == tenantId, cancellationToken);
            if (strategy is not null)
            {
                retrievalStrategy = new AgentRetrievalStrategyReferenceResponse(
                    strategy.Id,
                    strategy.StrategyKey,
                    strategy.VersionLabel,
                    strategy.IsEnabled);
            }
        }

        AgentTypeReferenceResponse? agentType = null;
        if (document.AgentTypeDefinitionVersionId != Guid.Empty)
        {
            var typeVersion = await dbContext.ArtifactVersions
                .AsNoTracking()
                .SingleOrDefaultAsync(item => item.Id == document.AgentTypeDefinitionVersionId && item.TenantId == tenantId, cancellationToken);
            if (typeVersion is not null)
            {
                var typeArtifact = await dbContext.Artifacts
                    .AsNoTracking()
                    .SingleOrDefaultAsync(item => item.Id == typeVersion.ArtifactId, cancellationToken);
                if (typeArtifact is not null)
                {
                    var typePayload = AgentTypeDefinitionPayloadParser.Deserialize(typeVersion.PayloadJson ?? "{}");
                    agentType = new AgentTypeReferenceResponse(
                        typeVersion.Id,
                        typeArtifact.Id,
                        typeArtifact.Name,
                        typePayload.TypeKey ?? string.Empty,
                        typeVersion.VersionLabel,
                        typeVersion.ReadinessState.ToString(),
                        typePayload.RiskBaseline ?? string.Empty);
                }
            }
        }

        return new ResolvedDependencies(
            agentType,
            capabilities,
            businessPolicies,
            optimizationModels,
            packages,
            ontologies,
            promptTemplate,
            outputSchema,
            queryIntent,
            retrievalStrategy,
            toolResponses,
            skills);
    }

    private async Task<AgentArtifactVersionReferenceResponse?> ResolveArtifactVersionReferenceAsync(
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

        return new AgentArtifactVersionReferenceResponse(
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
                AgentDefinitionArtifactTypes.AgentVersion,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new RequestValidationException(
                $"Artifact type '{artifact.ArtifactType}' does not match expected '{AgentDefinitionArtifactTypes.AgentVersion}'.");
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
                AgentDefinitionArtifactTypes.AgentVersion,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new RequestValidationException(
                $"Artifact type '{artifact.ArtifactType}' does not match expected '{AgentDefinitionArtifactTypes.AgentVersion}'.");
        }

        return artifact;
    }

    private async Task RequireDraftOwnerOrAdminAsync(
        ActiveTenantContext context,
        ArtifactVersion version,
        string action,
        CancellationToken cancellationToken)
    {
        var payload = AgentDefinitionPayloadParser.Deserialize(version.PayloadJson ?? "{}");
        if (payload.CreatedByUserId == context.UserId
            || await permissionService.HasPermissionAsync(context.TenantId, context.UserId, AgentPermissions.Admin, cancellationToken)
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
            "Only the draft creator or an agent administrator may perform this action.",
            cancellationToken);
        throw new TenantAccessDeniedException("User lacks draft ownership or agent administration permission.");
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
            $"The user lacks the {AgentPermissions.Read} permission.",
            cancellationToken);
        throw new TenantAccessDeniedException("User lacks agent read permission.");
    }

    private async Task<ActiveTenantContext> RequireCreatePermissionAsync(CancellationToken cancellationToken)
    {
        var context = await tenantContextResolver.ResolveAsync("agents.create", cancellationToken);
        if (await HasCreatePermissionAsync(context, cancellationToken))
        {
            return context;
        }

        await denialRecorder.RecordAsync(
            context.TenantId,
            context.UserId,
            "agents.create",
            "permission_denied",
            $"The user lacks the {AgentPermissions.Create} permission.",
            cancellationToken);
        throw new TenantAccessDeniedException("User lacks agent create permission.");
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
            $"The user lacks the {AgentPermissions.Readiness} permission.",
            cancellationToken);
        throw new TenantAccessDeniedException("User lacks agent readiness permission.");
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
        => await permissionService.HasPermissionAsync(context.TenantId, context.UserId, AgentPermissions.Read, cancellationToken)
            || await HasAdminPermissionAsync(context, cancellationToken);

    private async Task<bool> HasCreatePermissionAsync(ActiveTenantContext context, CancellationToken cancellationToken)
        => await permissionService.HasPermissionAsync(context.TenantId, context.UserId, AgentPermissions.Create, cancellationToken)
            || await HasAdminPermissionAsync(context, cancellationToken);

    private async Task<bool> HasReadinessPermissionAsync(ActiveTenantContext context, CancellationToken cancellationToken)
        => await permissionService.HasPermissionAsync(context.TenantId, context.UserId, AgentPermissions.Readiness, cancellationToken)
            || await HasAdminPermissionAsync(context, cancellationToken);

    private async Task<bool> HasAdminPermissionAsync(ActiveTenantContext context, CancellationToken cancellationToken)
        => await permissionService.HasPermissionAsync(context.TenantId, context.UserId, AgentPermissions.Admin, cancellationToken)
            || await permissionService.HasPermissionAsync(context.TenantId, context.UserId, IdentityPermissions.Wildcard, cancellationToken);

    private static string ExtractCapabilityKey(string? payloadJson)
    {
        if (string.IsNullOrWhiteSpace(payloadJson))
        {
            return string.Empty;
        }

        try
        {
            return CapabilityDefinitionPayloadParser.Deserialize(payloadJson).CapabilityKey ?? string.Empty;
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

    private static string ExtractSkillKey(string? payloadJson)
    {
        if (string.IsNullOrWhiteSpace(payloadJson))
        {
            return string.Empty;
        }

        try
        {
            return SkillDefinitionPayloadParser.Deserialize(payloadJson).SkillKey ?? string.Empty;
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

    private static string Slugify(string value)
    {
        var chars = value.Trim().ToLowerInvariant()
            .Select(character => char.IsLetterOrDigit(character) ? character : '-')
            .ToArray();
        var slug = new string(chars).Trim('-');
        while (slug.Contains("--", StringComparison.Ordinal))
        {
            slug = slug.Replace("--", "-", StringComparison.Ordinal);
        }

        return string.IsNullOrWhiteSpace(slug) ? $"agent-{Guid.NewGuid():N}"[..20] : slug;
    }

    private static string? TrimOptional(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private sealed record ResolvedDependencies(
        AgentTypeReferenceResponse? AgentType,
        IReadOnlyCollection<AgentCapabilityReferenceResponse> Capabilities,
        IReadOnlyCollection<AgentBusinessPolicyReferenceResponse> BusinessPolicies,
        IReadOnlyCollection<AgentOptimizationModelReferenceResponse> OptimizationModels,
        IReadOnlyCollection<AgentModelPackageReferenceResponse> ModelPackages,
        IReadOnlyCollection<AgentOntologyReferenceResponse> Ontologies,
        AgentArtifactVersionReferenceResponse? PromptTemplate,
        AgentArtifactVersionReferenceResponse? OutputSchema,
        AgentQueryIntentReferenceResponse? QueryIntent,
        AgentRetrievalStrategyReferenceResponse? RetrievalStrategy,
        IReadOnlyCollection<AgentToolReferenceResponse> Tools,
        IReadOnlyCollection<AgentSkillReferenceResponse> Skills);
}
