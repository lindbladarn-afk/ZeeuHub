using WebApp.Models.Integration.Speedrecon;
using WebApp.Services.Application;
using WebApp.Services.Integration.Speedrecon;

namespace WebApp.Tests;

// Speedrecon run tests verify the hub-side orchestration that replaces the SQL start procedure.
public sealed class SpeedreconRunServiceTests
{
    [Fact]
    public async Task RunAsync_ExecutesEnabledModulesAndUpdatesGeneralLedger()
    {
        var repository = new FakeSpeedreconRepository
        {
            Plans =
            [
                new SpeedreconRunPlan
                {
                    ReconDate = new DateTime(2026, 7, 6),
                    Kundreskontra = true,
                    Leverantorsreskontra = false,
                    InlevereratEjFakturerat = true
                }
            ]
        };
        var executedModuleKeys = new List<string>();
        var runners = new ISpeedreconModuleRunner[]
        {
            new FakeModuleRunner(SpeedreconModuleDefinitions.All.Single(item => item.Key == "kundresk"), executedModuleKeys),
            new FakeModuleRunner(SpeedreconModuleDefinitions.All.Single(item => item.Key == "levresk"), executedModuleKeys),
            new FakeModuleRunner(SpeedreconModuleDefinitions.All.Single(item => item.Key == "inlevejfakt"), executedModuleKeys),
            new FakeModuleRunner(SpeedreconModuleDefinitions.All.Single(item => item.Key == "lego"), executedModuleKeys)
        };
        var service = new SpeedreconRunService(repository, runners);

        var outcome = await service.RunAsync(RuntimeContext(), new DateTime(2026, 7, 6), CancellationToken.None);

        Assert.Equal(1, outcome.PlanCount);
        Assert.Equal(3, outcome.ModuleCount);
        Assert.Equal(["KUNDRESK", "INLEVEJFAKT", "LEGO"], repository.DeletedDescriptions);
        Assert.Equal(["kundresk", "inlevejfakt", "lego"], executedModuleKeys);
        Assert.True(repository.GeneralLedgerUpdated);
    }

    private static JeevesRuntimeContext RuntimeContext()
        => new()
        {
            UserId = "user-1",
            CompanyId = Guid.NewGuid(),
            CompanyCode = 100,
            CompanyName = "Testbolag",
            ConnectionString = "Server=example;Database=Jeeves;",
            PersSign = "ZU"
        };

    private sealed class FakeModuleRunner : ISpeedreconModuleRunner
    {
        private readonly List<string> _executedModuleKeys;

        public FakeModuleRunner(SpeedreconModuleDefinition definition, List<string> executedModuleKeys)
        {
            Definition = definition;
            _executedModuleKeys = executedModuleKeys;
        }

        public SpeedreconModuleDefinition Definition { get; }

        public Task RunAsync(
            JeevesRuntimeContext runtimeContext,
            SpeedreconRunPlan plan,
            CancellationToken cancellationToken = default)
        {
            _executedModuleKeys.Add(Definition.Key);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeSpeedreconRepository : ISpeedreconRepository
    {
        public IReadOnlyList<SpeedreconRunPlan> Plans { get; init; } = Array.Empty<SpeedreconRunPlan>();
        public List<string> DeletedDescriptions { get; } = [];
        public bool GeneralLedgerUpdated { get; private set; }

        public Task<SpeedreconProbeResult> ProbeAsync(
            JeevesRuntimeContext runtimeContext,
            DateTime reconDate,
            CancellationToken cancellationToken = default)
            => Task.FromResult(new SpeedreconProbeResult());

        public Task<IReadOnlyList<SpeedreconRunPlan>> GetRunPlansAsync(
            JeevesRuntimeContext runtimeContext,
            DateTime reconDate,
            CancellationToken cancellationToken = default)
            => Task.FromResult(Plans);

        public Task DeleteResultRowsAsync(
            JeevesRuntimeContext runtimeContext,
            DateTime reconDate,
            IReadOnlyCollection<string> descriptions,
            CancellationToken cancellationToken = default)
        {
            DeletedDescriptions.AddRange(descriptions);
            return Task.CompletedTask;
        }

        public Task ExecuteBatchAsync(
            JeevesRuntimeContext runtimeContext,
            string sql,
            DateTime reconDate,
            CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task UpdateGeneralLedgerAmountsAsync(
            JeevesRuntimeContext runtimeContext,
            DateTime reconDate,
            CancellationToken cancellationToken = default)
        {
            GeneralLedgerUpdated = true;
            return Task.CompletedTask;
        }

        public Task<int> CreateYearAsync(
            JeevesRuntimeContext runtimeContext,
            int fiscalYear,
            CancellationToken cancellationToken = default)
            => Task.FromResult(0);

        public Task<int> RunStandaloneDepreciationAsync(
            JeevesRuntimeContext runtimeContext,
            DateTime reconDate,
            CancellationToken cancellationToken = default)
            => Task.FromResult(0);
    }
}
