using System.ComponentModel.DataAnnotations;

namespace ETOS.Backend.Infrastructure.Configuration;

public sealed class FrontendOptions
{
    public const string SectionName = "Frontend";

    [MinLength(1)]
    public string[] AllowedOrigins { get; init; } = ["http://localhost:3000"];
}
