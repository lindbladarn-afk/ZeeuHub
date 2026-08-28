using Entities.Application;
using WebApp.Models.Integration;

namespace WebApp.Services.Integration.FlowEngine;

public interface IFlowEngineModuleService
{
    Task<FlowEngineModuleViewModel> BuildModuleViewModelAsync(
        UserSession? sessionUser,
        string? activeSection,
        Guid? selectedJobId,
        int historyPage,
        FlowEngineHistoryFilterState? historyFilters,
        FlowEngineWorkbenchSettingsState? workbenchSettings,
        CancellationToken cancellationToken = default);
}
