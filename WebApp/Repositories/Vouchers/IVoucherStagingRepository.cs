using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using WebApp.Models.Voucher;

namespace WebApp.Repositories.Vouchers
{
    public interface IVoucherStagingRepository
    {
        Task BulkInsertAsync(IEnumerable<PortalVoucherStagingRow> rows, CancellationToken cancellationToken = default);
    }
}
