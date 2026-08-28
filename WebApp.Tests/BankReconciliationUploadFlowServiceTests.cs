using Entities.Application;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Localization;
using WebApp.Services;
using WebApp.Services.Integration.BankReconciliation.Upload;
using WebApp.Services.Integration.BankReconciliation.UploadFlow;
using WebApp.ViewModels.Shared;

namespace WebApp.Tests;

// Upload flow tests cover session-bound camt source activation without MVC.
public sealed class BankReconciliationUploadFlowServiceTests
{
    [Fact]
    public async Task Upload_AcceptsNdaAndStoresPreparedFileInSession()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Features.Set<ISessionFeature>(new TestSessionFeature());
        httpContext.Session.Set("UserObject", new UserSession
        {
            CompanyId = Guid.Parse("11111111-1111-1111-1111-111111111111")
        });
        var uploadService = new FakeCamtUploadService
        {
            Result = new BankReconciliationCamtUploadResult
            {
                Success = true,
                StoredFilePath = "/tmp/statement.xml",
                TransactionCount = 1
            }
        };
        var service = new BankReconciliationUploadFlowService(
            new HttpContextAccessor { HttpContext = httpContext },
            uploadService,
            new DummyStringLocalizer());
        var file = CreateFormFile("content", "statement.nda");

        var result = await service.UploadAsync(file, CancellationToken.None);

        Assert.Null(result.UploadError);
        Assert.Equal("Integration_FileUploaded", result.UploadInfo);
        Assert.Equal("/tmp/statement.xml", httpContext.Session.GetString("BankRec.UploadedCamtFile"));
        Assert.Equal("statement.nda", httpContext.Session.GetString("BankRec.UploadedCamtDisplayName"));
        Assert.Equal("11111111111111111111111111111111", httpContext.Session.GetString("BankRec.UploadedCamtCompanyId"));
        Assert.Equal("statement.nda", service.ResolveLatestCamtDisplayName());
        Assert.Equal("statement.nda", uploadService.CapturedFile?.FileName);
    }

    [Fact]
    public async Task ResolveLatestCamtFile_RejectsUploadAfterCompanySwitch()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Features.Set<ISessionFeature>(new TestSessionFeature());
        httpContext.Session.Set("UserObject", new UserSession { CompanyId = Guid.Parse("11111111-1111-1111-1111-111111111111") });
        var service = new BankReconciliationUploadFlowService(
            new HttpContextAccessor { HttpContext = httpContext },
            new FakeCamtUploadService { Result = new BankReconciliationCamtUploadResult { Success = true, StoredFilePath = "/tmp/statement.xml" } },
            new DummyStringLocalizer());

        await service.UploadAsync(CreateFormFile("content", "statement.nda"), CancellationToken.None);
        httpContext.Session.Set("UserObject", new UserSession { CompanyId = Guid.Parse("22222222-2222-2222-2222-222222222222") });

        Assert.Null(service.ResolveLatestCamtFile());
        Assert.Null(service.ResolveLatestCamtDisplayName());
    }

    [Fact]
    public async Task Upload_RejectsUnsupportedExtensionBeforeStaging()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Features.Set<ISessionFeature>(new TestSessionFeature());
        var uploadService = new FakeCamtUploadService();
        var service = new BankReconciliationUploadFlowService(
            new HttpContextAccessor { HttpContext = httpContext },
            uploadService,
            new DummyStringLocalizer());
        var file = CreateFormFile("content", "statement.txt");

        var result = await service.UploadAsync(file, CancellationToken.None);

        Assert.Equal("Integration_InvalidFileFormatOnlyXml", result.UploadError);
        Assert.Null(uploadService.CapturedFile);
    }

    private static IFormFile CreateFormFile(string content, string fileName)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(content);
        return new FormFile(new MemoryStream(bytes), 0, bytes.Length, "file", fileName)
        {
            Headers = new HeaderDictionary(),
            ContentType = "application/octet-stream"
        };
    }

    private sealed class FakeCamtUploadService : IBankReconciliationCamtUploadService
    {
        public IFormFile? CapturedFile { get; private set; }
        public BankReconciliationCamtUploadResult Result { get; set; } = new();

        public Task<BankReconciliationCamtUploadResult> PrepareUploadAsync(
            IFormFile file,
            Guid companyId,
            string sessionId,
            string? previousFilePath,
            CancellationToken cancellationToken = default)
        {
            CapturedFile = file;
            return Task.FromResult(Result);
        }
    }

    private sealed class DummyStringLocalizer : IStringLocalizer<SharedResources>
    {
        public LocalizedString this[string name] => new(name, name);
        public LocalizedString this[string name, params object[] arguments] => new(name, name);
        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => Array.Empty<LocalizedString>();
    }

    private sealed class TestSessionFeature : ISessionFeature
    {
        public ISession Session { get; set; } = new TestSession();
    }

    private sealed class TestSession : ISession
    {
        private readonly Dictionary<string, byte[]> _values = new(StringComparer.Ordinal);

        public bool IsAvailable => true;
        public string Id { get; } = Guid.NewGuid().ToString("N");
        public IEnumerable<string> Keys => _values.Keys;

        public void Clear() => _values.Clear();
        public Task CommitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task LoadAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public void Remove(string key) => _values.Remove(key);
        public void Set(string key, byte[] value) => _values[key] = value;
        public bool TryGetValue(string key, out byte[]? value) => _values.TryGetValue(key, out value);
    }
}
