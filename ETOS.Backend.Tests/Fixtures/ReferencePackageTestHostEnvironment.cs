using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

namespace ETOS.Backend.Tests.Fixtures;

public sealed class ReferencePackageTestHostEnvironment(string contentRoot) : IWebHostEnvironment
{
    public string ApplicationName { get; set; } = "ETOS.Backend.Tests";
    public IFileProvider WebRootFileProvider { get; set; } = null!;
    public string WebRootPath { get; set; } = contentRoot;
    public string EnvironmentName { get; set; } = "Development";
    public string ContentRootPath { get; set; } = contentRoot;
    public IFileProvider ContentRootFileProvider { get; set; } = null!;
}
