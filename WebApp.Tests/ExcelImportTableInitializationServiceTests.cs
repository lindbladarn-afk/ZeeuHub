using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using WebApp.Services.ExcelImport;

namespace WebApp.Tests;

// Verifies that administrative schema DDL is fail-closed in production.
public sealed class ExcelImportTableInitializationServiceTests
{
    [Fact]
    public async Task EnsureImportTablesAsync_BlocksRuntimeDdlInProductionByDefault()
    {
        var connectionResolver = new ThrowingConnectionResolver();
        var service = new ExcelImportTableInitializationService(
            connectionResolver,
            new HttpContextAccessor(),
            NullLogger<ExcelImportTableInitializationService>.Instance,
            new TestWebHostEnvironment { EnvironmentName = "Production" },
            Options.Create(new ExcelImportSchemaInitializationOptions()));

        var result = await service.EnsureImportTablesAsync(CancellationToken.None);

        Assert.False(result.Success);
        Assert.False(connectionResolver.WasCalled);
        Assert.Contains(result.Items, item => item.Message.Contains("releaseprocess", StringComparison.OrdinalIgnoreCase));
    }

    private sealed class ThrowingConnectionResolver : IExcelImportConnectionResolver
    {
        public bool WasCalled { get; private set; }

        public string ResolveConnectionString()
        {
            WasCalled = true;
            throw new InvalidOperationException("A production connection must not be resolved.");
        }
    }

    private sealed class TestWebHostEnvironment : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "WebApp.Tests";
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string WebRootPath { get; set; } = string.Empty;
        public string EnvironmentName { get; set; } = string.Empty;
        public string ContentRootPath { get; set; } = string.Empty;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
