using System;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using WebApp.Services.Vouchers;

namespace WebApp.Controllers
{
    [Authorize(Roles = "Administrator, User")]
    [Route("[controller]")]
    public class VoucherImportController : Controller
    {
        private readonly IVoucherImportService _service;

        public VoucherImportController(IVoucherImportService service)
        {
            _service = service;
        }

        [HttpPost("upload")]
        public async Task<IActionResult> Upload(IFormFile file)
        {
            if (file == null || file.Length == 0)
                return BadRequest("Ingen fil vald.");

            var user = User?.FindFirst(ClaimTypes.Email)?.Value ?? User?.Identity?.Name ?? "unknown";
            var result = await _service.ImportAsync(file, user);
            return Ok(new
            {
                result.ImportBatchId,
                result.TotalRows,
                result.ValidRows,
                InvalidRows = result.InvalidRows,
                Message = "Import mottagen och sparad i q_zu_StagingVoucher."
            });
        }
    }
}
