using System.Text.Json;
using ETOS.Backend.Identity;

namespace ETOS.Backend.Workflows;

public static class WorkflowDefinitionPayloadParser
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    public static WorkflowDefinitionDetailResponse Parse(
        Guid artifactId,
        Guid versionId,
        string versionLabel,
        string artifactName,
        string? artifactDescription,
        string artifactReadinessState,
        string payloadJson,
        IReadOnlyCollection<WorkflowAgentReferenceResponse> referencedAgents,
        IReadOnlyCollection<WorkflowToolReferenceResponse> referencedTools,
        IReadOnlyCollection<WorkflowBusinessPolicyReferenceResponse> referencedBusinessPolicies,
        IReadOnlyCollection<WorkflowOptimizationModelReferenceResponse> referencedOptimizationModels,
        IReadOnlyCollection<WorkflowModelPackageReferenceResponse> modelPackages,
        IReadOnlyCollection<WorkflowOntologyReferenceResponse> ontologies,
        WorkflowArtifactVersionReferenceResponse? inputSchema,
        WorkflowArtifactVersionReferenceResponse? outputSchema)
    {
        var document = Deserialize(payloadJson);
        ValidateCore(document);
        var steps = ParseWorkflowDefinitionJson(document.WorkflowDefinitionJson);

        return new WorkflowDefinitionDetailResponse(
            artifactId,
            versionId,
            versionLabel,
            artifactName,
            artifactDescription,
            artifactReadinessState,
            document.WorkflowKey!.Trim(),
            document.DisplayName!.Trim(),
            TrimOptional(document.Description),
            document.WorkflowScope!.Trim(),
            steps,
            inputSchema,
            outputSchema,
            referencedAgents,
            referencedTools,
            referencedBusinessPolicies,
            referencedOptimizationModels,
            modelPackages,
            ontologies,
            document.SafeModeEnabled,
            document.PreviewModeDefault,
            TrimOptional(document.BlockedModeMessage),
            document.AllowPartialCompletion,
            document.DefaultStepSafeModeBehavior!.Trim(),
            MapTriggerConfig(document.TriggerConfig),
            document.ApprovalRequirements ?? [],
            document.CompatibilityTestNotes ?? [],
            document.CompatibilityFixtureKeys ?? [],
            MapDerivedCapabilityRisk(document.DerivedCapabilityRiskJson),
            document.CreatedByUserId);
    }

    public static string Serialize(WorkflowDefinitionPayloadDocument document)
        => JsonSerializer.Serialize(Normalize(document), JsonOptions);

    public static WorkflowDefinitionPayloadDocument Deserialize(string payloadJson)
    {
        var document = JsonSerializer.Deserialize<WorkflowDefinitionPayloadDocument>(payloadJson, JsonOptions)
            ?? throw new RequestValidationException("Workflow definition payload is invalid.");
        return document;
    }

    public static WorkflowDefinitionPayloadDocument Create(
        string workflowKey,
        string displayName,
        string? description,
        string workflowScope,
        IReadOnlyCollection<WorkflowStepDefinitionRequest>? steps,
        Guid? inputSchemaVersionId,
        Guid? outputSchemaVersionId,
        IReadOnlyCollection<Guid>? referencedAgentVersionIds,
        IReadOnlyCollection<Guid>? referencedToolDefinitionVersionIds,
        IReadOnlyCollection<Guid>? referencedBusinessPolicyDefinitionVersionIds,
        IReadOnlyCollection<Guid>? referencedOptimizationModelVersionIds,
        IReadOnlyCollection<Guid>? compatibleModelPackageVersionIds,
        IReadOnlyCollection<Guid>? compatibleOntologyVersionIds,
        bool safeModeEnabled,
        bool previewModeDefault,
        string? blockedModeMessage,
        bool allowPartialCompletion,
        string defaultStepSafeModeBehavior,
        WorkflowTriggerConfigRequest? triggerConfig,
        IReadOnlyCollection<string>? approvalRequirements,
        IReadOnlyCollection<string>? compatibilityTestNotes,
        IReadOnlyCollection<string>? compatibilityFixtureKeys,
        Guid createdByUserId,
        DerivedCapabilityRiskDocument? derivedCapabilityRiskJson = null)
    {
        var stepDocuments = MapStepRequests(steps);
        return Normalize(new WorkflowDefinitionPayloadDocument
        {
            WorkflowKey = workflowKey.Trim(),
            DisplayName = displayName.Trim(),
            Description = TrimOptional(description),
            WorkflowScope = workflowScope.Trim(),
            WorkflowDefinitionJson = SerializeWorkflowDefinitionJson(stepDocuments),
            InputSchemaVersionId = inputSchemaVersionId,
            OutputSchemaVersionId = outputSchemaVersionId,
            ReferencedAgentVersionIds = referencedAgentVersionIds?.Distinct().ToList() ?? [],
            ReferencedToolDefinitionVersionIds = referencedToolDefinitionVersionIds?.Distinct().ToList() ?? [],
            ReferencedBusinessPolicyDefinitionVersionIds = referencedBusinessPolicyDefinitionVersionIds?.Distinct().ToList() ?? [],
            ReferencedOptimizationModelVersionIds = referencedOptimizationModelVersionIds?.Distinct().ToList() ?? [],
            CompatibleModelPackageVersionIds = compatibleModelPackageVersionIds?.Distinct().ToList() ?? [],
            CompatibleOntologyVersionIds = compatibleOntologyVersionIds?.Distinct().ToList() ?? [],
            SafeModeEnabled = safeModeEnabled,
            PreviewModeDefault = previewModeDefault,
            BlockedModeMessage = TrimOptional(blockedModeMessage),
            AllowPartialCompletion = allowPartialCompletion,
            DefaultStepSafeModeBehavior = defaultStepSafeModeBehavior.Trim(),
            TriggerConfig = MapTriggerConfigRequest(triggerConfig),
            ApprovalRequirements = approvalRequirements?.Select(item => item.Trim()).Where(item => item.Length > 0).ToList() ?? [],
            CompatibilityTestNotes = compatibilityTestNotes?.Select(item => item.Trim()).Where(item => item.Length > 0).ToList() ?? [],
            CompatibilityFixtureKeys = compatibilityFixtureKeys?.Select(item => item.Trim()).Where(item => item.Length > 0).ToList() ?? [],
            DerivedCapabilityRiskJson = derivedCapabilityRiskJson,
            CreatedByUserId = createdByUserId
        });
    }

    public static void ValidateCore(WorkflowDefinitionPayloadDocument document)
    {
        if (string.IsNullOrWhiteSpace(document.WorkflowKey))
        {
            throw new RequestValidationException("workflowKey is required.");
        }

        if (string.IsNullOrWhiteSpace(document.DisplayName))
        {
            throw new RequestValidationException("displayName is required.");
        }

        if (string.IsNullOrWhiteSpace(document.WorkflowScope))
        {
            throw new RequestValidationException("workflowScope is required.");
        }

        if (!WorkflowScopes.All.Contains(document.WorkflowScope, StringComparer.OrdinalIgnoreCase))
        {
            throw new RequestValidationException(
                $"workflowScope '{document.WorkflowScope}' must be one of: {string.Join(", ", WorkflowScopes.All)}.");
        }

        if (string.IsNullOrWhiteSpace(document.DefaultStepSafeModeBehavior))
        {
            throw new RequestValidationException("defaultStepSafeModeBehavior is required.");
        }

        if (!WorkflowStepSafeModeBehaviors.All.Contains(document.DefaultStepSafeModeBehavior, StringComparer.OrdinalIgnoreCase))
        {
            throw new RequestValidationException(
                $"defaultStepSafeModeBehavior '{document.DefaultStepSafeModeBehavior}' must be one of: {string.Join(", ", WorkflowStepSafeModeBehaviors.All)}.");
        }

        if (document.CreatedByUserId == Guid.Empty)
        {
            throw new RequestValidationException("createdByUserId is required.");
        }

        var packageCount = document.CompatibleModelPackageVersionIds?.Count ?? 0;
        var ontologyCount = document.CompatibleOntologyVersionIds?.Count ?? 0;
        if (packageCount + ontologyCount == 0)
        {
            throw new RequestValidationException(
                "At least one compatibleModelPackageVersionId or compatibleOntologyVersionId is required.");
        }

        ValidateWorkflowDefinitionJson(document.WorkflowDefinitionJson);
    }

    public static IReadOnlyCollection<string> ValidateWorkflowDefinitionJson(string? workflowDefinitionJson)
    {
        var notes = new List<string>();

        if (string.IsNullOrWhiteSpace(workflowDefinitionJson))
        {
            notes.Add("workflowDefinitionJson is required.");
            return notes;
        }

        List<WorkflowStepDocument> steps;
        try
        {
            steps = DeserializeWorkflowDefinitionJson(workflowDefinitionJson);
        }
        catch (RequestValidationException exception)
        {
            notes.Add(exception.Message);
            return notes;
        }

        if (steps.Count == 0)
        {
            notes.Add("workflowDefinitionJson must contain at least one step.");
            return notes;
        }

        var stepKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var step in steps)
        {
            if (string.IsNullOrWhiteSpace(step.StepKey))
            {
                notes.Add("Each workflow step requires stepKey.");
                continue;
            }

            if (!stepKeys.Add(step.StepKey.Trim()))
            {
                notes.Add($"Duplicate workflow stepKey '{step.StepKey}'.");
            }

            if (string.IsNullOrWhiteSpace(step.StepType)
                || !WorkflowStepTypes.All.Contains(step.StepType, StringComparer.OrdinalIgnoreCase))
            {
                notes.Add($"Step '{step.StepKey}' has unsupported stepType '{step.StepType}'.");
                continue;
            }

            var safeMode = string.IsNullOrWhiteSpace(step.SafeModeOnBlock)
                ? WorkflowStepSafeModeBehaviors.Skip
                : step.SafeModeOnBlock.Trim();
            if (!WorkflowStepSafeModeBehaviors.All.Contains(safeMode, StringComparer.OrdinalIgnoreCase))
            {
                notes.Add($"Step '{step.StepKey}' has invalid safeModeOnBlock '{step.SafeModeOnBlock}'.");
            }

            notes.AddRange(ValidateStepConfig(step));
        }

        foreach (var step in steps)
        {
            foreach (var dependency in step.DependsOnStepKeys ?? [])
            {
                if (!stepKeys.Contains(dependency))
                {
                    notes.Add($"Step '{step.StepKey}' depends on unknown stepKey '{dependency}'.");
                }
            }
        }

        return notes;
    }

    public static IReadOnlyCollection<WorkflowStepDefinitionResponse> ParseWorkflowDefinitionJson(string? workflowDefinitionJson)
    {
        if (string.IsNullOrWhiteSpace(workflowDefinitionJson))
        {
            return [];
        }

        return DeserializeWorkflowDefinitionJson(workflowDefinitionJson)
            .Select(step => new WorkflowStepDefinitionResponse(
                step.StepKey!.Trim(),
                step.StepType!.Trim(),
                string.IsNullOrWhiteSpace(step.SafeModeOnBlock)
                    ? WorkflowStepSafeModeBehaviors.Skip
                    : step.SafeModeOnBlock.Trim(),
                step.DependsOnStepKeys ?? [],
                step.AgentVersionId,
                step.ToolDefinitionVersionId,
                step.BusinessPolicyDefinitionVersionId,
                step.OptimizationModelVersionId,
                TrimOptional(step.SourceStepKey),
                step.ReviewTaskTemplateVersionId))
            .ToList();
    }

    public static WorkflowDerivedCapabilityRiskResponse? MapDerivedCapabilityRisk(DerivedCapabilityRiskDocument? document)
    {
        if (document is null)
        {
            return null;
        }

        return new WorkflowDerivedCapabilityRiskResponse(
            document.EffectiveRiskLevel?.Trim() ?? string.Empty,
            document.ToolRiskContributions?.Select(item => new WorkflowToolRiskContributionResponse(
                item.ToolDefinitionVersionId,
                item.RiskLevel?.Trim() ?? string.Empty)).ToList() ?? [],
            document.PermissionCeiling?.Trim() ?? string.Empty);
    }

    public static List<WorkflowStepDocument> DeserializeWorkflowDefinitionJson(string workflowDefinitionJson)
    {
        using var document = JsonDocument.Parse(workflowDefinitionJson);
        if (document.RootElement.ValueKind != JsonValueKind.Array)
        {
            throw new RequestValidationException("workflowDefinitionJson must be a JSON array of steps.");
        }

        var steps = new List<WorkflowStepDocument>();
        foreach (var element in document.RootElement.EnumerateArray())
        {
            var step = element.Deserialize<WorkflowStepDocument>(JsonOptions)
                ?? throw new RequestValidationException("workflowDefinitionJson contains an invalid step.");
            steps.Add(step);
        }

        return steps;
    }

    public static string SerializeWorkflowDefinitionJson(IReadOnlyCollection<WorkflowStepDocument> steps)
        => JsonSerializer.Serialize(steps, JsonOptions);

    private static IReadOnlyCollection<string> ValidateStepConfig(WorkflowStepDocument step)
    {
        var notes = new List<string>();
        var stepType = step.StepType?.Trim() ?? string.Empty;

        switch (stepType)
        {
            case WorkflowStepTypes.AgentExecute:
                if (step.AgentVersionId is null || step.AgentVersionId == Guid.Empty)
                {
                    notes.Add($"Step '{step.StepKey}' requires agentVersionId.");
                }

                break;
            case WorkflowStepTypes.ToolExecute:
                if (step.ToolDefinitionVersionId is null || step.ToolDefinitionVersionId == Guid.Empty)
                {
                    notes.Add($"Step '{step.StepKey}' requires toolDefinitionVersionId.");
                }

                break;
            case WorkflowStepTypes.BusinessPolicyCheck:
                if (step.BusinessPolicyDefinitionVersionId is null || step.BusinessPolicyDefinitionVersionId == Guid.Empty)
                {
                    notes.Add($"Step '{step.StepKey}' requires businessPolicyDefinitionVersionId.");
                }

                break;
            case WorkflowStepTypes.OptimizationEvaluate:
                if (step.OptimizationModelVersionId is null || step.OptimizationModelVersionId == Guid.Empty)
                {
                    notes.Add($"Step '{step.StepKey}' requires optimizationModelVersionId.");
                }

                break;
            case WorkflowStepTypes.CreateRecommendation:
                if (string.IsNullOrWhiteSpace(step.SourceStepKey))
                {
                    notes.Add($"Step '{step.StepKey}' requires sourceStepKey.");
                }

                break;
            case WorkflowStepTypes.CreateReviewTask:
                if (step.ReviewTaskTemplateVersionId is null || step.ReviewTaskTemplateVersionId == Guid.Empty)
                {
                    notes.Add($"Step '{step.StepKey}' requires reviewTaskTemplateVersionId.");
                }

                break;
            default:
                notes.Add($"Step '{step.StepKey}' has unsupported stepType '{step.StepType}'.");
                break;
        }

        return notes;
    }

    private static List<WorkflowStepDocument> MapStepRequests(IReadOnlyCollection<WorkflowStepDefinitionRequest>? steps)
        => steps?.Select(step => new WorkflowStepDocument
        {
            StepKey = step.StepKey.Trim(),
            StepType = step.StepType.Trim(),
            SafeModeOnBlock = string.IsNullOrWhiteSpace(step.SafeModeOnBlock)
                ? WorkflowStepSafeModeBehaviors.Skip
                : step.SafeModeOnBlock.Trim(),
            DependsOnStepKeys = step.DependsOnStepKeys?.Select(item => item.Trim()).Where(item => item.Length > 0).ToList() ?? [],
            AgentVersionId = step.AgentVersionId,
            ToolDefinitionVersionId = step.ToolDefinitionVersionId,
            BusinessPolicyDefinitionVersionId = step.BusinessPolicyDefinitionVersionId,
            OptimizationModelVersionId = step.OptimizationModelVersionId,
            SourceStepKey = TrimOptional(step.SourceStepKey),
            ReviewTaskTemplateVersionId = step.ReviewTaskTemplateVersionId
        }).ToList() ?? [];

    private static TriggerConfigDocument MapTriggerConfigRequest(WorkflowTriggerConfigRequest? request)
        => new()
        {
            Manual = new ManualTriggerDocument { Enabled = request?.ManualEnabled ?? true },
            Scheduled = new PlaceholderTriggerDocument
            {
                Enabled = request?.ScheduledEnabled ?? false,
                Placeholder = TrimOptional(request?.ScheduledPlaceholder)
            },
            EventDriven = new PlaceholderTriggerDocument
            {
                Enabled = request?.EventDrivenEnabled ?? false,
                Placeholder = TrimOptional(request?.EventDrivenPlaceholder)
            }
        };

    private static WorkflowTriggerConfigResponse MapTriggerConfig(TriggerConfigDocument? document)
    {
        document ??= new TriggerConfigDocument();
        return new WorkflowTriggerConfigResponse(
            document.Manual?.Enabled ?? true,
            document.Scheduled?.Enabled ?? false,
            TrimOptional(document.Scheduled?.Placeholder),
            document.EventDriven?.Enabled ?? false,
            TrimOptional(document.EventDriven?.Placeholder));
    }

    private static WorkflowDefinitionPayloadDocument Normalize(WorkflowDefinitionPayloadDocument document)
    {
        document.WorkflowKey = document.WorkflowKey?.Trim() ?? string.Empty;
        document.DisplayName = document.DisplayName?.Trim() ?? string.Empty;
        document.Description = TrimOptional(document.Description);
        document.WorkflowScope = document.WorkflowScope?.Trim() ?? WorkflowScopes.Tenant;
        document.WorkflowDefinitionJson ??= "[]";
        document.ReferencedAgentVersionIds ??= [];
        document.ReferencedToolDefinitionVersionIds ??= [];
        document.ReferencedBusinessPolicyDefinitionVersionIds ??= [];
        document.ReferencedOptimizationModelVersionIds ??= [];
        document.CompatibleModelPackageVersionIds ??= [];
        document.CompatibleOntologyVersionIds ??= [];
        document.DefaultStepSafeModeBehavior = string.IsNullOrWhiteSpace(document.DefaultStepSafeModeBehavior)
            ? WorkflowStepSafeModeBehaviors.Skip
            : document.DefaultStepSafeModeBehavior.Trim();
        document.BlockedModeMessage = TrimOptional(document.BlockedModeMessage);
        document.ApprovalRequirements ??= [];
        document.CompatibilityTestNotes ??= [];
        document.CompatibilityFixtureKeys ??= [];
        document.TriggerConfig ??= MapTriggerConfigRequest(null);
        return document;
    }

    private static string? TrimOptional(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    public sealed class WorkflowDefinitionPayloadDocument
    {
        public string? WorkflowKey { get; set; }
        public string? DisplayName { get; set; }
        public string? Description { get; set; }
        public string? WorkflowScope { get; set; }
        public string? WorkflowDefinitionJson { get; set; }
        public Guid? InputSchemaVersionId { get; set; }
        public Guid? OutputSchemaVersionId { get; set; }
        public List<Guid>? ReferencedAgentVersionIds { get; set; }
        public List<Guid>? ReferencedToolDefinitionVersionIds { get; set; }
        public List<Guid>? ReferencedBusinessPolicyDefinitionVersionIds { get; set; }
        public List<Guid>? ReferencedOptimizationModelVersionIds { get; set; }
        public List<Guid>? CompatibleModelPackageVersionIds { get; set; }
        public List<Guid>? CompatibleOntologyVersionIds { get; set; }
        public bool SafeModeEnabled { get; set; }
        public bool PreviewModeDefault { get; set; }
        public string? BlockedModeMessage { get; set; }
        public bool AllowPartialCompletion { get; set; }
        public string? DefaultStepSafeModeBehavior { get; set; }
        public TriggerConfigDocument? TriggerConfig { get; set; }
        public List<string>? ApprovalRequirements { get; set; }
        public List<string>? CompatibilityTestNotes { get; set; }
        public List<string>? CompatibilityFixtureKeys { get; set; }
        public DerivedCapabilityRiskDocument? DerivedCapabilityRiskJson { get; set; }
        public Guid CreatedByUserId { get; set; }
    }

    public sealed class WorkflowStepDocument
    {
        public string? StepKey { get; set; }
        public string? StepType { get; set; }
        public string? SafeModeOnBlock { get; set; }
        public List<string>? DependsOnStepKeys { get; set; }
        public Guid? AgentVersionId { get; set; }
        public Guid? ToolDefinitionVersionId { get; set; }
        public Guid? BusinessPolicyDefinitionVersionId { get; set; }
        public Guid? OptimizationModelVersionId { get; set; }
        public string? SourceStepKey { get; set; }
        public Guid? ReviewTaskTemplateVersionId { get; set; }
    }

    public sealed class TriggerConfigDocument
    {
        public ManualTriggerDocument? Manual { get; set; }
        public PlaceholderTriggerDocument? Scheduled { get; set; }
        public PlaceholderTriggerDocument? EventDriven { get; set; }
    }

    public sealed class ManualTriggerDocument
    {
        public bool Enabled { get; set; } = true;
    }

    public sealed class PlaceholderTriggerDocument
    {
        public bool Enabled { get; set; }
        public string? Placeholder { get; set; }
    }

    public sealed class DerivedCapabilityRiskDocument
    {
        public string? EffectiveRiskLevel { get; set; }
        public List<ToolRiskContributionDocument>? ToolRiskContributions { get; set; }
        public string? PermissionCeiling { get; set; }
    }

    public sealed class ToolRiskContributionDocument
    {
        public Guid ToolDefinitionVersionId { get; set; }
        public string? RiskLevel { get; set; }
    }
}
