using System.Reflection;
using WebApp.Models.Integration.Speedrecon;
using WebApp.Services.Application;
using WebApp.Services.Integration.Speedrecon;
using WebApp.Services.Integration.Speedrecon.Modules;

namespace WebApp.Tests;

// Speedrecon ported module tests guard the modules that no longer depend on procedure source parsing.
public sealed class SpeedreconPortedModuleRunnerTests
{
    [Fact]
    public void ProbeSql_UsesEscapedRowCountAlias()
    {
        var field = typeof(SpeedreconRepository).GetField("ProbeSql", BindingFlags.NonPublic | BindingFlags.Static);
        var sql = Assert.IsType<string>(field?.GetValue(null));

        Assert.Contains("AS [RowCount]", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("AS RowCount", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("kundresk")]
    [InlineData("levresk")]
    [InlineData("inlevejfakt")]
    [InlineData("lego")]
    [InlineData("lagerflytt")]
    [InlineData("utlevejfakt")]
    [InlineData("orderunik")]
    [InlineData("periodisering")]
    [InlineData("anlaggning")]
    [InlineData("pia")]
    [InlineData("lagervarde")]
    [InlineData("intlevresk")]
    public async Task RunAsync_UsesExplicitBatchWithoutCallingSpeedreconProcedure(string moduleKey)
    {
        var repository = new CapturingSpeedreconRepository();
        var runner = BuildRunner(moduleKey, repository);

        await runner.RunAsync(RuntimeContext(), new SpeedreconRunPlan { ReconDate = new DateTime(2026, 7, 6) });

        Assert.NotNull(repository.LastSql);
        Assert.Contains("q_zu_speedrecon_result", repository.LastSql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("EXEC q_zu_speedrecon_", repository.LastSql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("EXEC dbo.q_zu_speedrecon_", repository.LastSql, StringComparison.OrdinalIgnoreCase);
    }

    private static ISpeedreconModuleRunner BuildRunner(string moduleKey, ISpeedreconRepository repository)
        => moduleKey switch
        {
            "kundresk" => new KundreskontraSpeedreconModuleRunner(repository),
            "levresk" => new LeverantorsreskontraSpeedreconModuleRunner(repository),
            "inlevejfakt" => new InlevereratEjFaktureratSpeedreconModuleRunner(repository),
            "lego" => new LegoSpeedreconModuleRunner(repository),
            "lagerflytt" => new LagerflyttSpeedreconModuleRunner(repository),
            "utlevejfakt" => new UtlevereratEjFaktureratSpeedreconModuleRunner(repository),
            "orderunik" => new OrderunikSpeedreconModuleRunner(repository),
            "periodisering" => new PeriodiseringSpeedreconModuleRunner(repository),
            "anlaggning" => new AnlaggningSpeedreconModuleRunner(repository),
            "pia" => new PiaSpeedreconModuleRunner(repository),
            "lagervarde" => new LagervardeSpeedreconModuleRunner(repository),
            "intlevresk" => new InternLeverantorsreskontraSpeedreconModuleRunner(repository),
            _ => throw new ArgumentOutOfRangeException(nameof(moduleKey), moduleKey, null)
        };

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

    private sealed class CapturingSpeedreconRepository : ISpeedreconRepository
    {
        public string? LastSql { get; private set; }

        public Task<SpeedreconProbeResult> ProbeAsync(
            JeevesRuntimeContext runtimeContext,
            DateTime reconDate,
            CancellationToken cancellationToken = default)
            => Task.FromResult(new SpeedreconProbeResult());

        public Task<IReadOnlyList<SpeedreconRunPlan>> GetRunPlansAsync(
            JeevesRuntimeContext runtimeContext,
            DateTime reconDate,
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<SpeedreconRunPlan>>(Array.Empty<SpeedreconRunPlan>());

        public Task DeleteResultRowsAsync(
            JeevesRuntimeContext runtimeContext,
            DateTime reconDate,
            IReadOnlyCollection<string> descriptions,
            CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task ExecuteBatchAsync(
            JeevesRuntimeContext runtimeContext,
            string sql,
            DateTime reconDate,
            CancellationToken cancellationToken = default)
        {
            LastSql = sql;
            return Task.CompletedTask;
        }

        public Task UpdateGeneralLedgerAmountsAsync(
            JeevesRuntimeContext runtimeContext,
            DateTime reconDate,
            CancellationToken cancellationToken = default)
            => Task.CompletedTask;

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
