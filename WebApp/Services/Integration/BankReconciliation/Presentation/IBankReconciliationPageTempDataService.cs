using Microsoft.AspNetCore.Mvc.ViewFeatures;
using WebApp.Models.Integration;
using WebApp.Services.Integration.BankReconciliation.DemoSession;
using WebApp.Services.Integration.BankReconciliation.UploadFlow;

namespace WebApp.Services.Integration.BankReconciliation.Presentation;

// Owns TempData mapping for the bank reconciliation page flow.
public interface IBankReconciliationPageTempDataService
{
    BankReconciliationPageFeedback ReadFeedback(ITempDataDictionary tempData);

    void ApplyUploadResult(ITempDataDictionary tempData, BankReconciliationUploadFlowResult result);

    void ApplyDemoScenarioResult(ITempDataDictionary tempData, BankReconciliationDemoSessionResult result);

    void ApplySourceError(ITempDataDictionary tempData, string errorMessage);
}
