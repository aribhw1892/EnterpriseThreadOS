using System.Net;
using System.Net.Http.Json;
using ETOS.Backend.Artifacts;
using ETOS.Backend.BusinessPolicies;
using ETOS.Backend.Identity;
using ETOS.Backend.Infrastructure.Persistence;
using ETOS.Backend.ToolRegistry;
using ETOS.Backend.WorkflowRuns;
using ETOS.Backend.Workflows;
using ETOS.Backend.Tests.Fixtures;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ETOS.Backend.Tests;

public sealed class WorkflowSafeModeTests
{
    [Fact]
    public async Task PartialSafeModeSkipsFailedPolicyStepAndPersistsSafeModeEvent()
    {
        var graphMemory = new AgentExecutionTestSupport.RecordingGraphMemoryService();
        await using var application = WorkflowExecutionTestSupport.CreateApplication(graphMemory);
        using var client = application.CreateClient();
        var packageContext = await ManufacturingModelPackageFixture.CreatePublishedPackageAsync(client);
        var policy = await ResolvePublishedPolicyAsync(application, packageContext.TenantId, "min-maturity-85");

        var created = await WorkflowExecutionTestSupport.CreateWorkflowAsync(
            client,
            packageContext.TenantId,
            packageContext.UserId,
            new CreateWorkflowDefinitionRequest(
                "Safe Mode Policy Skip Workflow",
                "Workflow that skips failed policy checks in partial safe mode.",
                $"safe-mode-policy-{Guid.NewGuid():N}"[..24],
                "Safe Mode Policy Skip Workflow",
                null,
                WorkflowScopes.Tenant,
                [
                    new WorkflowStepDefinitionRequest(
                        "policy-check",
                        WorkflowStepTypes.BusinessPolicyCheck,
                        WorkflowStepSafeModeBehaviors.Skip,
                        null,
                        null,
                        null,
                        policy.VersionId,
                        null,
                        null,
                        null)
                ],
                null,
                null,
                null,
                null,
                [policy.VersionId],
                null,
                [packageContext.ModelPackage.Id],
                null,
                false,
                false,
                null,
                true,
                WorkflowStepSafeModeBehaviors.Skip,
                new WorkflowTriggerConfigRequest(true, false, null, false, null),
                null,
                null,
                null));

        await WorkflowExecutionTestSupport.MarkWorkflowReadyAsync(
            client,
            packageContext.TenantId,
            packageContext.UserId,
            created.ArtifactId,
            created.VersionId);

        var execution = await WorkflowExecutionTestSupport.PreviewWorkflowAsync(
            client,
            packageContext.TenantId,
            packageContext.UserId,
            created.ArtifactId,
            created.VersionId,
            """{"minMaturityPercent":"50"}""");

        await using var scope = application.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<EnterpriseThreadDbContext>();

        var safeModeEvents = await dbContext.SafeModeEvents
            .Where(item => item.WorkflowRunId == execution.WorkflowRunId)
            .ToListAsync();

        Assert.NotEmpty(safeModeEvents);
        Assert.Contains(safeModeEvents, item => item.EventKind == SafeModeEventKinds.Skipped);
        Assert.Contains(safeModeEvents, item => item.StepKey == "policy-check");

        using var getRequest = new HttpRequestMessage(HttpMethod.Get, $"/api/admin/workflow-runs/{execution.WorkflowRunId}");
        AgentExecutionTestSupport.AddTenantHeaders(getRequest, packageContext.TenantId, packageContext.UserId);
        var getResponse = await client.SendAsync(getRequest);
        var detail = await getResponse.Content.ReadFromJsonAsync<WorkflowRunDetailResponse>();

        Assert.True(getResponse.StatusCode == HttpStatusCode.OK);
        Assert.NotNull(detail);
        Assert.NotEmpty(detail.SafeModeEvents);
        Assert.True(detail.SafeModeApplied || detail.Status is WorkflowRunStatuses.SafeModeCompleted or WorkflowRunStatuses.PreviewSucceeded);
    }

    private static async Task<(Guid ArtifactId, Guid VersionId)> ResolvePublishedToolAsync(
        WebApplicationFactory<Program> application,
        Guid tenantId,
        string toolKey)
    {
        await using var scope = application.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<EnterpriseThreadDbContext>();
        var versions = await dbContext.ArtifactVersions
            .AsNoTracking()
            .Include(item => item.Artifact)
            .Where(item => item.TenantId == tenantId
                && item.ReadinessState == ArtifactReadinessState.Published
                && item.Artifact!.ArtifactType == ToolDefinitionArtifactTypes.ToolDefinition)
            .ToListAsync();

        foreach (var version in versions)
        {
            var document = ToolDefinitionPayloadParser.Deserialize(version.PayloadJson ?? "{}");
            if (string.Equals(document.ToolKey, toolKey, StringComparison.OrdinalIgnoreCase))
            {
                return (version.ArtifactId, version.Id);
            }
        }

        throw new InvalidOperationException($"Published tool '{toolKey}' was not found.");
    }

    private static async Task<(Guid ArtifactId, Guid VersionId)> ResolvePublishedPolicyAsync(
        WebApplicationFactory<Program> application,
        Guid tenantId,
        string policyKey)
    {
        await using var scope = application.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<EnterpriseThreadDbContext>();
        var versions = await dbContext.ArtifactVersions
            .AsNoTracking()
            .Include(item => item.Artifact)
            .Where(item => item.TenantId == tenantId
                && item.ReadinessState == ArtifactReadinessState.Published
                && item.Artifact!.ArtifactType == BusinessPolicyDefinitionArtifactTypes.BusinessPolicyDefinition)
            .ToListAsync();

        foreach (var version in versions)
        {
            var document = BusinessPolicyDefinitionPayloadParser.Deserialize(version.PayloadJson ?? "{}");
            if (string.Equals(document.PolicyKey, policyKey, StringComparison.OrdinalIgnoreCase))
            {
                return (version.ArtifactId, version.Id);
            }
        }

        throw new InvalidOperationException($"Published policy '{policyKey}' was not found.");
    }
}
