using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using ETOS.Backend.GraphMemory;
using ETOS.Backend.Identity;
using ETOS.Backend.IdentityResolution;
using ETOS.Backend.Imports;
using ETOS.Backend.Infrastructure.Persistence;
using ETOS.Backend.Ontology;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ETOS.Backend.Tests;

public sealed class IdentityResolutionTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task CandidateGenerationIsIdempotentAcrossSourceSystems()
    {
        var graphMemory = new RecordingGraphMemoryService();
        await using var application = CreateApplication(graphMemory);
        using var client = application.CreateClient();
        var context = await CreatePublishedModelContextAsync(client);
        await CreateStagedImportAsync(client, context, "demo-pdm", "partNumber,lifecycle,cost\nP-100,released,12.50\n");
        var erpBatch = await CreateStagedImportAsync(client, context, "demo-erp", "partNumber,lifecycle,cost\nP-100,released,12.50\n");

        var firstRun = await GenerateCandidatesAsync(client, context, erpBatch.Id);
        var secondRun = await GenerateCandidatesAsync(client, context, erpBatch.Id);

        Assert.Equal(1, firstRun.CreatedCount);
        Assert.Equal(0, secondRun.CreatedCount);
        Assert.Single(secondRun.Candidates);
        var candidate = secondRun.Candidates.Single();
        Assert.Equal(IdentityCandidateState.Provisional, candidate.State);
        Assert.Equal(TrustState.Provisional, candidate.TrustState);
        Assert.True(candidate.ExcludedFromTrustedRecommendations);
        Assert.Empty(graphMemory.CreatedRelationshipRequests);
    }

    [Fact]
    public async Task ApprovalCreatesGraphRelationshipDecisionLearningEvidenceAndTrustScore()
    {
        var graphMemory = new RecordingGraphMemoryService();
        await using var application = CreateApplication(graphMemory);
        using var client = application.CreateClient();
        var context = await CreatePublishedModelContextAsync(client);
        await CreateStagedImportAsync(client, context, "demo-pdm", "partNumber,lifecycle,cost\nP-100,released,12.50\n");
        var erpBatch = await CreateStagedImportAsync(client, context, "demo-erp", "partNumber,lifecycle,cost\nP-100,released,12.50\n");
        var generated = await GenerateCandidatesAsync(client, context, erpBatch.Id);

        var approved = await ReviewCandidateAsync(
            client,
            context,
            generated.Candidates.Single().Id,
            "approve",
            "Confirmed same source-owned part.");

        Assert.Equal(IdentityCandidateState.Approved, approved.State);
        Assert.Equal(TrustState.Trusted, approved.TrustState);
        Assert.False(approved.ExcludedFromTrustedRecommendations);
        Assert.NotNull(approved.GraphRelationshipId);
        var relationship = Assert.Single(graphMemory.CreatedRelationshipRequests);
        Assert.Equal("IDENTITY_LINK", relationship.RelationshipType);
        Assert.Equal(TrustState.Trusted, relationship.TrustState);

        await using var scope = application.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<EnterpriseThreadDbContext>();
        var decision = await dbContext.IdentityResolutionDecisions.SingleAsync();
        var evidence = await dbContext.IdentityLearningEvidence.SingleAsync();
        var trustScore = await dbContext.TrustScoreRecords.SingleAsync();

        Assert.Equal(IdentityDecisionType.Approved, decision.DecisionType);
        Assert.Equal(IdentityDecisionType.Approved, evidence.Outcome);
        Assert.Equal(TrustScoreEntityType.IdentityCandidate, trustScore.EntityType);
        Assert.Equal(TrustState.Trusted, trustScore.TrustState);
        Assert.True(trustScore.Score >= 0.97m);
    }

    [Fact]
    public async Task RejectionRecordsLearningEvidenceWithoutGraphRelationship()
    {
        var graphMemory = new RecordingGraphMemoryService();
        await using var application = CreateApplication(graphMemory);
        using var client = application.CreateClient();
        var context = await CreatePublishedModelContextAsync(client);
        await CreateStagedImportAsync(client, context, "demo-pdm", "partNumber,lifecycle,cost\nP-100,released,12.50\n");
        var erpBatch = await CreateStagedImportAsync(client, context, "demo-erp", "partNumber,lifecycle,cost\nP-100,released,12.50\n");
        var generated = await GenerateCandidatesAsync(client, context, erpBatch.Id);

        var rejected = await ReviewCandidateAsync(
            client,
            context,
            generated.Candidates.Single().Id,
            "reject",
            "ERP value refers to a planning placeholder.");

        Assert.Equal(IdentityCandidateState.Rejected, rejected.State);
        Assert.True(rejected.ExcludedFromTrustedRecommendations);
        Assert.Null(rejected.GraphRelationshipId);
        Assert.Empty(graphMemory.CreatedRelationshipRequests);

        await using var scope = application.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<EnterpriseThreadDbContext>();
        var evidence = await dbContext.IdentityLearningEvidence.SingleAsync();
        var trustScore = await dbContext.TrustScoreRecords.SingleAsync();

        Assert.Equal(IdentityDecisionType.Rejected, evidence.Outcome);
        Assert.Equal(TrustState.Unverified, trustScore.TrustState);
        Assert.True(trustScore.Score < rejected.ConfidenceScore);
    }

    [Fact]
    public async Task CompetingCandidateTargetsAreMarkedConflictedAndExcluded()
    {
        await using var application = CreateApplication();
        using var client = application.CreateClient();
        var context = await CreatePublishedModelContextAsync(client);
        await CreateStagedImportAsync(client, context, "demo-pdm", "partNumber,lifecycle,cost\nP-100,released,12.50\n");
        await CreateStagedImportAsync(client, context, "demo-plm", "partNumber,lifecycle,cost\nP-100,released,12.50\n");
        var erpBatch = await CreateStagedImportAsync(client, context, "demo-erp", "partNumber,lifecycle,cost\nP-100,released,12.50\n");

        var generated = await GenerateCandidatesAsync(client, context, erpBatch.Id);

        Assert.Equal(2, generated.CreatedCount);
        Assert.All(generated.Candidates, candidate =>
        {
            Assert.Equal(IdentityCandidateState.Conflicted, candidate.State);
            Assert.Equal(TrustState.Conflicted, candidate.TrustState);
            Assert.True(candidate.ExcludedFromTrustedRecommendations);
        });
    }

    [Fact]
    public async Task CrossTenantCandidateAccessIsDeniedAndAudited()
    {
        await using var application = CreateApplication();
        using var client = application.CreateClient();
        var context = await CreatePublishedModelContextAsync(client, "tenant-a", "admin-a@example.test");
        var otherContext = await CreatePublishedModelContextAsync(client, "tenant-b", "admin-b@example.test");
        var batch = await CreateStagedImportAsync(client, context, "demo-pdm", "partNumber,lifecycle,cost\nP-100,released,12.50\n");

        using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/admin/identity-resolution/batches/{batch.Id}/candidates");
        AddTenantHeaders(request, otherContext.TenantId, otherContext.UserId);
        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        await using var scope = application.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<EnterpriseThreadDbContext>();
        var denial = await dbContext.AuditRecords.SingleAsync(
            record => record.Action == "identity_resolution.candidates.list" && record.Result == ETOS.Backend.Governance.AuditResult.Denied);

        Assert.Equal(otherContext.TenantId, denial.TenantId);
        Assert.Equal("import_tenant_mismatch", denial.Reason);
    }

    private static WebApplicationFactory<Program> CreateApplication(RecordingGraphMemoryService? graphMemory = null)
    {
        var databaseName = Guid.NewGuid().ToString();
        var storageRoot = Path.Combine(Path.GetTempPath(), "etos-identity-resolution-tests", Guid.NewGuid().ToString("N"));

        return new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureAppConfiguration((_, configuration) =>
                {
                    configuration.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["ImportFileStorage:RootPath"] = storageRoot,
                        ["GraphMemory:Neo4j:BootstrapOnStartup"] = "false"
                    });
                });
                builder.ConfigureServices(services =>
                {
                    services.RemoveAll<DbContextOptions<EnterpriseThreadDbContext>>();
                    services.RemoveAll<IDbContextOptionsConfiguration<EnterpriseThreadDbContext>>();
                    services.AddDbContext<EnterpriseThreadDbContext>(options =>
                        options.UseInMemoryDatabase(databaseName));
                    services.RemoveAll<IGraphMemoryService>();
                    services.AddSingleton<IGraphMemoryService>(graphMemory ?? new RecordingGraphMemoryService());
                });
            });
    }

    private static async Task<TestContext> CreatePublishedModelContextAsync(
        HttpClient client,
        string tenantIdentifier = "tenant-a",
        string email = "admin@example.test")
    {
        var userId = Guid.NewGuid();
        await CreateUserAsync(client, userId, userId, email);
        var tenant = await CreateTenantAsync(client, userId, tenantIdentifier);
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var ontology = await CreateOntologyAsync(client, tenant.Id, userId, suffix);
        var semanticLayer = await CreateSemanticLayerAsync(client, tenant.Id, userId, ontology.Id, suffix);
        var lifecycle = await CreateLifecycleAsync(client, tenant.Id, userId, suffix);
        var attributeSchema = await CreateAttributeSchemaAsync(client, tenant.Id, userId, ontology.Id, suffix);

        await PublishAsync<OntologyVersionResponse>(client, tenant.Id, userId, $"/api/admin/ontology/versions/{ontology.Id}/publish");
        await PublishAsync<SemanticLayerVersionResponse>(client, tenant.Id, userId, $"/api/admin/ontology/semantic-layers/{semanticLayer.Id}/publish");
        await PublishAsync<LifecycleVocabularyVersionResponse>(client, tenant.Id, userId, $"/api/admin/ontology/lifecycle-vocabularies/{lifecycle.Id}/publish");
        await PublishAsync<AttributeSchemaVersionResponse>(client, tenant.Id, userId, $"/api/admin/ontology/attribute-schemas/{attributeSchema.Id}/publish");
        var modelPackage = await CreateModelPackageAsync(client, tenant.Id, userId, ontology.Id, semanticLayer.Id, lifecycle.Id, attributeSchema.Id, suffix);
        await PublishAsync<ModelPackageVersionResponse>(client, tenant.Id, userId, $"/api/admin/ontology/model-packages/{modelPackage.Id}/publish");

        return new TestContext(tenant.Id, userId);
    }

    private static async Task<ImportBatchResponse> CreateStagedImportAsync(
        HttpClient client,
        TestContext context,
        string sourceSystem,
        string csv)
    {
        var batch = await CreateImportBatchAsync(client, context, sourceSystem);
        await UploadCsvAsync(client, context, batch.Id, csv);
        var mapping = await CreateMappingAsync(client, context, batch.Id, ["released"]);
        await ApproveMappingAsync(client, context, mapping.Id);
        await StageBatchAsync(client, context, batch.Id);
        return batch;
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

    private static async Task<OntologyVersionResponse> CreateOntologyAsync(HttpClient client, Guid tenantId, Guid userId, string suffix)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/admin/ontology/versions")
        {
            Content = JsonContent.Create(new CreateOntologyVersionRequest(
                $"canonical-manufacturing-{suffix}",
                "1.0.0",
                "Canonical manufacturing ontology.",
                [new CreateObjectTypeDefinitionRequest("part", "Part", "Source-owned part.", """["partNumber"]""", "Part identity.")],
                [],
                []))
        };
        AddTenantHeaders(request, tenantId, userId);
        var response = await client.SendAsync(request);
        var ontology = await response.Content.ReadFromJsonAsync<OntologyVersionResponse>();
        Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        Assert.NotNull(ontology);
        return ontology;
    }

    private static async Task<SemanticLayerVersionResponse> CreateSemanticLayerAsync(HttpClient client, Guid tenantId, Guid userId, Guid ontologyVersionId, string suffix)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/admin/ontology/semantic-layers")
        {
            Content = JsonContent.Create(new CreateSemanticLayerVersionRequest(
                $"canonical-semantic-{suffix}",
                "1.0.0",
                "Canonical graph mappings.",
                ontologyVersionId,
                """{"part":"Part"}""",
                """{}"""))
        };
        AddTenantHeaders(request, tenantId, userId);
        var response = await client.SendAsync(request);
        var semanticLayer = await response.Content.ReadFromJsonAsync<SemanticLayerVersionResponse>();
        Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        Assert.NotNull(semanticLayer);
        return semanticLayer;
    }

    private static async Task<LifecycleVocabularyVersionResponse> CreateLifecycleAsync(HttpClient client, Guid tenantId, Guid userId, string suffix)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/admin/ontology/lifecycle-vocabularies")
        {
            Content = JsonContent.Create(new CreateLifecycleVocabularyVersionRequest(
                $"canonical-lifecycle-{suffix}",
                "1.0.0",
                "Canonical lifecycle.",
                [
                    new CreateLifecycleStateDefinitionRequest("draft", "Draft", "working", 10, false),
                    new CreateLifecycleStateDefinitionRequest("released", "Released", "released", 20, false)
                ],
                [new CreateLifecycleTransitionDefinitionRequest("draft", "released", true, "Release approval.")]))
        };
        AddTenantHeaders(request, tenantId, userId);
        var response = await client.SendAsync(request);
        var lifecycle = await response.Content.ReadFromJsonAsync<LifecycleVocabularyVersionResponse>();
        Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        Assert.NotNull(lifecycle);
        return lifecycle;
    }

    private static async Task<AttributeSchemaVersionResponse> CreateAttributeSchemaAsync(HttpClient client, Guid tenantId, Guid userId, Guid ontologyVersionId, string suffix)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/admin/ontology/attribute-schemas")
        {
            Content = JsonContent.Create(new CreateAttributeSchemaVersionRequest(
                $"canonical-attributes-{suffix}",
                "1.0.0",
                "Canonical attributes.",
                ontologyVersionId,
                [
                    new CreateAttributeDefinitionRequest("partNumber", "part", AttributeValueType.Text, true, """{"maxLength":80}""", AttributeVisibility.Internal, null, true, true, "internal", "Part Number", "Part number identity."),
                    new CreateAttributeDefinitionRequest("cost", "part", AttributeValueType.Number, false, """{"minimum":0}""", AttributeVisibility.Internal, null, false, false, "internal", "Cost", "Part cost.")
                ]))
        };
        AddTenantHeaders(request, tenantId, userId);
        var response = await client.SendAsync(request);
        var attributeSchema = await response.Content.ReadFromJsonAsync<AttributeSchemaVersionResponse>();
        Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        Assert.NotNull(attributeSchema);
        return attributeSchema;
    }

    private static async Task<ModelPackageVersionResponse> CreateModelPackageAsync(
        HttpClient client,
        Guid tenantId,
        Guid userId,
        Guid ontologyVersionId,
        Guid semanticLayerVersionId,
        Guid lifecycleVocabularyVersionId,
        Guid attributeSchemaVersionId,
        string suffix)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/admin/ontology/model-packages")
        {
            Content = JsonContent.Create(new CreateModelPackageVersionRequest(
                $"canonical-package-{suffix}",
                "Canonical Package",
                "1.0.0",
                "Canonical model package.",
                ontologyVersionId,
                semanticLayerVersionId,
                lifecycleVocabularyVersionId,
                attributeSchemaVersionId))
        };
        AddTenantHeaders(request, tenantId, userId);
        var response = await client.SendAsync(request);
        var package = await response.Content.ReadFromJsonAsync<ModelPackageVersionResponse>();
        Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        Assert.NotNull(package);
        return package;
    }

    private static async Task<TResponse> PublishAsync<TResponse>(HttpClient client, Guid tenantId, Guid userId, string path)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, path)
        {
            Content = JsonContent.Create(new PublishOntologyVersionRequest("Published by test."))
        };
        AddTenantHeaders(request, tenantId, userId);
        var response = await client.SendAsync(request);
        var published = await response.Content.ReadFromJsonAsync<TResponse>();
        Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        Assert.NotNull(published);
        return published;
    }

    private static async Task<ImportBatchResponse> CreateImportBatchAsync(HttpClient client, TestContext context, string sourceSystem)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/admin/imports/batches")
        {
            Content = JsonContent.Create(new CreateImportBatchRequest(sourceSystem, "Demo import batch.", null))
        };
        AddTenantHeaders(request, context.TenantId, context.UserId);
        var response = await client.SendAsync(request);
        var batch = await response.Content.ReadFromJsonAsync<ImportBatchResponse>();
        Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        Assert.NotNull(batch);
        return batch;
    }

    private static async Task UploadCsvAsync(HttpClient client, TestContext context, Guid batchId, string csv)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/admin/imports/batches/{batchId}/files");
        AddTenantHeaders(request, context.TenantId, context.UserId);
        using var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(Encoding.UTF8.GetBytes(csv));
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("text/csv");
        content.Add(fileContent, "file", "import.csv");
        request.Content = content;

        var response = await client.SendAsync(request);
        Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
    }

    private static async Task<ImportMappingVersionResponse> CreateMappingAsync(
        HttpClient client,
        TestContext context,
        Guid batchId,
        IReadOnlyCollection<string> lifecycleValues)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/admin/imports/mappings")
        {
            Content = JsonContent.Create(new CreateImportMappingVersionRequest(
                batchId,
                $"1.0.0-{Guid.NewGuid():N}",
                "Test mapping.",
                [
                    new CreateImportColumnMappingRequest("partNumber", "part", "partNumber", true, true),
                    new CreateImportColumnMappingRequest("cost", "part", "cost", false, false)
                ],
                lifecycleValues.Select(value => new CreateImportLifecycleMappingRequest(value, "released")).ToList()))
        };
        AddTenantHeaders(request, context.TenantId, context.UserId);
        var response = await client.SendAsync(request);
        var mapping = await response.Content.ReadFromJsonAsync<ImportMappingVersionResponse>();
        Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        Assert.NotNull(mapping);
        return mapping;
    }

    private static async Task ApproveMappingAsync(HttpClient client, TestContext context, Guid mappingId)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/admin/imports/mappings/{mappingId}/approve")
        {
            Content = JsonContent.Create(new ApproveImportMappingRequest("Approved by test."))
        };
        AddTenantHeaders(request, context.TenantId, context.UserId);
        var response = await client.SendAsync(request);
        Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
    }

    private static async Task StageBatchAsync(HttpClient client, TestContext context, Guid batchId)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/admin/imports/batches/{batchId}/stage")
        {
            Content = JsonContent.Create(new { })
        };
        AddTenantHeaders(request, context.TenantId, context.UserId);
        var response = await client.SendAsync(request);
        Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
    }

    private static async Task<IdentityCandidateGenerationResponse> GenerateCandidatesAsync(HttpClient client, TestContext context, Guid batchId)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/admin/identity-resolution/batches/{batchId}/candidates/generate")
        {
            Content = JsonContent.Create(new GenerateIdentityCandidatesRequest(null))
        };
        AddTenantHeaders(request, context.TenantId, context.UserId);
        var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.OK, body);
        var generated = JsonSerializer.Deserialize<IdentityCandidateGenerationResponse>(body, JsonOptions);
        Assert.NotNull(generated);
        return generated;
    }

    private static async Task<IdentityCandidateLinkResponse> ReviewCandidateAsync(
        HttpClient client,
        TestContext context,
        Guid candidateId,
        string action,
        string rationale)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/admin/identity-resolution/candidates/{candidateId}/{action}")
        {
            Content = JsonContent.Create(new IdentityReviewDecisionRequest(rationale))
        };
        AddTenantHeaders(request, context.TenantId, context.UserId);
        var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.OK, body);
        var candidate = JsonSerializer.Deserialize<IdentityCandidateLinkResponse>(body, JsonOptions);
        Assert.NotNull(candidate);
        return candidate;
    }

    private static void AddTenantHeaders(HttpRequestMessage request, Guid tenantId, Guid userId)
    {
        request.Headers.Add(TenantHeaderNames.UserId, userId.ToString());
        request.Headers.Add(TenantHeaderNames.TenantId, tenantId.ToString());
    }

    private sealed record TestContext(Guid TenantId, Guid UserId);

    private sealed class RecordingGraphMemoryService : IGraphMemoryService
    {
        public List<CreateGraphNodeRequest> CreatedNodeRequests { get; } = [];
        public List<CreateGraphRelationshipRequest> CreatedRelationshipRequests { get; } = [];
        public List<BaseNode> Nodes { get; } = [];
        public List<BaseRelationship> Relationships { get; } = [];

        public Task<BaseNode> CreateNodeAsync(CreateGraphNodeRequest request, CancellationToken cancellationToken)
        {
            CreatedNodeRequests.Add(request);
            var now = DateTimeOffset.UtcNow;
            var node = new BaseNode(
                Guid.NewGuid(),
                request.TenantId,
                request.GraphSpace,
                request.ObjectType,
                request.TrustState,
                request.Attributes ?? new Dictionary<string, string?>(),
                request.SourceReference,
                now,
                now);
            Nodes.Add(node);
            return Task.FromResult(node);
        }

        public Task<BaseNode?> GetNodeAsync(Guid tenantId, Guid nodeId, CancellationToken cancellationToken)
        {
            return Task.FromResult(Nodes.SingleOrDefault(node => node.TenantId == tenantId && node.NodeId == nodeId));
        }

        public Task<BaseNode> UpdateNodeAsync(UpdateGraphNodeRequest request, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<BaseRelationship> CreateRelationshipAsync(CreateGraphRelationshipRequest request, CancellationToken cancellationToken)
        {
            CreatedRelationshipRequests.Add(request);
            var now = DateTimeOffset.UtcNow;
            var relationship = new BaseRelationship(
                Guid.NewGuid(),
                request.TenantId,
                request.FromNodeId,
                request.ToNodeId,
                request.RelationshipType,
                request.TrustState,
                request.Attributes ?? new Dictionary<string, string?>(),
                request.SourceReference,
                now,
                now);
            Relationships.Add(relationship);
            return Task.FromResult(relationship);
        }

        public Task<GraphTraversalResult> TraverseAsync(TraverseGraphRequest request, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<GraphReadModel> ListGraphAsync(
            Guid tenantId,
            GraphSpace? graphSpace,
            string? sourceBatchId,
            IReadOnlyCollection<Guid>? nodeIds,
            IReadOnlyCollection<Guid>? relationshipIds,
            CancellationToken cancellationToken)
        {
            var nodes = Nodes
                .Where(node => node.TenantId == tenantId
                    && (graphSpace is null || node.GraphSpace == graphSpace)
                    && (sourceBatchId is null || node.SourceReference?.SourceBatchId == sourceBatchId)
                    && (nodeIds is null || nodeIds.Count == 0 || nodeIds.Contains(node.NodeId)))
                .ToList();
            var relationships = Relationships
                .Where(relationship => relationship.TenantId == tenantId
                    && (sourceBatchId is null || relationship.SourceReference?.SourceBatchId == sourceBatchId)
                    && (relationshipIds is null || relationshipIds.Count == 0 || relationshipIds.Contains(relationship.RelationshipId)))
                .ToList();
            return Task.FromResult(new GraphReadModel(nodes, relationships));
        }

        public Task<GraphPromotionCopyResult> PromoteStagingAsync(
            Guid tenantId,
            IReadOnlyCollection<Guid> stagingNodeIds,
            IReadOnlyCollection<Guid> stagingRelationshipIds,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(new GraphPromotionCopyResult([], []));
        }
    }
}
