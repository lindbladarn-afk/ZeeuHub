using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using WebApp.Models.Voucher;

namespace WebApp.Services.Vouchers
{
    // Imports uploaded voucher files into the staging flow.
    public interface IVoucherImportService
    {
        Task<VoucherImportResult> ImportAsync(IFormFile file, string importedBy, DateTime? postingDate = null, DateTime? reversalDate = null, CancellationToken cancellationToken = default);
    }
}
