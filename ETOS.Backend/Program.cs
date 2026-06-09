using ETOS.Backend.Health;
using ETOS.Backend.Platform;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddEnterpriseThreadPlatform(builder.Configuration);

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseCors(EnterpriseThreadPlatform.CorsPolicyName);

app.MapGet("/", () => Results.Redirect("/health/app"));
app.MapEnterpriseThreadHealthEndpoints();

app.Run();

public partial class Program;
