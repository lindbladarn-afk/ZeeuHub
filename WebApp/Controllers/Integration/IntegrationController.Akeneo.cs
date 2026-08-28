// Handles the Akeneo export page and its protected XML export command.
using Microsoft.AspNetCore.Mvc;
using WebApp.Services.Integration.Akeneo;

namespace WebApp.Controllers
{
    public partial class IntegrationController
    {
        private static readonly Guid SubModuleAkeneoExportId = Guid.Parse("5f75d5c0-8e1e-4b6a-a24f-0b1a7f5b7c2d");
        private readonly IAkeneoExportService _akeneoExportService;

        [HttpGet]
        public async Task<IActionResult> AkeneoExport()
        {
            if (!await HasCompanyPermissionAsync(SubModuleAkeneoExportId))
                return Forbid();

            ViewBag.AkeneoError = TempData["AkeneoError"];
            return View("~/Views/Integration/Akeneo/AkeneoExport.cshtml");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AkeneoExport(int? limit, string? fileName, CancellationToken cancellationToken)
        {
            if (!await HasCompanyPermissionAsync(SubModuleAkeneoExportId))
                return Forbid();

            var safeLimit = limit.GetValueOrDefault(100);

            try
            {
                var result = await _akeneoExportService.ExportProductsXmlAsync(safeLimit, fileName, cancellationToken);
                var bytes = System.Text.Encoding.UTF8.GetBytes(result.Xml);
                return File(bytes, "application/xml", result.FileName);
            }
            catch (Exception ex)
            {
                TempData["AkeneoError"] = ex.Message;
                return RedirectToAction(nameof(AkeneoExport));
            }
        }
    }
}
