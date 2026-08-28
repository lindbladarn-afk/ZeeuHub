using WebApp.Models.Integration.Speedrecon;
using WebApp.Services.Application;

namespace WebApp.Services.Integration.Speedrecon;

// Reads and writes Speedrecon state through the active Jeeves SQL connection.
public interface ISpeedreconRepository
{
    Task<SpeedreconProbeResult> ProbeAsync(
        JeevesRuntimeContext runtimeContext,
        DateTime reconDate,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SpeedreconRunPlan>> GetRunPlansAsync(
        JeevesRuntimeContext runtimeContext,
        DateTime reconDate,
        CancellationToken cancellationToken = default);

    Task DeleteResultRowsAsync(
        JeevesRuntimeContext runtimeContext,
        DateTime reconDate,
        IReadOnlyCollection<string> descriptions,
        CancellationToken cancellationToken = default);

    Task ExecuteBatchAsync(
        JeevesRuntimeContext runtimeContext,
        string sql,
        DateTime reconDate,
        CancellationToken cancellationToken = default);

    Task UpdateGeneralLedgerAmountsAsync(
        JeevesRuntimeContext runtimeContext,
        DateTime reconDate,
        CancellationToken cancellationToken = default);

    Task<int> CreateYearAsync(
        JeevesRuntimeContext runtimeContext,
        int fiscalYear,
        CancellationToken cancellationToken = default);

    Task<int> RunStandaloneDepreciationAsync(
        JeevesRuntimeContext runtimeContext,
        DateTime reconDate,
        CancellationToken cancellationToken = default);
}
