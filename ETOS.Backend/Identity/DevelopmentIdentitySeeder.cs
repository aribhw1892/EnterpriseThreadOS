using ETOS.Backend.AiTrace;
using ETOS.Backend.Artifacts;
using ETOS.Backend.BusinessPolicies;
using ETOS.Backend.Capabilities;
using ETOS.Backend.OptimizationModels;
using ETOS.Backend.AgentRuns;
using ETOS.Backend.AgentTemplates;
using ETOS.Backend.AgentTypes;
using ETOS.Backend.Classification;
using ETOS.Backend.Dashboards;
using ETOS.Backend.DataQuality;
using ETOS.Backend.Explorers;
using ETOS.Backend.Governance;
using ETOS.Backend.GovernedChat;
using ETOS.Backend.GovernedQuery;
using ETOS.Backend.Infrastructure.Persistence;
using ETOS.Backend.IdentityResolution;
using ETOS.Backend.Decisions;
using ETOS.Backend.GovernanceAnalytics;
using ETOS.Backend.DigitalThread;
using ETOS.Backend.Learning;
using ETOS.Backend.Outcomes;
using ETOS.Backend.Recommendations;
using ETOS.Backend.ReviewTasks;
using ETOS.Backend.ToolRegistry;
using ETOS.Backend.Workflows;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ETOS.Backend.Identity;

public interface IDevelopmentIdentitySeeder
{
    Task SeedAsync(CancellationToken cancellationToken);
}

public sealed class DevelopmentIdentitySeeder(
    EnterpriseThreadDbContext dbContext,
    UserManager<EtosUser> userManager,
    IAuditRecorder auditRecorder,
    IOptions<SeedIdentityOptions> options,
    ILogger<DevelopmentIdentitySeeder> logger) : IDevelopmentIdentitySeeder
{
    public async Task SeedAsync(CancellationToken cancellationToken)
    {
        var seedOptions = options.Value;
        if (!seedOptions.Enabled)
        {
            return;
        }

        var admin = await EnsureAdminUserAsync(seedOptions, cancellationToken);
        var tenant = await EnsureTenantAsync(seedOptions, cancellationToken);
        var adminPermission = await EnsurePermissionAsync(IdentityPermissions.IdentityAdmin, "Manage tenant identity and access.", cancellationToken);
        var wildcardPermission = await EnsurePermissionAsync(IdentityPermissions.Wildcard, "Tenant administrator wildcard permission.", cancellationToken);
        var artifactReadPermission = await EnsurePermissionAsync(ArtifactPermissions.Read, "Read tenant artifact registry records.", cancellationToken);
        var artifactCreatePermission = await EnsurePermissionAsync(ArtifactPermissions.Create, "Create tenant artifact registry records.", cancellationToken);
        var artifactPublishPermission = await EnsurePermissionAsync(ArtifactPermissions.Publish, "Publish tenant artifact versions.", cancellationToken);
        var artifactAdminPermission = await EnsurePermissionAsync(ArtifactPermissions.Admin, "Administer tenant artifact registry records.", cancellationToken);
        var classificationReadPermission = await EnsurePermissionAsync(ClassificationPermissions.Read, "Read tenant classification and policy records.", cancellationToken);
        var classificationManagePermission = await EnsurePermissionAsync(ClassificationPermissions.Manage, "Manage tenant classification and policy drafts.", cancellationToken);
        var classificationPublishPermission = await EnsurePermissionAsync(ClassificationPermissions.Publish, "Publish tenant classification and policy versions.", cancellationToken);
        var policyEvaluatePermission = await EnsurePermissionAsync(ClassificationPermissions.Evaluate, "Evaluate tenant policy against governed context.", cancellationToken);
        var policyAdminPermission = await EnsurePermissionAsync(ClassificationPermissions.Admin, "Administer tenant policy enforcement.", cancellationToken);
        var identityResolutionReadPermission = await EnsurePermissionAsync(IdentityResolutionPermissions.Read, "Read tenant identity resolution records.", cancellationToken);
        var identityResolutionManagePermission = await EnsurePermissionAsync(IdentityResolutionPermissions.Manage, "Manage tenant identity resolution rules and candidate generation.", cancellationToken);
        var identityResolutionReviewPermission = await EnsurePermissionAsync(IdentityResolutionPermissions.Review, "Review tenant identity resolution candidates.", cancellationToken);
        var identityResolutionAdminPermission = await EnsurePermissionAsync(IdentityResolutionPermissions.Admin, "Administer tenant identity resolution.", cancellationToken);
        var dataQualityReadPermission = await EnsurePermissionAsync(DataQualityPermissions.Read, "Read tenant data quality issues and review hooks.", cancellationToken);
        var dataQualityManagePermission = await EnsurePermissionAsync(DataQualityPermissions.Manage, "Manage tenant data quality issues and issue generation.", cancellationToken);
        var dataQualityReviewHookPermission = await EnsurePermissionAsync(DataQualityPermissions.ReviewHook, "Create data quality review hooks from governed events.", cancellationToken);
        var dataQualityAdminPermission = await EnsurePermissionAsync(DataQualityPermissions.Admin, "Administer tenant data quality records.", cancellationToken);
        var aiTraceReadPermission = await EnsurePermissionAsync(AiTracePermissions.Read, "Read tenant AI Trace records.", cancellationToken);
        var aiTraceExportPermission = await EnsurePermissionAsync(AiTracePermissions.Export, "Export tenant AI Trace packages.", cancellationToken);
        var aiTraceAdminPermission = await EnsurePermissionAsync(AiTracePermissions.Admin, "Administer tenant AI Trace records.", cancellationToken);
        var governedQueryReadPermission = await EnsurePermissionAsync(GovernedQueryPermissions.Read, "Read tenant governed query records.", cancellationToken);
        var governedQueryRunPermission = await EnsurePermissionAsync(GovernedQueryPermissions.Run, "Run tenant governed query retrieval.", cancellationToken);
        var governedQueryAdminPermission = await EnsurePermissionAsync(GovernedQueryPermissions.Admin, "Administer tenant governed query records.", cancellationToken);
        var governedChatRunPermission = await EnsurePermissionAsync(GovernedChatPermissions.Run, "Run tenant governed chat turns.", cancellationToken);
        var governedChatDraftPermission = await EnsurePermissionAsync(GovernedChatPermissions.Draft, "Create draft artifacts from governed chat.", cancellationToken);
        var governedChatAdminPermission = await EnsurePermissionAsync(GovernedChatPermissions.Admin, "Administer tenant governed chat records.", cancellationToken);
        var explorersReadPermission = await EnsurePermissionAsync(ExplorerPermissions.Read, "Read tenant explorer surfaces.", cancellationToken);
        var contextViewReadPermission = await EnsurePermissionAsync(ExplorerPermissions.ContextView, "Read tenant 360° context views.", cancellationToken);
        var governanceFlowReadPermission = await EnsurePermissionAsync(ExplorerPermissions.GovernanceFlow, "Read tenant governance flow views.", cancellationToken);
        var graphExplorerReadPermission = await EnsurePermissionAsync(ExplorerPermissions.GraphExplorer, "Read tenant governed graph explorer records.", cancellationToken);
        var dashboardReportPreviewPermission = await EnsurePermissionAsync(DashboardReportPermissions.Preview, "Preview tenant dashboards and reports.", cancellationToken);
        var dashboardReportExportPermission = await EnsurePermissionAsync(DashboardReportPermissions.Export, "Export tenant dashboards and reports.", cancellationToken);
        var dashboardReportReadinessPermission = await EnsurePermissionAsync(DashboardReportPermissions.Readiness, "Mark tenant dashboard and report versions ready.", cancellationToken);
        var dashboardReportAdminPermission = await EnsurePermissionAsync(DashboardReportPermissions.Admin, "Administer tenant dashboard and report records.", cancellationToken);
        var recommendationReadPermission = await EnsurePermissionAsync(RecommendationPermissions.Read, "Read tenant recommendation artifacts.", cancellationToken);
        var recommendationCreatePermission = await EnsurePermissionAsync(RecommendationPermissions.Create, "Create tenant recommendation artifacts.", cancellationToken);
        var recommendationReviewPermission = await EnsurePermissionAsync(RecommendationPermissions.Review, "Review tenant recommendation artifacts.", cancellationToken);
        var recommendationReadinessPermission = await EnsurePermissionAsync(RecommendationPermissions.Readiness, "Mark tenant recommendation versions ready.", cancellationToken);
        var recommendationAdminPermission = await EnsurePermissionAsync(RecommendationPermissions.Admin, "Administer tenant recommendation records.", cancellationToken);
        var capabilityReadPermission = await EnsurePermissionAsync(CapabilityDefinitionPermissions.Read, "Read tenant capability definition artifacts.", cancellationToken);
        var capabilityCreatePermission = await EnsurePermissionAsync(CapabilityDefinitionPermissions.Create, "Create tenant capability definition artifacts.", cancellationToken);
        var capabilityReadinessPermission = await EnsurePermissionAsync(CapabilityDefinitionPermissions.Readiness, "Mark tenant capability definition versions ready.", cancellationToken);
        var capabilityAdminPermission = await EnsurePermissionAsync(CapabilityDefinitionPermissions.Admin, "Administer tenant capability definition records.", cancellationToken);
        var businessPolicyReadPermission = await EnsurePermissionAsync(BusinessPolicyDefinitionPermissions.Read, "Read tenant business policy definition artifacts.", cancellationToken);
        var businessPolicyCreatePermission = await EnsurePermissionAsync(BusinessPolicyDefinitionPermissions.Create, "Create tenant business policy definition artifacts.", cancellationToken);
        var businessPolicyReadinessPermission = await EnsurePermissionAsync(BusinessPolicyDefinitionPermissions.Readiness, "Mark tenant business policy definition versions ready.", cancellationToken);
        var businessPolicyAdminPermission = await EnsurePermissionAsync(BusinessPolicyDefinitionPermissions.Admin, "Administer tenant business policy definition records.", cancellationToken);
        var optimizationModelReadPermission = await EnsurePermissionAsync(OptimizationModelDefinitionPermissions.Read, "Read tenant optimization model definition artifacts.", cancellationToken);
        var optimizationModelCreatePermission = await EnsurePermissionAsync(OptimizationModelDefinitionPermissions.Create, "Create tenant optimization model definition artifacts.", cancellationToken);
        var optimizationModelReadinessPermission = await EnsurePermissionAsync(OptimizationModelDefinitionPermissions.Readiness, "Mark tenant optimization model definition versions ready.", cancellationToken);
        var optimizationModelAdminPermission = await EnsurePermissionAsync(OptimizationModelDefinitionPermissions.Admin, "Administer tenant optimization model definition records.", cancellationToken);
        var agentTemplateReadPermission = await EnsurePermissionAsync(AgentTemplateDefinitionPermissions.Read, "Read tenant agent template definition artifacts.", cancellationToken);
        var agentTemplateCreatePermission = await EnsurePermissionAsync(AgentTemplateDefinitionPermissions.Create, "Create tenant agent template definition artifacts.", cancellationToken);
        var agentTemplateReadinessPermission = await EnsurePermissionAsync(AgentTemplateDefinitionPermissions.Readiness, "Mark tenant agent template definition versions ready.", cancellationToken);
        var agentTemplateAdminPermission = await EnsurePermissionAsync(AgentTemplateDefinitionPermissions.Admin, "Administer tenant agent template definition records.", cancellationToken);
        var toolReadPermission = await EnsurePermissionAsync(ToolDefinitionPermissions.Read, "Read tenant tool definition artifacts.", cancellationToken);
        var toolCreatePermission = await EnsurePermissionAsync(ToolDefinitionPermissions.Create, "Create tenant tool definition artifacts.", cancellationToken);
        var toolReadinessPermission = await EnsurePermissionAsync(ToolDefinitionPermissions.Readiness, "Mark tenant tool definition versions ready.", cancellationToken);
        var toolAdminPermission = await EnsurePermissionAsync(ToolDefinitionPermissions.Admin, "Administer tenant tool definition records.", cancellationToken);
        var toolExecutePermission = await EnsurePermissionAsync(ToolDefinitionPermissions.Execute, "Execute published tenant tools.", cancellationToken);
        var toolDryRunPermission = await EnsurePermissionAsync(ToolDefinitionPermissions.DryRun, "Dry-run published tenant tools.", cancellationToken);
        var skillReadPermission = await EnsurePermissionAsync(SkillDefinitionPermissions.Read, "Read tenant skill definition artifacts.", cancellationToken);
        var skillCreatePermission = await EnsurePermissionAsync(SkillDefinitionPermissions.Create, "Create tenant skill definition artifacts.", cancellationToken);
        var skillReadinessPermission = await EnsurePermissionAsync(SkillDefinitionPermissions.Readiness, "Mark tenant skill definition versions ready.", cancellationToken);
        var skillAdminPermission = await EnsurePermissionAsync(SkillDefinitionPermissions.Admin, "Administer tenant skill definition records.", cancellationToken);
        var connectorReadPermission = await EnsurePermissionAsync(ConnectorDefinitionPermissions.Read, "Read tenant connector definition artifacts.", cancellationToken);
        var connectorCreatePermission = await EnsurePermissionAsync(ConnectorDefinitionPermissions.Create, "Create tenant connector definition artifacts.", cancellationToken);
        var connectorReadinessPermission = await EnsurePermissionAsync(ConnectorDefinitionPermissions.Readiness, "Mark tenant connector definition versions ready.", cancellationToken);
        var connectorAdminPermission = await EnsurePermissionAsync(ConnectorDefinitionPermissions.Admin, "Administer tenant connector definition records.", cancellationToken);
        var toolRunReadPermission = await EnsurePermissionAsync(ToolRunPermissions.Read, "Read tenant tool run records.", cancellationToken);
        var agentRunReadPermission = await EnsurePermissionAsync(AgentRunPermissions.Read, "Read tenant agent run records.", cancellationToken);
        var agentTypeReadPermission = await EnsurePermissionAsync(AgentTypeDefinitionPermissions.Read, "Read tenant agent type definition artifacts.", cancellationToken);
        var agentTypeCreatePermission = await EnsurePermissionAsync(AgentTypeDefinitionPermissions.Create, "Create tenant agent type definition artifacts.", cancellationToken);
        var agentTypeReadinessPermission = await EnsurePermissionAsync(AgentTypeDefinitionPermissions.Readiness, "Mark tenant agent type definition versions ready.", cancellationToken);
        var agentTypeAdminPermission = await EnsurePermissionAsync(AgentTypeDefinitionPermissions.Admin, "Administer tenant agent type definition records.", cancellationToken);
        var agentsReadPermission = await EnsurePermissionAsync(AgentPermissions.Read, "Read tenant agent version artifacts.", cancellationToken);
        var agentsCreatePermission = await EnsurePermissionAsync(AgentPermissions.Create, "Create tenant agent version artifacts.", cancellationToken);
        var agentsReadinessPermission = await EnsurePermissionAsync(AgentPermissions.Readiness, "Mark tenant agent versions ready.", cancellationToken);
        var agentsAdminPermission = await EnsurePermissionAsync(AgentPermissions.Admin, "Administer tenant agent records.", cancellationToken);
        var agentsTestPermission = await EnsurePermissionAsync(AgentPermissions.Test, "Test-run draft tenant agents.", cancellationToken);
        var agentsExecutePermission = await EnsurePermissionAsync(AgentPermissions.Execute, "Execute published tenant agents.", cancellationToken);
        var workflowsReadPermission = await EnsurePermissionAsync(WorkflowPermissions.Read, "Read tenant workflow version artifacts.", cancellationToken);
        var workflowsCreatePermission = await EnsurePermissionAsync(WorkflowPermissions.Create, "Create tenant workflow version artifacts.", cancellationToken);
        var workflowsReadinessPermission = await EnsurePermissionAsync(WorkflowPermissions.Readiness, "Mark tenant workflow versions ready.", cancellationToken);
        var workflowsAdminPermission = await EnsurePermissionAsync(WorkflowPermissions.Admin, "Administer tenant workflow records.", cancellationToken);
        var workflowsPreviewPermission = await EnsurePermissionAsync(WorkflowPermissions.Preview, "Preview and test-run draft tenant workflows.", cancellationToken);
        var workflowsExecutePermission = await EnsurePermissionAsync(WorkflowPermissions.Execute, "Execute published tenant workflows.", cancellationToken);
        var workflowRunsReadPermission = await EnsurePermissionAsync(WorkflowRunPermissions.Read, "Read tenant workflow run records.", cancellationToken);
        var reviewTaskReadPermission = await EnsurePermissionAsync(ReviewTaskPermissions.Read, "Read tenant review task artifacts.", cancellationToken);
        var reviewTaskCreatePermission = await EnsurePermissionAsync(ReviewTaskPermissions.Create, "Create tenant review task artifacts.", cancellationToken);
        var reviewTaskAssignPermission = await EnsurePermissionAsync(ReviewTaskPermissions.Assign, "Assign tenant review tasks.", cancellationToken);
        var reviewTaskManagePermission = await EnsurePermissionAsync(ReviewTaskPermissions.Manage, "Manage tenant review task lifecycle.", cancellationToken);
        var reviewTaskAdminPermission = await EnsurePermissionAsync(ReviewTaskPermissions.Admin, "Administer tenant review task records.", cancellationToken);
        var reviewTaskTemplateReadPermission = await EnsurePermissionAsync(ReviewTaskTemplatePermissions.Read, "Read tenant review task template artifacts.", cancellationToken);
        var reviewTaskTemplateCreatePermission = await EnsurePermissionAsync(ReviewTaskTemplatePermissions.Create, "Create tenant review task template artifacts.", cancellationToken);
        var reviewTaskTemplateReadinessPermission = await EnsurePermissionAsync(ReviewTaskTemplatePermissions.Readiness, "Mark tenant review task template versions ready.", cancellationToken);
        var reviewTaskTemplateAdminPermission = await EnsurePermissionAsync(ReviewTaskTemplatePermissions.Admin, "Administer tenant review task template records.", cancellationToken);
        var decisionReadPermission = await EnsurePermissionAsync(DecisionPermissions.Read, "Read tenant decision artifacts.", cancellationToken);
        var decisionVotePermission = await EnsurePermissionAsync(DecisionPermissions.Vote, "Vote on tenant decision artifacts.", cancellationToken);
        var decisionManagePermission = await EnsurePermissionAsync(DecisionPermissions.Manage, "Manage tenant decision lifecycle.", cancellationToken);
        var decisionAdminPermission = await EnsurePermissionAsync(DecisionPermissions.Admin, "Administer tenant decision records.", cancellationToken);
        var outcomeReadPermission = await EnsurePermissionAsync(OutcomePermissions.Read, "Read tenant outcome taxonomy artifacts.", cancellationToken);
        var outcomeRecordPermission = await EnsurePermissionAsync(OutcomePermissions.Record, "Record manual decision outcomes.", cancellationToken);
        var outcomeAdminPermission = await EnsurePermissionAsync(OutcomePermissions.Admin, "Administer tenant outcome records.", cancellationToken);
        var learningReadPermission = await EnsurePermissionAsync(LearningPermissions.Read, "Read tenant learning signal artifacts.", cancellationToken);
        var learningAdminPermission = await EnsurePermissionAsync(LearningPermissions.Admin, "Administer tenant learning records.", cancellationToken);
        var digitalThreadReadPermission = await EnsurePermissionAsync(
            DigitalThreadPermissions.Read,
            "Read tenant digital thread projection summary, systems, and events.",
            cancellationToken);
        var digitalThreadAdminPermission = await EnsurePermissionAsync(
            DigitalThreadPermissions.Admin,
            "Administer tenant digital thread projection access.",
            cancellationToken);
        var governanceAnalyticsReadPermission = await EnsurePermissionAsync(
            GovernanceAnalyticsPermissions.Read,
            "Read tenant governance analytics and KPI dashboards.",
            cancellationToken);
        var adminRole = await EnsureTenantRoleAsync(tenant.Id, cancellationToken);
        var chatRunnerRole = await EnsureChatRunnerRoleAsync(tenant.Id, cancellationToken);
        var chatRunner = await EnsureChatRunnerUserAsync(seedOptions, cancellationToken);

        await EnsureMembershipAsync(tenant.Id, admin.Id, adminRole.Id, cancellationToken);
        await EnsureRolePermissionAsync(tenant.Id, adminRole.Id, adminPermission.Id, cancellationToken);
        await EnsureRolePermissionAsync(tenant.Id, adminRole.Id, wildcardPermission.Id, cancellationToken);
        await EnsureRolePermissionAsync(tenant.Id, adminRole.Id, artifactReadPermission.Id, cancellationToken);
        await EnsureRolePermissionAsync(tenant.Id, adminRole.Id, artifactCreatePermission.Id, cancellationToken);
        await EnsureRolePermissionAsync(tenant.Id, adminRole.Id, artifactPublishPermission.Id, cancellationToken);
        await EnsureRolePermissionAsync(tenant.Id, adminRole.Id, artifactAdminPermission.Id, cancellationToken);
        await EnsureRolePermissionAsync(tenant.Id, adminRole.Id, classificationReadPermission.Id, cancellationToken);
        await EnsureRolePermissionAsync(tenant.Id, adminRole.Id, classificationManagePermission.Id, cancellationToken);
        await EnsureRolePermissionAsync(tenant.Id, adminRole.Id, classificationPublishPermission.Id, cancellationToken);
        await EnsureRolePermissionAsync(tenant.Id, adminRole.Id, policyEvaluatePermission.Id, cancellationToken);
        await EnsureRolePermissionAsync(tenant.Id, adminRole.Id, policyAdminPermission.Id, cancellationToken);
        await EnsureRolePermissionAsync(tenant.Id, adminRole.Id, identityResolutionReadPermission.Id, cancellationToken);
        await EnsureRolePermissionAsync(tenant.Id, adminRole.Id, identityResolutionManagePermission.Id, cancellationToken);
        await EnsureRolePermissionAsync(tenant.Id, adminRole.Id, identityResolutionReviewPermission.Id, cancellationToken);
        await EnsureRolePermissionAsync(tenant.Id, adminRole.Id, identityResolutionAdminPermission.Id, cancellationToken);
        await EnsureRolePermissionAsync(tenant.Id, adminRole.Id, dataQualityReadPermission.Id, cancellationToken);
        await EnsureRolePermissionAsync(tenant.Id, adminRole.Id, dataQualityManagePermission.Id, cancellationToken);
        await EnsureRolePermissionAsync(tenant.Id, adminRole.Id, dataQualityReviewHookPermission.Id, cancellationToken);
        await EnsureRolePermissionAsync(tenant.Id, adminRole.Id, dataQualityAdminPermission.Id, cancellationToken);
        await EnsureRolePermissionAsync(tenant.Id, adminRole.Id, aiTraceReadPermission.Id, cancellationToken);
        await EnsureRolePermissionAsync(tenant.Id, adminRole.Id, aiTraceExportPermission.Id, cancellationToken);
        await EnsureRolePermissionAsync(tenant.Id, adminRole.Id, aiTraceAdminPermission.Id, cancellationToken);
        await EnsureRolePermissionAsync(tenant.Id, adminRole.Id, governedQueryReadPermission.Id, cancellationToken);
        await EnsureRolePermissionAsync(tenant.Id, adminRole.Id, governedQueryRunPermission.Id, cancellationToken);
        await EnsureRolePermissionAsync(tenant.Id, adminRole.Id, governedQueryAdminPermission.Id, cancellationToken);
        await EnsureRolePermissionAsync(tenant.Id, adminRole.Id, governedChatRunPermission.Id, cancellationToken);
        await EnsureRolePermissionAsync(tenant.Id, adminRole.Id, governedChatDraftPermission.Id, cancellationToken);
        await EnsureRolePermissionAsync(tenant.Id, adminRole.Id, governedChatAdminPermission.Id, cancellationToken);
        await EnsureRolePermissionAsync(tenant.Id, adminRole.Id, explorersReadPermission.Id, cancellationToken);
        await EnsureRolePermissionAsync(tenant.Id, adminRole.Id, contextViewReadPermission.Id, cancellationToken);
        await EnsureRolePermissionAsync(tenant.Id, adminRole.Id, governanceFlowReadPermission.Id, cancellationToken);
        await EnsureRolePermissionAsync(tenant.Id, adminRole.Id, graphExplorerReadPermission.Id, cancellationToken);
        await EnsureRolePermissionAsync(tenant.Id, adminRole.Id, dashboardReportPreviewPermission.Id, cancellationToken);
        await EnsureRolePermissionAsync(tenant.Id, adminRole.Id, dashboardReportExportPermission.Id, cancellationToken);
        await EnsureRolePermissionAsync(tenant.Id, adminRole.Id, dashboardReportReadinessPermission.Id, cancellationToken);
        await EnsureRolePermissionAsync(tenant.Id, adminRole.Id, dashboardReportAdminPermission.Id, cancellationToken);
        await EnsureRolePermissionAsync(tenant.Id, adminRole.Id, recommendationReadPermission.Id, cancellationToken);
        await EnsureRolePermissionAsync(tenant.Id, adminRole.Id, recommendationCreatePermission.Id, cancellationToken);
        await EnsureRolePermissionAsync(tenant.Id, adminRole.Id, recommendationReviewPermission.Id, cancellationToken);
        await EnsureRolePermissionAsync(tenant.Id, adminRole.Id, recommendationReadinessPermission.Id, cancellationToken);
        await EnsureRolePermissionAsync(tenant.Id, adminRole.Id, recommendationAdminPermission.Id, cancellationToken);
        await EnsureRolePermissionAsync(tenant.Id, adminRole.Id, capabilityReadPermission.Id, cancellationToken);
        await EnsureRolePermissionAsync(tenant.Id, adminRole.Id, capabilityCreatePermission.Id, cancellationToken);
        await EnsureRolePermissionAsync(tenant.Id, adminRole.Id, capabilityReadinessPermission.Id, cancellationToken);
        await EnsureRolePermissionAsync(tenant.Id, adminRole.Id, capabilityAdminPermission.Id, cancellationToken);
        await EnsureRolePermissionAsync(tenant.Id, adminRole.Id, businessPolicyReadPermission.Id, cancellationToken);
        await EnsureRolePermissionAsync(tenant.Id, adminRole.Id, businessPolicyCreatePermission.Id, cancellationToken);
        await EnsureRolePermissionAsync(tenant.Id, adminRole.Id, businessPolicyReadinessPermission.Id, cancellationToken);
        await EnsureRolePermissionAsync(tenant.Id, adminRole.Id, businessPolicyAdminPermission.Id, cancellationToken);
        await EnsureRolePermissionAsync(tenant.Id, adminRole.Id, optimizationModelReadPermission.Id, cancellationToken);
        await EnsureRolePermissionAsync(tenant.Id, adminRole.Id, optimizationModelCreatePermission.Id, cancellationToken);
        await EnsureRolePermissionAsync(tenant.Id, adminRole.Id, optimizationModelReadinessPermission.Id, cancellationToken);
        await EnsureRolePermissionAsync(tenant.Id, adminRole.Id, optimizationModelAdminPermission.Id, cancellationToken);
        await EnsureRolePermissionAsync(tenant.Id, adminRole.Id, agentTemplateReadPermission.Id, cancellationToken);
        await EnsureRolePermissionAsync(tenant.Id, adminRole.Id, agentTemplateCreatePermission.Id, cancellationToken);
        await EnsureRolePermissionAsync(tenant.Id, adminRole.Id, agentTemplateReadinessPermission.Id, cancellationToken);
        await EnsureRolePermissionAsync(tenant.Id, adminRole.Id, agentTemplateAdminPermission.Id, cancellationToken);
        await EnsureRolePermissionAsync(tenant.Id, adminRole.Id, toolReadPermission.Id, cancellationToken);
        await EnsureRolePermissionAsync(tenant.Id, adminRole.Id, toolCreatePermission.Id, cancellationToken);
        await EnsureRolePermissionAsync(tenant.Id, adminRole.Id, toolReadinessPermission.Id, cancellationToken);
        await EnsureRolePermissionAsync(tenant.Id, adminRole.Id, toolAdminPermission.Id, cancellationToken);
        await EnsureRolePermissionAsync(tenant.Id, adminRole.Id, toolExecutePermission.Id, cancellationToken);
        await EnsureRolePermissionAsync(tenant.Id, adminRole.Id, toolDryRunPermission.Id, cancellationToken);
        await EnsureRolePermissionAsync(tenant.Id, adminRole.Id, skillReadPermission.Id, cancellationToken);
        await EnsureRolePermissionAsync(tenant.Id, adminRole.Id, skillCreatePermission.Id, cancellationToken);
        await EnsureRolePermissionAsync(tenant.Id, adminRole.Id, skillReadinessPermission.Id, cancellationToken);
        await EnsureRolePermissionAsync(tenant.Id, adminRole.Id, skillAdminPermission.Id, cancellationToken);
        await EnsureRolePermissionAsync(tenant.Id, adminRole.Id, connectorReadPermission.Id, cancellationToken);
        await EnsureRolePermissionAsync(tenant.Id, adminRole.Id, connectorCreatePermission.Id, cancellationToken);
        await EnsureRolePermissionAsync(tenant.Id, adminRole.Id, connectorReadinessPermission.Id, cancellationToken);
        await EnsureRolePermissionAsync(tenant.Id, adminRole.Id, connectorAdminPermission.Id, cancellationToken);
        await EnsureRolePermissionAsync(tenant.Id, adminRole.Id, toolRunReadPermission.Id, cancellationToken);
        await EnsureRolePermissionAsync(tenant.Id, adminRole.Id, agentRunReadPermission.Id, cancellationToken);
        await EnsureRolePermissionAsync(tenant.Id, adminRole.Id, agentTypeReadPermission.Id, cancellationToken);
        await EnsureRolePermissionAsync(tenant.Id, adminRole.Id, agentTypeCreatePermission.Id, cancellationToken);
        await EnsureRolePermissionAsync(tenant.Id, adminRole.Id, agentTypeReadinessPermission.Id, cancellationToken);
        await EnsureRolePermissionAsync(tenant.Id, adminRole.Id, agentTypeAdminPermission.Id, cancellationToken);
        await EnsureRolePermissionAsync(tenant.Id, adminRole.Id, agentsReadPermission.Id, cancellationToken);
        await EnsureRolePermissionAsync(tenant.Id, adminRole.Id, agentsCreatePermission.Id, cancellationToken);
        await EnsureRolePermissionAsync(tenant.Id, adminRole.Id, agentsReadinessPermission.Id, cancellationToken);
        await EnsureRolePermissionAsync(tenant.Id, adminRole.Id, agentsAdminPermission.Id, cancellationToken);
        await EnsureRolePermissionAsync(tenant.Id, adminRole.Id, agentsTestPermission.Id, cancellationToken);
        await EnsureRolePermissionAsync(tenant.Id, adminRole.Id, agentsExecutePermission.Id, cancellationToken);
        await EnsureRolePermissionAsync(tenant.Id, adminRole.Id, workflowsReadPermission.Id, cancellationToken);
        await EnsureRolePermissionAsync(tenant.Id, adminRole.Id, workflowsCreatePermission.Id, cancellationToken);
        await EnsureRolePermissionAsync(tenant.Id, adminRole.Id, workflowsReadinessPermission.Id, cancellationToken);
        await EnsureRolePermissionAsync(tenant.Id, adminRole.Id, workflowsAdminPermission.Id, cancellationToken);
        await EnsureRolePermissionAsync(tenant.Id, adminRole.Id, workflowsPreviewPermission.Id, cancellationToken);
        await EnsureRolePermissionAsync(tenant.Id, adminRole.Id, workflowsExecutePermission.Id, cancellationToken);
        await EnsureRolePermissionAsync(tenant.Id, adminRole.Id, workflowRunsReadPermission.Id, cancellationToken);
        await EnsureRolePermissionAsync(tenant.Id, adminRole.Id, reviewTaskReadPermission.Id, cancellationToken);
        await EnsureRolePermissionAsync(tenant.Id, adminRole.Id, reviewTaskCreatePermission.Id, cancellationToken);
        await EnsureRolePermissionAsync(tenant.Id, adminRole.Id, reviewTaskAssignPermission.Id, cancellationToken);
        await EnsureRolePermissionAsync(tenant.Id, adminRole.Id, reviewTaskManagePermission.Id, cancellationToken);
        await EnsureRolePermissionAsync(tenant.Id, adminRole.Id, reviewTaskAdminPermission.Id, cancellationToken);
        await EnsureRolePermissionAsync(tenant.Id, adminRole.Id, reviewTaskTemplateReadPermission.Id, cancellationToken);
        await EnsureRolePermissionAsync(tenant.Id, adminRole.Id, reviewTaskTemplateCreatePermission.Id, cancellationToken);
        await EnsureRolePermissionAsync(tenant.Id, adminRole.Id, reviewTaskTemplateReadinessPermission.Id, cancellationToken);
        await EnsureRolePermissionAsync(tenant.Id, adminRole.Id, reviewTaskTemplateAdminPermission.Id, cancellationToken);
        await EnsureRolePermissionAsync(tenant.Id, adminRole.Id, decisionReadPermission.Id, cancellationToken);
        await EnsureRolePermissionAsync(tenant.Id, adminRole.Id, decisionVotePermission.Id, cancellationToken);
        await EnsureRolePermissionAsync(tenant.Id, adminRole.Id, decisionManagePermission.Id, cancellationToken);
        await EnsureRolePermissionAsync(tenant.Id, adminRole.Id, decisionAdminPermission.Id, cancellationToken);
        await EnsureRolePermissionAsync(tenant.Id, adminRole.Id, outcomeReadPermission.Id, cancellationToken);
        await EnsureRolePermissionAsync(tenant.Id, adminRole.Id, outcomeRecordPermission.Id, cancellationToken);
        await EnsureRolePermissionAsync(tenant.Id, adminRole.Id, outcomeAdminPermission.Id, cancellationToken);
        await EnsureRolePermissionAsync(tenant.Id, adminRole.Id, learningReadPermission.Id, cancellationToken);
        await EnsureRolePermissionAsync(tenant.Id, adminRole.Id, learningAdminPermission.Id, cancellationToken);
        await EnsureRolePermissionAsync(tenant.Id, adminRole.Id, digitalThreadReadPermission.Id, cancellationToken);
        await EnsureRolePermissionAsync(tenant.Id, adminRole.Id, digitalThreadAdminPermission.Id, cancellationToken);
        await EnsureRolePermissionAsync(tenant.Id, adminRole.Id, governanceAnalyticsReadPermission.Id, cancellationToken);

        await EnsureMembershipAsync(tenant.Id, chatRunner.Id, chatRunnerRole.Id, cancellationToken);
        await EnsureRolePermissionAsync(tenant.Id, chatRunnerRole.Id, governedChatRunPermission.Id, cancellationToken);
        await EnsureRolePermissionAsync(tenant.Id, chatRunnerRole.Id, governedQueryRunPermission.Id, cancellationToken);
        await EnsureRolePermissionAsync(tenant.Id, chatRunnerRole.Id, governedQueryReadPermission.Id, cancellationToken);
        await EnsureRolePermissionAsync(tenant.Id, chatRunnerRole.Id, aiTraceReadPermission.Id, cancellationToken);
        await EnsureRolePermissionAsync(tenant.Id, chatRunnerRole.Id, dashboardReportPreviewPermission.Id, cancellationToken);
        await EnsureRolePermissionAsync(tenant.Id, chatRunnerRole.Id, dashboardReportReadinessPermission.Id, cancellationToken);
        await EnsureRolePermissionAsync(tenant.Id, chatRunnerRole.Id, recommendationReadPermission.Id, cancellationToken);
        await EnsureRolePermissionAsync(tenant.Id, chatRunnerRole.Id, recommendationCreatePermission.Id, cancellationToken);

        await EnsureAnalysisAgentTypeDefinitionAsync(tenant, admin, cancellationToken);
        var outcomeSeed = await OutcomeTaxonomyDevelopmentSeeder.SeedPublishedTaxonomyAsync(dbContext, tenant.Id, admin.Id, cancellationToken);
        await ReviewTaskDevelopmentTemplateSeeder.SeedPublishedTemplatesAsync(
            dbContext,
            tenant.Id,
            admin.Id,
            outcomeSeed?.TaxonomyVersionId,
            cancellationToken);
        await LearningDevelopmentSeeder.SeedPlaceholderArtifactsAsync(dbContext, tenant.Id, admin.Id, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        await EnsureBootstrapAuditAsync(tenant.Id, admin.Id, tenant.Identifier, cancellationToken);

        logger.LogInformation(
            "Seeded development identity admin {AdminEmail} for tenant {TenantIdentifier}.",
            seedOptions.AdminEmail,
            seedOptions.TenantIdentifier);
    }

    private async Task EnsureAnalysisAgentTypeDefinitionAsync(
        Tenant tenant,
        EtosUser admin,
        CancellationToken cancellationToken)
    {
        var normalizedType = AgentTypeDefinitionArtifactTypes.AgentTypeDefinition.ToUpperInvariant();
        var exists = await dbContext.Artifacts.AnyAsync(
            artifact => artifact.TenantId == tenant.Id && artifact.NormalizedArtifactType == normalizedType,
            cancellationToken);

        if (exists)
        {
            return;
        }

        var payload = AgentTypeDefinitionPayloadParser.Create(
            "analysis-agent",
            "Governed analysis and investigation agents for local development.",
            ["object-360-context", "bom-impact-context"],
            "investigator",
            ToolRiskLevels.Medium);

        var artifact = new Artifact
        {
            Id = Guid.NewGuid(),
            TenantId = tenant.Id,
            ArtifactType = AgentTypeDefinitionArtifactTypes.AgentTypeDefinition,
            NormalizedArtifactType = normalizedType,
            Name = "Analysis Agent Type",
            Description = "Development seed agent type catalog entry.",
            OwnerUserId = admin.Id,
            LifecycleState = ArtifactLifecycleState.Active,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        var version = new ArtifactVersion
        {
            Id = Guid.NewGuid(),
            TenantId = tenant.Id,
            ArtifactId = artifact.Id,
            VersionLabel = "1.0.0",
            NormalizedVersionLabel = "1.0.0",
            Summary = "Development seed analysis agent type.",
            PayloadJson = AgentTypeDefinitionPayloadParser.Serialize(payload),
            ReadinessState = ArtifactReadinessState.Published,
            CompatibilityStatus = ArtifactCompatibilityStatus.Compatible,
            CompatibilitySummary = "Development seed publish.",
            PolicyRiskStatus = ArtifactPolicyRiskStatus.Acceptable,
            CreatedByUserId = admin.Id,
            CreatedAt = DateTimeOffset.UtcNow,
            PublishedByUserId = admin.Id,
            PublishedAt = DateTimeOffset.UtcNow,
            PublishSummary = "Development seed publish."
        };

        dbContext.Artifacts.Add(artifact);
        dbContext.ArtifactVersions.Add(version);
    }

    private async Task EnsureBootstrapAuditAsync(
        Guid tenantId,
        Guid userId,
        string tenantIdentifier,
        CancellationToken cancellationToken)
    {
        var bootstrapAction = "development.seed.completed";
        var exists = await dbContext.AuditRecords.AnyAsync(
            record => record.TenantId == tenantId && record.Action == bootstrapAction,
            cancellationToken);

        if (exists)
        {
            return;
        }

        await auditRecorder.RecordAsync(
            new AuditRecordWriteRequest(
                tenantId,
                userId,
                bootstrapAction,
                AuditResult.Success,
                null,
                $"Development identity seed verified for tenant '{tenantIdentifier}'.",
                SourceObjectType: nameof(Tenant),
                SourceObjectId: tenantId.ToString(),
                RetentionCategory: AuditRetentionCategory.Operational),
            cancellationToken);
    }

    private async Task<EtosUser> EnsureAdminUserAsync(SeedIdentityOptions seedOptions, CancellationToken cancellationToken)
    {
        var normalizedEmail = seedOptions.AdminEmail.Trim().ToUpperInvariant();
        var existing = await dbContext.Users.SingleOrDefaultAsync(user => user.NormalizedEmail == normalizedEmail, cancellationToken);
        if (existing is not null)
        {
            return existing;
        }

        var admin = new EtosUser
        {
            Id = seedOptions.AdminUserId,
            UserName = seedOptions.AdminEmail.Trim(),
            Email = seedOptions.AdminEmail.Trim(),
            DisplayName = "ETOS Local Admin",
            EmailConfirmed = true,
            CreatedAt = DateTimeOffset.UtcNow
        };

        var result = await userManager.CreateAsync(admin, seedOptions.AdminPassword);
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(
                $"Development admin seed failed: {string.Join("; ", result.Errors.Select(error => error.Description))}");
        }

        return admin;
    }

    private async Task<Tenant> EnsureTenantAsync(SeedIdentityOptions seedOptions, CancellationToken cancellationToken)
    {
        var normalizedIdentifier = Normalize(seedOptions.TenantIdentifier);
        var existing = await dbContext.Tenants.SingleOrDefaultAsync(tenant => tenant.NormalizedIdentifier == normalizedIdentifier, cancellationToken);
        if (existing is not null)
        {
            return existing;
        }

        var tenant = new Tenant
        {
            Id = seedOptions.TenantId,
            Identifier = seedOptions.TenantIdentifier.Trim(),
            NormalizedIdentifier = normalizedIdentifier,
            Name = seedOptions.TenantName.Trim(),
            Description = "Development seed tenant.",
            CreatedAt = DateTimeOffset.UtcNow
        };

        dbContext.Tenants.Add(tenant);
        return tenant;
    }

    private async Task<Permission> EnsurePermissionAsync(string key, string description, CancellationToken cancellationToken)
    {
        var normalizedKey = Normalize(key);
        var existing = await dbContext.Permissions.SingleOrDefaultAsync(permission => permission.NormalizedKey == normalizedKey, cancellationToken);
        if (existing is not null)
        {
            return existing;
        }

        var permission = new Permission
        {
            Id = Guid.NewGuid(),
            Key = key,
            NormalizedKey = normalizedKey,
            Description = description,
            CreatedAt = DateTimeOffset.UtcNow
        };

        dbContext.Permissions.Add(permission);
        return permission;
    }

    private async Task<TenantRole> EnsureChatRunnerRoleAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var normalizedRoleName = Normalize("Chat Runner");
        var existing = await dbContext.TenantRoles.SingleOrDefaultAsync(
            role => role.TenantId == tenantId && role.NormalizedName == normalizedRoleName,
            cancellationToken);

        if (existing is not null)
        {
            return existing;
        }

        var role = new TenantRole
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = "Chat Runner",
            NormalizedName = normalizedRoleName,
            Description = "Run governed chat without draft artifact permissions.",
            CreatedAt = DateTimeOffset.UtcNow
        };

        dbContext.TenantRoles.Add(role);
        return role;
    }

    private async Task<EtosUser> EnsureChatRunnerUserAsync(SeedIdentityOptions seedOptions, CancellationToken cancellationToken)
    {
        const string email = "chat-runner@example.test";
        var normalizedEmail = email.ToUpperInvariant();
        var existing = await dbContext.Users.SingleOrDefaultAsync(user => user.NormalizedEmail == normalizedEmail, cancellationToken);
        if (existing is not null)
        {
            return existing;
        }

        var user = new EtosUser
        {
            Id = Guid.NewGuid(),
            UserName = email,
            Email = email,
            DisplayName = "ETOS Chat Runner",
            EmailConfirmed = true,
            CreatedAt = DateTimeOffset.UtcNow
        };

        var result = await userManager.CreateAsync(user, seedOptions.AdminPassword);
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(
                $"Development chat runner seed failed: {string.Join("; ", result.Errors.Select(error => error.Description))}");
        }

        return user;
    }

    private async Task<TenantRole> EnsureTenantRoleAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var normalizedRoleName = Normalize("Tenant Admin");
        var existing = await dbContext.TenantRoles.SingleOrDefaultAsync(
            role => role.TenantId == tenantId && role.NormalizedName == normalizedRoleName,
            cancellationToken);

        if (existing is not null)
        {
            return existing;
        }

        var role = new TenantRole
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = "Tenant Admin",
            NormalizedName = normalizedRoleName,
            Description = "Default tenant administrator role.",
            CreatedAt = DateTimeOffset.UtcNow
        };

        dbContext.TenantRoles.Add(role);
        return role;
    }

    private async Task EnsureMembershipAsync(Guid tenantId, Guid userId, Guid tenantRoleId, CancellationToken cancellationToken)
    {
        var exists = await dbContext.TenantMemberships.AnyAsync(
            membership => membership.TenantId == tenantId
                && membership.UserId == userId
                && membership.TenantRoleId == tenantRoleId,
            cancellationToken);

        if (exists)
        {
            return;
        }

        dbContext.TenantMemberships.Add(new TenantMembership
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            UserId = userId,
            TenantRoleId = tenantRoleId,
            CreatedAt = DateTimeOffset.UtcNow
        });
    }

    private async Task EnsureRolePermissionAsync(Guid tenantId, Guid tenantRoleId, Guid permissionId, CancellationToken cancellationToken)
    {
        var exists = await dbContext.TenantRolePermissions.AnyAsync(
            rolePermission => rolePermission.TenantId == tenantId
                && rolePermission.TenantRoleId == tenantRoleId
                && rolePermission.PermissionId == permissionId,
            cancellationToken);

        if (exists)
        {
            return;
        }

        dbContext.TenantRolePermissions.Add(new TenantRolePermission
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            TenantRoleId = tenantRoleId,
            PermissionId = permissionId,
            CreatedAt = DateTimeOffset.UtcNow
        });
    }

    private static string Normalize(string value) => value.Trim().ToUpperInvariant();
}
