using System.Threading;
using System.Threading.Tasks;
using WebApp.Models.AI;

namespace WebApp.Services.Application.AI
{
    public interface IAiSqlExecutor
    {
        Task<SqlQueryResult> ExecuteSelectAsync(
        string connectionString,
        string sql,
        int maxRows = 200,
        CancellationToken ct = default,
        bool allowSchemaIntrospection = false);
    }
}
