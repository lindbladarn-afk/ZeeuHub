// Defines the database-chat workflow and its optional real-time progress callback.
using System.Threading;
using System.Threading.Tasks;
using WebApp.Models.AI;

namespace WebApp.Services.Application.AI;

public interface IAiDbChatOrchestrator
{
    Task<AiQueryResponse> AskDatabaseAsync(
        AiQueryRequest request,
        AiProgressCallback? progress = null,
        CancellationToken ct = default);
}
