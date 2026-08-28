using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;

namespace WebApp.Tests;

internal sealed class TestHostEnvironment : IWebHostEnvironment
{
    public string ApplicationName { get; set; } = "WebApp.Tests";
    public IFileProvider WebRootFileProvider { get; set; } = null!;
    public string WebRootPath { get; set; } = string.Empty;
    public string EnvironmentName { get; set; } = "Development";
    public string ContentRootPath { get; set; } = string.Empty;
    public IFileProvider ContentRootFileProvider { get; set; } = null!;
}
