using System.Net;
using System.Net.Http.Json;
using ETOS.Backend.Artifacts;
using ETOS.Backend.Classification;
using ETOS.Backend.Governance;
using ETOS.Backend.Identity;
using ETOS.Backend.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ETOS.Backend.Tests;

public sealed class ClassificationPolicyTests
{
    [Fact]
    public async Task PolicyEvaluationSeparatesAllowedDeniedAndSensitiveReferences()
    {
        await using var application = CreateApplication();
        using var client = application.CreateClient();
        var adminUserId = Guid.NewGuid();
        var analystUserId = Guid.NewGuid();

        await CreateUserAsync(client, adminUserId, adminUserId, "admin@example.test");
        await CreateUserAsync(client, adminUserId, analystUserId, "analyst@example.test");
        var tenant = await CreateTenantAsync(client, adminUserId, "tenant-a");
        var role = await CreateRoleAsync(client, tenant.Id, adminUserId, "Analyst");
        await CreateMembershipAsync(client, tenant.Id, adminUserId, analystUserId, role.Id);
        await CreateGrantAsync(client, tenant.Id, adminUserId, analystUserId, ClassificationPermissions.Evaluate, DateTimeOffset.UtcNow.AddHours(1));
        await CreatePublishedPolicyWithRuleAsync(client, tenant.Id, adminUserId, "secret", "cost", "restricted.cost.read", requiresGrant: false);

        var evaluation = await EvaluateAsync(client, tenant.Id, analystUserId, new EvaluatePolicyRequest(
            "tests.policy.evaluate",
            "default-context",
            [
                new PolicyEvaluationContextItem("allowed-1", "artifact", "public", null, null, "Public context."),
                new PolicyEvaluationContextItem("denied-1", "artifact", "secret", "cost", "doc-1", "Sensitive cost context.")
            ]));

        Assert.Single(evaluation.AllowedContext);
        Assert.Single(evaluation.DeniedSummaries);
        Assert.Single(evaluation.SensitiveDeniedReferences);
        Assert.Equal("allowed-1", evaluation.AllowedContext.Single().ContextId);
        Assert.Equal("denied-1", evaluation.DeniedSummaries.Single().ContextId);
        Assert.Equal("doc-1", evaluation.SensitiveDeniedReferences.Single().DocumentId);

        await using var scope = application.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<EnterpriseThreadDbContext>();
        var audit = await dbContext.AuditRecords.SingleAsync(record => record.Action == "tests.policy.evaluate");

        Assert.Equal(AuditResult.Denied, audit.Result);
        Assert.Equal("policy_context_denied", audit.Reason);
        Assert.DoesNotContain("doc-1", audit.SafeSummary, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TemporaryGrantAllowsRestrictedContextUntilExpiration()
    {
        await using var application = CreateApplication();
        using var client = application.CreateClient();
        var adminUserId = Guid.NewGuid();
        var analystUserId = Guid.NewGuid();
        const string permissionKey = "restricted.cost.read";

        await CreateUserAsync(client, adminUserId, adminUserId, "admin@example.test");
        await CreateUserAsync(client, adminUserId, analystUserId, "analyst@example.test");
        var tenant = await CreateTenantAsync(client, adminUserId, "tenant-a");
        var role = await CreateRoleAsync(client, tenant.Id, adminUserId, "Analyst");
        await CreateMembershipAsync(client, tenant.Id, adminUserId, analystUserId, role.Id);
        await CreateGrantAsync(client, tenant.Id, adminUserId, analystUserId, ClassificationPermissions.Evaluate, DateTimeOffset.UtcNow.AddHours(1));
        await CreateGrantAsync(client, tenant.Id, adminUserId, analystUserId, permissionKey, DateTimeOffset.UtcNow.AddHours(1));
        await CreatePublishedPolicyWithRuleAsync(client, tenant.Id, adminUserId, "secret", "cost", permissionKey, requiresGrant: true);

        var allowedEvaluation = await EvaluateSingleRestrictedItemAsync(client, tenant.Id, analystUserId);

        Assert.Single(allowedEvaluation.AllowedContext);
        Assert.Empty(allowedEvaluation.DeniedSummaries);

        await using var scope = application.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<EnterpriseThreadDbContext>();
        var grant = await dbContext.AccessGrants.SingleAsync(
            candidate => candidate.UserId == analystUserId && candidate.NormalizedPermissionKey == permissionKey.ToUpperInvariant());
        grant.ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(-1);
        await dbContext.SaveChangesAsync();

        var deniedEvaluation = await EvaluateSingleRestrictedItemAsync(client, tenant.Id, analystUserId);

        Assert.Empty(deniedEvaluation.AllowedContext);
        Assert.Single(deniedEvaluation.DeniedSummaries);
    }

    [Fact]
    public async Task PublishedPolicyVersionsRejectRuleMutation()
    {
        await using var application = CreateApplication();
        using var client = application.CreateClient();
        var adminUserId = Guid.NewGuid();

        await CreateUserAsync(client, adminUserId, adminUserId, "admin@example.test");
        var tenant = await CreateTenantAsync(client, adminUserId, "tenant-a");
        var policy = await CreatePublishedPolicyWithRuleAsync(client, tenant.Id, adminUserId, "secret", "cost", "restricted.cost.read", requiresGrant: false);

        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/admin/classification/policies/{policy.Id}/rules")
        {
            Content = JsonContent.Create(new CreateRestrictedContextRuleRequest(
                "secret",
                "margin",
                null,
                "restricted.margin.read",
                null,
                false,
                PolicyRuleEffect.Deny,
                "Margin details are restricted."))
        };
        AddTenantHeaders(request, tenant.Id, adminUserId);

        var response = await client.SendAsync(request);
        var problem = await response.Content.ReadFromJsonAsync<ProblemResponse>();

        Assert.True(response.StatusCode == HttpStatusCode.BadRequest, await response.Content.ReadAsStringAsync());
        Assert.NotNull(problem);
        Assert.Contains("draft policy versions", problem.Error);
    }

    [Fact]
    public async Task ArtifactPublishIsBlockedWhenActivePolicyFindsRestrictedPayload()
    {
        await using var application = CreateApplication();
        using var client = application.CreateClient();
        var adminUserId = Guid.NewGuid();

        await CreateUserAsync(client, adminUserId, adminUserId, "admin@example.test");
        var tenant = await CreateTenantAsync(client, adminUserId, "tenant-a");
        await CreatePublishedPolicyWithRuleAsync(client, tenant.Id, adminUserId, "secret", null, "restricted.secret.read", requiresGrant: false);
        var artifact = await CreateArtifactAsync(client, tenant.Id, adminUserId, "query-intent", "Restricted Query");
        var version = await CreateVersionAsync(
            client,
            tenant.Id,
            adminUserId,
            artifact.Id,
            "1.0.0",
            """{"classification":"SECRET"}""");

        var publish = await PublishAsync(client, tenant.Id, adminUserId, artifact.Id, version.Id);

        Assert.False(publish.Succeeded);
        Assert.Equal(ArtifactPolicyRiskStatus.RequiresApproval, publish.PolicyRiskStatus);
        Assert.Contains(publish.BlockingReasons, reason => reason.Contains("Policy risk status", StringComparison.OrdinalIgnoreCase));
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

    private static async Task CreateGrantAsync(
        HttpClient client,
        Guid tenantId,
        Guid adminUserId,
        Guid userId,
        string permissionKey,
        DateTimeOffset expiresAt)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/admin/identity/grants")
        {
            Content = JsonContent.Create(new CreateAccessGrantRequest(
                userId,
                permissionKey,
                AccessGrantKind.Temporary,
                expiresAt,
                "Temporary test grant."))
        };
        AddTenantHeaders(request, tenantId, adminUserId);

        var response = await client.SendAsync(request);
        Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
    }

    private static async Task<PolicyVersionResponse> CreatePublishedPolicyWithRuleAsync(
        HttpClient client,
        Guid tenantId,
        Guid userId,
        string classificationKey,
        string? attributeKey,
        string permissionKey,
        bool requiresGrant)
    {
        var scheme = await CreateSchemeAsync(client, tenantId, userId);
        var schemeVersion = await CreateSchemeVersionAsync(client, tenantId, userId, scheme.Id);
        var publishedSchemeVersion = await PublishSchemeVersionAsync(client, tenantId, userId, scheme.Id, schemeVersion.Id);
        var policy = await CreatePolicyAsync(client, tenantId, userId, publishedSchemeVersion.Id);
        await AddRuleAsync(client, tenantId, userId, policy.Id, classificationKey, attributeKey, permissionKey, requiresGrant);
        return await PublishPolicyAsync(client, tenantId, userId, policy.Id);
    }

    private static async Task<ClassificationSchemeResponse> CreateSchemeAsync(HttpClient client, Guid tenantId, Guid userId)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/admin/classification/schemes")
        {
            Content = JsonContent.Create(new CreateClassificationSchemeRequest("default", "Default", null))
        };
        AddTenantHeaders(request, tenantId, userId);

        var response = await client.SendAsync(request);
        var scheme = await response.Content.ReadFromJsonAsync<ClassificationSchemeResponse>();

        Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        Assert.NotNull(scheme);

        return scheme;
    }

    private static async Task<ClassificationSchemeVersionResponse> CreateSchemeVersionAsync(
        HttpClient client,
        Guid tenantId,
        Guid userId,
        Guid schemeId)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/admin/classification/schemes/{schemeId}/versions")
        {
            Content = JsonContent.Create(new CreateClassificationSchemeVersionRequest(
                "1.0.0",
                "Default classification levels.",
                """{"levels":["public","secret"]}"""))
        };
        AddTenantHeaders(request, tenantId, userId);

        var response = await client.SendAsync(request);
        var version = await response.Content.ReadFromJsonAsync<ClassificationSchemeVersionResponse>();

        Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        Assert.NotNull(version);

        return version;
    }

    private static async Task<ClassificationSchemeVersionResponse> PublishSchemeVersionAsync(
        HttpClient client,
        Guid tenantId,
        Guid userId,
        Guid schemeId,
        Guid versionId)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/admin/classification/schemes/{schemeId}/versions/{versionId}/publish")
        {
            Content = JsonContent.Create(new PublishClassificationSchemeVersionRequest("Publish scheme."))
        };
        AddTenantHeaders(request, tenantId, userId);

        var response = await client.SendAsync(request);
        var version = await response.Content.ReadFromJsonAsync<ClassificationSchemeVersionResponse>();

        Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        Assert.NotNull(version);

        return version;
    }

    private static async Task<PolicyVersionResponse> CreatePolicyAsync(
        HttpClient client,
        Guid tenantId,
        Guid userId,
        Guid schemeVersionId)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/admin/classification/policies")
        {
            Content = JsonContent.Create(new CreatePolicyVersionRequest(
                "default-context",
                "Default Context Policy",
                "1.0.0",
                "Default context filtering policy.",
                schemeVersionId))
        };
        AddTenantHeaders(request, tenantId, userId);

        var response = await client.SendAsync(request);
        var policy = await response.Content.ReadFromJsonAsync<PolicyVersionResponse>();

        Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        Assert.NotNull(policy);

        return policy;
    }

    private static async Task AddRuleAsync(
        HttpClient client,
        Guid tenantId,
        Guid userId,
        Guid policyVersionId,
        string classificationKey,
        string? attributeKey,
        string permissionKey,
        bool requiresGrant)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/admin/classification/policies/{policyVersionId}/rules")
        {
            Content = JsonContent.Create(new CreateRestrictedContextRuleRequest(
                classificationKey,
                attributeKey,
                null,
                permissionKey,
                null,
                requiresGrant,
                PolicyRuleEffect.Deny,
                "Restricted context was withheld by policy."))
        };
        AddTenantHeaders(request, tenantId, userId);

        var response = await client.SendAsync(request);
        Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
    }

    private static async Task<PolicyVersionResponse> PublishPolicyAsync(HttpClient client, Guid tenantId, Guid userId, Guid policyVersionId)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/admin/classification/policies/{policyVersionId}/publish")
        {
            Content = JsonContent.Create(new PublishPolicyVersionRequest("Publish policy."))
        };
        AddTenantHeaders(request, tenantId, userId);

        var response = await client.SendAsync(request);
        var policy = await response.Content.ReadFromJsonAsync<PolicyVersionResponse>();

        Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        Assert.NotNull(policy);

        return policy;
    }

    private static async Task<PolicyEvaluationResponse> EvaluateSingleRestrictedItemAsync(HttpClient client, Guid tenantId, Guid userId)
    {
        return await EvaluateAsync(client, tenantId, userId, new EvaluatePolicyRequest(
            "tests.policy.evaluate",
            "default-context",
            [new PolicyEvaluationContextItem("restricted-1", "artifact", "secret", "cost", "doc-1", "Sensitive cost context.")]));
    }

    private static async Task<PolicyEvaluationResponse> EvaluateAsync(
        HttpClient client,
        Guid tenantId,
        Guid userId,
        EvaluatePolicyRequest evaluationRequest)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/admin/classification/evaluate")
        {
            Content = JsonContent.Create(evaluationRequest)
        };
        AddTenantHeaders(request, tenantId, userId);

        var response = await client.SendAsync(request);
        var evaluation = await response.Content.ReadFromJsonAsync<PolicyEvaluationResponse>();

        Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        Assert.NotNull(evaluation);

        return evaluation;
    }

    private static async Task<ArtifactSummaryResponse> CreateArtifactAsync(
        HttpClient client,
        Guid tenantId,
        Guid userId,
        string artifactType,
        string name)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/admin/artifacts")
        {
            Content = JsonContent.Create(new CreateArtifactRequest(artifactType, name, null, null))
        };
        AddTenantHeaders(request, tenantId, userId);

        var response = await client.SendAsync(request);
        var artifact = await response.Content.ReadFromJsonAsync<ArtifactSummaryResponse>();

        Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        Assert.NotNull(artifact);

        return artifact;
    }

    private static async Task<ArtifactVersionSummaryResponse> CreateVersionAsync(
        HttpClient client,
        Guid tenantId,
        Guid userId,
        Guid artifactId,
        string versionLabel,
        string payloadJson)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/admin/artifacts/{artifactId}/versions")
        {
            Content = JsonContent.Create(new CreateArtifactVersionRequest(
                versionLabel,
                $"Version {versionLabel}",
                payloadJson,
                ArtifactReadinessState.Ready,
                ArtifactCompatibilityStatus.Compatible,
                "Compatible test version."))
        };
        AddTenantHeaders(request, tenantId, userId);

        var response = await client.SendAsync(request);
        var version = await response.Content.ReadFromJsonAsync<ArtifactVersionSummaryResponse>();

        Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        Assert.NotNull(version);

        return version;
    }

    private static async Task<PublishArtifactVersionResult> PublishAsync(
        HttpClient client,
        Guid tenantId,
        Guid userId,
        Guid artifactId,
        Guid versionId)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/admin/artifacts/{artifactId}/versions/{versionId}/publish")
        {
            Content = JsonContent.Create(new PublishArtifactVersionRequest("Publish from classification policy test."))
        };
        AddTenantHeaders(request, tenantId, userId);

        var response = await client.SendAsync(request);
        var publish = await response.Content.ReadFromJsonAsync<PublishArtifactVersionResult>();

        Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        Assert.NotNull(publish);

        return publish;
    }

    private static void AddTenantHeaders(HttpRequestMessage request, Guid tenantId, Guid userId)
    {
        request.Headers.Add(TenantHeaderNames.UserId, userId.ToString());
        request.Headers.Add(TenantHeaderNames.TenantId, tenantId.ToString());
    }
}
