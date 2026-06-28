using ETOS.Backend.Artifacts;
using ETOS.Backend.Classification;
using ETOS.Backend.Dashboards;
using ETOS.Backend.DataQuality;
using ETOS.Backend.Documents;
using ETOS.Backend.Explorers;
using ETOS.Backend.GraphMemory;
using ETOS.Backend.AiTrace;
using ETOS.Backend.GovernedChat;
using ETOS.Backend.GovernedQuery;
using ETOS.Backend.Health;
using ETOS.Backend.Governance;
using ETOS.Backend.Identity;
using ETOS.Backend.Imports;
using ETOS.Backend.IdentityResolution;
using ETOS.Backend.Ontology;
using ETOS.Backend.BusinessPolicies;
using ETOS.Backend.Capabilities;
using ETOS.Backend.OptimizationModels;
using ETOS.Backend.AgentRuns;
using ETOS.Backend.AgentRuntime;
using ETOS.Backend.AgentTemplates;
using ETOS.Backend.AgentTypes;
using ETOS.Backend.Agents;
using ETOS.Backend.ToolRegistry;
using ETOS.Backend.Recommendations;
using ETOS.Backend.ReviewTasks;
using ETOS.Backend.Packages;
using ETOS.Backend.Platform;
using ETOS.Backend.Platform.Development;
using Finbuckle.MultiTenant.AspNetCore.Extensions;
using Microsoft.AspNetCore.Http;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddEnterpriseThreadPlatform(builder.Configuration);

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
    app.MapEnterpriseThreadDevelopmentEndpoints();
    await SeedDevelopmentIdentityAsync(app);
}

app.UseCors(EnterpriseThreadPlatform.CorsPolicyName);
app.UseAuthentication();
app.UseMultiTenant();
app.UseAuthorization();

app.MapGet("/", () => Results.Redirect("/health/app"));
app.MapEnterpriseThreadHealthEndpoints();
app.MapEnterpriseThreadIdentityEndpoints();
app.MapEnterpriseThreadGovernanceEndpoints();
app.MapEnterpriseThreadArtifactEndpoints();
app.MapEnterpriseThreadClassificationEndpoints();
app.MapEnterpriseThreadOntologyEndpoints();
app.MapEnterpriseThreadImportEndpoints();
app.MapEnterpriseThreadIdentityResolutionEndpoints();
app.MapEnterpriseThreadDataQualityEndpoints();
app.MapEnterpriseThreadGraphMemoryEndpoints();
app.MapEnterpriseThreadDocumentEndpoints();
app.MapEnterpriseThreadGovernedQueryEndpoints();
app.MapEnterpriseThreadAiTraceEndpoints();
app.MapEnterpriseThreadGovernedChatEndpoints();
app.MapEnterpriseThreadExplorerEndpoints();
app.MapEnterpriseThreadDashboardReportEndpoints();
app.MapEnterpriseThreadRecommendationEndpoints();
app.MapEnterpriseThreadReviewTaskEndpoints();
app.MapEnterpriseThreadReviewTaskTemplateEndpoints();
app.MapEnterpriseThreadCapabilityDefinitionEndpoints();
app.MapEnterpriseThreadBusinessPolicyDefinitionEndpoints();
app.MapEnterpriseThreadOptimizationModelDefinitionEndpoints();
app.MapEnterpriseThreadAgentTemplateDefinitionEndpoints();
app.MapEnterpriseThreadAgentTypeDefinitionEndpoints();
app.MapEnterpriseThreadAgentDefinitionEndpoints();
app.MapEnterpriseThreadAgentExecutionEndpoints();
app.MapEnterpriseThreadToolDefinitionEndpoints();
app.MapEnterpriseThreadConnectorDefinitionEndpoints();
app.MapEnterpriseThreadSkillDefinitionEndpoints();
app.MapEnterpriseThreadToolRunEndpoints();
app.MapEnterpriseThreadAgentRunEndpoints();

app.Run();

static async Task SeedDevelopmentIdentityAsync(WebApplication app)
{
    try
    {
        await using var scope = app.Services.CreateAsyncScope();
        var seeder = scope.ServiceProvider.GetRequiredService<IDevelopmentIdentitySeeder>();
        await seeder.SeedAsync(CancellationToken.None);

        var seedOptions = scope.ServiceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<SeedIdentityOptions>>().Value;
        if (seedOptions.InstallReferencePackage)
        {
            var httpContextAccessor = scope.ServiceProvider.GetRequiredService<IHttpContextAccessor>();
            httpContextAccessor.HttpContext = new DefaultHttpContext
            {
                User = new System.Security.Claims.ClaimsPrincipal(
                    new System.Security.Claims.ClaimsIdentity(
                        [new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.NameIdentifier, seedOptions.AdminUserId.ToString())],
                        "DevelopmentSeed"))
            };
            httpContextAccessor.HttpContext.Request.Headers[TenantHeaderNames.TenantId] = seedOptions.TenantId.ToString();
            httpContextAccessor.HttpContext.Request.Headers[TenantHeaderNames.UserId] = seedOptions.AdminUserId.ToString();

            var packageSeeder = scope.ServiceProvider.GetRequiredService<IDevelopmentPackageSeeder>();
            await packageSeeder.SeedAsync(CancellationToken.None);
        }
    }
    catch (Exception exception)
    {
        app.Logger.LogWarning(
            exception,
            "Development identity seed did not complete. Apply EF migrations and ensure local infrastructure is running, then restart the backend.");
    }
}

public partial class Program;
