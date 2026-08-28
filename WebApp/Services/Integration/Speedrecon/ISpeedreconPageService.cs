using Entities.Application;
using WebApp.ViewModels.Integration.Speedrecon;

namespace WebApp.Services.Integration.Speedrecon;

// Builds and mutates the Speedrecon page using the current tenant context.
public interface ISpeedreconPageService
{
    Task<SpeedreconPageViewModel> BuildPageAsync(
        UserSession? user,
        DateTime? reconDate,
        string? statusMessage,
        string? statusTone,
        CancellationToken cancellationToken = default);

    Task<string> RunAsync(
        UserSession? user,
        DateTime reconDate,
        CancellationToken cancellationToken = default);

    Task<string> CreateYearAsync(
        UserSession? user,
        int fiscalYear,
        CancellationToken cancellationToken = default);

    Task<string> RunStandaloneDepreciationAsync(
        UserSession? user,
        DateTime reconDate,
        CancellationToken cancellationToken = default);
}
