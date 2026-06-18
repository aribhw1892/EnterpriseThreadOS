using ETOS.Backend.Ontology;
using ETOS.Backend.Packages;
using Microsoft.Extensions.Options;

namespace ETOS.Backend.Tests.Fixtures;

public static class ReferencePackageTestProfiles
{
    private static readonly Lazy<LoadedReferencePackageManifest> LoadedManifest = new(LoadManifest);

    public static ModelPackageImportProfile ImportProfile => LoadedManifest.Value.ImportProfile;

    public static ModelPackageQueryIntentExtensions QueryIntentExtensions => LoadedManifest.Value.QueryIntentExtensions;

    public static string ImportProfileJson =>
        System.Text.Json.JsonSerializer.Serialize(ImportProfile);

    public static string QueryIntentExtensionsJson =>
        System.Text.Json.JsonSerializer.Serialize(QueryIntentExtensions);

    private static LoadedReferencePackageManifest LoadManifest()
    {
        var packagesRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "packages"));
        var environment = new ReferencePackageTestHostEnvironment(packagesRoot);
        var loader = new ReferencePackageManifestLoader(
            environment,
            Options.Create(new ReferencePackageOptions { RootPath = packagesRoot }));
        return loader.Load(ManufacturingReferencePackageKeys.PackageKey);
    }
}
