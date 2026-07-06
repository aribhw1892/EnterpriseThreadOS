using System.Text.Json;
using ETOS.Backend.AiTrace;
using ETOS.Backend.Artifacts;
using ETOS.Backend.Governance;
using ETOS.Backend.Identity;
using ETOS.Backend.Infrastructure.Persistence;
using ETOS.Backend.Workflows;
using ETOS.Backend.WorkflowRuns;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ETOS.Backend.WorkflowRuntime;

public sealed class WorkflowExecutionService(
    EnterpriseThreadDbContext dbContext,
    ITenantContextResolver tenantContextResolver,
    IAccessPermissionService permissionService,
    IAccessDenialRecorder denialRecorder,
    IAuditRecorder auditRecorder,
    IAiTraceRecorder aiTraceRecorder,
    IWorkflowRuntimeAdapterSelector adapterSelector,
    IOptions<WorkflowRuntimeOptions> runtimeOptions) : IWorkflowExecutionService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public Task<WorkflowExecutionResponse> PreviewAsync(
        Guid artifactId,
        Guid versionId,
        WorkflowExecutionRequest request,
        CancellationToken cancellationToken)
        => RunAsync(artifactId, versionId, request, WorkflowExecutionMode.Preview, cancellationToken);

    public Task<WorkflowExecutionResponse> TestRunAsync(
        Guid artifactId,
        Guid versionId,
        WorkflowExecutionRequest request,
        CancellationToken cancellationToken)
        => RunAsync(artifactId, versionId, request, WorkflowExecutionMode.TestRun, cancellationToken);

    public Task<WorkflowExecutionResponse> ExecuteAsync(
        Guid artifactId,
        Guid versionId,
        WorkflowExecutionRequest request,
        CancellationToken cancellationToken)
        => RunAsync(artifactId, versionId, request, WorkflowExecutionMode.Execute, cancellationToken);

    private async Task<WorkflowExecutionResponse> RunAsync(
        Guid artifactId,
        Guid versionId,
        WorkflowExecutionRequest request,
        WorkflowExecutionMode mode,
        CancellationToken cancellationToken)
    {
        var action = mode switch
        {
            WorkflowExecutionMode.Preview => "workflows.preview",
            WorkflowExecutionMode.TestRun => "workflows.preview",
            WorkflowExecutionMode.Execute => "workflows.execute",
            _ => throw new ArgumentOutOfRangeException(nameof(mode))
        };

        var (context, version, payload) = await LoadWorkflowVersionAsync(artifactId, versionId, action, cancellationToken);
        await RequireExecutionPermissionAsync(context, payload, version, mode, action, cancellationToken);

        var isPreview = mode == WorkflowExecutionMode.Preview;
        var isDryRun = mode == WorkflowExecutionMode.TestRun;
        var inputSafeSummary = BuildInputSafeSummary(request);

        var runtimeTrust = await RecalculateRuntimeTrustAsync(context.TenantId, payload, cancellationToken);
        var inheritedRiskSnapshotJson = payload.DerivedCapabilityRiskJson is null
            ? null
            : JsonSerializer.Serialize(payload.DerivedCapabilityRiskJson, JsonOptions);
        var runtimeTrustJson = JsonSerializer.Serialize(runtimeTrust, JsonOptions);

        if (payload.SafeModeEnabled && !isPreview)
        {
            return await FinalizeWholeWorkflowSafeModeBlockedAsync(
                context,
                version.Id,
                payload,
                request,
                isPreview,
                isDryRun,
                inputSafeSummary,
                inheritedRiskSnapshotJson,
                runtimeTrustJson,
                action,
                cancellationToken);
        }

        var safeModeActive = payload.SafeModeEnabled || runtimeTrust.TrustDowngraded;
        var workflowRun = new WorkflowRun
        {
            Id = Guid.NewGuid(),
            TenantId = context.TenantId,
            WorkflowVersionId = version.Id,
            RequestedByUserId = context.UserId,
            Status = WorkflowRunStatuses.Running,
            IsPreview = isPreview,
            SafeModeApplied = safeModeActive,
            PartialCompletion = payload.AllowPartialCompletion,
            InputSafeSummaryJson = inputSafeSummary,
            InheritedRiskSnapshotJson = inheritedRiskSnapshotJson,
            RuntimeTrustRecalculationJson = runtimeTrustJson,
            StartedAt = DateTimeOffset.UtcNow
        };
        dbContext.WorkflowRuns.Add(workflowRun);
        await dbContext.SaveChangesAsync(cancellationToken);

        if (runtimeTrust.TrustDowngraded)
        {
            dbContext.SafeModeEvents.Add(new SafeModeEvent
            {
                Id = Guid.NewGuid(),
                TenantId = context.TenantId,
                WorkflowRunId = workflowRun.Id,
                StepKey = "_runtime_trust",
                EventKind = SafeModeEventKinds.RuntimeTrustDowngrade,
                Reason = runtimeTrust.DowngradeReason ?? "Runtime trust recalculation downgraded workflow execution.",
                CreatedAt = DateTimeOffset.UtcNow
            });
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        var adapter = adapterSelector.Resolve(runtimeOptions.Value.AdapterKey);
        var adapterResult = await adapter.StartManualRunAsync(
            new WorkflowRuntimeStartRequest(
                workflowRun.Id,
                version.Id,
                artifactId,
                context.TenantId,
                context.UserId,
                payload,
                request.StructuredInputJson ?? "{}",
                mode,
                isPreview,
                isDryRun,
                safeModeActive,
                payload.AllowPartialCompletion),
            cancellationToken);

        workflowRun.Status = adapterResult.Status;
        workflowRun.SafeModeApplied = adapterResult.SafeModeApplied || safeModeActive;
        workflowRun.PartialCompletion = adapterResult.PartialCompletion;
        workflowRun.OutputSafeSummaryJson = BuildOutputSafeSummary(adapterResult.OutputContextJson);
        workflowRun.StepResultsJson = adapterResult.StepResultsJson;
        workflowRun.RecommendationArtifactIdsJson = adapterResult.RecommendationArtifactIds.Count == 0
            ? null
            : JsonSerializer.Serialize(adapterResult.RecommendationArtifactIds, JsonOptions);
        workflowRun.ReviewTaskArtifactIdsJson = adapterResult.ReviewTaskArtifactIds.Count == 0
            ? null
            : JsonSerializer.Serialize(adapterResult.ReviewTaskArtifactIds, JsonOptions);
        workflowRun.CompletedAt = DateTimeOffset.UtcNow;

        if (!string.IsNullOrWhiteSpace(adapterResult.OutputContextJson))
        {
            WorkflowReadOnlyGuards.GuardStructuredOutputAgainstDecisionCreation(adapterResult.OutputContextJson);
        }

        var audit = await auditRecorder.RecordAsync(
            new AuditRecordWriteRequest(
                context.TenantId,
                context.UserId,
                action,
                AuditResult.Success,
                null,
                $"Workflow run {workflowRun.Id} completed with status {workflowRun.Status}.",
                nameof(WorkflowRun),
                workflowRun.Id.ToString(),
                RetentionCategory: AuditRetentionCategory.Operational),
            cancellationToken);
        workflowRun.AuditRecordId = audit.Id;

        var traceId = await aiTraceRecorder.CreateFromWorkflowRunAsync(workflowRun.Id, audit.Id, cancellationToken);
        workflowRun.AiTraceRecordId = traceId;
        await dbContext.SaveChangesAsync(cancellationToken);

        return new WorkflowExecutionResponse(
            workflowRun.Id,
            workflowRun.Status,
            workflowRun.IsPreview,
            workflowRun.SafeModeApplied,
            workflowRun.PartialCompletion,
            workflowRun.OutputSafeSummaryJson,
            traceId,
            audit.Id,
            adapterResult.RecommendationArtifactIds,
            adapterResult.ReviewTaskArtifactIds,
            runtimeTrust.ValidationNotes);
    }

    private async Task<WorkflowExecutionResponse> FinalizeWholeWorkflowSafeModeBlockedAsync(
        ActiveTenantContext context,
        Guid workflowVersionId,
        WorkflowDefinitionPayloadParser.WorkflowDefinitionPayloadDocument payload,
        WorkflowExecutionRequest request,
        bool isPreview,
        bool isDryRun,
        string inputSafeSummary,
        string? inheritedRiskSnapshotJson,
        string runtimeTrustJson,
        string action,
        CancellationToken cancellationToken)
    {
        var workflowRun = new WorkflowRun
        {
            Id = Guid.NewGuid(),
            TenantId = context.TenantId,
            WorkflowVersionId = workflowVersionId,
            RequestedByUserId = context.UserId,
            Status = WorkflowRunStatuses.SafeModeBlocked,
            IsPreview = isPreview,
            SafeModeApplied = true,
            PartialCompletion = false,
            InputSafeSummaryJson = inputSafeSummary,
            OutputSafeSummaryJson = payload.BlockedModeMessage ?? "Workflow safe mode blocked execution.",
            InheritedRiskSnapshotJson = inheritedRiskSnapshotJson,
            RuntimeTrustRecalculationJson = runtimeTrustJson,
            StartedAt = DateTimeOffset.UtcNow,
            CompletedAt = DateTimeOffset.UtcNow
        };
        dbContext.WorkflowRuns.Add(workflowRun);
        await dbContext.SaveChangesAsync(cancellationToken);

        dbContext.SafeModeEvents.Add(new SafeModeEvent
        {
            Id = Guid.NewGuid(),
            TenantId = context.TenantId,
            WorkflowRunId = workflowRun.Id,
            StepKey = "_workflow",
            EventKind = SafeModeEventKinds.Blocked,
            Reason = payload.BlockedModeMessage ?? "Workflow safe mode is enabled and blocked non-preview execution.",
            BlockedAction = "workflow_execute",
            CreatedAt = DateTimeOffset.UtcNow
        });
        await dbContext.SaveChangesAsync(cancellationToken);

        var audit = await auditRecorder.RecordAsync(
            new AuditRecordWriteRequest(
                context.TenantId,
                context.UserId,
                action,
                AuditResult.Denied,
                null,
                payload.BlockedModeMessage ?? "Workflow safe mode blocked execution.",
                nameof(WorkflowRun),
                workflowRun.Id.ToString(),
                RetentionCategory: AuditRetentionCategory.Operational),
            cancellationToken);
        workflowRun.AuditRecordId = audit.Id;
        var traceId = await aiTraceRecorder.CreateFromWorkflowRunAsync(workflowRun.Id, audit.Id, cancellationToken);
        workflowRun.AiTraceRecordId = traceId;
        await dbContext.SaveChangesAsync(cancellationToken);

        return new WorkflowExecutionResponse(
            workflowRun.Id,
            workflowRun.Status,
            workflowRun.IsPreview,
            workflowRun.SafeModeApplied,
            workflowRun.PartialCompletion,
            workflowRun.OutputSafeSummaryJson,
            traceId,
            audit.Id,
            [],
            [],
            [payload.BlockedModeMessage ?? "Workflow safe mode blocked execution."]);
    }

    private async Task<RuntimeTrustRecalculationResult> RecalculateRuntimeTrustAsync(
        Guid tenantId,
        WorkflowDefinitionPayloadParser.WorkflowDefinitionPayloadDocument payload,
        CancellationToken cancellationToken)
    {
        var (notes, currentDerivedRisk) = await WorkflowDefinitionReadinessValidator.ValidatePublishedDependenciesAsync(
            dbContext,
            tenantId,
            payload,
            cancellationToken);

        var trustDowngraded = notes.Count > 0;
        var downgradeReason = trustDowngraded
            ? string.Join(" ", notes.Take(5))
            : null;

        if (!trustDowngraded
            && payload.DerivedCapabilityRiskJson?.EffectiveRiskLevel is not null
            && currentDerivedRisk?.EffectiveRiskLevel is not null
            && !string.Equals(
                payload.DerivedCapabilityRiskJson.EffectiveRiskLevel,
                currentDerivedRisk.EffectiveRiskLevel,
                StringComparison.OrdinalIgnoreCase))
        {
            trustDowngraded = true;
            downgradeReason =
                $"Derived risk changed from '{payload.DerivedCapabilityRiskJson.EffectiveRiskLevel}' to '{currentDerivedRisk.EffectiveRiskLevel}'.";
        }

        return new RuntimeTrustRecalculationResult(
            trustDowngraded,
            downgradeReason,
            notes,
            currentDerivedRisk);
    }

    private async Task<(ActiveTenantContext Context, ArtifactVersion Version, WorkflowDefinitionPayloadParser.WorkflowDefinitionPayloadDocument Payload)> LoadWorkflowVersionAsync(
        Guid artifactId,
        Guid versionId,
        string action,
        CancellationToken cancellationToken)
    {
        var context = await tenantContextResolver.ResolveAsync(action, cancellationToken);
        var artifact = await dbContext.Artifacts
            .SingleOrDefaultAsync(item => item.Id == artifactId, cancellationToken)
            ?? throw new RequestValidationException("Workflow artifact was not found.");

        if (artifact.TenantId != context.TenantId)
        {
            await denialRecorder.RecordAsync(context.TenantId, context.UserId, action, "tenant_access_denied", "Record belongs to a different tenant.", cancellationToken);
            throw new TenantAccessDeniedException("Record is not available in the active tenant.");
        }

        if (!artifact.ArtifactType.Equals(WorkflowDefinitionArtifactTypes.WorkflowVersion, StringComparison.OrdinalIgnoreCase))
        {
            throw new RequestValidationException("Artifact is not a workflow version.");
        }

        var version = await dbContext.ArtifactVersions
            .SingleOrDefaultAsync(item => item.Id == versionId && item.ArtifactId == artifactId, cancellationToken)
            ?? throw new RequestValidationException("Workflow version was not found.");

        var payload = WorkflowDefinitionPayloadParser.Deserialize(version.PayloadJson ?? "{}");
        WorkflowDefinitionPayloadParser.ValidateCore(payload);
        return (context, version, payload);
    }

    private async Task RequireExecutionPermissionAsync(
        ActiveTenantContext context,
        WorkflowDefinitionPayloadParser.WorkflowDefinitionPayloadDocument payload,
        ArtifactVersion version,
        WorkflowExecutionMode mode,
        string action,
        CancellationToken cancellationToken)
    {
        var isPublished = version.ReadinessState == ArtifactReadinessState.Published;

        switch (mode)
        {
            case WorkflowExecutionMode.Preview:
            case WorkflowExecutionMode.TestRun:
                if (isPublished)
                {
                    throw new RequestValidationException("Preview and test-run are only available for unpublished workflow versions.");
                }

                if (payload.CreatedByUserId != context.UserId
                    && !await HasAdminPermissionAsync(context, cancellationToken)
                    && !await permissionService.HasPermissionAsync(context.TenantId, context.UserId, WorkflowPermissions.Preview, cancellationToken)
                    && !await permissionService.HasPermissionAsync(context.TenantId, context.UserId, IdentityPermissions.Wildcard, cancellationToken))
                {
                    await denialRecorder.RecordAsync(
                        context.TenantId,
                        context.UserId,
                        action,
                        "permission_denied",
                        "Draft workflow preview/test requires creator, workflows.preview, or workflows.admin permission.",
                        cancellationToken);
                    throw new TenantAccessDeniedException("User lacks draft workflow preview permission.");
                }

                return;

            case WorkflowExecutionMode.Execute:
                if (!isPublished)
                {
                    throw new RequestValidationException("Execute requires a published workflow version.");
                }

                if (payload.TriggerConfig?.Manual?.Enabled != true)
                {
                    throw new RequestValidationException("Manual trigger must be enabled for workflow execution.");
                }

                if (await permissionService.HasPermissionAsync(context.TenantId, context.UserId, WorkflowPermissions.Execute, cancellationToken)
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
                    $"The user lacks the {WorkflowPermissions.Execute} permission.",
                    cancellationToken);
                throw new TenantAccessDeniedException("User lacks workflow execute permission.");

            default:
                throw new ArgumentOutOfRangeException(nameof(mode));
        }
    }

    private async Task<bool> HasAdminPermissionAsync(ActiveTenantContext context, CancellationToken cancellationToken)
        => await permissionService.HasPermissionAsync(context.TenantId, context.UserId, WorkflowPermissions.Admin, cancellationToken)
            || await permissionService.HasPermissionAsync(context.TenantId, context.UserId, IdentityPermissions.Wildcard, cancellationToken);

    private static string BuildInputSafeSummary(WorkflowExecutionRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.StructuredInputJson))
        {
            return JsonSerializer.Serialize(new { propertyCount = 0 }, JsonOptions);
        }

        try
        {
            using var document = JsonDocument.Parse(request.StructuredInputJson);
            var keys = document.RootElement.EnumerateObject().Select(pair => pair.Name).Take(10).ToArray();
            return JsonSerializer.Serialize(new { propertyCount = keys.Length, propertyKeys = keys }, JsonOptions);
        }
        catch (JsonException)
        {
            return JsonSerializer.Serialize(new { propertyCount = 0, invalidJson = true }, JsonOptions);
        }
    }

    private static string? BuildOutputSafeSummary(string? outputContextJson)
    {
        if (string.IsNullOrWhiteSpace(outputContextJson))
        {
            return null;
        }

        return outputContextJson.Length <= 1000 ? outputContextJson : outputContextJson[..1000];
    }

    private sealed record RuntimeTrustRecalculationResult(
        bool TrustDowngraded,
        string? DowngradeReason,
        IReadOnlyCollection<string> ValidationNotes,
        WorkflowDefinitionPayloadParser.DerivedCapabilityRiskDocument? CurrentDerivedRisk);
}
