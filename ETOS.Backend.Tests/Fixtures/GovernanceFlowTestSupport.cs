using System.Net;
using System.Net.Http.Json;
using ETOS.Backend.Classification;
using ETOS.Backend.GovernedQuery;
using ETOS.Backend.Decisions;
using ETOS.Backend.GovernedChat;
using ETOS.Backend.Identity;
using ETOS.Backend.Outcomes;
using ETOS.Backend.Recommendations;
using ETOS.Backend.ReviewTasks;

namespace ETOS.Backend.Tests.Fixtures;

internal static class GovernanceFlowTestSupport
{
    internal static async Task<GovernedChatSessionSummaryResponse> CreateGovernedChatSessionAsync(
        HttpClient client,
        ImportFlowTestSupport.ImportFlowContext context,
        Guid? startGraphNodeId)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/admin/governed-chat/sessions")
        {
            Content = JsonContent.Create(new CreateGovernedChatSessionRequest("MVP demo chat", startGraphNodeId, null))
        };
        ImportFlowTestSupport.AddTenantHeaders(request, context.TenantId, context.UserId);
        var response = await client.SendAsync(request);
        var session = await response.Content.ReadFromJsonAsync<GovernedChatSessionSummaryResponse>();
        Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        Assert.NotNull(session);
        return session;
    }

    internal static async Task<(HttpStatusCode StatusCode, GovernedChatTurnResponse? Turn)> PostGovernedChatTurnAsync(
        HttpClient client,
        ImportFlowTestSupport.ImportFlowContext context,
        Guid sessionId,
        string message,
        string? intentKey = null,
        Guid? startGraphNodeId = null,
        ChatDraftArtifactKind? draftKind = null,
        string? policyKey = "default-context")
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/admin/governed-chat/sessions/{sessionId}/turns")
        {
            Content = JsonContent.Create(new CreateGovernedChatTurnRequest(
                message,
                intentKey,
                startGraphNodeId,
                null,
                policyKey,
                draftKind))
        };
        ImportFlowTestSupport.AddTenantHeaders(request, context.TenantId, context.UserId);
        var response = await client.SendAsync(request);
        if (response.StatusCode != HttpStatusCode.OK)
        {
            return (response.StatusCode, null);
        }

        var turn = await response.Content.ReadFromJsonAsync<GovernedChatTurnResponse>();
        Assert.NotNull(turn);
        return (response.StatusCode, turn);
    }

    internal static async Task<GovernedChatTurnResponse> AskGovernedChatAsync(
        HttpClient client,
        ImportFlowTestSupport.ImportFlowContext context,
        Guid sessionId,
        string message,
        string? intentKey = null,
        Guid? startGraphNodeId = null,
        ChatDraftArtifactKind? draftKind = null,
        string? policyKey = "default-context")
    {
        var (statusCode, turn) = await PostGovernedChatTurnAsync(
            client,
            context,
            sessionId,
            message,
            intentKey,
            startGraphNodeId,
            draftKind,
            policyKey);
        Assert.True(statusCode == HttpStatusCode.OK, $"Governed chat turn failed with status {statusCode}.");
        Assert.NotNull(turn);
        return turn;
    }

    internal static async Task<CreateRecommendationResponse> CreateRecommendationFromBomComparisonAsync(
        HttpClient client,
        ImportFlowTestSupport.ImportFlowContext context,
        Guid runId)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/admin/recommendations/from-bom-comparison/{runId}")
        {
            Content = JsonContent.Create(new { })
        };
        ImportFlowTestSupport.AddTenantHeaders(request, context.TenantId, context.UserId);
        var response = await client.SendAsync(request);
        var recommendation = await response.Content.ReadFromJsonAsync<CreateRecommendationResponse>();
        Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        Assert.NotNull(recommendation);
        return recommendation;
    }

    internal static async Task<RecommendationPayloadResponse> GetRecommendationAsync(
        HttpClient client,
        ImportFlowTestSupport.ImportFlowContext context,
        Guid artifactId,
        Guid versionId)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/admin/recommendations/{artifactId}/versions/{versionId}");
        ImportFlowTestSupport.AddTenantHeaders(request, context.TenantId, context.UserId);
        var response = await client.SendAsync(request);
        var recommendation = await response.Content.ReadFromJsonAsync<RecommendationPayloadResponse>();
        Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        Assert.NotNull(recommendation);
        return recommendation;
    }

    internal static async Task<CreateReviewTaskResponse> CreateReviewTaskFromRecommendationActionAsync(
        HttpClient client,
        ImportFlowTestSupport.ImportFlowContext context,
        Guid recommendationArtifactId,
        Guid recommendationVersionId,
        Guid actionId)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"/api/admin/review-tasks/from-recommendation/{recommendationArtifactId}/versions/{recommendationVersionId}/actions/{actionId}")
        {
            Content = JsonContent.Create(new { })
        };
        ImportFlowTestSupport.AddTenantHeaders(request, context.TenantId, context.UserId);
        var response = await client.SendAsync(request);
        var reviewTask = await response.Content.ReadFromJsonAsync<CreateReviewTaskResponse>();
        Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        Assert.NotNull(reviewTask);
        return reviewTask;
    }

    internal static async Task<CompleteReviewTaskResponse> CompleteReviewTaskAsync(
        HttpClient client,
        ImportFlowTestSupport.ImportFlowContext context,
        Guid artifactId,
        Guid versionId,
        ReviewTaskCompletionResolution resolution = ReviewTaskCompletionResolution.Accepted,
        string? summary = "Accepted in MVP demonstration flow.")
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/admin/review-tasks/{artifactId}/versions/{versionId}/complete")
        {
            Content = JsonContent.Create(new CompleteReviewTaskRequest(resolution, summary, "accept"))
        };
        ImportFlowTestSupport.AddTenantHeaders(request, context.TenantId, context.UserId);
        var response = await client.SendAsync(request);
        var completed = await response.Content.ReadFromJsonAsync<CompleteReviewTaskResponse>();
        Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        Assert.NotNull(completed);
        return completed;
    }

    internal static async Task<RecordManualOutcomeResponse> RecordDecisionOutcomeAsync(
        HttpClient client,
        ImportFlowTestSupport.ImportFlowContext context,
        Guid decisionArtifactId,
        Guid decisionVersionId,
        Guid? recommendationArtifactId = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/admin/decisions/{decisionArtifactId}/versions/{decisionVersionId}/outcomes")
        {
            Content = JsonContent.Create(new RecordManualOutcomeRequest(
                "manual-verification",
                "accept",
                "accept",
                OutcomeCheckStatus.Successful,
                0.95m,
                "MVP demonstration outcome recorded.",
                recommendationArtifactId))
        };
        ImportFlowTestSupport.AddTenantHeaders(request, context.TenantId, context.UserId);
        var response = await client.SendAsync(request);
        var outcome = await response.Content.ReadFromJsonAsync<RecordManualOutcomeResponse>();
        Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        Assert.NotNull(outcome);
        return outcome;
    }

    internal static async Task<ImportFlowTestSupport.ImportFlowContext> CreateChatRunnerContextAsync(
        HttpClient client,
        ImportFlowTestSupport.ImportFlowContext adminContext)
    {
        var chatRunnerUserId = Guid.NewGuid();
        var chatRunnerEmail = $"chat-runner-{chatRunnerUserId:N}@example.test";
        await AgentExecutionTestSupport.CreateUserAsync(client, adminContext.UserId, chatRunnerUserId, chatRunnerEmail);

        using var roleRequest = new HttpRequestMessage(HttpMethod.Post, "/api/admin/identity/roles")
        {
            Content = JsonContent.Create(new CreateTenantRoleRequest("Chat Runner", "Run governed chat without draft permissions."))
        };
        ImportFlowTestSupport.AddTenantHeaders(roleRequest, adminContext.TenantId, adminContext.UserId);
        var roleResponse = await client.SendAsync(roleRequest);
        var role = await roleResponse.Content.ReadFromJsonAsync<TenantRoleResponse>();
        Assert.True(roleResponse.StatusCode == HttpStatusCode.OK, await roleResponse.Content.ReadAsStringAsync());
        Assert.NotNull(role);

        using var membershipRequest = new HttpRequestMessage(HttpMethod.Post, "/api/admin/identity/memberships")
        {
            Content = JsonContent.Create(new CreateTenantMembershipRequest(chatRunnerUserId, role.Id, null))
        };
        ImportFlowTestSupport.AddTenantHeaders(membershipRequest, adminContext.TenantId, adminContext.UserId);
        var membershipResponse = await client.SendAsync(membershipRequest);
        Assert.True(membershipResponse.StatusCode == HttpStatusCode.OK, await membershipResponse.Content.ReadAsStringAsync());

        await AgentExecutionTestSupport.CreateGrantAsync(client, adminContext.TenantId, adminContext.UserId, chatRunnerUserId, GovernedChatPermissions.Run);
        await AgentExecutionTestSupport.CreateGrantAsync(client, adminContext.TenantId, adminContext.UserId, chatRunnerUserId, GovernedQueryPermissions.Run);
        await AgentExecutionTestSupport.CreateGrantAsync(client, adminContext.TenantId, adminContext.UserId, chatRunnerUserId, ClassificationPermissions.Evaluate);

        return new ImportFlowTestSupport.ImportFlowContext(adminContext.TenantId, chatRunnerUserId);
    }

    internal static async Task<PolicyEvaluationResponse> EvaluateRestrictedContextAsync(
        HttpClient client,
        ImportFlowTestSupport.ImportFlowContext context)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/admin/classification/evaluate")
        {
            Content = JsonContent.Create(new EvaluatePolicyRequest(
                "mvp.demo.policy.evaluate",
                "default-context",
                [
                    new PolicyEvaluationContextItem(
                        "denied-1",
                        "artifact",
                        "secret",
                        "cost",
                        "doc-1",
                        "Sensitive cost context.")
                ]))
        };
        ImportFlowTestSupport.AddTenantHeaders(request, context.TenantId, context.UserId);
        var response = await client.SendAsync(request);
        var evaluation = await response.Content.ReadFromJsonAsync<PolicyEvaluationResponse>();
        Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        Assert.NotNull(evaluation);
        return evaluation;
    }

    internal static async Task CreatePublishedDenyPolicyAsync(
        HttpClient client,
        ImportFlowTestSupport.ImportFlowContext context)
    {
        using var schemeRequest = new HttpRequestMessage(HttpMethod.Post, "/api/admin/classification/schemes")
        {
            Content = JsonContent.Create(new CreateClassificationSchemeRequest("default", "Default", null))
        };
        ImportFlowTestSupport.AddTenantHeaders(schemeRequest, context.TenantId, context.UserId);
        var schemeResponse = await client.SendAsync(schemeRequest);
        var scheme = await schemeResponse.Content.ReadFromJsonAsync<ClassificationSchemeResponse>();
        Assert.True(schemeResponse.StatusCode == HttpStatusCode.OK, await schemeResponse.Content.ReadAsStringAsync());
        Assert.NotNull(scheme);

        using var schemeVersionRequest = new HttpRequestMessage(HttpMethod.Post, $"/api/admin/classification/schemes/{scheme.Id}/versions")
        {
            Content = JsonContent.Create(new CreateClassificationSchemeVersionRequest(
                "1.0.0",
                "Default classification levels.",
                """{"levels":["public","secret"]}"""))
        };
        ImportFlowTestSupport.AddTenantHeaders(schemeVersionRequest, context.TenantId, context.UserId);
        var schemeVersionResponse = await client.SendAsync(schemeVersionRequest);
        var schemeVersion = await schemeVersionResponse.Content.ReadFromJsonAsync<ClassificationSchemeVersionResponse>();
        Assert.True(schemeVersionResponse.StatusCode == HttpStatusCode.OK, await schemeVersionResponse.Content.ReadAsStringAsync());
        Assert.NotNull(schemeVersion);

        using var publishSchemeRequest = new HttpRequestMessage(
            HttpMethod.Post,
            $"/api/admin/classification/schemes/{scheme.Id}/versions/{schemeVersion.Id}/publish")
        {
            Content = JsonContent.Create(new PublishClassificationSchemeVersionRequest("Publish scheme."))
        };
        ImportFlowTestSupport.AddTenantHeaders(publishSchemeRequest, context.TenantId, context.UserId);
        var publishSchemeResponse = await client.SendAsync(publishSchemeRequest);
        Assert.True(publishSchemeResponse.StatusCode == HttpStatusCode.OK, await publishSchemeResponse.Content.ReadAsStringAsync());

        using var policyRequest = new HttpRequestMessage(HttpMethod.Post, "/api/admin/classification/policies")
        {
            Content = JsonContent.Create(new CreatePolicyVersionRequest(
                "default-context",
                "Default Context Policy",
                "1.0.0",
                "Default context filtering policy.",
                schemeVersion.Id))
        };
        ImportFlowTestSupport.AddTenantHeaders(policyRequest, context.TenantId, context.UserId);
        var policyResponse = await client.SendAsync(policyRequest);
        var policy = await policyResponse.Content.ReadFromJsonAsync<PolicyVersionResponse>();
        Assert.True(policyResponse.StatusCode == HttpStatusCode.OK, await policyResponse.Content.ReadAsStringAsync());
        Assert.NotNull(policy);

        using var ruleRequest = new HttpRequestMessage(HttpMethod.Post, $"/api/admin/classification/policies/{policy.Id}/rules")
        {
            Content = JsonContent.Create(new CreateRestrictedContextRuleRequest(
                "secret",
                "cost",
                null,
                "restricted.cost.read",
                null,
                false,
                PolicyRuleEffect.Deny,
                "Restricted context was withheld by policy."))
        };
        ImportFlowTestSupport.AddTenantHeaders(ruleRequest, context.TenantId, context.UserId);
        var ruleResponse = await client.SendAsync(ruleRequest);
        Assert.True(ruleResponse.StatusCode == HttpStatusCode.OK, await ruleResponse.Content.ReadAsStringAsync());

        using var publishPolicyRequest = new HttpRequestMessage(HttpMethod.Post, $"/api/admin/classification/policies/{policy.Id}/publish")
        {
            Content = JsonContent.Create(new PublishPolicyVersionRequest("Publish policy."))
        };
        ImportFlowTestSupport.AddTenantHeaders(publishPolicyRequest, context.TenantId, context.UserId);
        var publishPolicyResponse = await client.SendAsync(publishPolicyRequest);
        Assert.True(publishPolicyResponse.StatusCode == HttpStatusCode.OK, await publishPolicyResponse.Content.ReadAsStringAsync());
    }
}
