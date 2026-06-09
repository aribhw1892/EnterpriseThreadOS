using ETOS.Backend.Health;
using ETOS.Backend.Infrastructure.Configuration;
using ETOS.Backend.Infrastructure.Persistence;
using ETOS.Backend.Platform.Extensions;
using ETOS.Backend.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ETOS.Backend.Platform;

public static class EnterpriseThreadPlatform
{
    public const string CorsPolicyName = "frontend-shell";

    public static IServiceCollection AddEnterpriseThreadPlatform(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOptions<OperationalStoreOptions>()
            .Bind(configuration.GetSection(OperationalStoreOptions.SectionName))
            .Validate(options => !string.IsNullOrWhiteSpace(options.ConnectionString), "PostgreSQL connection string is required.")
            .ValidateOnStart();

        services.AddOptions<InfrastructureHealthOptions>()
            .Bind(configuration.GetSection(InfrastructureHealthOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddOptions<FrontendOptions>()
            .Bind(configuration.GetSection(FrontendOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddDbContext<EnterpriseThreadDbContext>((serviceProvider, options) =>
        {
            var storeOptions = serviceProvider.GetRequiredService<IOptions<OperationalStoreOptions>>().Value;
            options.UseNpgsql(storeOptions.ConnectionString);
        });

        services.AddCors(options =>
        {
            options.AddPolicy(CorsPolicyName, policy =>
            {
                var frontendOptions = configuration.GetSection(FrontendOptions.SectionName).Get<FrontendOptions>() ?? new FrontendOptions();
                policy.WithOrigins(frontendOptions.AllowedOrigins)
                    .AllowAnyHeader()
                    .AllowAnyMethod();
            });
        });

        services.AddHttpClient<IInfrastructureHealthService, InfrastructureHealthService>();
        services.AddSingleton<ITenantScopeValidator, TenantScopeValidator>();
        services.AddSingleton<IExtensionPointCatalog, StaticExtensionPointCatalog>();

        return services;
    }
}
