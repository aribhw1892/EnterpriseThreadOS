using ETOS.Backend.Identity;
using Microsoft.Extensions.Options;

namespace ETOS.Backend.Packages;

public interface IDevelopmentPackageSeeder
{
    Task SeedAsync(CancellationToken cancellationToken);
}

public sealed class DevelopmentPackageSeeder(
    IOptions<SeedIdentityOptions> identityOptions,
    IReferencePackageInstaller referencePackageInstaller,
    ILogger<DevelopmentPackageSeeder> logger) : IDevelopmentPackageSeeder
{
    public async Task SeedAsync(CancellationToken cancellationToken)
    {
        var seedOptions = identityOptions.Value;
        if (!seedOptions.Enabled || !seedOptions.InstallReferencePackage)
        {
            return;
        }

        try
        {
            await referencePackageInstaller.InstallAsync(
                new InstallReferencePackageRequest(ManufacturingReferencePackageKeys.PackageKey),
                cancellationToken);
            logger.LogInformation(
                "Installed reference package {PackageKey} for development tenant {TenantIdentifier}.",
                ManufacturingReferencePackageKeys.PackageKey,
                seedOptions.TenantIdentifier);
        }
        catch (Exception exception) when (exception is TenantAccessDeniedException or RequestValidationException)
        {
            logger.LogWarning(
                exception,
                "Development reference package seed skipped for tenant {TenantIdentifier}.",
                seedOptions.TenantIdentifier);
        }
    }
}
