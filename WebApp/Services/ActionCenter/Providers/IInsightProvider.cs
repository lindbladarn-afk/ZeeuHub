using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Entities.Application;
using WebApp.Models.ActionCenter;
using WebApp.Services.Application;

namespace WebApp.Services.ActionCenter;

/// <summary>
/// Contract for generating action-center insights. Each provider encapsulates en affärsregel.
/// </summary>
public interface IInsightProvider
{
    string ProviderKey { get; }
    ActionCenterAudience Audience { get; }
    Task<IEnumerable<ActionCenterInsight>> GetInsightsAsync(UserSession user, JeevesRuntimeContext? runtimeContext, CancellationToken cancellationToken);
}
