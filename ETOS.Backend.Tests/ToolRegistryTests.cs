using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ETOS.Backend.Artifacts;
using ETOS.Backend.Capabilities;
using ETOS.Backend.GraphMemory;
using ETOS.Backend.Identity;
using ETOS.Backend.Infrastructure.Persistence;
using ETOS.Backend.ToolRegistry;
using ETOS.Backend.Tests.Fixtures;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ETOS.Backend.Tests;

public sealed class ToolRegistryTests
{
    private const string ValidInputSchema =
        """{"type":"object","required":["intentKey","queryText"],"properties":{"intentKey":{"type":"string"},"queryText":{"type":"string"}}}""";

    private const string ValidOutputSchema =
        """{"type":"object","required":["retrievalRunId","safeSummary","status"],"properties":{"retrievalRunId":{"type":"string"},"safeSummary":{"type":"string"},"status":{"type":"string"}}}""";

    [Fact]
    public async Task PublishBlockedOnInvalidJsonSchema()
    {
        await using var application = CreateApplication();
        using var client = application.CreateClient();
        var packageContext = await ManufacturingModelPackageFixture.CreatePublishedPackageAsync(client);
        var created = await CreateToolAsync(
            client,
            packageContext.TenantId,
            packageContext.UserId,
            packageContext.ModelPackage.Id,
            capabilityVersionId: null,
            connectorVersionId: null,
            inputSchemaJson: "{not-a-valid-schema",
            outputSchemaJson: ValidOutputSchema);

        var scan = await CompatibilityScanAsync(
            client,
            packageContext.TenantId,
            packageContext.UserId,
            created.ArtifactId,
            created.VersionId);

        Assert.False(scan.IsCompatible);
        Assert.Contains(scan.BlockingNotes, note => note.Contains("Schema", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task PublishBlockedWhenWriteToolLacksDisabledConnector()
    {
        await using var application = CreateApplication();
        using var client = application.CreateClient();
        var packageContext = await ManufacturingModelPackageFixture.CreatePublishedPackageAsync(client);
        var created = await CreateToolAsync(
            client,
            packageContext.TenantId,
            packageContext.UserId,
            packageContext.ModelPackage.Id,
            capabilityVersionId: null,
            connectorVersionId: null,
            writesExternalSystem: true,
            callsExternalSystem: true,
            internalHandlerKey: ToolInternalHandlerKeys.DisabledWriteConnector);

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"/api/admin/tools/{created.ArtifactId}/versions/{created.VersionId}/mark-ready");
        AddTenantHeaders(request, packageContext.TenantId, packageContext.UserId);

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("connectorDefinitionVersionId", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task MarkReadyBlockedOnUnpublishedCapabilityRef()
    {
        await using var application = CreateApplication();
        using var client = application.CreateClient();
        var packageContext = await ManufacturingModelPackageFixture.CreatePublishedPackageAsync(client);
        var draftCapability = await CreateDraftCapabilityAsync(
            client,
            packageContext.TenantId,
            packageContext.UserId,
            packageContext.ModelPackage.Id);
        var created = await CreateToolAsync(
            client,
            packageContext.TenantId,
            packageContext.UserId,
            packageContext.ModelPackage.Id,
            capabilityVersionId: draftCapability.VersionId,
            connectorVersionId: null);

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"/api/admin/tools/{created.ArtifactId}/versions/{created.VersionId}/mark-ready");
        AddTenantHeaders(request, packageContext.TenantId, packageContext.UserId);

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("must be published", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task DryRunSucceedsWithoutCreatingRetrievalRun()
    {
        await using var application = CreateApplication();
        using var client = application.CreateClient();
        var packageContext = await ManufacturingModelPackageFixture.CreatePublishedPackageAsync(client);
        var graphQueryTool = await ResolvePublishedToolAsync(application, packageContext.TenantId, "graph-query-tool");

        await using var scope = application.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<EnterpriseThreadDbContext>();
        var retrievalRunsBefore = await dbContext.RetrievalRuns.CountAsync();

        var dryRun = await DryRunAsync(
            client,
            packageContext.TenantId,
            packageContext.UserId,
            graphQueryTool.ArtifactId,
            graphQueryTool.VersionId,
            """{"intentKey":"bom-impact-context","queryText":"Dry-run governed query."}""");

        var retrievalRunsAfter = await dbContext.RetrievalRuns.CountAsync();

        Assert.Equal(ToolRunStatuses.DryRunSucceeded, dryRun.Status);
        Assert.Equal(retrievalRunsBefore, retrievalRunsAfter);
    }

    [Fact]
    public async Task ExecuteGovernedQueryToolCreatesToolRunAndAudit()
    {
        var graphMemory = new RecordingGraphMemoryService();
        await using var application = CreateApplication(graphMemory);
        using var client = application.CreateClient();
        var packageContext = await ManufacturingModelPackageFixture.CreatePublishedPackageAsync(client);
        var graphQueryTool = await ResolvePublishedToolAsync(application, packageContext.TenantId, "graph-query-tool");

        var execution = await ExecuteAsync(
            client,
            packageContext.TenantId,
            packageContext.UserId,
            graphQueryTool.ArtifactId,
            graphQueryTool.VersionId,
            $$"""{"intentKey":"bom-impact-context","queryText":"Execute governed query tool.","startGraphNodeId":"{{Guid.NewGuid()}}"}""");

        Assert.Equal(ToolRunStatuses.Succeeded, execution.Status);
        Assert.NotNull(execution.AuditRecordId);

        await using var scope = application.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<EnterpriseThreadDbContext>();
        var toolRun = await dbContext.ToolRuns.SingleAsync(item => item.Id == execution.ToolRunId);
        var audit = await dbContext.AuditRecords.SingleAsync(item => item.Id == execution.AuditRecordId!.Value);

        Assert.Equal(packageContext.TenantId, toolRun.TenantId);
        Assert.False(toolRun.IsDryRun);
        Assert.Equal("tools.execute", audit.Action);
        Assert.NotNull(toolRun.RetrievalRunId);
    }

    [Fact]
    public async Task WriteConnectorToolExecutionBlocked()
    {
        await using var application = CreateApplication();
        using var client = application.CreateClient();
        var packageContext = await ManufacturingModelPackageFixture.CreatePublishedPackageAsync(client);
        var writeConnector = await ResolvePublishedConnectorAsync(application, packageContext.TenantId, "mock-erp-write-item");
        var created = await CreateToolAsync(
            client,
            packageContext.TenantId,
            packageContext.UserId,
            packageContext.ModelPackage.Id,
            capabilityVersionId: null,
            connectorVersionId: writeConnector.VersionId,
            writesExternalSystem: true,
            callsExternalSystem: true,
            internalHandlerKey: ToolInternalHandlerKeys.DisabledWriteConnector);
        await MarkReadyAsync(client, packageContext.TenantId, packageContext.UserId, created.ArtifactId, created.VersionId);
        await PublishAsync(client, packageContext.TenantId, packageContext.UserId, created.ArtifactId, created.VersionId);

        var execution = await ExecuteAsync(
            client,
            packageContext.TenantId,
            packageContext.UserId,
            created.ArtifactId,
            created.VersionId,
            """{"itemNumber":"PART-001"}""");

        Assert.Equal(ToolRunStatuses.Blocked, execution.Status);
        Assert.NotNull(execution.AuditRecordId);
    }

    [Fact]
    public async Task CrossTenantToolRunDenied()
    {
        await using var application = CreateApplication();
        using var client = application.CreateClient();
        var ownerContext = await ManufacturingModelPackageFixture.CreatePublishedPackageAsync(client, "tenant-tool-a", "owner@example.test");
        var graphQueryTool = await ResolvePublishedToolAsync(application, ownerContext.TenantId, "graph-query-tool");

        var otherUserId = Guid.NewGuid();
        await CreateUserAsync(client, otherUserId, otherUserId, "other@example.test");
        var otherTenant = await CreateTenantAsync(client, otherUserId, "tenant-tool-b");

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"/api/admin/tools/{graphQueryTool.ArtifactId}/versions/{graphQueryTool.VersionId}/dry-run")
        {
            Content = JsonContent.Create(new ToolExecutionRequest("""{"intentKey":"bom-impact-context","queryText":"cross tenant"}"""))
        };
        request.Headers.Add(TenantHeaderNames.UserId, otherUserId.ToString());
        request.Headers.Add(TenantHeaderNames.TenantId, otherTenant.Id.ToString());

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task ScopedCredentialResponseHasNoSecretFieldsInConnectorDryRunPath()
    {
        await using var application = CreateApplication();
        using var client = application.CreateClient();
        var packageContext = await ManufacturingModelPackageFixture.CreatePublishedPackageAsync(client);
        var readConnector = await ResolvePublishedConnectorAsync(application, packageContext.TenantId, "mock-erp-read");
        var created = await CreateToolAsync(
            client,
            packageContext.TenantId,
            packageContext.UserId,
            packageContext.ModelPackage.Id,
            capabilityVersionId: null,
            connectorVersionId: readConnector.VersionId,
            callsExternalSystem: true,
            internalHandlerKey: ToolInternalHandlerKeys.GovernedQuery);
        await MarkReadyAsync(client, packageContext.TenantId, packageContext.UserId, created.ArtifactId, created.VersionId);
        await PublishAsync(client, packageContext.TenantId, packageContext.UserId, created.ArtifactId, created.VersionId);

        var dryRun = await DryRunAsync(
            client,
            packageContext.TenantId,
            packageContext.UserId,
            created.ArtifactId,
            created.VersionId,
            """{"intentKey":"bom-impact-context","queryText":"Connector dry-run."}""");

        await using var scope = application.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<EnterpriseThreadDbContext>();
        var toolRun = await dbContext.ToolRuns.SingleAsync(item => item.Id == dryRun.ToolRunId);
        Assert.False(string.IsNullOrWhiteSpace(toolRun.ConnectorCredentialSafeSummaryJson));

        var credentialJson = toolRun.ConnectorCredentialSafeSummaryJson!;
        using var document = JsonDocument.Parse(credentialJson);
        foreach (var property in document.RootElement.EnumerateObject())
        {
            Assert.DoesNotContain("secret", property.Name, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("token", property.Name, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("password", property.Name, StringComparison.OrdinalIgnoreCase);
        }

        Assert.True(document.RootElement.TryGetProperty("credentialReferenceId", out _));
        Assert.True(document.RootElement.TryGetProperty("safeSummary", out _));
    }

    private static WebApplicationFactory<Program> CreateApplication(RecordingGraphMemoryService? graphMemory = null)
    {
        var databaseName = Guid.NewGuid().ToString();

        return new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Development");
                builder.ConfigureAppConfiguration((_, configuration) =>
                {
                    configuration.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["ReferencePackages:RootPath"] = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "packages")),
                        ["GraphMemory:Neo4j:BootstrapOnStartup"] = "false"
                    });
                });
                builder.ConfigureServices(services =>
                {
                    services.RemoveAll<DbContextOptions<EnterpriseThreadDbContext>>();
                    services.RemoveAll<IDbContextOptionsConfiguration<EnterpriseThreadDbContext>>();
                    services.AddDbContext<EnterpriseThreadDbContext>(options =>
                        options.UseInMemoryDatabase(databaseName));
                    if (graphMemory is not null)
                    {
                        services.RemoveAll<IGraphMemoryService>();
                        services.AddSingleton<IGraphMemoryService>(graphMemory);
                    }
                });
            });
    }

    private static async Task<CreateToolDefinitionResponse> CreateToolAsync(
        HttpClient client,
        Guid tenantId,
        Guid userId,
        Guid modelPackageVersionId,
        Guid? capabilityVersionId,
        Guid? connectorVersionId,
        string? inputSchemaJson = null,
        string? outputSchemaJson = null,
        bool writesExternalSystem = false,
        bool callsExternalSystem = false,
        string internalHandlerKey = ToolInternalHandlerKeys.GovernedQuery)
    {
        IReadOnlyCollection<Guid>? capabilityIds = capabilityVersionId is null ? null : [capabilityVersionId.Value];

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/admin/tools")
        {
            Content = JsonContent.Create(new CreateToolDefinitionRequest(
                "Test Tool",
                "Tool registry integration fixture.",
                $"test-tool-{Guid.NewGuid():N}"[..24],
                "retrieval",
                ToolRiskLevels.Medium,
                ReadOnly: !writesExternalSystem,
                CreatesPlatformArtifact: false,
                CreatesReviewTask: false,
                CreatesDecision: false,
                CallsExternalSystem: callsExternalSystem,
                WritesExternalSystem: writesExternalSystem,
                RequiresApproval: false,
                SupportsDryRun: true,
                RequiredPermissionKeys: null,
                inputSchemaJson ?? ValidInputSchema,
                outputSchemaJson ?? ValidOutputSchema,
                internalHandlerKey,
                ReferencedOutputSchemaVersionId: null,
                connectorVersionId,
                [modelPackageVersionId],
                CompatibleOntologyVersionIds: null,
                capabilityIds,
                ReferencedBusinessPolicyDefinitionVersionIds: null,
                AllowedQueryIntentKeys: ["bom-impact-context"],
                CompositionMetadata: null,
                FutureExtensionPlaceholders: null))
        };
        AddTenantHeaders(request, tenantId, userId);

        var response = await client.SendAsync(request);
        var created = await response.Content.ReadFromJsonAsync<CreateToolDefinitionResponse>();
        Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        Assert.NotNull(created);
        return created;
    }

    private static async Task<CreateCapabilityDefinitionResponse> CreateDraftCapabilityAsync(
        HttpClient client,
        Guid tenantId,
        Guid userId,
        Guid modelPackageVersionId)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/admin/capabilities")
        {
            Content = JsonContent.Create(new CreateCapabilityDefinitionRequest(
                "Draft Capability",
                "Draft capability for tool readiness test.",
                $"draft-cap-{Guid.NewGuid():N}"[..20],
                "structural_analysis",
                "Draft capability summary.",
                new Dictionary<string, string> { ["domain"] = "manufacturing" },
                [modelPackageVersionId],
                CompatibleOntologyVersionIds: null,
                ["bom-impact-context"],
                []))
        };
        AddTenantHeaders(request, tenantId, userId);

        var response = await client.SendAsync(request);
        var created = await response.Content.ReadFromJsonAsync<CreateCapabilityDefinitionResponse>();
        Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        Assert.NotNull(created);
        return created;
    }

    private static async Task<ToolCompatibilityScanResponse> CompatibilityScanAsync(
        HttpClient client,
        Guid tenantId,
        Guid userId,
        Guid artifactId,
        Guid versionId)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"/api/admin/tools/{artifactId}/versions/{versionId}/compatibility-scan");
        AddTenantHeaders(request, tenantId, userId);

        var response = await client.SendAsync(request);
        var scan = await response.Content.ReadFromJsonAsync<ToolCompatibilityScanResponse>();
        Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        Assert.NotNull(scan);
        return scan;
    }

    private static async Task<MarkToolDefinitionReadyResponse> MarkReadyAsync(
        HttpClient client,
        Guid tenantId,
        Guid userId,
        Guid artifactId,
        Guid versionId)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"/api/admin/tools/{artifactId}/versions/{versionId}/mark-ready");
        AddTenantHeaders(request, tenantId, userId);

        var response = await client.SendAsync(request);
        var ready = await response.Content.ReadFromJsonAsync<MarkToolDefinitionReadyResponse>();
        Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        Assert.NotNull(ready);
        return ready;
    }

    private static async Task<PublishToolDefinitionResponse> PublishAsync(
        HttpClient client,
        Guid tenantId,
        Guid userId,
        Guid artifactId,
        Guid versionId)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"/api/admin/tools/{artifactId}/versions/{versionId}/publish")
        {
            Content = JsonContent.Create(new PublishArtifactVersionRequest("Published by tool registry test."))
        };
        AddTenantHeaders(request, tenantId, userId);

        var response = await client.SendAsync(request);
        var publish = await response.Content.ReadFromJsonAsync<PublishToolDefinitionResponse>();
        Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        Assert.NotNull(publish);
        return publish;
    }

    private static async Task<ToolExecutionResponse> DryRunAsync(
        HttpClient client,
        Guid tenantId,
        Guid userId,
        Guid artifactId,
        Guid versionId,
        string inputJson)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"/api/admin/tools/{artifactId}/versions/{versionId}/dry-run")
        {
            Content = JsonContent.Create(new ToolExecutionRequest(inputJson))
        };
        AddTenantHeaders(request, tenantId, userId);

        var response = await client.SendAsync(request);
        var dryRun = await response.Content.ReadFromJsonAsync<ToolExecutionResponse>();
        Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        Assert.NotNull(dryRun);
        return dryRun;
    }

    private static async Task<ToolExecutionResponse> ExecuteAsync(
        HttpClient client,
        Guid tenantId,
        Guid userId,
        Guid artifactId,
        Guid versionId,
        string inputJson)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"/api/admin/tools/{artifactId}/versions/{versionId}/execute")
        {
            Content = JsonContent.Create(new ToolExecutionRequest(inputJson))
        };
        AddTenantHeaders(request, tenantId, userId);

        var response = await client.SendAsync(request);
        var execution = await response.Content.ReadFromJsonAsync<ToolExecutionResponse>();
        Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        Assert.NotNull(execution);
        return execution;
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

        throw new InvalidOperationException($"Published tool '{toolKey}' was not found for tenant '{tenantId}'.");
    }

    private static async Task<(Guid ArtifactId, Guid VersionId)> ResolvePublishedConnectorAsync(
        WebApplicationFactory<Program> application,
        Guid tenantId,
        string connectorKey)
    {
        await using var scope = application.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<EnterpriseThreadDbContext>();
        var versions = await dbContext.ArtifactVersions
            .AsNoTracking()
            .Include(item => item.Artifact)
            .Where(item => item.TenantId == tenantId
                && item.ReadinessState == ArtifactReadinessState.Published
                && item.Artifact!.ArtifactType == ConnectorDefinitionArtifactTypes.ConnectorDefinition)
            .ToListAsync();

        foreach (var version in versions)
        {
            var document = ConnectorDefinitionPayloadParser.Deserialize(version.PayloadJson ?? "{}");
            if (string.Equals(document.ConnectorKey, connectorKey, StringComparison.OrdinalIgnoreCase))
            {
                return (version.ArtifactId, version.Id);
            }
        }

        throw new InvalidOperationException($"Published connector '{connectorKey}' was not found for tenant '{tenantId}'.");
    }

    private static async Task CreateUserAsync(HttpClient client, Guid actorUserId, Guid userId, string email)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/admin/identity/users")
        {
            Content = JsonContent.Create(new CreateUserRequest(userId, email, email, email, "local-password"))
        };
        request.Headers.Add(TenantHeaderNames.UserId, actorUserId.ToString());
        var response = await client.SendAsync(request);
        Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
    }

    private static async Task<TenantResponse> CreateTenantAsync(HttpClient client, Guid actorUserId, string identifier)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/admin/identity/tenants")
        {
            Content = JsonContent.Create(new CreateTenantRequest(identifier, identifier, null))
        };
        request.Headers.Add(TenantHeaderNames.UserId, actorUserId.ToString());
        var response = await client.SendAsync(request);
        var tenant = await response.Content.ReadFromJsonAsync<TenantResponse>();
        Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        Assert.NotNull(tenant);
        return tenant;
    }

    private static void AddTenantHeaders(HttpRequestMessage request, Guid tenantId, Guid userId)
    {
        request.Headers.Add(TenantHeaderNames.UserId, userId.ToString());
        request.Headers.Add(TenantHeaderNames.TenantId, tenantId.ToString());
    }

    private sealed class RecordingGraphMemoryService : IGraphMemoryService
    {
        public Task<BaseNode> CreateNodeAsync(CreateGraphNodeRequest request, CancellationToken cancellationToken)
        {
            var now = DateTimeOffset.UtcNow;
            return Task.FromResult(new BaseNode(
                Guid.NewGuid(),
                request.TenantId,
                request.GraphSpace,
                request.ObjectType,
                request.TrustState,
                request.Attributes ?? new Dictionary<string, string?>(),
                request.SourceReference,
                now,
                now));
        }

        public Task<BaseNode?> GetNodeAsync(Guid tenantId, Guid nodeId, CancellationToken cancellationToken) =>
            Task.FromResult<BaseNode?>(null);

        public Task<BaseNode> UpdateNodeAsync(UpdateGraphNodeRequest request, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<BaseRelationship> CreateRelationshipAsync(CreateGraphRelationshipRequest request, CancellationToken cancellationToken)
        {
            var now = DateTimeOffset.UtcNow;
            return Task.FromResult(new BaseRelationship(
                Guid.NewGuid(),
                request.TenantId,
                request.FromNodeId,
                request.ToNodeId,
                request.RelationshipType,
                request.TrustState,
                request.Attributes ?? new Dictionary<string, string?>(),
                request.SourceReference,
                now,
                now));
        }

        public Task<GraphTraversalResult> TraverseAsync(TraverseGraphRequest request, CancellationToken cancellationToken)
        {
            var now = DateTimeOffset.UtcNow;
            var startNode = new BaseNode(
                request.StartNodeId,
                request.TenantId,
                request.GraphSpace ?? GraphSpace.Trusted,
                "part",
                TrustState.Trusted,
                new Dictionary<string, string?>(),
                null,
                now,
                now);
            return Task.FromResult(new GraphTraversalResult(startNode, [startNode], []));
        }

        public Task<GraphReadModel> ListGraphAsync(
            Guid tenantId,
            GraphSpace? graphSpace,
            string? sourceBatchId,
            IReadOnlyCollection<Guid>? nodeIds,
            IReadOnlyCollection<Guid>? relationshipIds,
            CancellationToken cancellationToken) =>
            Task.FromResult(new GraphReadModel([], []));

        public Task<GraphPromotionCopyResult> PromoteStagingAsync(
            Guid tenantId,
            IReadOnlyCollection<Guid> nodeIds,
            IReadOnlyCollection<Guid> relationshipIds,
            CancellationToken cancellationToken) =>
            Task.FromResult(new GraphPromotionCopyResult([], []));
    }
}
