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
using ETOS.Backend.Recommendations;
using ETOS.Backend.Platform;
using Finbuckle.MultiTenant.AspNetCore.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddEnterpriseThreadPlatform(builder.Configuration);

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
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

app.Run();

static async Task SeedDevelopmentIdentityAsync(WebApplication app)
{
    try
    {
        await using var scope = app.Services.CreateAsyncScope();
        var seeder = scope.ServiceProvider.GetRequiredService<IDevelopmentIdentitySeeder>();
        await seeder.SeedAsync(CancellationToken.None);
    }
    catch (Exception exception)
    {
        app.Logger.LogWarning(
            exception,
            "Development identity seed did not complete. Apply EF migrations and ensure local infrastructure is running, then restart the backend.");
    }
}

public partial class Program;
