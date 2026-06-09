namespace ETOS.Backend.Platform.Extensions;

public interface IExtensionPointCatalog
{
    IReadOnlyCollection<ExtensionPoint> List();
}
