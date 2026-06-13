using System.Text.Json;
using ETOS.Backend.Artifacts;
using ETOS.Backend.AiTrace;
using ETOS.Backend.Governance;
using ETOS.Backend.GovernedQuery;
using ETOS.Backend.Identity;
using ETOS.Backend.GovernedChat.Llm;
using ETOS.Backend.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ETOS.Backend.GovernedChat;

public interface IGovernedChatService
{
    Task<GovernedChatSessionSummaryResponse> CreateSessionAsync(CreateGovernedChatSessionRequest request, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<GovernedChatSessionSummaryResponse>> ListSessionsAsync(CancellationToken cancellationToken);
    Task<GovernedChatSessionDetailResponse> GetSessionAsync(Guid sessionId, CancellationToken cancellationToken);
    Task<GovernedChatTurnResponse> AskAsync(Guid sessionId, CreateGovernedChatTurnRequest request, CancellationToken cancellationToken);
    Task<GovernedChatTurnResponse> GetTurnAsync(Guid turnId, CancellationToken cancellationToken);
}

public sealed class GovernedChatService(
    EnterpriseThreadDbContext dbContext,
    ITenantContextResolver tenantContextResolver,
    IAccessPermissionService permissionService,
    IAccessDenialRecorder denialRecorder,
    IAuditRecorder auditRecorder,
    IGovernedQueryService governedQueryService,
    IGovernedChatArtifactSeeder artifactSeeder,
    ILlmCompletionService llmCompletionService,
    IOutputSchemaValidator outputSchemaValidator,
    IChatArtifactDraftBuilder draftBuilder,
    IAiTraceRecorder aiTraceRecorder) : IGovernedChatService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<GovernedChatSessionSummaryResponse> CreateSessionAsync(CreateGovernedChatSessionRequest request, CancellationToken cancellationToken)
    {
        var context = await RequirePermissionAsync("governed_chat.sessions.create", GovernedChatPermissions.Run, cancellationToken);
        var title = string.IsNullOrWhiteSpace(request.Title) ? "Governed chat session" : request.Title.Trim();
        var session = new GovernedChatSession
        {
            Id = Guid.NewGuid(),
            TenantId = context.TenantId,
            Title = title,
            StartedByUserId = context.UserId,
            StartGraphNodeId = request.StartGraphNodeId,
            DocumentArtifactId = request.DocumentArtifactId,
            CreatedAt = DateTimeOffset.UtcNow
        };

        dbContext.GovernedChatSessions.Add(session);
        await dbContext.SaveChangesAsync(cancellationToken);

        await auditRecorder.RecordAsync(
            new AuditRecordWriteRequest(
                context.TenantId,
                context.UserId,
                "governed_chat.session.create",
                AuditResult.Success,
                null,
                $"Created governed chat session '{title}'.",
                nameof(GovernedChatSession),
                session.Id.ToString(),
                RetentionCategory: AuditRetentionCategory.Operational,
                IsArchiveEligible: true),
            cancellationToken);

        return ToSessionSummary(session, 0);
    }

    public async Task<IReadOnlyCollection<GovernedChatSessionSummaryResponse>> ListSessionsAsync(CancellationToken cancellationToken)
    {
        var context = await RequirePermissionAsync("governed_chat.sessions.list", GovernedChatPermissions.Run, cancellationToken);
        var sessions = await dbContext.GovernedChatSessions
            .AsNoTracking()
            .Where(session => session.TenantId == context.TenantId)
            .OrderByDescending(session => session.LastTurnAt ?? session.CreatedAt)
            .Select(session => new
            {
                session,
                turnCount = dbContext.GovernedChatTurns.Count(turn => turn.TenantId == context.TenantId && turn.SessionId == session.Id)
            })
            .ToListAsync(cancellationToken);

        return sessions
            .Select(item => ToSessionSummary(item.session, item.turnCount))
            .ToList();
    }

    public async Task<GovernedChatSessionDetailResponse> GetSessionAsync(Guid sessionId, CancellationToken cancellationToken)
    {
        var context = await RequirePermissionAsync("governed_chat.sessions.get", GovernedChatPermissions.Run, cancellationToken);
        var session = await dbContext.GovernedChatSessions
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == sessionId, cancellationToken)
            ?? throw new RequestValidationException("Governed chat session was not found.");
        if (session.TenantId != context.TenantId)
        {
            await RecordTenantMismatchAsync(context, "governed_chat.sessions.get", cancellationToken);
        }

        var turns = await dbContext.GovernedChatTurns
            .AsNoTracking()
            .Where(turn => turn.TenantId == context.TenantId && turn.SessionId == sessionId)
            .OrderByDescending(turn => turn.CreatedAt)
            .Select(turn => new GovernedChatTurnSummaryResponse(
                turn.Id,
                turn.SessionId,
                turn.UserMessage,
                turn.AssistantSafeSummary,
                turn.AiTraceRecordId,
                turn.DraftArtifactKind,
                turn.CreatedAt))
            .ToListAsync(cancellationToken);

        return new GovernedChatSessionDetailResponse(
            session.Id,
            session.TenantId,
            session.Title,
            session.StartedByUserId,
            session.StartGraphNodeId,
            session.DocumentArtifactId,
            session.CreatedAt,
            session.LastTurnAt,
            turns);
    }

    public async Task<GovernedChatTurnResponse> AskAsync(Guid sessionId, CreateGovernedChatTurnRequest request, CancellationToken cancellationToken)
    {
        var context = await RequirePermissionAsync("governed_chat.ask", GovernedChatPermissions.Run, cancellationToken);
        if (request.DraftArtifactKind is not null)
        {
            await RequirePermissionAsync("governed_chat.draft", GovernedChatPermissions.Draft, cancellationToken, context);
        }

        var session = await dbContext.GovernedChatSessions.SingleOrDefaultAsync(item => item.Id == sessionId, cancellationToken)
            ?? throw new RequestValidationException("Governed chat session was not found.");
        if (session.TenantId != context.TenantId)
        {
            await RecordTenantMismatchAsync(context, "governed_chat.ask", cancellationToken);
        }

        var message = request.Message?.Trim();
        if (string.IsNullOrWhiteSpace(message))
        {
            throw new RequestValidationException("Chat message is required.");
        }

        var intentKey = string.IsNullOrWhiteSpace(request.IntentKey) ? "object-360-context" : request.IntentKey.Trim();
        var startGraphNodeId = request.StartGraphNodeId ?? session.StartGraphNodeId;
        var documentArtifactId = request.DocumentArtifactId ?? session.DocumentArtifactId;
        var platformArtifacts = await artifactSeeder.EnsurePlatformArtifactsAsync(context, cancellationToken);

        var run = await governedQueryService.RunAsync(
            new RunGovernedQueryRequest(
                intentKey,
                startGraphNodeId,
                documentArtifactId,
                request.PolicyKey,
                message,
                2,
                CreateAiTrace: false),
            cancellationToken);
        var package = run.ContextPackage
            ?? throw new RequestValidationException("Governed retrieval did not produce a context package.");

        var prompt = BuildPrompt(platformArtifacts.PromptTemplate.PayloadJson, intentKey, message, package.LlmVisibleContext);
        var generatedOutputJson = await llmCompletionService.CompleteStructuredAsync(
            prompt,
            platformArtifacts.ChatAnswerSchema.PayloadJson,
            cancellationToken);
        outputSchemaValidator.Validate(generatedOutputJson, platformArtifacts.ChatAnswerSchema.PayloadJson);

        var parsedOutput = JsonSerializer.Deserialize<ChatAnswerOutput>(generatedOutputJson, JsonOptions)
            ?? throw new RequestValidationException("Validated chat output could not be deserialized.");
        var evidence = (parsedOutput.Evidence ?? [])
            .Select(item => new GovernedChatEvidenceResponse(item.ContextId, item.ContextType, item.SafeSummary))
            .ToList();
        var trustFilteredCount = Math.Max(0, run.RetrievedCount - run.FilteredCount);
        var confidence = new GovernedChatConfidenceResponse(
            parsedOutput.Confidence?.Overall ?? 0,
            run.RetrievedCount,
            package.AllowedCount,
            package.DeniedCount,
            trustFilteredCount,
            parsedOutput.Confidence?.Notes ?? "Governed chat confidence derived from retrieval and policy filtering.");

        var turn = new GovernedChatTurn
        {
            Id = Guid.NewGuid(),
            TenantId = context.TenantId,
            SessionId = session.Id,
            UserMessage = message,
            AssistantSafeSummary = Trim(parsedOutput.Answer ?? "No answer generated.", 1000),
            RetrievalRunId = run.Id,
            ContextPackageId = package.Id,
            PromptTemplateArtifactId = platformArtifacts.PromptTemplate.ArtifactId,
            PromptTemplateVersionId = platformArtifacts.PromptTemplate.VersionId,
            OutputSchemaArtifactId = platformArtifacts.ChatAnswerSchema.ArtifactId,
            OutputSchemaVersionId = platformArtifacts.ChatAnswerSchema.VersionId,
            GeneratedOutputJson = generatedOutputJson,
            EvidenceJson = Serialize(evidence),
            ConfidenceJson = Serialize(confidence),
            CreatedAt = DateTimeOffset.UtcNow
        };

        dbContext.GovernedChatTurns.Add(turn);
        session.LastTurnAt = turn.CreatedAt;
        await dbContext.SaveChangesAsync(cancellationToken);

        GovernedChatDraftArtifactResponse? draftArtifact = null;
        if (request.DraftArtifactKind is not null)
        {
            var draftSchema = ResolveDraftSchema(platformArtifacts, request.DraftArtifactKind.Value);
            var draftPrompt = BuildPrompt(platformArtifacts.PromptTemplate.PayloadJson, intentKey, message, package.LlmVisibleContext);
            var draftOutputJson = await llmCompletionService.CompleteStructuredAsync(
                draftPrompt,
                draftSchema.PayloadJson,
                cancellationToken);
            outputSchemaValidator.Validate(draftOutputJson, draftSchema.PayloadJson);
            draftArtifact = await draftBuilder.CreateDraftAsync(
                context,
                request.DraftArtifactKind.Value,
                turn,
                draftSchema,
                platformArtifacts.PromptTemplate,
                draftOutputJson,
                cancellationToken);

            turn.DraftArtifactId = draftArtifact.ArtifactId;
            turn.DraftArtifactVersionId = draftArtifact.VersionId;
            turn.DraftArtifactKind = request.DraftArtifactKind;
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        var audit = await auditRecorder.RecordAsync(
            new AuditRecordWriteRequest(
                context.TenantId,
                context.UserId,
                "governed_chat.ask",
                AuditResult.Success,
                null,
                $"Governed chat turn completed with provider '{llmCompletionService.ProviderName}'.",
                nameof(GovernedChatTurn),
                turn.Id.ToString(),
                RetentionCategory: AuditRetentionCategory.Operational,
                IsArchiveEligible: true),
            cancellationToken);

        var traceId = await aiTraceRecorder.CreateFromChatTurnAsync(turn.Id, audit.Id, cancellationToken);
        turn.AiTraceRecordId = traceId;
        await dbContext.SaveChangesAsync(cancellationToken);

        if (request.DraftArtifactKind is not null)
        {
            await auditRecorder.RecordAsync(
                new AuditRecordWriteRequest(
                    context.TenantId,
                    context.UserId,
                    "governed_chat.draft",
                    AuditResult.Success,
                    null,
                    $"Created draft {draftArtifact!.ArtifactType} version '{draftArtifact.VersionLabel}'.",
                    nameof(ArtifactVersion),
                    draftArtifact.VersionId.ToString(),
                    RetentionCategory: AuditRetentionCategory.Operational,
                    IsArchiveEligible: true),
                cancellationToken);
        }

        return BuildTurnResponse(turn, evidence, confidence, package.DeniedCount, draftArtifact);
    }

    public async Task<GovernedChatTurnResponse> GetTurnAsync(Guid turnId, CancellationToken cancellationToken)
    {
        var context = await RequirePermissionAsync("governed_chat.turns.get", GovernedChatPermissions.Run, cancellationToken);
        var turn = await dbContext.GovernedChatTurns
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == turnId, cancellationToken)
            ?? throw new RequestValidationException("Governed chat turn was not found.");
        if (turn.TenantId != context.TenantId)
        {
            await RecordTenantMismatchAsync(context, "governed_chat.turns.get", cancellationToken);
        }

        var evidence = Deserialize<IReadOnlyCollection<GovernedChatEvidenceResponse>>(turn.EvidenceJson);
        var confidence = Deserialize<GovernedChatConfidenceResponse>(turn.ConfidenceJson);
        var deniedCount = await dbContext.ContextPackages
            .AsNoTracking()
            .Where(package => package.Id == turn.ContextPackageId)
            .Select(package => package.DeniedCount)
            .SingleAsync(cancellationToken);

        GovernedChatDraftArtifactResponse? draft = null;
        if (turn.DraftArtifactId is not null && turn.DraftArtifactVersionId is not null)
        {
            var draftVersion = await dbContext.ArtifactVersions
                .AsNoTracking()
                .Include(version => version.Artifact)
                .SingleAsync(version => version.Id == turn.DraftArtifactVersionId.Value, cancellationToken);
            draft = new GovernedChatDraftArtifactResponse(
                turn.DraftArtifactId.Value,
                turn.DraftArtifactVersionId.Value,
                draftVersion.Artifact!.ArtifactType,
                draftVersion.VersionLabel,
                draftVersion.ReadinessState);
        }

        return BuildTurnResponse(turn, evidence, confidence, deniedCount, draft);
    }

    private static PlatformArtifactVersion ResolveDraftSchema(GovernedChatPlatformArtifacts artifacts, ChatDraftArtifactKind draftKind)
    {
        return draftKind switch
        {
            ChatDraftArtifactKind.QueryIntent => artifacts.DraftQueryIntentSchema,
            ChatDraftArtifactKind.Dashboard => artifacts.DraftDashboardSchema,
            ChatDraftArtifactKind.Report => artifacts.DraftReportSchema,
            _ => throw new RequestValidationException("Unsupported draft artifact kind.")
        };
    }

    private static string BuildPrompt(
        string templatePayloadJson,
        string intentKey,
        string question,
        IReadOnlyCollection<ContextItemResponse> visibleContext)
    {
        var templateRoot = JsonSerializer.Deserialize<Dictionary<string, string>>(templatePayloadJson, JsonOptions)
            ?? throw new RequestValidationException("Prompt template payload is invalid.");
        if (!templateRoot.TryGetValue("template", out var template) || string.IsNullOrWhiteSpace(template))
        {
            throw new RequestValidationException("Prompt template payload is missing template text.");
        }

        return template
            .Replace("{{intentKey}}", intentKey, StringComparison.Ordinal)
            .Replace("{{question}}", question, StringComparison.Ordinal)
            .Replace("{{contextJson}}", Serialize(visibleContext), StringComparison.Ordinal);
    }

    private static GovernedChatTurnResponse BuildTurnResponse(
        GovernedChatTurn turn,
        IReadOnlyCollection<GovernedChatEvidenceResponse> evidence,
        GovernedChatConfidenceResponse confidence,
        int deniedSummaryCount,
        GovernedChatDraftArtifactResponse? draftArtifact)
    {
        return new GovernedChatTurnResponse(
            turn.Id,
            turn.SessionId,
            turn.AssistantSafeSummary,
            evidence,
            confidence,
            deniedSummaryCount,
            turn.AiTraceRecordId ?? Guid.Empty,
            turn.RetrievalRunId,
            turn.ContextPackageId,
            draftArtifact);
    }

    private static GovernedChatSessionSummaryResponse ToSessionSummary(GovernedChatSession session, int turnCount)
    {
        return new GovernedChatSessionSummaryResponse(
            session.Id,
            session.TenantId,
            session.Title,
            session.StartedByUserId,
            session.StartGraphNodeId,
            session.DocumentArtifactId,
            session.CreatedAt,
            session.LastTurnAt,
            turnCount);
    }

    private async Task<ActiveTenantContext> RequirePermissionAsync(
        string action,
        string permissionKey,
        CancellationToken cancellationToken,
        ActiveTenantContext? existingContext = null)
    {
        var context = existingContext ?? await tenantContextResolver.ResolveAsync(action, cancellationToken);
        var hasPermission = await permissionService.HasPermissionAsync(context.TenantId, context.UserId, permissionKey, cancellationToken)
            || await permissionService.HasPermissionAsync(context.TenantId, context.UserId, GovernedChatPermissions.Admin, cancellationToken)
            || await permissionService.HasPermissionAsync(context.TenantId, context.UserId, IdentityPermissions.Wildcard, cancellationToken);
        if (!hasPermission)
        {
            await denialRecorder.RecordAsync(context.TenantId, context.UserId, action, "permission_denied", $"The user lacks the {permissionKey} permission.", cancellationToken);
            throw new TenantAccessDeniedException("User lacks governed chat permission.");
        }

        return context;
    }

    private async Task RecordTenantMismatchAsync(ActiveTenantContext context, string action, CancellationToken cancellationToken)
    {
        await denialRecorder.RecordAsync(context.TenantId, context.UserId, action, "tenant_access_denied", "Governed chat record belongs to a different tenant.", cancellationToken);
        throw new TenantAccessDeniedException("Governed chat record is not available in the active tenant.");
    }

    private static string Serialize<T>(T value) => JsonSerializer.Serialize(value, JsonOptions);

    private static T Deserialize<T>(string value) => JsonSerializer.Deserialize<T>(value, JsonOptions) ?? throw new InvalidOperationException("Stored governed chat JSON could not be deserialized.");

    private static string Trim(string value, int maxLength) => value.Length <= maxLength ? value : value[..maxLength];

    private sealed record ChatAnswerOutput(string? Answer, IReadOnlyCollection<ChatEvidenceOutput>? Evidence, ChatConfidenceOutput? Confidence);

    private sealed record ChatEvidenceOutput(string ContextId, string ContextType, string SafeSummary);

    private sealed record ChatConfidenceOutput(double Overall, string? Notes);
}
