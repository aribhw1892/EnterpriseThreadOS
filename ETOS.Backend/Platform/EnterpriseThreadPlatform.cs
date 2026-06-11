using ETOS.Backend.Artifacts;
using ETOS.Backend.Classification;
using ETOS.Backend.DataQuality;
using ETOS.Backend.GraphMemory;
using ETOS.Backend.Health;
using ETOS.Backend.Governance;
using ETOS.Backend.Identity;
using ETOS.Backend.Imports;
using ETOS.Backend.IdentityResolution;
using ETOS.Backend.Infrastructure.Configuration;
using ETOS.Backend.Infrastructure.Persistence;
using ETOS.Backend.Ontology;
using ETOS.Backend.Platform.Extensions;
using ETOS.Backend.Tenancy;
using Finbuckle.MultiTenant.AspNetCore.Extensions;
using Finbuckle.MultiTenant.Extensions;
using Microsoft.AspNetCore.Authentication;
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

        services.AddOptions<SeedIdentityOptions>()
            .Bind(configuration.GetSection(SeedIdentityOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddOptions<ImportFileStorageOptions>()
            .Bind(configuration.GetSection(ImportFileStorageOptions.SectionName))
            .ValidateOnStart();

        services.AddEnterpriseThreadGraphMemory(configuration);

        services.AddDbContext<EnterpriseThreadDbContext>((serviceProvider, options) =>
        {
            var storeOptions = serviceProvider.GetRequiredService<IOptions<OperationalStoreOptions>>().Value;
            options.UseNpgsql(storeOptions.ConnectionString);
        });

        services.AddIdentityCore<EtosUser>(options =>
            {
                options.Password.RequiredLength = 8;
                options.Password.RequireDigit = false;
                options.Password.RequireLowercase = false;
                options.Password.RequireUppercase = false;
                options.Password.RequireNonAlphanumeric = false;
                options.Lockout.AllowedForNewUsers = true;
                options.Lockout.MaxFailedAccessAttempts = 10;
            })
            .AddRoles<EtosIdentityRole>()
            .AddEntityFrameworkStores<EnterpriseThreadDbContext>();

        services.AddAuthentication(LocalHeaderAuthenticationHandler.SchemeName)
            .AddScheme<AuthenticationSchemeOptions, LocalHeaderAuthenticationHandler>(
                LocalHeaderAuthenticationHandler.SchemeName,
                _ => { });

        services.AddAuthorization();

        services.AddHttpContextAccessor();
        services.AddMultiTenant<EtosTenantInfo>()
            .WithHeaderStrategy(TenantHeaderNames.TenantId)
            .WithStore<EtosTenantStore>(ServiceLifetime.Scoped);

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
        services.AddScoped<IIdentityAdminService, IdentityAdminService>();
        services.AddScoped<ITenantContextResolver, TenantContextResolver>();
        services.AddScoped<IAccessPermissionService, AccessPermissionService>();
        services.AddScoped<IAccessDenialRecorder, AccessDenialRecorder>();
        services.AddScoped<IAuditRecorder, AuditRecorder>();
        services.AddScoped<IAuditExplorerService, AuditExplorerService>();
        services.AddScoped<IClassificationPolicyService, ClassificationPolicyService>();
        services.AddScoped<IArtifactRegistryService, ArtifactRegistryService>();
        services.AddScoped<IOntologyService, OntologyService>();
        services.AddScoped<IImportFileStorage, LocalImportFileStorage>();
        services.AddScoped<IImportFileParser, CsvImportFileParser>();
        services.AddScoped<IImportService, ImportService>();
        services.AddScoped<IIdentityResolutionService, IdentityResolutionService>();
        services.AddScoped<IDataQualityIssueService, DataQualityIssueService>();
        services.AddScoped<IDevelopmentIdentitySeeder, DevelopmentIdentitySeeder>();

        return services;
    }
}
