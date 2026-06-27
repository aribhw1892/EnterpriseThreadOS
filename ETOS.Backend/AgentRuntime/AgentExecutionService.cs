using System.Text.Json;
using System.Text.Json.Nodes;
using ETOS.Backend.AgentRuns;
using ETOS.Backend.Agents;
using ETOS.Backend.AgentTemplates;
using ETOS.Backend.Artifacts;
using ETOS.Backend.AiTrace;
using ETOS.Backend.Governance;
using ETOS.Backend.GovernedChat;
using ETOS.Backend.GovernedQuery;
using ETOS.Backend.Identity;
using ETOS.Backend.Infrastructure.Persistence;
using ETOS.Backend.Recommendations;
using ETOS.Backend.ToolRegistry;
using Microsoft.EntityFrameworkCore;

namespace ETOS.Backend.AgentRuntime;

public interface IAgentExecutionService
{
    Task<AgentExecutionResponse> PreviewAsync(
        Guid artifactId,
        Guid versionId,
        AgentExecutionRequest request,
        CancellationToken cancellationToken);

    Task<AgentExecutionResponse> TestRunAsync(
        Guid artifactId,
        Guid versionId,
        AgentExecutionRequest request,
        CancellationToken cancellationToken);

    Task<AgentExecutionResponse> ExecuteAsync(
        Guid artifactId,
        Guid versionId,
        AgentExecutionRequest request,
        CancellationToken cancellationToken);
}

public sealed class AgentExecutionService(
    EnterpriseThreadDbContext dbContext,
    ITenantContextResolver tenantContextResolver,
    IAccessPermissionService permissionService,
    IAccessDenialRecorder denialRecorder,
    IAuditRecorder auditRecorder,
    IGovernedQueryService governedQueryService,
    IAgentRuntimePreviewOrchestrator previewOrchestrator,
    IOutputSchemaValidator outputSchemaValidator,
    IRecommendationFactory recommendationFactory,
    IAiTraceRecorder aiTraceRecorder) : IAgentExecutionService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public Task<AgentExecutionResponse> PreviewAsync(
        Guid artifactId,
        Guid versionId,
        AgentExecutionRequest request,
        CancellationToken cancellationToken)
        => RunAsync(artifactId, versionId, request, AgentExecutionMode.Preview, cancellationToken);

    public Task<AgentExecutionResponse> TestRunAsync(
        Guid artifactId,
        Guid versionId,
        AgentExecutionRequest request,
        CancellationToken cancellationToken)
        => RunAsync(artifactId, versionId, request, AgentExecutionMode.TestRun, cancellationToken);

    public Task<AgentExecutionResponse> ExecuteAsync(
        Guid artifactId,
        Guid versionId,
        AgentExecutionRequest request,
        CancellationToken cancellationToken)
        => RunAsync(artifactId, versionId, request, AgentExecutionMode.Execute, cancellationToken);

    private async Task<AgentExecutionResponse> RunAsync(
        Guid artifactId,
        Guid versionId,
        AgentExecutionRequest request,
        AgentExecutionMode mode,
        CancellationToken cancellationToken)
    {
        var action = mode switch
        {
            AgentExecutionMode.Preview => "agents.preview",
            AgentExecutionMode.TestRun => "agents.test",
            AgentExecutionMode.Execute => "agents.execute",
            _ => throw new ArgumentOutOfRangeException(nameof(mode))
        };

        var (context, artifact, version, payload) = await LoadAgentVersionAsync(artifactId, versionId, action, cancellationToken);
        await RequireExecutionPermissionAsync(context, payload, version, mode, action, cancellationToken);

        if (AgentMvpBlockedRuntimeAdapters.All.Contains(payload.PreferredRuntimeAdapterKey ?? string.Empty, StringComparer.OrdinalIgnoreCase))
        {
            throw new RequestValidationException(
                $"Agent runtime adapter '{payload.PreferredRuntimeAdapterKey}' is deferred and cannot execute in this release.");
        }

        var isPreview = mode == AgentExecutionMode.Preview;
        var isDryRun = mode == AgentExecutionMode.TestRun;
        var inputSafeSummary = BuildInputSafeSummary(request);

        if (payload.SafeModeEnabled && !isPreview)
        {
            return await FinalizeSafeModeBlockedAsync(
                context,
                version.Id,
                payload,
                request,
                isPreview,
                isDryRun,
                inputSafeSummary,
                action,
                cancellationToken);
        }

        var agentRun = new AgentRun
        {
            Id = Guid.NewGuid(),
            TenantId = context.TenantId,
            AgentVersionId = version.Id,
            RequestedByUserId = context.UserId,
            Status = AgentRunStatuses.Running,
            IsPreview = isPreview,
            IsDryRun = isDryRun,
            SafeModeApplied = false,
            InputSafeSummaryJson = inputSafeSummary,
            DerivedRiskSnapshotJson = payload.DerivedCapabilityRiskJson is null
                ? null
                : JsonSerializer.Serialize(payload.DerivedCapabilityRiskJson, JsonOptions),
            StartedAt = DateTimeOffset.UtcNow
        };
        dbContext.AgentRuns.Add(agentRun);
        await dbContext.SaveChangesAsync(cancellationToken);

        var toolRunIds = new List<Guid>();
        var validationNotes = new List<string>();

        try
        {
            var queryIntent = await RequireQueryIntentAsync(context.TenantId, payload.QueryIntentVersionId!.Value, cancellationToken);
            var queryText = ResolveQueryText(request, payload);
            var retrievalRun = await governedQueryService.RunAsync(
                new RunGovernedQueryRequest(
                    queryIntent.IntentKey,
                    request.StartGraphNodeId,
                    request.DocumentArtifactId,
                    null,
                    queryText,
                    2,
                    CreateAiTrace: false),
                cancellationToken);

            agentRun.RetrievalRunId = retrievalRun.Id;
            var contextPackage = retrievalRun.ContextPackage
                ?? throw new RequestValidationException("Governed retrieval did not produce a context package.");

            var governedContextSummaryJson = BuildGovernedContextSummary(contextPackage, retrievalRun);
            agentRun.GovernedContextSummaryJson = governedContextSummaryJson;

            var profile = await BuildExecutionProfileAsync(context.TenantId, version.Id, payload, cancellationToken);

            var orchestratorResult = await previewOrchestrator.RunPreviewAsync(
                profile,
                new AgentRuntimePreviewInput(
                    context.TenantId,
                    context.UserId,
                    governedContextSummaryJson,
                    request.StructuredInputJson,
                    isPreview || isDryRun,
                    isDryRun,
                    version.Id,
                    agentRun.Id,
                    _ => BuildToolInputJson(queryIntent.IntentKey, queryText, request)),
                cancellationToken);

            foreach (var prefetch in orchestratorResult.ToolPrefetchSummaries.Where(item => item.ToolRunId != Guid.Empty))
            {
                toolRunIds.Add(prefetch.ToolRunId);
            }

            var runtimeResult = orchestratorResult.RuntimeResult;
            if (!string.Equals(runtimeResult.Status, AgentRuntimeExecutionStatuses.Succeeded, StringComparison.OrdinalIgnoreCase))
            {
                var failureMessage = runtimeResult.TraceNotes.Count > 0
                    ? string.Join(" ", runtimeResult.TraceNotes)
                    : "Agent runtime execution failed.";
                throw new RequestValidationException(failureMessage);
            }

            var structuredOutputJson = runtimeResult.StructuredOutputJson
                ?? throw new RequestValidationException("Agent runtime did not return structured output.");

            outputSchemaValidator.Validate(structuredOutputJson, orchestratorResult.OutputSchemaJson);
            GuardAgainstDecisionCreation(orchestratorResult.OutputSchemaJson, structuredOutputJson);

            agentRun.StructuredOutputJson = structuredOutputJson;
            agentRun.OutputSafeSummaryJson = BuildOutputSafeSummary(structuredOutputJson);
            agentRun.FallbackUsedJson = runtimeResult.FallbackAppliedJson;
            agentRun.ValidationResultJson = validationNotes.Count == 0
                ? null
                : JsonSerializer.Serialize(validationNotes, JsonOptions);
            agentRun.Status = isPreview ? AgentRunStatuses.PreviewSucceeded : AgentRunStatuses.Succeeded;
            agentRun.CompletedAt = DateTimeOffset.UtcNow;

            CreateRecommendationResponse? recommendation = null;
            if (mode == AgentExecutionMode.Execute)
            {
                recommendation = await recommendationFactory.FromAgentRunAsync(agentRun.Id, cancellationToken);
                agentRun.RecommendationArtifactId = recommendation.ArtifactId;
            }

            var audit = await auditRecorder.RecordAsync(
                new AuditRecordWriteRequest(
                    context.TenantId,
                    context.UserId,
                    action,
                    AuditResult.Success,
                    null,
                    $"Agent run '{agentRun.Id}' completed with status '{agentRun.Status}'.",
                    nameof(AgentRun),
                    agentRun.Id.ToString(),
                    RetentionCategory: AuditRetentionCategory.Operational,
                    IsArchiveEligible: true),
                cancellationToken);
            agentRun.AuditRecordId = audit.Id;

            var traceId = await aiTraceRecorder.CreateFromAgentRunAsync(agentRun.Id, audit.Id, cancellationToken);
            agentRun.AiTraceRecordId = traceId;
            await dbContext.SaveChangesAsync(cancellationToken);

            return new AgentExecutionResponse(
                agentRun.Id,
                agentRun.Status,
                agentRun.IsPreview,
                agentRun.IsDryRun,
                agentRun.StructuredOutputJson,
                agentRun.OutputSafeSummaryJson,
                recommendation?.ArtifactId,
                recommendation?.VersionId,
                traceId,
                agentRun.RetrievalRunId,
                toolRunIds,
                validationNotes);
        }
        catch (Exception exception) when (exception is RequestValidationException or InvalidOperationException)
        {
            agentRun.Status = AgentRunStatuses.Failed;
            agentRun.ErrorSafeSummary = Trim(exception.Message, 1000);
            agentRun.CompletedAt = DateTimeOffset.UtcNow;
            await dbContext.SaveChangesAsync(cancellationToken);
            throw;
        }
        catch (Exception exception)
        {
            agentRun.Status = AgentRunStatuses.Failed;
            agentRun.ErrorSafeSummary = Trim(exception.Message, 1000);
            agentRun.CompletedAt = DateTimeOffset.UtcNow;
            await dbContext.SaveChangesAsync(cancellationToken);
            throw new RequestValidationException($"Agent execution failed: {exception.Message}");
        }
    }

    private async Task<AgentExecutionResponse> FinalizeSafeModeBlockedAsync(
        ActiveTenantContext context,
        Guid agentVersionId,
        AgentDefinitionPayloadParser.AgentDefinitionPayloadDocument payload,
        AgentExecutionRequest request,
        bool isPreview,
        bool isDryRun,
        string inputSafeSummary,
        string action,
        CancellationToken cancellationToken)
    {
        var blockedMessage = string.IsNullOrWhiteSpace(payload.BlockedModeMessage)
            ? "Agent safe mode is enabled. Execution is blocked until safe mode is disabled."
            : payload.BlockedModeMessage.Trim();

        var agentRun = new AgentRun
        {
            Id = Guid.NewGuid(),
            TenantId = context.TenantId,
            AgentVersionId = agentVersionId,
            RequestedByUserId = context.UserId,
            Status = AgentRunStatuses.SafeModeBlocked,
            IsPreview = isPreview,
            IsDryRun = isDryRun,
            SafeModeApplied = true,
            InputSafeSummaryJson = inputSafeSummary,
            ErrorSafeSummary = blockedMessage,
            DerivedRiskSnapshotJson = payload.DerivedCapabilityRiskJson is null
                ? null
                : JsonSerializer.Serialize(payload.DerivedCapabilityRiskJson, JsonOptions),
            StartedAt = DateTimeOffset.UtcNow,
            CompletedAt = DateTimeOffset.UtcNow
        };
        dbContext.AgentRuns.Add(agentRun);

        var audit = await auditRecorder.RecordAsync(
            new AuditRecordWriteRequest(
                context.TenantId,
                context.UserId,
                action,
                AuditResult.Denied,
                null,
                blockedMessage,
                nameof(AgentRun),
                agentRun.Id.ToString(),
                RetentionCategory: AuditRetentionCategory.Operational,
                IsArchiveEligible: true),
            cancellationToken);
        agentRun.AuditRecordId = audit.Id;

        var traceId = await aiTraceRecorder.CreateFromAgentRunAsync(agentRun.Id, audit.Id, cancellationToken);
        agentRun.AiTraceRecordId = traceId;
        await dbContext.SaveChangesAsync(cancellationToken);

        return new AgentExecutionResponse(
            agentRun.Id,
            agentRun.Status,
            agentRun.IsPreview,
            agentRun.IsDryRun,
            null,
            null,
            null,
            null,
            traceId,
            null,
            [],
            [blockedMessage]);
    }

    private async Task<(ActiveTenantContext Context, Artifact Artifact, ArtifactVersion Version, AgentDefinitionPayloadParser.AgentDefinitionPayloadDocument Payload)> LoadAgentVersionAsync(
        Guid artifactId,
        Guid versionId,
        string action,
        CancellationToken cancellationToken)
    {
        var context = await tenantContextResolver.ResolveAsync(action, cancellationToken);
        var artifact = await dbContext.Artifacts
            .SingleOrDefaultAsync(item => item.Id == artifactId, cancellationToken)
            ?? throw new RequestValidationException("Agent artifact was not found.");

        if (artifact.TenantId != context.TenantId)
        {
            await RecordTenantMismatchAsync(context, action, cancellationToken);
        }

        if (!artifact.ArtifactType.Equals(AgentDefinitionArtifactTypes.AgentVersion, StringComparison.OrdinalIgnoreCase))
        {
            throw new RequestValidationException("Artifact is not an agent version.");
        }

        var version = await dbContext.ArtifactVersions
            .SingleOrDefaultAsync(item => item.Id == versionId && item.ArtifactId == artifactId, cancellationToken)
            ?? throw new RequestValidationException("Agent version was not found.");

        if (version.TenantId != context.TenantId)
        {
            await RecordTenantMismatchAsync(context, action, cancellationToken);
        }

        var payload = AgentDefinitionPayloadParser.Deserialize(version.PayloadJson ?? "{}");
        AgentDefinitionPayloadParser.ValidateCore(payload);
        return (context, artifact, version, payload);
    }

    private async Task RequireExecutionPermissionAsync(
        ActiveTenantContext context,
        AgentDefinitionPayloadParser.AgentDefinitionPayloadDocument payload,
        ArtifactVersion version,
        AgentExecutionMode mode,
        string action,
        CancellationToken cancellationToken)
    {
        var isPublished = version.ReadinessState == ArtifactReadinessState.Published;

        switch (mode)
        {
            case AgentExecutionMode.Preview:
            case AgentExecutionMode.TestRun:
                if (isPublished)
                {
                    throw new RequestValidationException("Preview and test-run are only available for unpublished agent versions.");
                }

                if (payload.CreatedByUserId != context.UserId
                    && !await HasAdminPermissionAsync(context, cancellationToken)
                    && !await permissionService.HasPermissionAsync(context.TenantId, context.UserId, AgentPermissions.Test, cancellationToken)
                    && !await permissionService.HasPermissionAsync(context.TenantId, context.UserId, IdentityPermissions.Wildcard, cancellationToken))
                {
                    await denialRecorder.RecordAsync(
                        context.TenantId,
                        context.UserId,
                        action,
                        "permission_denied",
                        "Draft agent preview/test requires creator, agents.test, or agents.admin permission.",
                        cancellationToken);
                    throw new TenantAccessDeniedException("User lacks draft agent test permission.");
                }

                return;

            case AgentExecutionMode.Execute:
                if (!isPublished)
                {
                    throw new RequestValidationException("Execute requires a published agent version.");
                }

                if (await permissionService.HasPermissionAsync(context.TenantId, context.UserId, AgentPermissions.Execute, cancellationToken)
                    || await HasAdminPermissionAsync(context, cancellationToken)
                    || await permissionService.HasPermissionAsync(context.TenantId, context.UserId, IdentityPermissions.Wildcard, cancellationToken))
                {
                    return;
                }

                await denialRecorder.RecordAsync(
                    context.TenantId,
                    context.UserId,
                    action,
                    "permission_denied",
                    $"The user lacks the {AgentPermissions.Execute} permission.",
                    cancellationToken);
                throw new TenantAccessDeniedException("User lacks agent execute permission.");

            default:
                throw new ArgumentOutOfRangeException(nameof(mode));
        }
    }

    private async Task<QueryIntentVersion> RequireQueryIntentAsync(
        Guid tenantId,
        Guid queryIntentVersionId,
        CancellationToken cancellationToken)
        => await dbContext.QueryIntentVersions
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == queryIntentVersionId && item.TenantId == tenantId, cancellationToken)
            ?? throw new RequestValidationException("Pinned query intent version was not found.");

    private static string ResolveQueryText(
        AgentExecutionRequest request,
        AgentDefinitionPayloadParser.AgentDefinitionPayloadDocument payload)
    {
        if (!string.IsNullOrWhiteSpace(request.QueryText))
        {
            return request.QueryText.Trim();
        }

        if (!string.IsNullOrWhiteSpace(request.StructuredInputJson))
        {
            try
            {
                using var document = JsonDocument.Parse(request.StructuredInputJson);
                foreach (var propertyName in new[] { "queryText", "question", "prompt", "message" })
                {
                    if (document.RootElement.TryGetProperty(propertyName, out var property)
                        && property.ValueKind == JsonValueKind.String)
                    {
                        var value = property.GetString();
                        if (!string.IsNullOrWhiteSpace(value))
                        {
                            return value.Trim();
                        }
                    }
                }
            }
            catch (JsonException)
            {
                // Fall through to default query text.
            }
        }

        return payload.DisplayName ?? payload.AgentKey ?? "Agent execution";
    }

    private static string BuildToolInputJson(string intentKey, string queryText, AgentExecutionRequest request)
        => JsonSerializer.Serialize(new
        {
            intentKey,
            queryText,
            startGraphNodeId = request.StartGraphNodeId,
            documentArtifactId = request.DocumentArtifactId,
            maxDepth = 2
        }, JsonOptions);

    private static string BuildGovernedContextSummary(ContextPackageResponse package, RetrievalRunResponse retrievalRun)
        => JsonSerializer.Serialize(new
        {
            retrievalRunId = retrievalRun.Id,
            intentKey = retrievalRun.QueryIntent.IntentKey,
            strategyKey = retrievalRun.RetrievalStrategy.StrategyKey,
            safeSummary = retrievalRun.SafeSummary,
            retrievedCount = retrievalRun.RetrievedCount,
            filteredCount = retrievalRun.FilteredCount,
            deniedCount = retrievalRun.DeniedCount,
            llmVisibleContext = package.LlmVisibleContext
        }, JsonOptions);

    private static string BuildInputSafeSummary(AgentExecutionRequest request)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(request.QueryText))
        {
            parts.Add($"queryText={Trim(request.QueryText, 300)}");
        }

        if (!string.IsNullOrWhiteSpace(request.StructuredInputJson))
        {
            parts.Add($"structuredInput={Trim(request.StructuredInputJson, 500)}");
        }

        return parts.Count == 0 ? "{}" : string.Join("; ", parts);
    }

    private static string BuildOutputSafeSummary(string structuredOutputJson)
    {
        try
        {
            using var document = JsonDocument.Parse(structuredOutputJson);
            foreach (var propertyName in new[] { "summary", "answer", "title", "recommendation", "rationale" })
            {
                if (document.RootElement.TryGetProperty(propertyName, out var property)
                    && property.ValueKind == JsonValueKind.String)
                {
                    var value = property.GetString();
                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        return Trim(value, 1000);
                    }
                }
            }
        }
        catch (JsonException)
        {
            // Fall through.
        }

        return Trim(structuredOutputJson, 1000);
    }

    private static void GuardAgainstDecisionCreation(string outputSchemaJson, string structuredOutputJson)
    {
        if (OutputSchemaCreatesDecision(outputSchemaJson))
        {
            throw new RequestValidationException("Agent output schema must not create decision artifacts.");
        }

        try
        {
            using var output = JsonDocument.Parse(structuredOutputJson);
            foreach (var property in output.RootElement.EnumerateObject())
            {
                if (property.Name.Equals("createsDecision", StringComparison.OrdinalIgnoreCase)
                    && property.Value.ValueKind == JsonValueKind.True)
                {
                    throw new RequestValidationException("Agent structured output must not create decision artifacts.");
                }
            }
        }
        catch (JsonException)
        {
            throw new RequestValidationException("Agent structured output is not valid JSON.");
        }
    }

    private static bool OutputSchemaCreatesDecision(string? payloadJson)
    {
        if (string.IsNullOrWhiteSpace(payloadJson))
        {
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(payloadJson);
            foreach (var property in document.RootElement.EnumerateObject())
            {
                if (property.Name.Equals("createsDecision", StringComparison.OrdinalIgnoreCase)
                    && property.Value.ValueKind == JsonValueKind.True)
                {
                    return true;
                }
            }
        }
        catch (JsonException)
        {
            return false;
        }

        return false;
    }

    private async Task<AgentExecutionProfile> BuildExecutionProfileAsync(
        Guid tenantId,
        Guid agentVersionId,
        AgentDefinitionPayloadParser.AgentDefinitionPayloadDocument payload,
        CancellationToken cancellationToken)
    {
        var patternCategory = "investigator";
        if (payload.SourceAgentTemplateVersionId is Guid templateVersionId)
        {
            var templateVersion = await dbContext.ArtifactVersions
                .AsNoTracking()
                .SingleOrDefaultAsync(item => item.Id == templateVersionId && item.TenantId == tenantId, cancellationToken);
            if (templateVersion?.PayloadJson is not null)
            {
                var template = AgentTemplateDefinitionPayloadParser.Deserialize(templateVersion.PayloadJson);
                if (!string.IsNullOrWhiteSpace(template.PatternCategory))
                {
                    patternCategory = template.PatternCategory.Trim();
                }
            }
        }

        return AgentExecutionProfile.FromAgentPayload(payload.AgentKey!.Trim(), patternCategory, agentVersionId, payload);
    }

    private async Task<bool> HasAdminPermissionAsync(ActiveTenantContext context, CancellationToken cancellationToken)
        => await permissionService.HasPermissionAsync(context.TenantId, context.UserId, AgentPermissions.Admin, cancellationToken);

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

    private static string Trim(string value, int maxLength)
        => value.Length <= maxLength ? value : value[..maxLength];

    private enum AgentExecutionMode
    {
        Preview,
        TestRun,
        Execute
    }
}
