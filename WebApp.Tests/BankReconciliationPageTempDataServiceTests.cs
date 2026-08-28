using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using WebApp.Models.Integration;
using WebApp.Services.Integration.BankReconciliation.DemoSession;
using WebApp.Services.Integration.BankReconciliation.Presentation;
using WebApp.Services.Integration.BankReconciliation.UploadFlow;

namespace WebApp.Tests;

// TempData tests cover the MVC feedback bridge for bank reconciliation.
public sealed class BankReconciliationPageTempDataServiceTests
{
    [Fact]
    public void ApplyAndReadFeedback_PreservesUploadAndStatusValues()
    {
        var service = new BankReconciliationPageTempDataService();
        var tempData = CreateTempData();

        service.ApplyUploadResult(tempData, new BankReconciliationUploadFlowResult
        {
            UploadError = "upload-error",
            UploadInfo = "upload-info",
            StatusTone = "success",
            StatusMessage = "status-message"
        });

        var feedback = service.ReadFeedback(tempData);

        Assert.Equal("upload-error", feedback.UploadError);
        Assert.Equal("upload-info", feedback.UploadInfo);
        Assert.Equal("status-message", feedback.StatusMessage);
        Assert.Equal("success", feedback.StatusTone);
    }

    [Fact]
    public void ApplyDemoScenarioResult_UpdatesStatusFields()
    {
        var service = new BankReconciliationPageTempDataService();
        var tempData = CreateTempData();

        service.ApplyDemoScenarioResult(tempData, new BankReconciliationDemoSessionResult
        {
            StatusTone = "info",
            StatusMessage = "demo loaded"
        });

        var feedback = service.ReadFeedback(tempData);

        Assert.Equal("demo loaded", feedback.StatusMessage);
        Assert.Equal("info", feedback.StatusTone);
    }

    [Fact]
    public void ApplySourceError_StoresUploadError()
    {
        var service = new BankReconciliationPageTempDataService();
        var tempData = CreateTempData();

        service.ApplySourceError(tempData, "read error");

        var feedback = service.ReadFeedback(tempData);

        Assert.Equal("read error", feedback.UploadError);
        Assert.Equal("info", feedback.StatusTone);
    }

    private static ITempDataDictionary CreateTempData()
        => new TempDataDictionary(new DefaultHttpContext(), new DummyTempDataProvider());

    private sealed class DummyTempDataProvider : ITempDataProvider
    {
        public IDictionary<string, object?> LoadTempData(HttpContext context)
            => new Dictionary<string, object?>();

        public void SaveTempData(HttpContext context, IDictionary<string, object?> values)
        {
        }
    }
}
