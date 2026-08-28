using System.Threading;
using System.Threading.Tasks;
using WebApp.Models.AI;

namespace WebApp.Services.Application.AI;

// Handles invoice-related AI shortcuts that should reuse the invoice module instead of free-form SQL.
public interface IAiInvoiceQuestionService
{
    Task<AiQueryResponse?> TryAnswerAsync(
        string question,
        string connectionString,
        int? companyCode,
        CancellationToken ct = default);
}
