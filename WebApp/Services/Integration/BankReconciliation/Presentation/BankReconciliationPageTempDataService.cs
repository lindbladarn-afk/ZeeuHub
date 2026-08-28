using Microsoft.AspNetCore.Mvc.ViewFeatures;
using WebApp.Models.Integration;
using WebApp.Services.Integration.BankReconciliation.DemoSession;
using WebApp.Services.Integration.BankReconciliation.UploadFlow;

namespace WebApp.Services.Integration.BankReconciliation.Presentation;

// Translates TempData values to and from the bank reconciliation page contract.
public sealed class BankReconciliationPageTempDataService : IBankReconciliationPageTempDataService
{
    private const string UploadErrorKey = "BankRecUploadError";
    private const string UploadInfoKey = "BankRecUploadInfo";
    private const string StatusMessageKey = "BankRecStatusMessage";
    private const string StatusToneKey = "BankRecStatusTone";

    public BankReconciliationPageFeedback ReadFeedback(ITempDataDictionary tempData)
    {
        return new BankReconciliationPageFeedback
        {
            UploadError = tempData[UploadErrorKey] as string,
            UploadInfo = tempData[UploadInfoKey] as string,
            StatusMessage = tempData[StatusMessageKey] as string,
            StatusTone = tempData[StatusToneKey] as string ?? "info"
        };
    }

    public void ApplyUploadResult(ITempDataDictionary tempData, BankReconciliationUploadFlowResult result)
    {
        if (!string.IsNullOrWhiteSpace(result.UploadError))
        {
            tempData[UploadErrorKey] = result.UploadError;
        }

        if (!string.IsNullOrWhiteSpace(result.UploadInfo))
        {
            tempData[UploadInfoKey] = result.UploadInfo;
        }

        if (!string.IsNullOrWhiteSpace(result.StatusTone))
        {
            tempData[StatusToneKey] = result.StatusTone;
        }

        if (!string.IsNullOrWhiteSpace(result.StatusMessage))
        {
            tempData[StatusMessageKey] = result.StatusMessage;
        }
    }

    public void ApplyDemoScenarioResult(ITempDataDictionary tempData, BankReconciliationDemoSessionResult result)
    {
        if (!string.IsNullOrWhiteSpace(result.StatusTone))
        {
            tempData[StatusToneKey] = result.StatusTone;
        }

        if (!string.IsNullOrWhiteSpace(result.StatusMessage))
        {
            tempData[StatusMessageKey] = result.StatusMessage;
        }
    }

    public void ApplySourceError(ITempDataDictionary tempData, string errorMessage)
    {
        if (!string.IsNullOrWhiteSpace(errorMessage))
        {
            tempData[UploadErrorKey] = errorMessage;
        }
    }
}
