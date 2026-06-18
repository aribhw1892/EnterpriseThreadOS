using ETOS.Backend.Ontology;

namespace ETOS.Backend.Packages;

public sealed record InstallReferencePackageRequest(string PackageKey);

public sealed record InstalledReferenceArtifactResponse(
    string ArtifactKind,
    string Key,
    Guid ArtifactId,
    Guid VersionId);

public sealed record InstallReferencePackageResponse(
    string PackageKey,
    bool AlreadyInstalled,
    ModelPackageVersionResponse ModelPackage,
    IReadOnlyCollection<InstalledReferenceArtifactResponse> Artifacts,
    string Summary);
