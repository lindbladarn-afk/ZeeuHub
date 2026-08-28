using Entities.Application;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using WebApp.Helpers;
using WebApp.Models.Integration.Speedrecon;
using WebApp.Services.Application;
using WebApp.Services.Integration.Speedrecon;

namespace WebApp.Tests;

// Speedrecon page tests cover tenant-safe runtime handling and hub-owned execution.
public sealed class SpeedreconPageServiceTests
{
    [Fact]
    public async Task BuildPageAsync_ReturnsSafeBanner_WhenRuntimeContextFails()
    {
        var repository = new FakeSpeedreconRepository();
        var service = BuildService(new FailingRuntimeContextService(), repository);

        var model = await service.BuildPageAsync(
            new UserSession { UserId = "user-1", CompanyId = Guid.NewGuid() },
            new DateTime(2026, 7, 6),
            null,
            null,
            CancellationToken.None);

        Assert.False(model.Probe.RuntimeAvailable);
        Assert.NotNull(model.RuntimeBanner);
        Assert.Equal("warning", model.RuntimeBanner!.Tone);
        Assert.DoesNotContain("authorization=secret-value", model.RuntimeBanner.Message, StringComparison.OrdinalIgnoreCase);
        Assert.False(repository.ProbeCalled);
    }

    [Fact]
    public async Task BuildPageAsync_MapsProbeAndEnablesRun_WhenRequiredTablesExist()
    {
        var repository = new FakeSpeedreconRepository
        {
            ProbeResult = new SpeedreconProbeResult
            {
                RuntimeAvailable = true,
                IsEnabledInJeeves = true,
                CompanyCode = 100,
                CompanyName = "Testbolag",
                PersSign = "ZU",
                Objects =
                [
                    new SpeedreconObjectStatus { ObjectName = "q_zu_speedrecon", ObjectType = "Table", Exists = true },
                    new SpeedreconObjectStatus { ObjectName = "q_zu_speedrecon_result", ObjectType = "Table", Exists = true }
                ]
            }
        };
        var service = BuildService(new SuccessfulRuntimeContextService(), repository);

        var model = await service.BuildPageAsync(
            new UserSession { UserId = "user-1", CompanyId = Guid.NewGuid() },
            new DateTime(2026, 7, 6, 13, 45, 0),
            "klart",
            "info",
            CancellationToken.None);

        Assert.True(repository.ProbeCalled);
        Assert.Equal(new DateTime(2026, 7, 6), repository.ProbeReconDate);
        Assert.Equal("klart", model.StatusMessage);
        Assert.True(model.CanRun);
        Assert.Null(model.RuntimeBanner);
    }

    [Fact]
    public async Task RunAsync_DelegatesToHubRunService_WithNormalizedDate()
    {
        var repository = new FakeSpeedreconRepository();
        var runService = new FakeSpeedreconRunService();
        var service = BuildService(new SuccessfulRuntimeContextService(), repository, runService);

        var message = await service.RunAsync(
            new UserSession { UserId = "user-1", CompanyId = Guid.NewGuid() },
            new DateTime(2026, 7, 6, 14, 30, 0),
            CancellationToken.None);

        Assert.True(runService.RunCalled);
        Assert.Equal(new DateTime(2026, 7, 6), runService.RunReconDate);
        Assert.Contains("2026-07-06", message);
    }

    [Fact]
    public async Task CreateYearAsync_DelegatesToRepository()
    {
        var repository = new FakeSpeedreconRepository { CreateYearRows = 12 };
        var service = BuildService(new SuccessfulRuntimeContextService(), repository);

        var message = await service.CreateYearAsync(
            new UserSession { UserId = "user-1", CompanyId = Guid.NewGuid() },
            2026,
            CancellationToken.None);

        Assert.Equal(2026, repository.CreateYearFiscalYear);
        Assert.Contains("12", message);
    }

    [Fact]
    public async Task RunStandaloneDepreciationAsync_DelegatesToRepository_WithNormalizedDate()
    {
        var repository = new FakeSpeedreconRepository { StandaloneDepreciationRows = 2 };
        var service = BuildService(new SuccessfulRuntimeContextService(), repository);

        var message = await service.RunStandaloneDepreciationAsync(
            new UserSession { UserId = "user-1", CompanyId = Guid.NewGuid() },
            new DateTime(2026, 7, 6, 14, 30, 0),
            CancellationToken.None);

        Assert.Equal(new DateTime(2026, 7, 6), repository.StandaloneDepreciationReconDate);
        Assert.Contains("2", message);
    }

    private static SpeedreconPageService BuildService(
        IJeevesRuntimeContextService runtimeContextService,
        ISpeedreconRepository repository,
        ISpeedreconRunService? runService = null)
        => new(
            runtimeContextService,
            repository,
            runService ?? new FakeSpeedreconRunService(),
            new HttpContextAccessor { HttpContext = new DefaultHttpContext() },
            NullLogger<SpeedreconPageService>.Instance);

    private sealed class SuccessfulRuntimeContextService : IJeevesRuntimeContextService
    {
        public Task<OperationResult<JeevesRuntimeContext>> ResolveAsync(UserSession? sessionUser, CancellationToken cancellationToken = default)
            => Task.FromResult(OperationResult<JeevesRuntimeContext>.Ok(new JeevesRuntimeContext
            {
                UserId = sessionUser?.UserId ?? "user-1",
                CompanyId = sessionUser?.CompanyId ?? Guid.NewGuid(),
                CompanyCode = 100,
                CompanyName = "Testbolag",
                ConnectionString = "Server=example;Database=Jeeves;",
                PersSign = "ZU"
            }));
    }

    private sealed class FailingRuntimeContextService : IJeevesRuntimeContextService
    {
        public Task<OperationResult<JeevesRuntimeContext>> ResolveAsync(UserSession? sessionUser, CancellationToken cancellationToken = default)
            => Task.FromResult(OperationResult<JeevesRuntimeContext>.Fail("authorization=secret-value missing tenant data"));
    }

    private sealed class FakeSpeedreconRepository : ISpeedreconRepository
    {
        public bool ProbeCalled { get; private set; }
        public DateTime? ProbeReconDate { get; private set; }
        public int? CreateYearFiscalYear { get; private set; }
        public int CreateYearRows { get; init; }
        public DateTime? StandaloneDepreciationReconDate { get; private set; }
        public int StandaloneDepreciationRows { get; init; }
        public SpeedreconProbeResult ProbeResult { get; init; } = new()
        {
            RuntimeAvailable = true,
            IsEnabledInJeeves = true
        };

        public Task<SpeedreconProbeResult> ProbeAsync(
            JeevesRuntimeContext runtimeContext,
            DateTime reconDate,
            CancellationToken cancellationToken = default)
        {
            ProbeCalled = true;
            ProbeReconDate = reconDate;
            return Task.FromResult(ProbeResult);
        }

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
            => Task.CompletedTask;

        public Task UpdateGeneralLedgerAmountsAsync(
            JeevesRuntimeContext runtimeContext,
            DateTime reconDate,
            CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<int> CreateYearAsync(
            JeevesRuntimeContext runtimeContext,
            int fiscalYear,
            CancellationToken cancellationToken = default)
        {
            CreateYearFiscalYear = fiscalYear;
            return Task.FromResult(CreateYearRows);
        }

        public Task<int> RunStandaloneDepreciationAsync(
            JeevesRuntimeContext runtimeContext,
            DateTime reconDate,
            CancellationToken cancellationToken = default)
        {
            StandaloneDepreciationReconDate = reconDate;
            return Task.FromResult(StandaloneDepreciationRows);
        }
    }

    private sealed class FakeSpeedreconRunService : ISpeedreconRunService
    {
        public bool RunCalled { get; private set; }
        public DateTime? RunReconDate { get; private set; }

        public Task<SpeedreconRunOutcome> RunAsync(
            JeevesRuntimeContext runtimeContext,
            DateTime reconDate,
            CancellationToken cancellationToken = default)
        {
            RunCalled = true;
            RunReconDate = reconDate;
            return Task.FromResult(new SpeedreconRunOutcome
            {
                ReconDate = reconDate,
                PlanCount = 1,
                ModuleCount = 2,
                ModuleNames = ["Kundreskontra", "Leverantorsreskontra"]
            });
        }
    }
}
