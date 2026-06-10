using System.Net;
using System.Net.Http.Json;
using ETOS.Backend.Governance;
using ETOS.Backend.Identity;
using ETOS.Backend.Infrastructure.Persistence;
using ETOS.Backend.Ontology;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ETOS.Backend.Tests;

public sealed class OntologyTests
{
    [Fact]
    public async Task ModelPackagePublishRequiresPublishedDependenciesAndBecomesActive()
    {
        await using var application = CreateApplication();
        using var client = application.CreateClient();
        var adminUserId = Guid.NewGuid();

        await CreateUserAsync(client, adminUserId, adminUserId, "admin@example.test");
        var tenant = await CreateTenantAsync(client, adminUserId, "tenant-a");
        await AddTenantAdminAccessAsync(client, tenant.Id, adminUserId);

        var ontology = await CreateOntologyAsync(client, tenant.Id, adminUserId, "1.0.0");
        var semanticLayer = await CreateSemanticLayerAsync(client, tenant.Id, adminUserId, ontology.Id, "1.0.0");
        var lifecycle = await CreateLifecycleAsync(client, tenant.Id, adminUserId, "1.0.0");
        var attributeSchema = await CreateAttributeSchemaAsync(client, tenant.Id, adminUserId, ontology.Id, "1.0.0");
        var package = await CreateModelPackageAsync(client, tenant.Id, adminUserId, ontology.Id, semanticLayer.Id, lifecycle.Id, attributeSchema.Id, "1.0.0");

        using var blockedRequest = new HttpRequestMessage(HttpMethod.Post, $"/api/admin/ontology/model-packages/{package.Id}/publish")
        {
            Content = JsonContent.Create(new PublishOntologyVersionRequest("Try to publish too early."))
        };
        AddTenantHeaders(blockedRequest, tenant.Id, adminUserId);
        var blockedResponse = await client.SendAsync(blockedRequest);
        var blockedProblem = await blockedResponse.Content.ReadFromJsonAsync<ProblemResponse>();

        Assert.Equal(HttpStatusCode.BadRequest, blockedResponse.StatusCode);
        Assert.NotNull(blockedProblem);
        Assert.Contains("must be published", blockedProblem.Error, StringComparison.OrdinalIgnoreCase);

        await PublishAsync<OntologyVersionResponse>(client, tenant.Id, adminUserId, $"/api/admin/ontology/versions/{ontology.Id}/publish");
        await PublishAsync<SemanticLayerVersionResponse>(client, tenant.Id, adminUserId, $"/api/admin/ontology/semantic-layers/{semanticLayer.Id}/publish");
        await PublishAsync<LifecycleVocabularyVersionResponse>(client, tenant.Id, adminUserId, $"/api/admin/ontology/lifecycle-vocabularies/{lifecycle.Id}/publish");
        await PublishAsync<AttributeSchemaVersionResponse>(client, tenant.Id, adminUserId, $"/api/admin/ontology/attribute-schemas/{attributeSchema.Id}/publish");
        var publishedPackage = await PublishAsync<ModelPackageVersionResponse>(client, tenant.Id, adminUserId, $"/api/admin/ontology/model-packages/{package.Id}/publish");

        Assert.Equal(OntologyPublicationState.Published, publishedPackage.State);

        using var activeRequest = new HttpRequestMessage(HttpMethod.Get, "/api/admin/ontology/model-packages/active");
        AddTenantHeaders(activeRequest, tenant.Id, adminUserId);
        var activeResponse = await client.SendAsync(activeRequest);
        var activePackage = await activeResponse.Content.ReadFromJsonAsync<ModelPackageVersionResponse>();

        Assert.Equal(HttpStatusCode.OK, activeResponse.StatusCode);
        Assert.NotNull(activePackage);
        Assert.Equal(publishedPackage.Id, activePackage.Id);
    }

    [Fact]
    public async Task AttributeSchemasValidateRestrictedAttributePermissionsAndBomMetadata()
    {
        await using var application = CreateApplication();
        using var client = application.CreateClient();
        var adminUserId = Guid.NewGuid();

        await CreateUserAsync(client, adminUserId, adminUserId, "admin@example.test");
        var tenant = await CreateTenantAsync(client, adminUserId, "tenant-a");
        await AddTenantAdminAccessAsync(client, tenant.Id, adminUserId);
        var ontology = await CreateOntologyAsync(client, tenant.Id, adminUserId, "1.0.0");

        using var invalidAttributeRequest = new HttpRequestMessage(HttpMethod.Post, "/api/admin/ontology/attribute-schemas")
        {
            Content = JsonContent.Create(new CreateAttributeSchemaVersionRequest(
                "canonical-attributes",
                "1.0.0",
                null,
                ontology.Id,
                [
                    new CreateAttributeDefinitionRequest(
                        "cost",
                        "part",
                        AttributeValueType.Number,
                        false,
                        null,
                        AttributeVisibility.Restricted,
                        null,
                        false,
                        false,
                        "secret",
                        "Cost",
                        "Restricted cost value.")
                ]))
        };
        AddTenantHeaders(invalidAttributeRequest, tenant.Id, adminUserId);
        var invalidAttributeResponse = await client.SendAsync(invalidAttributeRequest);
        var invalidAttributeProblem = await invalidAttributeResponse.Content.ReadFromJsonAsync<ProblemResponse>();

        Assert.Equal(HttpStatusCode.BadRequest, invalidAttributeResponse.StatusCode);
        Assert.NotNull(invalidAttributeProblem);
        Assert.Contains("required permission", invalidAttributeProblem.Error, StringComparison.OrdinalIgnoreCase);

        using var detailRequest = new HttpRequestMessage(HttpMethod.Get, $"/api/admin/ontology/versions/{ontology.Id}");
        AddTenantHeaders(detailRequest, tenant.Id, adminUserId);
        var detailResponse = await client.SendAsync(detailRequest);
        var detail = await detailResponse.Content.ReadFromJsonAsync<OntologyVersionDetailResponse>();

        Assert.Equal(HttpStatusCode.OK, detailResponse.StatusCode);
        Assert.NotNull(detail);
        var bom = Assert.Single(detail.BomRelationships);
        Assert.Equal("contains", bom.RelationshipType);
        Assert.Equal("quantity", bom.QuantityAttributeKey);
        Assert.Equal("approvalRecordId", bom.AuditReferenceAttributeKey);
        Assert.True(bom.RequiresApproval);
    }

    [Fact]
    public async Task CrossTenantOntologyAccessIsDeniedAndAudited()
    {
        await using var application = CreateApplication();
        using var client = application.CreateClient();
        var adminUserId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();

        await CreateUserAsync(client, adminUserId, adminUserId, "admin@example.test");
        await CreateUserAsync(client, otherUserId, otherUserId, "other@example.test");
        var tenant = await CreateTenantAsync(client, adminUserId, "tenant-a");
        var otherTenant = await CreateTenantAsync(client, otherUserId, "tenant-b");
        await AddTenantAdminAccessAsync(client, tenant.Id, adminUserId);
        await AddTenantAdminAccessAsync(client, otherTenant.Id, otherUserId);
        var ontology = await CreateOntologyAsync(client, tenant.Id, adminUserId, "1.0.0");

        using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/admin/ontology/versions/{ontology.Id}");
        AddTenantHeaders(request, otherTenant.Id, otherUserId);
        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        await using var scope = application.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<EnterpriseThreadDbContext>();
        var audit = await dbContext.AuditRecords.SingleAsync(
            record => record.Action == "ontology.versions.get" && record.Result == AuditResult.Denied);

        Assert.Equal(otherTenant.Id, audit.TenantId);
        Assert.Equal("ontology_tenant_mismatch", audit.Reason);
    }

    [Fact]
    public async Task PublishedOntologyVersionsHaveNoUpdateEndpoint()
    {
        await using var application = CreateApplication();
        using var client = application.CreateClient();
        var adminUserId = Guid.NewGuid();

        await CreateUserAsync(client, adminUserId, adminUserId, "admin@example.test");
        var tenant = await CreateTenantAsync(client, adminUserId, "tenant-a");
        await AddTenantAdminAccessAsync(client, tenant.Id, adminUserId);
        var ontology = await CreateOntologyAsync(client, tenant.Id, adminUserId, "1.0.0");
        await PublishAsync<OntologyVersionResponse>(client, tenant.Id, adminUserId, $"/api/admin/ontology/versions/{ontology.Id}/publish");

        using var request = new HttpRequestMessage(HttpMethod.Put, $"/api/admin/ontology/versions/{ontology.Id}")
        {
            Content = JsonContent.Create(new { summary = "mutated" })
        };
        AddTenantHeaders(request, tenant.Id, adminUserId);
        var response = await client.SendAsync(request);

        Assert.True(
            response.StatusCode is HttpStatusCode.MethodNotAllowed or HttpStatusCode.NotFound,
            await response.Content.ReadAsStringAsync());
    }

    private static WebApplicationFactory<Program> CreateApplication()
    {
        var databaseName = Guid.NewGuid().ToString();

        return new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureServices(services =>
                {
                    services.RemoveAll<DbContextOptions<EnterpriseThreadDbContext>>();
                    services.RemoveAll<IDbContextOptionsConfiguration<EnterpriseThreadDbContext>>();
                    services.AddDbContext<EnterpriseThreadDbContext>(options =>
                        options.UseInMemoryDatabase(databaseName));
                });
            });
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

    private static async Task AddTenantAdminAccessAsync(HttpClient client, Guid tenantId, Guid userId)
    {
        var role = await CreateRoleAsync(client, tenantId, userId, "Admin");
        await CreateMembershipAsync(client, tenantId, userId, userId, role.Id);
        await CreateGrantAsync(client, tenantId, userId, userId, OntologyPermissions.Read);
        await CreateGrantAsync(client, tenantId, userId, userId, OntologyPermissions.Manage);
        await CreateGrantAsync(client, tenantId, userId, userId, OntologyPermissions.Publish);
    }

    private static async Task<TenantRoleResponse> CreateRoleAsync(HttpClient client, Guid tenantId, Guid userId, string name)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/admin/identity/roles")
        {
            Content = JsonContent.Create(new CreateTenantRoleRequest(name, null))
        };
        AddTenantHeaders(request, tenantId, userId);

        var response = await client.SendAsync(request);
        var role = await response.Content.ReadFromJsonAsync<TenantRoleResponse>();

        Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        Assert.NotNull(role);
        return role;
    }

    private static async Task CreateMembershipAsync(HttpClient client, Guid tenantId, Guid adminUserId, Guid userId, Guid roleId)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/admin/identity/memberships")
        {
            Content = JsonContent.Create(new CreateTenantMembershipRequest(userId, roleId, null))
        };
        AddTenantHeaders(request, tenantId, adminUserId);

        var response = await client.SendAsync(request);
        Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
    }

    private static async Task CreateGrantAsync(HttpClient client, Guid tenantId, Guid adminUserId, Guid userId, string permissionKey)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/admin/identity/grants")
        {
            Content = JsonContent.Create(new CreateAccessGrantRequest(
                userId,
                permissionKey,
                AccessGrantKind.Temporary,
                DateTimeOffset.UtcNow.AddHours(1),
                "Temporary test grant."))
        };
        AddTenantHeaders(request, tenantId, adminUserId);

        var response = await client.SendAsync(request);
        Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
    }

    private static async Task<OntologyVersionResponse> CreateOntologyAsync(HttpClient client, Guid tenantId, Guid userId, string versionLabel)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/admin/ontology/versions")
        {
            Content = JsonContent.Create(new CreateOntologyVersionRequest(
                "canonical-manufacturing",
                versionLabel,
                "Canonical manufacturing ontology.",
                [
                    new CreateObjectTypeDefinitionRequest("part", "Part", "Source-owned part.", """["partNumber","revision"]""", "Part identity."),
                    new CreateObjectTypeDefinitionRequest("document", "Document", "Document evidence.", """["documentNumber","revision"]""", "Document identity.")
                ],
                [
                    new CreateSemanticRelationshipDefinitionRequest("references", "part", "document", "Part references document.", true)
                ],
                [
                    new CreateBomRelationshipDefinitionRequest(
                        "contains",
                        "part",
                        "part",
                        "quantity",
                        "unit",
                        "findNumber",
                        "referenceDesignator",
                        """{"allowedParentStates":["released"]}""",
                        true,
                        "approvalRecordId")
                ]))
        };
        AddTenantHeaders(request, tenantId, userId);

        var response = await client.SendAsync(request);
        var ontology = await response.Content.ReadFromJsonAsync<OntologyVersionResponse>();

        Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        Assert.NotNull(ontology);
        return ontology;
    }

    private static async Task<SemanticLayerVersionResponse> CreateSemanticLayerAsync(HttpClient client, Guid tenantId, Guid userId, Guid ontologyVersionId, string versionLabel)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/admin/ontology/semantic-layers")
        {
            Content = JsonContent.Create(new CreateSemanticLayerVersionRequest(
                "canonical-semantic",
                versionLabel,
                "Canonical graph mappings.",
                ontologyVersionId,
                """{"part":"Part","document":"Document"}""",
                """{"contains":"contains","references":"references"}"""))
        };
        AddTenantHeaders(request, tenantId, userId);

        var response = await client.SendAsync(request);
        var semanticLayer = await response.Content.ReadFromJsonAsync<SemanticLayerVersionResponse>();

        Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        Assert.NotNull(semanticLayer);
        return semanticLayer;
    }

    private static async Task<LifecycleVocabularyVersionResponse> CreateLifecycleAsync(HttpClient client, Guid tenantId, Guid userId, string versionLabel)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/admin/ontology/lifecycle-vocabularies")
        {
            Content = JsonContent.Create(new CreateLifecycleVocabularyVersionRequest(
                "canonical-lifecycle",
                versionLabel,
                "Canonical lifecycle.",
                [
                    new CreateLifecycleStateDefinitionRequest("draft", "Draft", "working", 10, false),
                    new CreateLifecycleStateDefinitionRequest("released", "Released", "released", 20, false)
                ],
                [
                    new CreateLifecycleTransitionDefinitionRequest("draft", "released", true, "Release approval.")
                ]))
        };
        AddTenantHeaders(request, tenantId, userId);

        var response = await client.SendAsync(request);
        var lifecycle = await response.Content.ReadFromJsonAsync<LifecycleVocabularyVersionResponse>();

        Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        Assert.NotNull(lifecycle);
        return lifecycle;
    }

    private static async Task<AttributeSchemaVersionResponse> CreateAttributeSchemaAsync(HttpClient client, Guid tenantId, Guid userId, Guid ontologyVersionId, string versionLabel)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/admin/ontology/attribute-schemas")
        {
            Content = JsonContent.Create(new CreateAttributeSchemaVersionRequest(
                "canonical-attributes",
                versionLabel,
                "Canonical attributes.",
                ontologyVersionId,
                [
                    new CreateAttributeDefinitionRequest(
                        "partNumber",
                        "part",
                        AttributeValueType.Text,
                        true,
                        """{"maxLength":80}""",
                        AttributeVisibility.Internal,
                        null,
                        true,
                        true,
                        "internal",
                        "Part Number",
                        "Part number identity.")
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
        string versionLabel)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/admin/ontology/model-packages")
        {
            Content = JsonContent.Create(new CreateModelPackageVersionRequest(
                "canonical-package",
                "Canonical Package",
                versionLabel,
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

    private static void AddTenantHeaders(HttpRequestMessage request, Guid tenantId, Guid userId)
    {
        request.Headers.Add(TenantHeaderNames.UserId, userId.ToString());
        request.Headers.Add(TenantHeaderNames.TenantId, tenantId.ToString());
    }
}
