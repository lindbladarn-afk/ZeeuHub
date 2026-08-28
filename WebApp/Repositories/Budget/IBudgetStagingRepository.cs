using System.Collections.Generic;
using System.Threading.Tasks;
using WebApp.Models.Budget;

namespace WebApp.Repositories.Budget
{
    public interface IBudgetStagingRepository
    {
        Task BulkInsertAsync(IEnumerable<PortalBudgetStagingRow> rows, CancellationToken cancellationToken = default);
    }
}
