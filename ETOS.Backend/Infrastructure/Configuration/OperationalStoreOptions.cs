namespace ETOS.Backend.Infrastructure.Configuration;

public sealed class OperationalStoreOptions
{
    public const string SectionName = "OperationalStore";

    public string ConnectionString { get; init; } = string.Empty;
}
