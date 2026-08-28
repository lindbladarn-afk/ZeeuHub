// Handles the tenant-aware order matching entry points for integration users.
using Microsoft.AspNetCore.Mvc;
using WebApp.ViewModels.Shared;

namespace WebApp.Controllers
{
    public partial class IntegrationController
    {
        private static readonly Guid SubModuleOrderMatchingId = Guid.Parse("e6f41a9a-3b0a-4f7b-9d1a-1c6e2f3c9b7a");
        private static readonly Guid SubModuleOngoingId = Guid.Parse("7b5f1d3f-7f1a-4f93-9f4f-8d0f4a3c2b10");

        public async Task<IActionResult> OrderMatching()
        {
            if (!await HasCompanyPermissionAnyAsync(SubModuleOrderMatchingId, SubModuleOngoingId))
                return Forbid();

            await PopulateOrderMatchingRuntimeBannerAsync(HttpContext.RequestAborted);
            return View("~/Views/Integration/OrderMatching/OrderMatching.cshtml");
        }

        [HttpGet]
        public async Task<IActionResult> Centra()
        {
            if (!await HasCompanyPermissionAnyAsync(SubModuleOrderMatchingId, SubModuleOngoingId))
                return Forbid();

            await PopulateOrderMatchingRuntimeBannerAsync(HttpContext.RequestAborted);
            return View("~/Views/Integration/OrderMatching/OrderMatching.cshtml");
        }

        private async Task PopulateOrderMatchingRuntimeBannerAsync(CancellationToken cancellationToken)
        {
            var user = GetFlowEngineSessionUser();
            var runtimeContext = await ResolveRuntimeContextAsync(user, cancellationToken);
            if (!runtimeContext.Success || runtimeContext.Value is null)
            {
                ViewBag.OrderMatchingRuntimeBanner = BuildTenantDataUnavailableBanner(
                    "Matchning mot Jeeves kräver tenantdata för valt bolag.",
                    "Du kan fortfarande öppna integrationssidan och kontrollera externa kopplingar, men ordermatchningen mot Jeeves kan inte laddas just nu.",
                    runtimeContext.Error);
            }
        }

        private static ModuleBannerViewModel BuildTenantDataUnavailableBanner(string message, string note, string? detail)
        {
            return new ModuleBannerViewModel
            {
                Title = "Tenantdata från Jeeves är tillfälligt otillgänglig",
                Message = message,
                Note = string.IsNullOrWhiteSpace(detail) ? note : $"{note} {detail}",
                Tone = "warning",
                IconClass = "fa fa-plug-circle-xmark"
            };
        }
    }
}
