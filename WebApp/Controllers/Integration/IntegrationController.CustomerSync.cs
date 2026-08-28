// Handles Customer Sync pages and operational commands within the existing integration routes.
using Microsoft.AspNetCore.Mvc;
using WebApp.Services.Integration;
using WebApp.Services.Integration.CustomerSync.Application;
using WebApp.Services.Integration.CustomerSync.Presentation;
using WebApp.ViewModels.Integration.CustomerSync;

namespace WebApp.Controllers
{
    public partial class IntegrationController
    {
        private static readonly Guid CustomerSyncSubModuleId = Guid.Parse("0f5c9db5-5b7b-4a2f-9d51-3e2c9a1b8a44");
        private readonly CustomerSyncPagePresenter _customerSyncPagePresenter;
        private readonly ICustomerSyncRuntimeConfigurationService _customerSyncRuntimeConfigurationService;
        private readonly ICustomerSyncHubSpotImportService _customerSyncHubSpotImportService;

        [HttpGet]
        public async Task<IActionResult> CustomerSync(int importedPage = 1)
        {
            if (!await HasCustomerSyncAccessAsync())
                return Forbid();

            var model = await BuildCustomerSyncPageModelAsync(importedPage);
            return View("~/Views/Integration/CustomerSync/CustomerSync.cshtml", model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CustomerSyncImportHubSpotCompanies(CancellationToken cancellationToken)
        {
            if (!await HasCustomerSyncAccessAsync())
                return Forbid();

            try
            {
                var result = await _customerSyncHubSpotImportService.ImportCompaniesAsync(cancellationToken);
                TempData["CustomerSyncStatusTone"] = result.ImportedCount > 0 ? "info" : "warning";
                TempData["CustomerSyncStatusMessage"] = result.Summary;
            }
            catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
            {
                TempData["CustomerSyncStatusTone"] = "warning";
                TempData["CustomerSyncStatusMessage"] = $"HubSpot-företag kunde inte hämtas: {IntegrationLogSanitizer.Diagnostic(ex.Message)}";
            }

            return RedirectToAction(nameof(CustomerSync));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CustomerSyncSetEnabled(bool enabled, CancellationToken cancellationToken)
        {
            if (!await HasCustomerSyncAccessAsync())
                return Forbid();

            try
            {
                var runtime = await _customerSyncRuntimeConfigurationService.GetRuntimeConfigurationAsync(cancellationToken);
                runtime.Enabled = enabled;
                await _customerSyncRuntimeConfigurationService.SaveRuntimeConfigurationAsync(runtime, cancellationToken);

                TempData["CustomerSyncStatusTone"] = "info";
                TempData["CustomerSyncStatusMessage"] = enabled
                    ? "CustomerSync är aktiverad för automatisk körning igen."
                    : "CustomerSync är pausad för automatisk körning.";
            }
            catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
            {
                TempData["CustomerSyncStatusTone"] = "warning";
                TempData["CustomerSyncStatusMessage"] = $"CustomerSync kunde inte uppdateras: {IntegrationLogSanitizer.Diagnostic(ex.Message)}";
            }

            return RedirectToAction(nameof(CustomerSync));
        }

        private Task<bool> HasCustomerSyncAccessAsync()
            => HasCompanyPermissionAsync(CustomerSyncSubModuleId);

        private async Task<CustomerSyncPageViewModel> BuildCustomerSyncPageModelAsync(
            int importedPage = 1,
            CancellationToken cancellationToken = default)
        {
            var options = await _customerSyncRuntimeConfigurationService.GetEffectiveOptionsAsync(cancellationToken);
            return await _customerSyncPagePresenter.BuildAsync(options, importedPage, cancellationToken);
        }
    }
}
