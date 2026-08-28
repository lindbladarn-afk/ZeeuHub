using Microsoft.AspNetCore.Mvc;
using WebApp.Services.Admin.ApprovalChains;
using WebApp.ViewModels.Admin.ApprovalChains;

namespace WebApp.Controllers;

// Keeps the legacy admin route alive while the approval-chain designer now lives under Web Approval.
public partial class AdminController
{
    [HttpGet]
    public IActionResult ApprovalChains()
    {
        return RedirectToAction("AttestChains", "WebApproval");
    }

    [HttpGet]
    public async Task<IActionResult> PurchaseApprovalParity(CancellationToken cancellationToken)
    {
        var runtimeContext = await ResolveCurrentRuntimeContextAsync(cancellationToken);

        return View("~/Views/Admin/ApprovalChains/PurchaseApprovalParity.cshtml", new ApprovalChainPurchaseParityPageViewModel
        {
            CompanyCode = runtimeContext is null ? null : (short)runtimeContext.CompanyCode,
            PurchaseOrderNumber = null,
            FlowId = 0,
            CurrentApproverPersSign = runtimeContext?.PersSign ?? string.Empty,
            RuntimeCompanyName = runtimeContext?.CompanyName
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> PurchaseApprovalParity(ApprovalChainPurchaseParityPageViewModel model, CancellationToken cancellationToken)
    {
        var runtimeContext = await ResolveCurrentRuntimeContextAsync(cancellationToken);
        model.RuntimeCompanyName = runtimeContext?.CompanyName;

        if (runtimeContext is null)
        {
            ModelState.AddModelError(string.Empty, "Kunde inte läsa aktuell Jeeves-koppling för användaren.");
        }

        if (model.CompanyCode is null)
        {
            ModelState.AddModelError(nameof(model.CompanyCode), "Företagskod är obligatorisk.");
        }

        if (model.PurchaseOrderNumber is null)
        {
            ModelState.AddModelError(nameof(model.PurchaseOrderNumber), "Beställningsnummer är obligatoriskt.");
        }

        if (string.IsNullOrWhiteSpace(model.CurrentApproverPersSign))
        {
            ModelState.AddModelError(nameof(model.CurrentApproverPersSign), "PersSign är obligatoriskt.");
        }

        if (!ModelState.IsValid || runtimeContext is null || model.CompanyCode is null || model.PurchaseOrderNumber is null || string.IsNullOrWhiteSpace(model.CurrentApproverPersSign))
        {
            return View("~/Views/Admin/ApprovalChains/PurchaseApprovalParity.cshtml", model);
        }

        var result = await _approvalChainPurchaseParityService.CompareAsync(
            new ApprovalChainPurchaseParityRequest(
                runtimeContext.ConnectionString,
                model.CompanyCode.Value,
                model.PurchaseOrderNumber.Value,
                model.FlowId,
                model.CurrentApproverPersSign.Trim()),
            cancellationToken);

        model.Result = result;
        return View("~/Views/Admin/ApprovalChains/PurchaseApprovalParity.cshtml", model);
    }
}
