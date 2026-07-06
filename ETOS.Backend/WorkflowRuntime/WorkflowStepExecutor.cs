using System.Text.Json;
using System.Text.Json.Nodes;
using ETOS.Backend.AgentRuntime;
using ETOS.Backend.Artifacts;
using ETOS.Backend.Identity;
using ETOS.Backend.Infrastructure.Persistence;
using ETOS.Backend.Recommendations;
using ETOS.Backend.ReviewTasks;
using ETOS.Backend.ToolRegistry;
using ETOS.Backend.Workflows;
using ETOS.Backend.WorkflowRuns;
using Microsoft.EntityFrameworkCore;

namespace ETOS.Backend.WorkflowRuntime;

public sealed class WorkflowStepExecutor(
    EnterpriseThreadDbContext dbContext,
    IAgentExecutionService agentExecutionService,
    IToolGateway toolGateway,
    IBusinessPolicyWorkflowEvaluator businessPolicyWorkflowEvaluator,
    IGovernedOptimizationEvaluationService optimizationEvaluationService,
    IRecommendationFactory recommendationFactory,
    IReviewTaskFactory reviewTaskFactory) : IWorkflowStepExecutor
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<WorkflowStepExecutionResult> ExecuteAsync(
        WorkflowStepExecutionContext context,
        CancellationToken cancellationToken)
    {
        var step = context.Step;
        var stepType = step.StepType?.Trim() ?? string.Empty;

        if (context.StepBlockedBySafeMode
            && (stepType.Equals(WorkflowStepTypes.CreateRecommendation, StringComparison.OrdinalIgnoreCase)
                || stepType.Equals(WorkflowStepTypes.CreateReviewTask, StringComparison.OrdinalIgnoreCase)))
        {
            var blockedEvent = await RecordSafeModeEventAsync(
                context,
                SafeModeEventKinds.Blocked,
                $"Step '{step.StepKey}' blocked because workflow safe mode is active.",
                null,
                stepType,
                cancellationToken);

            return new WorkflowStepExecutionResult(
                step.StepKey!.Trim(),
                WorkflowRunStatuses.Blocked,
                context.AccumulatedContextJson,
                null,
                null,
                null,
                null,
                blockedEvent);
        }

        return stepType switch
        {
            WorkflowStepTypes.AgentExecute => await ExecuteAgentStepAsync(context, cancellationToken),
            WorkflowStepTypes.ToolExecute => await ExecuteToolStepAsync(context, cancellationToken),
            WorkflowStepTypes.BusinessPolicyCheck => await ExecutePolicyStepAsync(context, cancellationToken),
            WorkflowStepTypes.OptimizationEvaluate => await ExecuteOptimizationStepAsync(context, cancellationToken),
            WorkflowStepTypes.CreateRecommendation => await ExecuteRecommendationStepAsync(context, cancellationToken),
            WorkflowStepTypes.CreateReviewTask => await ExecuteReviewTaskStepAsync(context, cancellationToken),
            _ => throw new RequestValidationException($"Unsupported workflow step type '{step.StepType}'.")
        };
    }

    private async Task<WorkflowStepExecutionResult> ExecuteAgentStepAsync(
        WorkflowStepExecutionContext context,
        CancellationToken cancellationToken)
    {
        var agentVersionId = context.Step.AgentVersionId
            ?? throw new RequestValidationException($"Step '{context.Step.StepKey}' requires agentVersionId.");

        var agentVersion = await dbContext.ArtifactVersions
            .AsNoTracking()
            .Include(item => item.Artifact)
            .SingleOrDefaultAsync(item => item.Id == agentVersionId && item.TenantId == context.TenantId, cancellationToken)
            ?? throw new RequestValidationException("Agent version was not found.");

        var startGraphNodeId = TryExtractGuidFromContext(context.AccumulatedContextJson, "startGraphNodeId");
        var queryText = TryExtractStringFromContext(context.AccumulatedContextJson, "queryText");

        var agentRequest = new AgentExecutionRequest(
            context.AccumulatedContextJson,
            queryText,
            startGraphNodeId,
            null,
            context.WorkflowRunId);

        var agentResponse = context.Mode switch
        {
            WorkflowExecutionMode.Preview when agentVersion.ReadinessState != ArtifactReadinessState.Published
                => await agentExecutionService.PreviewAsync(agentVersion.ArtifactId, agentVersion.Id, agentRequest, cancellationToken),
            WorkflowExecutionMode.TestRun when agentVersion.ReadinessState != ArtifactReadinessState.Published
                => await agentExecutionService.TestRunAsync(agentVersion.ArtifactId, agentVersion.Id, agentRequest, cancellationToken),
            _ => await agentExecutionService.ExecuteAsync(agentVersion.ArtifactId, agentVersion.Id, agentRequest, cancellationToken)
        };

        if (!string.IsNullOrWhiteSpace(agentResponse.StructuredOutputJson))
        {
            WorkflowReadOnlyGuards.GuardStructuredOutputAgainstDecisionCreation(agentResponse.StructuredOutputJson);
        }

        var mergedContext = MergeStepOutput(
            context.AccumulatedContextJson,
            context.Step.StepKey!,
            agentResponse.StructuredOutputJson ?? agentResponse.OutputSafeSummaryJson);

        return new WorkflowStepExecutionResult(
            context.Step.StepKey!.Trim(),
            agentResponse.Status,
            mergedContext,
            agentResponse.AgentRunId,
            agentResponse.ToolRunIds.FirstOrDefault(),
            null,
            null,
            null);
    }

    private async Task<WorkflowStepExecutionResult> ExecuteToolStepAsync(
        WorkflowStepExecutionContext context,
        CancellationToken cancellationToken)
    {
        var toolVersionId = context.Step.ToolDefinitionVersionId
            ?? throw new RequestValidationException($"Step '{context.Step.StepKey}' requires toolDefinitionVersionId.");

        var toolVersion = await dbContext.ArtifactVersions
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == toolVersionId && item.TenantId == context.TenantId, cancellationToken)
            ?? throw new RequestValidationException("Tool definition version was not found.");

        var toolRequest = new ToolExecutionRequest(
            context.AccumulatedContextJson,
            null,
            context.WorkflowRunId);

        var toolResponse = context.IsPreview || context.IsDryRun
            ? await toolGateway.DryRunAsync(toolVersion.ArtifactId, toolVersion.Id, toolRequest, cancellationToken)
            : await toolGateway.ExecuteAsync(toolVersion.ArtifactId, toolVersion.Id, toolRequest, cancellationToken);

        var mergedContext = MergeStepOutput(
            context.AccumulatedContextJson,
            context.Step.StepKey!,
            toolResponse.OutputSafeSummaryJson);

        return new WorkflowStepExecutionResult(
            context.Step.StepKey!.Trim(),
            toolResponse.Status,
            mergedContext,
            null,
            toolResponse.ToolRunId,
            null,
            null,
            null);
    }

    private async Task<WorkflowStepExecutionResult> ExecutePolicyStepAsync(
        WorkflowStepExecutionContext context,
        CancellationToken cancellationToken)
    {
        var policyVersionId = context.Step.BusinessPolicyDefinitionVersionId
            ?? throw new RequestValidationException($"Step '{context.Step.StepKey}' requires businessPolicyDefinitionVersionId.");

        var evaluation = await businessPolicyWorkflowEvaluator.EvaluateAsync(
            policyVersionId,
            context.AccumulatedContextJson,
            cancellationToken);

        if (evaluation.Passed)
        {
            var passedContext = MergeStepOutput(
                context.AccumulatedContextJson,
                context.Step.StepKey!,
                JsonSerializer.Serialize(new
                {
                    passed = true,
                    policyRuleKey = evaluation.FailedRuleKey
                }, JsonOptions));

            return new WorkflowStepExecutionResult(
                context.Step.StepKey!.Trim(),
                WorkflowRunStatuses.Succeeded,
                passedContext,
                null,
                null,
                null,
                null,
                null);
        }

        return await HandleStepFailureAsync(
            context,
            evaluation.Reason ?? "Business policy check failed.",
            evaluation.FailedRuleKey,
            cancellationToken);
    }

    private async Task<WorkflowStepExecutionResult> ExecuteOptimizationStepAsync(
        WorkflowStepExecutionContext context,
        CancellationToken cancellationToken)
    {
        var optimizationVersionId = context.Step.OptimizationModelVersionId
            ?? throw new RequestValidationException($"Step '{context.Step.StepKey}' requires optimizationModelVersionId.");

        var evaluation = await optimizationEvaluationService.EvaluateAsync(
            optimizationVersionId,
            context.AccumulatedContextJson,
            cancellationToken);

        if (!evaluation.Succeeded)
        {
            return await HandleStepFailureAsync(
                context,
                evaluation.Reason ?? "Optimization evaluation failed.",
                null,
                cancellationToken);
        }

        return new WorkflowStepExecutionResult(
            context.Step.StepKey!.Trim(),
            WorkflowRunStatuses.Succeeded,
            evaluation.EvaluationResultJson,
            null,
            null,
            null,
            null,
            null);
    }

    private async Task<WorkflowStepExecutionResult> ExecuteRecommendationStepAsync(
        WorkflowStepExecutionContext context,
        CancellationToken cancellationToken)
    {
        if (context.Mode != WorkflowExecutionMode.Execute)
        {
            var skippedContext = MergeStepOutput(
                context.AccumulatedContextJson,
                context.Step.StepKey!,
                JsonSerializer.Serialize(new { skipped = true, reason = "Recommendation creation requires workflow execute mode." }, JsonOptions));

            return new WorkflowStepExecutionResult(
                context.Step.StepKey!.Trim(),
                WorkflowRunStatuses.PreviewSucceeded,
                skippedContext,
                null,
                null,
                null,
                null,
                null);
        }

        var recommendation = await recommendationFactory.FromWorkflowRunAsync(context.WorkflowRunId, cancellationToken);
        var mergedContext = MergeStepOutput(
            context.AccumulatedContextJson,
            context.Step.StepKey!,
            JsonSerializer.Serialize(new
            {
                recommendationArtifactId = recommendation.ArtifactId,
                recommendationVersionId = recommendation.VersionId
            }, JsonOptions));

        return new WorkflowStepExecutionResult(
            context.Step.StepKey!.Trim(),
            WorkflowRunStatuses.Succeeded,
            mergedContext,
            null,
            null,
            recommendation.ArtifactId,
            null,
            null);
    }

    private async Task<WorkflowStepExecutionResult> ExecuteReviewTaskStepAsync(
        WorkflowStepExecutionContext context,
        CancellationToken cancellationToken)
    {
        if (context.Mode != WorkflowExecutionMode.Execute)
        {
            var skippedContext = MergeStepOutput(
                context.AccumulatedContextJson,
                context.Step.StepKey!,
                JsonSerializer.Serialize(new { skipped = true, reason = "Review task creation requires workflow execute mode." }, JsonOptions));

            return new WorkflowStepExecutionResult(
                context.Step.StepKey!.Trim(),
                WorkflowRunStatuses.PreviewSucceeded,
                skippedContext,
                null,
                null,
                null,
                null,
                null);
        }

        var reviewTask = await reviewTaskFactory.FromWorkflowOutputAsync(
            context.WorkflowRunId,
            context.Step.StepKey!.Trim(),
            null,
            cancellationToken);

        var mergedContext = MergeStepOutput(
            context.AccumulatedContextJson,
            context.Step.StepKey!,
            JsonSerializer.Serialize(new
            {
                reviewTaskArtifactId = reviewTask.ArtifactId,
                reviewTaskVersionId = reviewTask.VersionId
            }, JsonOptions));

        return new WorkflowStepExecutionResult(
            context.Step.StepKey!.Trim(),
            WorkflowRunStatuses.Succeeded,
            mergedContext,
            null,
            null,
            null,
            reviewTask.ArtifactId,
            null);
    }

    private async Task<WorkflowStepExecutionResult> HandleStepFailureAsync(
        WorkflowStepExecutionContext context,
        string reason,
        string? policyRuleKey,
        CancellationToken cancellationToken)
    {
        var safeModeBehavior = string.IsNullOrWhiteSpace(context.Step.SafeModeOnBlock)
            ? context.Payload.DefaultStepSafeModeBehavior ?? WorkflowStepSafeModeBehaviors.Skip
            : context.Step.SafeModeOnBlock.Trim();

        if (safeModeBehavior.Equals(WorkflowStepSafeModeBehaviors.StopWorkflow, StringComparison.OrdinalIgnoreCase))
        {
            var blockedEvent = await RecordSafeModeEventAsync(
                context,
                SafeModeEventKinds.Blocked,
                reason,
                policyRuleKey,
                context.Step.StepType,
                cancellationToken);

            return new WorkflowStepExecutionResult(
                context.Step.StepKey!.Trim(),
                WorkflowRunStatuses.Blocked,
                context.AccumulatedContextJson,
                null,
                null,
                null,
                null,
                blockedEvent);
        }

        var skippedEvent = await RecordSafeModeEventAsync(
            context,
            SafeModeEventKinds.Skipped,
            reason,
            policyRuleKey,
            context.Step.StepType,
            cancellationToken);

        if (IsHighImpactStep(context.Step.StepType))
        {
            var warningTask = await reviewTaskFactory.FromSafeModeEventAsync(skippedEvent.Id, cancellationToken);
            return new WorkflowStepExecutionResult(
                context.Step.StepKey!.Trim(),
                WorkflowRunStatuses.SafeModeCompleted,
                context.AccumulatedContextJson,
                null,
                null,
                null,
                warningTask.ArtifactId,
                skippedEvent);
        }

        return new WorkflowStepExecutionResult(
            context.Step.StepKey!.Trim(),
            WorkflowRunStatuses.SafeModeCompleted,
            context.AccumulatedContextJson,
            null,
            null,
            null,
            null,
            skippedEvent);
    }

    private static bool IsHighImpactStep(string? stepType)
        => stepType is not null
            && (stepType.Equals(WorkflowStepTypes.AgentExecute, StringComparison.OrdinalIgnoreCase)
                || stepType.Equals(WorkflowStepTypes.ToolExecute, StringComparison.OrdinalIgnoreCase)
                || stepType.Equals(WorkflowStepTypes.OptimizationEvaluate, StringComparison.OrdinalIgnoreCase));

    private async Task<SafeModeEvent> RecordSafeModeEventAsync(
        WorkflowStepExecutionContext context,
        string eventKind,
        string reason,
        string? policyRuleKey,
        string? blockedAction,
        CancellationToken cancellationToken)
    {
        var safeModeEvent = new SafeModeEvent
        {
            Id = Guid.NewGuid(),
            TenantId = context.TenantId,
            WorkflowRunId = context.WorkflowRunId,
            StepKey = context.Step.StepKey!.Trim(),
            EventKind = eventKind,
            Reason = reason,
            PolicyRuleKey = policyRuleKey,
            BlockedAction = blockedAction,
            CreatedAt = DateTimeOffset.UtcNow
        };

        dbContext.SafeModeEvents.Add(safeModeEvent);
        await dbContext.SaveChangesAsync(cancellationToken);
        return safeModeEvent;
    }

    private static string MergeStepOutput(string accumulatedContextJson, string stepKey, string? stepOutputJson)
    {
        var root = JsonNode.Parse(string.IsNullOrWhiteSpace(accumulatedContextJson) ? "{}" : accumulatedContextJson) as JsonObject
            ?? new JsonObject();
        var steps = root["steps"] as JsonObject ?? new JsonObject();

        JsonNode? outputNode = null;
        if (!string.IsNullOrWhiteSpace(stepOutputJson))
        {
            try
            {
                outputNode = JsonNode.Parse(stepOutputJson);
            }
            catch (JsonException)
            {
                outputNode = JsonValue.Create(stepOutputJson);
            }
        }

        steps[stepKey] = outputNode ?? JsonValue.Create((string?)null);
        root["steps"] = steps;
        return root.ToJsonString(JsonOptions);
    }

    private static Guid? TryExtractGuidFromContext(string? contextJson, string propertyName)
    {
        if (string.IsNullOrWhiteSpace(contextJson))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(contextJson);
            if (document.RootElement.TryGetProperty(propertyName, out var property)
                && Guid.TryParse(property.GetString(), out var value))
            {
                return value;
            }
        }
        catch (JsonException)
        {
            return null;
        }

        return null;
    }

    private static string? TryExtractStringFromContext(string? contextJson, string propertyName)
    {
        if (string.IsNullOrWhiteSpace(contextJson))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(contextJson);
            if (document.RootElement.TryGetProperty(propertyName, out var property))
            {
                return property.GetString();
            }
        }
        catch (JsonException)
        {
            return null;
        }

        return null;
    }
}
