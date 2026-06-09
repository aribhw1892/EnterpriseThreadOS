using ETOS.Backend.Health;
using ETOS.Backend.Governance;
using ETOS.Backend.Identity;
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
