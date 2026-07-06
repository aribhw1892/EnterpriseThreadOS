using ETOS.Backend.Tests.Fixtures;

namespace ETOS.Backend.Tests;

[Trait("Category", "Dapr")]
public sealed class DaprWorkflowIntegrationTests
{
    [Fact]
    public async Task ExecuteManufacturingReferenceWorkflowViaDapr_WhenSidecarAvailable()
    {
        if (!string.Equals(Environment.GetEnvironmentVariable("ETOS_DAPR_INTEGRATION"), "1", StringComparison.Ordinal))
        {
            return;
        }

        await using var application = WorkflowExecutionTestSupport.CreateDaprApplication();
        using var client = application.CreateClient();
        var packageContext = await ManufacturingModelPackageFixture.CreatePublishedPackageAsync(client);
        var (artifactId, versionId) = await WorkflowExecutionTestSupport.ResolvePublishedWorkflowAsync(
            application,
            packageContext.TenantId,
            "bom-impact-review");

        var execution = await WorkflowExecutionTestSupport.ExecuteWorkflowAsync(
            client,
            packageContext.TenantId,
            packageContext.UserId,
            artifactId,
            versionId,
            """{"partNumber":"PN-100"}""");

        Assert.False(string.IsNullOrWhiteSpace(execution.Status));
    }
}
