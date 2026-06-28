using System.Net;
using System.Net.Http.Json;
using ETOS.Backend.AgentRuns;
using ETOS.Backend.AgentRuntime;
using ETOS.Backend.Agents;
using ETOS.Backend.Artifacts;
using ETOS.Backend.Identity;
using ETOS.Backend.Infrastructure.Persistence;
using ETOS.Backend.Tests.Fixtures;
using ETOS.Backend.ToolRegistry;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ETOS.Backend.Tests;

public sealed class AgentVersionTests
{
    [Fact]
    public async Task CreateFromManufacturingInvestigatorTemplate()
    {
        await using var application = AgentExecutionTestSupport.CreateApplication();
        using var client = application.CreateClient();
        var packageContext = await ManufacturingModelPackageFixture.CreatePublishedPackageAsync(client);
        var template = await AgentExecutionTestSupport.ResolvePublishedAgentTemplateAsync(
            application,
            packageContext.TenantId,
            "manufacturing-investigator");
        await AgentExecutionTestSupport.CreateAndPublishAnalysisAgentTypeAsync(
            client,
            packageContext.TenantId,
            packageContext.UserId);

        var created = await AgentExecutionTestSupport.CreateAgentFromTemplateAsync(
            client,
            packageContext.TenantId,
            packageContext.UserId,
            template.VersionId,
            "manufacturing-investigator-copy");

        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"/api/admin/agents/{created.ArtifactId}/versions/{created.VersionId}");
        AgentExecutionTestSupport.AddTenantHeaders(request, packageContext.TenantId, packageContext.UserId);

        var response = await client.SendAsync(request);
        var detail = await response.Content.ReadFromJsonAsync<AgentDefinitionDetailResponse>();

        Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        Assert.NotNull(detail);
        Assert.Equal("manufacturing-investigator-copy", detail.AgentKey);
        Assert.Equal(template.VersionId, detail.SourceAgentTemplateVersionId);
        Assert.Equal(AgentRuntimeAdapterKeys.PydanticAi, detail.PreferredRuntimeAdapterKey);
        Assert.NotEmpty(detail.ReferencedTools);
    }

    [Fact]
    public async Task MarkReadyBlockedWhenDeferredRuntimeAdapterSelected()
    {
        await using var application = AgentExecutionTestSupport.CreateApplication();
        using var client = application.CreateClient();
        var packageContext = await ManufacturingModelPackageFixture.CreatePublishedPackageAsync(client);
        var template = await AgentExecutionTestSupport.ResolvePublishedAgentTemplateAsync(
            application,
            packageContext.TenantId,
            "manufacturing-investigator");
        await AgentExecutionTestSupport.CreateAndPublishAnalysisAgentTypeAsync(
            client,
            packageContext.TenantId,
            packageContext.UserId);
        var created = await AgentExecutionTestSupport.CreateAgentFromTemplateAsync(
            client,
            packageContext.TenantId,
            packageContext.UserId,
            template.VersionId);

        await using var scope = application.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<EnterpriseThreadDbContext>();
        var version = await dbContext.ArtifactVersions.SingleAsync(item => item.Id == created.VersionId);
        var document = AgentDefinitionPayloadParser.Deserialize(version.PayloadJson ?? "{}");
        document.PreferredRuntimeAdapterKey = AgentRuntimeAdapterKeys.Hermes;
        version.PayloadJson = AgentDefinitionPayloadParser.Serialize(document);
        await dbContext.SaveChangesAsync();

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"/api/admin/agents/{created.ArtifactId}/versions/{created.VersionId}/mark-ready");
        AgentExecutionTestSupport.AddTenantHeaders(request, packageContext.TenantId, packageContext.UserId);

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("deferred", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task MarkReadyComputesDerivedRiskFromPinnedTools()
    {
        await using var application = AgentExecutionTestSupport.CreateApplication();
        using var client = application.CreateClient();
        var packageContext = await ManufacturingModelPackageFixture.CreatePublishedPackageAsync(client);
        var (agent, _) = await AgentExecutionTestSupport.PrepareDraftManufacturingAgentAsync(
            client,
            application,
            packageContext.TenantId,
            packageContext.UserId,
            "derived-risk-agent");

        var ready = await AgentExecutionTestSupport.MarkAgentReadyAsync(
            client,
            packageContext.TenantId,
            packageContext.UserId,
            agent.ArtifactId,
            agent.VersionId);

        Assert.NotNull(ready.DerivedCapabilityRisk);
        Assert.Equal(ToolRiskLevels.Medium, ready.DerivedCapabilityRisk.EffectiveRiskLevel);
        Assert.NotEmpty(ready.DerivedCapabilityRisk.ToolRiskContributions);
    }

    [Fact]
    public async Task DraftPreviewDeniedForNonCreatorWithoutTestPermission()
    {
        await using var application = AgentExecutionTestSupport.CreateApplication();
        using var client = application.CreateClient();
        var packageContext = await ManufacturingModelPackageFixture.CreatePublishedPackageAsync(client);
        var (agent, _) = await AgentExecutionTestSupport.PrepareDraftManufacturingAgentAsync(
            client,
            application,
            packageContext.TenantId,
            packageContext.UserId,
            "draft-permission-agent");

        var otherUserId = Guid.NewGuid();
        await AgentExecutionTestSupport.CreateUserAsync(client, packageContext.UserId, otherUserId, "reader@example.test");
        await AgentExecutionTestSupport.CreateGrantAsync(
            client,
            packageContext.TenantId,
            packageContext.UserId,
            otherUserId,
            AgentPermissions.Read);

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"/api/admin/agents/{agent.ArtifactId}/versions/{agent.VersionId}/preview")
        {
            Content = JsonContent.Create(new AgentExecutionRequest(null, "Unauthorized preview attempt.", Guid.NewGuid()))
        };
        AgentExecutionTestSupport.AddTenantHeaders(request, packageContext.TenantId, otherUserId);

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task PublishSucceedsAfterReady()
    {
        await using var application = AgentExecutionTestSupport.CreateApplication();
        using var client = application.CreateClient();
        var packageContext = await ManufacturingModelPackageFixture.CreatePublishedPackageAsync(client);
        var (agent, _) = await AgentExecutionTestSupport.PrepareDraftManufacturingAgentAsync(
            client,
            application,
            packageContext.TenantId,
            packageContext.UserId,
            "publish-agent");

        await AgentExecutionTestSupport.MarkAgentReadyAsync(
            client,
            packageContext.TenantId,
            packageContext.UserId,
            agent.ArtifactId,
            agent.VersionId);
        var publish = await AgentExecutionTestSupport.PublishAgentAsync(
            client,
            packageContext.TenantId,
            packageContext.UserId,
            agent.ArtifactId,
            agent.VersionId);

        Assert.True(publish.Succeeded);
        Assert.Equal(nameof(ArtifactReadinessState.Published), publish.ReadinessState);
    }

    [Fact]
    public async Task UpdateModelConfigOnPublishedAgentCreatesDraftVersion()
    {
        await using var application = AgentExecutionTestSupport.CreateApplication();
        using var client = application.CreateClient();
        var packageContext = await ManufacturingModelPackageFixture.CreatePublishedPackageAsync(client);
        var (agent, _) = await AgentExecutionTestSupport.PreparePublishedManufacturingAgentAsync(
            client,
            application,
            packageContext.TenantId,
            packageContext.UserId,
            "model-config-published-agent");

        var updated = await AgentExecutionTestSupport.UpdateAgentModelConfigAsync(
            client,
            packageContext.TenantId,
            packageContext.UserId,
            agent.ArtifactId,
            agent.VersionId,
            new UpdateAgentModelConfigRequest(
                "openai",
                "gpt-4o-mini",
                [
                    new AgentFallbackModelRequest(
                        "openai-compatible",
                        "local-model",
                        "local unavailable")
                ]));

        Assert.True(updated.CreatedNewVersion);
        Assert.Equal("1.0.1", updated.VersionLabel);
        Assert.Equal(nameof(ArtifactReadinessState.Draft), updated.ReadinessState);
        Assert.NotEqual(agent.VersionId, updated.VersionId);

        using var detailRequest = new HttpRequestMessage(
            HttpMethod.Get,
            $"/api/admin/agents/{agent.ArtifactId}/versions/{updated.VersionId}");
        AgentExecutionTestSupport.AddTenantHeaders(detailRequest, packageContext.TenantId, packageContext.UserId);
        var detailResponse = await client.SendAsync(detailRequest);
        var detail = await detailResponse.Content.ReadFromJsonAsync<AgentDefinitionDetailResponse>();

        Assert.True(detailResponse.StatusCode == HttpStatusCode.OK, await detailResponse.Content.ReadAsStringAsync());
        Assert.NotNull(detail);
        Assert.Equal("openai", detail.PrimaryModelProviderKey);
        Assert.Equal("gpt-4o-mini", detail.PrimaryModelId);
        Assert.Single(detail.FallbackModels);
        Assert.Equal("openai-compatible", detail.FallbackModels.First().ProviderKey);
    }

    [Fact]
    public async Task UpdateModelConfigOnDraftUpdatesInPlace()
    {
        await using var application = AgentExecutionTestSupport.CreateApplication();
        using var client = application.CreateClient();
        var packageContext = await ManufacturingModelPackageFixture.CreatePublishedPackageAsync(client);
        var (agent, _) = await AgentExecutionTestSupport.PrepareDraftManufacturingAgentAsync(
            client,
            application,
            packageContext.TenantId,
            packageContext.UserId,
            "model-config-draft-agent");

        var updated = await AgentExecutionTestSupport.UpdateAgentModelConfigAsync(
            client,
            packageContext.TenantId,
            packageContext.UserId,
            agent.ArtifactId,
            agent.VersionId,
            new UpdateAgentModelConfigRequest("openai-compatible", "local-model", []));

        Assert.False(updated.CreatedNewVersion);
        Assert.Equal(agent.VersionId, updated.VersionId);
        Assert.Equal("1.0.0", updated.VersionLabel);

        using var detailRequest = new HttpRequestMessage(
            HttpMethod.Get,
            $"/api/admin/agents/{agent.ArtifactId}/versions/{agent.VersionId}");
        AgentExecutionTestSupport.AddTenantHeaders(detailRequest, packageContext.TenantId, packageContext.UserId);
        var detail = await (await client.SendAsync(detailRequest)).Content.ReadFromJsonAsync<AgentDefinitionDetailResponse>();

        Assert.NotNull(detail);
        Assert.Equal("openai-compatible", detail.PrimaryModelProviderKey);
        Assert.Equal("local-model", detail.PrimaryModelId);
    }

    [Fact]
    public async Task UpdateModelConfigRejectsInvalidProvider()
    {
        await using var application = AgentExecutionTestSupport.CreateApplication();
        using var client = application.CreateClient();
        var packageContext = await ManufacturingModelPackageFixture.CreatePublishedPackageAsync(client);
        var (agent, _) = await AgentExecutionTestSupport.PrepareDraftManufacturingAgentAsync(
            client,
            application,
            packageContext.TenantId,
            packageContext.UserId,
            "model-config-invalid-provider");

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"/api/admin/agents/{agent.ArtifactId}/versions/{agent.VersionId}/model-config")
        {
            Content = JsonContent.Create(new UpdateAgentModelConfigRequest("unsupported-provider", "mock-v1", []))
        };
        AgentExecutionTestSupport.AddTenantHeaders(request, packageContext.TenantId, packageContext.UserId);

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("unsupported-provider", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task UpdateModelConfigThenMarkReadyAndPublishSucceeds()
    {
        await using var application = AgentExecutionTestSupport.CreateApplication();
        using var client = application.CreateClient();
        var packageContext = await ManufacturingModelPackageFixture.CreatePublishedPackageAsync(client);
        var (agent, _) = await AgentExecutionTestSupport.PreparePublishedManufacturingAgentAsync(
            client,
            application,
            packageContext.TenantId,
            packageContext.UserId,
            "model-config-publish-flow");

        var updated = await AgentExecutionTestSupport.UpdateAgentModelConfigAsync(
            client,
            packageContext.TenantId,
            packageContext.UserId,
            agent.ArtifactId,
            agent.VersionId,
            new UpdateAgentModelConfigRequest("openai", "gpt-4o-mini", []));

        await AgentExecutionTestSupport.MarkAgentReadyAsync(
            client,
            packageContext.TenantId,
            packageContext.UserId,
            updated.ArtifactId,
            updated.VersionId);
        var publish = await AgentExecutionTestSupport.PublishAgentAsync(
            client,
            packageContext.TenantId,
            packageContext.UserId,
            updated.ArtifactId,
            updated.VersionId);

        Assert.True(publish.Succeeded);
        Assert.Equal(nameof(ArtifactReadinessState.Published), publish.ReadinessState);
    }
}
