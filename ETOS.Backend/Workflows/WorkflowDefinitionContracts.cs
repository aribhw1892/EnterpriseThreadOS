namespace ETOS.Backend.Workflows;

public static class WorkflowDefinitionArtifactTypes
{
    public const string WorkflowVersion = "WorkflowVersion";
}

public static class WorkflowScopes
{
    public const string Platform = "platform";
    public const string Tenant = "tenant";
    public const string Personal = "personal";

    public static readonly IReadOnlyCollection<string> All =
        [Platform, Tenant, Personal];
}

public static class WorkflowStepTypes
{
    public const string AgentExecute = "agent_execute";
    public const string ToolExecute = "tool_execute";
    public const string BusinessPolicyCheck = "business_policy_check";
    public const string OptimizationEvaluate = "optimization_evaluate";
    public const string CreateRecommendation = "create_recommendation";
    public const string CreateReviewTask = "create_review_task";

    public static readonly IReadOnlyCollection<string> All =
    [
        AgentExecute,
        ToolExecute,
        BusinessPolicyCheck,
        OptimizationEvaluate,
        CreateRecommendation,
        CreateReviewTask
    ];
}

public static class WorkflowStepSafeModeBehaviors
{
    public const string Skip = "skip";
    public const string StopWorkflow = "stop_workflow";

    public static readonly IReadOnlyCollection<string> All =
        [Skip, StopWorkflow];
}

public sealed record WorkflowAgentReferenceResponse(
    Guid AgentVersionId,
    Guid AgentArtifactId,
    string AgentArtifactName,
    string AgentKey,
    string VersionLabel,
    string ReadinessState);

public sealed record WorkflowToolReferenceResponse(
    Guid ToolDefinitionVersionId,
    Guid ToolArtifactId,
    string ToolArtifactName,
    string VersionLabel,
    string ReadinessState,
    string RiskLevel);

public sealed record WorkflowBusinessPolicyReferenceResponse(
    Guid BusinessPolicyDefinitionVersionId,
    Guid BusinessPolicyArtifactId,
    string BusinessPolicyArtifactName,
    string PolicyKey,
    string VersionLabel,
    string ReadinessState);

public sealed record WorkflowOptimizationModelReferenceResponse(
    Guid OptimizationModelVersionId,
    Guid OptimizationModelArtifactId,
    string OptimizationModelArtifactName,
    string OptimizationKey,
    string VersionLabel,
    string ReadinessState);

public sealed record WorkflowModelPackageReferenceResponse(
    Guid ModelPackageVersionId,
    string Key,
    string Name,
    string VersionLabel,
    string State);

public sealed record WorkflowOntologyReferenceResponse(
    Guid OntologyVersionId,
    string Key,
    string VersionLabel,
    string State);

public sealed record WorkflowArtifactVersionReferenceResponse(
    Guid VersionId,
    Guid ArtifactId,
    string ArtifactType,
    string ArtifactName,
    string VersionLabel,
    string ReadinessState);

public sealed record WorkflowDerivedCapabilityRiskResponse(
    string EffectiveRiskLevel,
    IReadOnlyCollection<WorkflowToolRiskContributionResponse> ToolRiskContributions,
    string PermissionCeiling);

public sealed record WorkflowToolRiskContributionResponse(
    Guid ToolDefinitionVersionId,
    string RiskLevel);

public sealed record WorkflowStepDefinitionResponse(
    string StepKey,
    string StepType,
    string SafeModeOnBlock,
    IReadOnlyCollection<string> DependsOnStepKeys,
    Guid? AgentVersionId,
    Guid? ToolDefinitionVersionId,
    Guid? BusinessPolicyDefinitionVersionId,
    Guid? OptimizationModelVersionId,
    string? SourceStepKey,
    Guid? ReviewTaskTemplateVersionId);

public sealed record WorkflowTriggerConfigResponse(
    bool ManualEnabled,
    bool ScheduledEnabled,
    string? ScheduledPlaceholder,
    bool EventDrivenEnabled,
    string? EventDrivenPlaceholder);

public sealed record WorkflowDefinitionArtifactSummaryResponse(
    Guid Id,
    Guid TenantId,
    string ArtifactType,
    string Name,
    string? Description,
    string? LatestVersionLabel,
    string? ReadinessState,
    string? WorkflowKey,
    string? DisplayName,
    string? WorkflowScope,
    DateTimeOffset UpdatedAt);

public sealed record WorkflowDefinitionDetailResponse(
    Guid ArtifactId,
    Guid VersionId,
    string VersionLabel,
    string Name,
    string? Description,
    string ArtifactReadinessState,
    string WorkflowKey,
    string DisplayName,
    string? WorkflowDescription,
    string WorkflowScope,
    IReadOnlyCollection<WorkflowStepDefinitionResponse> Steps,
    WorkflowArtifactVersionReferenceResponse? InputSchema,
    WorkflowArtifactVersionReferenceResponse? OutputSchema,
    IReadOnlyCollection<WorkflowAgentReferenceResponse> ReferencedAgents,
    IReadOnlyCollection<WorkflowToolReferenceResponse> ReferencedTools,
    IReadOnlyCollection<WorkflowBusinessPolicyReferenceResponse> ReferencedBusinessPolicies,
    IReadOnlyCollection<WorkflowOptimizationModelReferenceResponse> ReferencedOptimizationModels,
    IReadOnlyCollection<WorkflowModelPackageReferenceResponse> CompatibleModelPackages,
    IReadOnlyCollection<WorkflowOntologyReferenceResponse> CompatibleOntologies,
    bool SafeModeEnabled,
    bool PreviewModeDefault,
    string? BlockedModeMessage,
    bool AllowPartialCompletion,
    string DefaultStepSafeModeBehavior,
    WorkflowTriggerConfigResponse TriggerConfig,
    IReadOnlyCollection<string> ApprovalRequirements,
    IReadOnlyCollection<string> CompatibilityTestNotes,
    IReadOnlyCollection<string> CompatibilityFixtureKeys,
    WorkflowDerivedCapabilityRiskResponse? DerivedCapabilityRisk,
    Guid CreatedByUserId);

public sealed record WorkflowDependencySummaryResponse(
    IReadOnlyCollection<WorkflowAgentReferenceResponse> Agents,
    IReadOnlyCollection<WorkflowToolReferenceResponse> Tools,
    IReadOnlyCollection<WorkflowBusinessPolicyReferenceResponse> BusinessPolicies,
    IReadOnlyCollection<WorkflowOptimizationModelReferenceResponse> OptimizationModels,
    IReadOnlyCollection<WorkflowModelPackageReferenceResponse> ModelPackages,
    IReadOnlyCollection<WorkflowOntologyReferenceResponse> Ontologies,
    WorkflowArtifactVersionReferenceResponse? InputSchema,
    WorkflowArtifactVersionReferenceResponse? OutputSchema);

public sealed record WorkflowStepDefinitionRequest(
    string StepKey,
    string StepType,
    string SafeModeOnBlock,
    IReadOnlyCollection<string>? DependsOnStepKeys,
    Guid? AgentVersionId,
    Guid? ToolDefinitionVersionId,
    Guid? BusinessPolicyDefinitionVersionId,
    Guid? OptimizationModelVersionId,
    string? SourceStepKey,
    Guid? ReviewTaskTemplateVersionId);

public sealed record WorkflowTriggerConfigRequest(
    bool ManualEnabled,
    bool ScheduledEnabled,
    string? ScheduledPlaceholder,
    bool EventDrivenEnabled,
    string? EventDrivenPlaceholder);

public sealed record CreateWorkflowDefinitionRequest(
    string Name,
    string? Description,
    string WorkflowKey,
    string DisplayName,
    string? WorkflowDescription,
    string WorkflowScope,
    IReadOnlyCollection<WorkflowStepDefinitionRequest>? Steps,
    Guid? InputSchemaVersionId,
    Guid? OutputSchemaVersionId,
    IReadOnlyCollection<Guid>? ReferencedAgentVersionIds,
    IReadOnlyCollection<Guid>? ReferencedToolDefinitionVersionIds,
    IReadOnlyCollection<Guid>? ReferencedBusinessPolicyDefinitionVersionIds,
    IReadOnlyCollection<Guid>? ReferencedOptimizationModelVersionIds,
    IReadOnlyCollection<Guid>? CompatibleModelPackageVersionIds,
    IReadOnlyCollection<Guid>? CompatibleOntologyVersionIds,
    bool SafeModeEnabled,
    bool PreviewModeDefault,
    string? BlockedModeMessage,
    bool AllowPartialCompletion,
    string DefaultStepSafeModeBehavior,
    WorkflowTriggerConfigRequest? TriggerConfig,
    IReadOnlyCollection<string>? ApprovalRequirements,
    IReadOnlyCollection<string>? CompatibilityTestNotes,
    IReadOnlyCollection<string>? CompatibilityFixtureKeys);

public sealed record CreateWorkflowDefinitionVersionRequest(
    string VersionLabel,
    string? Summary,
    string WorkflowKey,
    string DisplayName,
    string? WorkflowDescription,
    string WorkflowScope,
    IReadOnlyCollection<WorkflowStepDefinitionRequest>? Steps,
    Guid? InputSchemaVersionId,
    Guid? OutputSchemaVersionId,
    IReadOnlyCollection<Guid>? ReferencedAgentVersionIds,
    IReadOnlyCollection<Guid>? ReferencedToolDefinitionVersionIds,
    IReadOnlyCollection<Guid>? ReferencedBusinessPolicyDefinitionVersionIds,
    IReadOnlyCollection<Guid>? ReferencedOptimizationModelVersionIds,
    IReadOnlyCollection<Guid>? CompatibleModelPackageVersionIds,
    IReadOnlyCollection<Guid>? CompatibleOntologyVersionIds,
    bool SafeModeEnabled,
    bool PreviewModeDefault,
    string? BlockedModeMessage,
    bool AllowPartialCompletion,
    string DefaultStepSafeModeBehavior,
    WorkflowTriggerConfigRequest? TriggerConfig,
    IReadOnlyCollection<string>? ApprovalRequirements,
    IReadOnlyCollection<string>? CompatibilityTestNotes,
    IReadOnlyCollection<string>? CompatibilityFixtureKeys);

public sealed record CreateWorkflowDefinitionResponse(
    Guid ArtifactId,
    Guid VersionId,
    string VersionLabel);

public sealed record CreateWorkflowDefinitionVersionResponse(
    Guid ArtifactId,
    Guid VersionId,
    string VersionLabel);

public sealed record MarkWorkflowDefinitionReadyResponse(
    Guid ArtifactId,
    Guid VersionId,
    string ReadinessState,
    IReadOnlyCollection<string> ValidationNotes,
    WorkflowDerivedCapabilityRiskResponse? DerivedCapabilityRisk);

public sealed record PublishWorkflowDefinitionResponse(
    bool Succeeded,
    string ReadinessState,
    IReadOnlyCollection<string> BlockingReasons,
    Guid ArtifactId,
    Guid VersionId);
