using System.Text.Json;
using System.Text.Json.Nodes;
using ETOS.Backend.AiTrace;
using ETOS.Backend.Artifacts;
using ETOS.Backend.Governance;
using ETOS.Backend.Identity;
using ETOS.Backend.Infrastructure.Persistence;
using ETOS.Backend.Platform.JsonSchema;
using Microsoft.EntityFrameworkCore;

namespace ETOS.Backend.ToolRegistry;

public interface IToolGateway
{
    Task<ToolExecutionResponse> DryRunAsync(
        Guid artifactId,
        Guid versionId,
        ToolExecutionRequest request,
        CancellationToken cancellationToken);

    Task<ToolExecutionResponse> ExecuteAsync(
        Guid artifactId,
        Guid versionId,
        ToolExecutionRequest request,
        CancellationToken cancellationToken);
}

public sealed class ToolGatewayService(
    EnterpriseThreadDbContext dbContext,
    ITenantContextResolver tenantContextResolver,
    IAccessPermissionService permissionService,
    IAccessDenialRecorder denialRecorder,
    IAuditRecorder auditRecorder,
    IJsonSchemaValidator jsonSchemaValidator,
    ITenantSecretProvider tenantSecretProvider,
    IAiTraceRecorder aiTraceRecorder,
    IEnumerable<IToolHandler> toolHandlers) : IToolGateway
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly IReadOnlyDictionary<string, IToolHandler> handlerLookup = toolHandlers
        .ToDictionary(handler => handler.HandlerKey, StringComparer.OrdinalIgnoreCase);

    public Task<ToolExecutionResponse> DryRunAsync(
        Guid artifactId,
        Guid versionId,
        ToolExecutionRequest request,
        CancellationToken cancellationToken)
        => RunInternalAsync(artifactId, versionId, request, isDryRun: true, cancellationToken);

    public Task<ToolExecutionResponse> ExecuteAsync(
        Guid artifactId,
        Guid versionId,
        ToolExecutionRequest request,
        CancellationToken cancellationToken)
        => RunInternalAsync(artifactId, versionId, request, isDryRun: false, cancellationToken);

    private async Task<ToolExecutionResponse> RunInternalAsync(
        Guid artifactId,
        Guid versionId,
        ToolExecutionRequest request,
        bool isDryRun,
        CancellationToken cancellationToken)
    {
        var action = isDryRun ? "tools.dry_run" : "tools.execute";
        var context = await RequireExecutionPermissionAsync(action, isDryRun, cancellationToken);
        var (artifact, version, document) = await RequirePublishedToolAsync(
            artifactId,
            versionId,
            action,
            context,
            cancellationToken);

        foreach (var permissionKey in document.RequiredPermissionKeys ?? [])
        {
            if (!await permissionService.HasPermissionAsync(context.TenantId, context.UserId, permissionKey, cancellationToken)
                && !await HasAdminPermissionAsync(context, cancellationToken))
            {
                await denialRecorder.RecordAsync(
                    context.TenantId,
                    context.UserId,
                    action,
                    "permission_denied",
                    $"Tool requires permission '{permissionKey}'.",
                    cancellationToken);
                throw new TenantAccessDeniedException($"User lacks required tool permission '{permissionKey}'.");
            }
        }

        var inputSafeSummary = BuildInputSafeSummary(request.InputJson);
        var validationNotes = new List<string>();

        try
        {
            jsonSchemaValidator.ValidateDocumentAgainstSchema(request.InputJson, document.InputSchemaJson ?? "{}");
        }
        catch (RequestValidationException exception)
        {
            return await PersistBlockedRunAsync(
                context,
                version.Id,
                document.ConnectorDefinitionVersionId,
                isDryRun,
                inputSafeSummary,
                [exception.Message],
                exception.Message,
                action,
                request.ParentAgentRunId,
                cancellationToken);
        }

        ConnectorDefinitionPayloadParser.ConnectorDefinitionPayloadDocument? connectorDocument = null;
        string? connectorCredentialSafeSummaryJson = null;
        if (document.ConnectorDefinitionVersionId is Guid connectorVersionId)
        {
            connectorDocument = await LoadConnectorDocumentAsync(connectorVersionId, context.TenantId, cancellationToken);
            if (connectorDocument.WritesExternalSystem || !connectorDocument.ExecutionEnabled)
            {
                return await PersistBlockedRunAsync(
                    context,
                    version.Id,
                    connectorVersionId,
                    isDryRun,
                    inputSafeSummary,
                    ["Connector execution is disabled in MVP."],
                    connectorDocument.DisabledReason ?? "Connector execution is disabled in MVP.",
                    action,
                    request.ParentAgentRunId,
                    cancellationToken);
            }

            var credential = await tenantSecretProvider.IssueScopedCredentialAsync(
                context.TenantId,
                connectorDocument.ConnectorKey!,
                connectorDocument.CredentialScopeKey!,
                cancellationToken);
            connectorCredentialSafeSummaryJson = JsonSerializer.Serialize(credential, JsonOptions);
        }

        if (document.WritesExternalSystem)
        {
            return await PersistBlockedRunAsync(
                context,
                version.Id,
                document.ConnectorDefinitionVersionId,
                isDryRun,
                inputSafeSummary,
                ["Write-capable tools are disabled in MVP."],
                "Write-capable tools are disabled in MVP.",
                action,
                request.ParentAgentRunId,
                cancellationToken);
        }

        if (string.IsNullOrWhiteSpace(document.InternalHandlerKey)
            || !handlerLookup.TryGetValue(document.InternalHandlerKey, out var handler))
        {
            return await PersistBlockedRunAsync(
                context,
                version.Id,
                document.ConnectorDefinitionVersionId,
                isDryRun,
                inputSafeSummary,
                [$"Unknown internal handler '{document.InternalHandlerKey}'."],
                "Tool handler is not available.",
                action,
                request.ParentAgentRunId,
                cancellationToken);
        }

        var handlerContext = new ToolHandlerContext(
            context.TenantId,
            context.UserId,
            request.InputJson,
            document,
            connectorDocument);

        if (isDryRun)
        {
            var simulation = handler.SimulateDryRun(handlerContext);
            var toolRun = await PersistRunAsync(
                context,
                version.Id,
                document.ConnectorDefinitionVersionId,
                true,
                ToolRunStatuses.DryRunSucceeded,
                inputSafeSummary,
                simulation.SimulationSafeSummary,
                JsonSerializer.Serialize(new { mode = "dry-run", simulation.ExpectedOutputSchemaJson }, JsonOptions),
                null,
                connectorCredentialSafeSummaryJson ?? simulation.ConnectorCredentialSafeSummaryJson,
                null,
                request.ParentAgentRunId,
                cancellationToken);

            var auditId = await RecordAuditAsync(context, action, toolRun.Id, "Tool dry-run completed.", cancellationToken);
            toolRun.AuditRecordId = auditId;
            var traceId = await aiTraceRecorder.CreateFromToolRunAsync(toolRun.Id, auditId, cancellationToken);
            toolRun.AiTraceRecordId = traceId;
            await dbContext.SaveChangesAsync(cancellationToken);

            return new ToolExecutionResponse(
                toolRun.Id,
                toolRun.Status,
                toolRun.OutputSafeSummaryJson,
                traceId,
                auditId,
                validationNotes);
        }

        try
        {
            var result = await handler.ExecuteAsync(handlerContext, cancellationToken);
            if (!result.Succeeded)
            {
                return await PersistBlockedRunAsync(
                    context,
                    version.Id,
                    document.ConnectorDefinitionVersionId,
                    false,
                    inputSafeSummary,
                    validationNotes,
                    result.ErrorSafeSummary ?? "Tool execution failed.",
                    action,
                    request.ParentAgentRunId,
                    cancellationToken);
            }

            jsonSchemaValidator.ValidateDocumentAgainstSchema(result.OutputSafeSummaryJson, document.OutputSchemaJson ?? "{}");

            var succeededRun = await PersistRunAsync(
                context,
                version.Id,
                document.ConnectorDefinitionVersionId,
                false,
                ToolRunStatuses.Succeeded,
                inputSafeSummary,
                result.OutputSafeSummaryJson,
                JsonSerializer.Serialize(new { validated = true }, JsonOptions),
                null,
                connectorCredentialSafeSummaryJson,
                result.RetrievalRunId,
                request.ParentAgentRunId,
                cancellationToken);

            var successAuditId = await RecordAuditAsync(context, action, succeededRun.Id, "Tool execution completed.", cancellationToken);
            succeededRun.AuditRecordId = successAuditId;
            var successTraceId = await aiTraceRecorder.CreateFromToolRunAsync(succeededRun.Id, successAuditId, cancellationToken);
            succeededRun.AiTraceRecordId = successTraceId;
            await dbContext.SaveChangesAsync(cancellationToken);

            return new ToolExecutionResponse(
                succeededRun.Id,
                succeededRun.Status,
                succeededRun.OutputSafeSummaryJson,
                successTraceId,
                successAuditId,
                validationNotes);
        }
        catch (RequestValidationException exception)
        {
            return await PersistBlockedRunAsync(
                context,
                version.Id,
                document.ConnectorDefinitionVersionId,
                false,
                inputSafeSummary,
                [exception.Message],
                exception.Message,
                action,
                request.ParentAgentRunId,
                cancellationToken);
        }
        catch (Exception exception)
        {
            var failedRun = await PersistRunAsync(
                context,
                version.Id,
                document.ConnectorDefinitionVersionId,
                false,
                ToolRunStatuses.Failed,
                inputSafeSummary,
                null,
                null,
                exception.Message,
                connectorCredentialSafeSummaryJson,
                null,
                request.ParentAgentRunId,
                cancellationToken);
            var failedAuditId = await RecordAuditAsync(context, action, failedRun.Id, "Tool execution failed.", cancellationToken);
            failedRun.AuditRecordId = failedAuditId;
            await dbContext.SaveChangesAsync(cancellationToken);

            return new ToolExecutionResponse(
                failedRun.Id,
                failedRun.Status,
                failedRun.OutputSafeSummaryJson,
                null,
                failedAuditId,
                [exception.Message]);
        }
    }

    private async Task<ToolExecutionResponse> PersistBlockedRunAsync(
        ActiveTenantContext context,
        Guid toolVersionId,
        Guid? connectorVersionId,
        bool isDryRun,
        string inputSafeSummary,
        IReadOnlyCollection<string> validationNotes,
        string errorSafeSummary,
        string action,
        Guid? parentAgentRunId,
        CancellationToken cancellationToken)
    {
        var run = await PersistRunAsync(
            context,
            toolVersionId,
            connectorVersionId,
            isDryRun,
            ToolRunStatuses.Blocked,
            inputSafeSummary,
            null,
            JsonSerializer.Serialize(validationNotes, JsonOptions),
            errorSafeSummary,
            null,
            null,
            parentAgentRunId,
            cancellationToken);
        var auditId = await RecordAuditAsync(context, action, run.Id, errorSafeSummary, cancellationToken);
        run.AuditRecordId = auditId;
        await dbContext.SaveChangesAsync(cancellationToken);
        return new ToolExecutionResponse(run.Id, run.Status, run.OutputSafeSummaryJson, null, auditId, validationNotes);
    }

    private async Task<ToolRun> PersistRunAsync(
        ActiveTenantContext context,
        Guid toolVersionId,
        Guid? connectorVersionId,
        bool isDryRun,
        string status,
        string inputSafeSummary,
        string? outputSafeSummary,
        string? validationResultJson,
        string? errorSafeSummary,
        string? connectorCredentialSafeSummaryJson,
        Guid? retrievalRunId,
        Guid? parentAgentRunId,
        CancellationToken cancellationToken)
    {
        var run = new ToolRun
        {
            Id = Guid.NewGuid(),
            TenantId = context.TenantId,
            ToolDefinitionVersionId = toolVersionId,
            ConnectorDefinitionVersionId = connectorVersionId,
            ParentAgentRunId = parentAgentRunId,
            RequestedByUserId = context.UserId,
            Status = status,
            IsDryRun = isDryRun,
            InputSafeSummaryJson = ToolSafeSummaryTruncator.Truncate(inputSafeSummary) ?? inputSafeSummary,
            OutputSafeSummaryJson = ToolSafeSummaryTruncator.Truncate(outputSafeSummary),
            ValidationResultJson = ToolSafeSummaryTruncator.Truncate(validationResultJson),
            ErrorSafeSummary = ToolSafeSummaryTruncator.Truncate(errorSafeSummary),
            ConnectorCredentialSafeSummaryJson = ToolSafeSummaryTruncator.Truncate(connectorCredentialSafeSummaryJson),
            RetrievalRunId = retrievalRunId,
            CreatedAt = DateTimeOffset.UtcNow,
            CompletedAt = DateTimeOffset.UtcNow
        };
        dbContext.ToolRuns.Add(run);
        await dbContext.SaveChangesAsync(cancellationToken);
        return run;
    }

    private async Task<Guid> RecordAuditAsync(
        ActiveTenantContext context,
        string action,
        Guid toolRunId,
        string summary,
        CancellationToken cancellationToken)
    {
        var audit = await auditRecorder.RecordAsync(
            new AuditRecordWriteRequest(
                context.TenantId,
                context.UserId,
                action,
                AuditResult.Success,
                null,
                summary,
                nameof(ToolRun),
                toolRunId.ToString(),
                RetentionCategory: AuditRetentionCategory.Operational),
            cancellationToken);
        return audit.Id;
    }

    private static string BuildInputSafeSummary(string inputJson)
    {
        try
        {
            var node = JsonNode.Parse(inputJson) as JsonObject ?? new JsonObject();
            var keys = node.Select(pair => pair.Key).Take(10).ToArray();
            return JsonSerializer.Serialize(new
            {
                propertyCount = node.Count,
                propertyKeys = keys
            }, JsonOptions);
        }
        catch
        {
            return JsonSerializer.Serialize(new { propertyCount = 0, propertyKeys = Array.Empty<string>() }, JsonOptions);
        }
    }

    private async Task<ConnectorDefinitionPayloadParser.ConnectorDefinitionPayloadDocument> LoadConnectorDocumentAsync(
        Guid connectorVersionId,
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        var version = await dbContext.ArtifactVersions
            .AsNoTracking()
            .Include(item => item.Artifact)
            .SingleOrDefaultAsync(item => item.Id == connectorVersionId, cancellationToken)
            ?? throw new RequestValidationException("Connector definition was not found.");

        if (version.TenantId != tenantId)
        {
            throw new TenantAccessDeniedException("Connector definition is not available in the active tenant.");
        }

        return ConnectorDefinitionPayloadParser.Deserialize(version.PayloadJson ?? "{}");
    }

    private async Task<(Artifact Artifact, ArtifactVersion Version, ToolDefinitionPayloadParser.ToolDefinitionPayloadDocument Document)> RequirePublishedToolAsync(
        Guid artifactId,
        Guid versionId,
        string action,
        ActiveTenantContext context,
        CancellationToken cancellationToken)
    {
        var artifact = await dbContext.Artifacts
            .SingleOrDefaultAsync(item => item.Id == artifactId, cancellationToken)
            ?? throw new RequestValidationException("Artifact was not found.");

        if (artifact.TenantId != context.TenantId)
        {
            await denialRecorder.RecordAsync(context.TenantId, context.UserId, action, "tenant_access_denied", "Record belongs to a different tenant.", cancellationToken);
            throw new TenantAccessDeniedException("Record is not available in the active tenant.");
        }

        if (!artifact.ArtifactType.Equals(ToolDefinitionArtifactTypes.ToolDefinition, StringComparison.OrdinalIgnoreCase))
        {
            throw new RequestValidationException("Artifact is not a ToolDefinitionVersion.");
        }

        var version = await dbContext.ArtifactVersions
            .SingleOrDefaultAsync(item => item.Id == versionId && item.ArtifactId == artifactId, cancellationToken)
            ?? throw new RequestValidationException("Artifact version was not found.");

        if (version.ReadinessState != ArtifactReadinessState.Published)
        {
            throw new RequestValidationException("Tool definition must be published before execution.");
        }

        var document = ToolDefinitionPayloadParser.Deserialize(version.PayloadJson ?? "{}");
        return (artifact, version, document);
    }

    private async Task<ActiveTenantContext> RequireExecutionPermissionAsync(
        string action,
        bool isDryRun,
        CancellationToken cancellationToken)
    {
        var context = await tenantContextResolver.ResolveAsync(action, cancellationToken);
        var permission = isDryRun ? ToolDefinitionPermissions.DryRun : ToolDefinitionPermissions.Execute;
        if (await permissionService.HasPermissionAsync(context.TenantId, context.UserId, permission, cancellationToken)
            || await HasAdminPermissionAsync(context, cancellationToken))
        {
            return context;
        }

        await denialRecorder.RecordAsync(
            context.TenantId,
            context.UserId,
            action,
            "permission_denied",
            $"The user lacks the {permission} permission.",
            cancellationToken);
        throw new TenantAccessDeniedException($"User lacks {permission} permission.");
    }

    private async Task<bool> HasAdminPermissionAsync(ActiveTenantContext context, CancellationToken cancellationToken)
        => await permissionService.HasPermissionAsync(context.TenantId, context.UserId, ToolDefinitionPermissions.Admin, cancellationToken)
            || await permissionService.HasPermissionAsync(context.TenantId, context.UserId, IdentityPermissions.Wildcard, cancellationToken);
}
