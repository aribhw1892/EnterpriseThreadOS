namespace ETOS.Backend.Packages;

public sealed class ReferencePackageOptions
{
    public const string SectionName = "ReferencePackages";

    /// <summary>
    /// Optional absolute or relative path to the packages root directory.
    /// When unset, the host resolves ../packages from ContentRootPath.
    /// </summary>
    public string? RootPath { get; set; }
}
