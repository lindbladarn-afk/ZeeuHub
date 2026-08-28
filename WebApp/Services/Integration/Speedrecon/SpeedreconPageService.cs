using Entities.Application;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using WebApp.Helpers;
using WebApp.Models.Integration.Speedrecon;
using WebApp.Services.Application;
using WebApp.ViewModels.Integration.Speedrecon;
using WebApp.ViewModels.Shared;

namespace WebApp.Services.Integration.Speedrecon;

// Coordinates Speedrecon diagnostics and manual runs for the current user context.
public sealed class SpeedreconPageService : ISpeedreconPageService
{
    private readonly IJeevesRuntimeContextService _runtimeContextService;
    private readonly ISpeedreconRepository _repository;
    private readonly ISpeedreconRunService _runService;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<SpeedreconPageService> _logger;

    public SpeedreconPageService(
        IJeevesRuntimeContextService runtimeContextService,
        ISpeedreconRepository repository,
        ISpeedreconRunService runService,
        IHttpContextAccessor httpContextAccessor,
        ILogger<SpeedreconPageService> logger)
    {
        _runtimeContextService = runtimeContextService;
        _repository = repository;
        _runService = runService;
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
    }

    public async Task<SpeedreconPageViewModel> BuildPageAsync(
        UserSession? user,
        DateTime? reconDate,
        string? statusMessage,
        string? statusTone,
        CancellationToken cancellationToken = default)
    {
        var effectiveReconDate = NormalizeReconDate(reconDate);
        var runtime = await ResolveRuntimeAsync(user, cancellationToken);
        if (!runtime.Success || runtime.Value is null)
        {
            var runtimeMessage = "Aktiv Jeeves-koppling saknas eller kunde inte valideras.";
            return new SpeedreconPageViewModel
            {
                ReconDate = effectiveReconDate,
                StatusMessage = statusMessage,
                StatusTone = statusTone ?? "warning",
                RuntimeBanner = BuildBanner("Speedrecon kunde inte nå Jeeves.", runtimeMessage, "warning"),
                Probe = new SpeedreconProbeResult
                {
                    RuntimeAvailable = false,
                    RuntimeMessage = runtimeMessage,
                    ProbeTimeUtc = DateTime.UtcNow
                }
            };
        }

        try
        {
            var probe = await _repository.ProbeAsync(runtime.Value, effectiveReconDate, cancellationToken);
            return new SpeedreconPageViewModel
            {
                ReconDate = effectiveReconDate,
                StatusMessage = statusMessage,
                StatusTone = statusTone ?? "info",
                RuntimeBanner = BuildRuntimeBanner(probe),
                Probe = probe
            };
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            var message = LogAndSanitize(ex, "Speedrecon-proben kunde inte köras.");
            return new SpeedreconPageViewModel
            {
                ReconDate = effectiveReconDate,
                StatusMessage = statusMessage ?? message,
                StatusTone = "warning",
                RuntimeBanner = BuildBanner("Speedrecon kunde inte läsa Jeeves.", message, "warning"),
                Probe = new SpeedreconProbeResult
                {
                    CompanyCode = runtime.Value.CompanyCode,
                    CompanyName = runtime.Value.CompanyName,
                    PersSign = runtime.Value.PersSign,
                    RuntimeAvailable = false,
                    RuntimeMessage = message,
                    ProbeTimeUtc = DateTime.UtcNow
                }
            };
        }
    }

    public async Task<string> RunAsync(
        UserSession? user,
        DateTime reconDate,
        CancellationToken cancellationToken = default)
    {
        var runtime = await ResolveRuntimeAsync(user, cancellationToken);
        if (!runtime.Success || runtime.Value is null)
            throw new InvalidOperationException(runtime.Error ?? "Aktiv Jeeves-koppling saknas.");

        var outcome = await _runService.RunAsync(runtime.Value, NormalizeReconDate(reconDate), cancellationToken);
        return $"Speedrecon kördes i hubben för {outcome.ReconDate:yyyy-MM-dd}. Moduler: {outcome.ModuleCount}.";
    }

    public async Task<string> CreateYearAsync(
        UserSession? user,
        int fiscalYear,
        CancellationToken cancellationToken = default)
    {
        if (fiscalYear is < 2000 or > 2100)
            throw new ArgumentOutOfRangeException(nameof(fiscalYear), "Redovisningsaret ar inte giltigt.");

        var runtime = await ResolveRuntimeAsync(user, cancellationToken);
        if (!runtime.Success || runtime.Value is null)
            throw new InvalidOperationException(runtime.Error ?? "Aktiv Jeeves-koppling saknas.");

        var rows = await _repository.CreateYearAsync(runtime.Value, fiscalYear, cancellationToken);
        return $"Speedrecon skapade {rows} körplansrader för {fiscalYear}.";
    }

    public async Task<string> RunStandaloneDepreciationAsync(
        UserSession? user,
        DateTime reconDate,
        CancellationToken cancellationToken = default)
    {
        var runtime = await ResolveRuntimeAsync(user, cancellationToken);
        if (!runtime.Success || runtime.Value is null)
            throw new InvalidOperationException(runtime.Error ?? "Aktiv Jeeves-koppling saknas.");

        var safeReconDate = NormalizeReconDate(reconDate);
        var rows = await _repository.RunStandaloneDepreciationAsync(runtime.Value, safeReconDate, cancellationToken);
        return $"Speedrecon fristående avskrivning kördes i hubben för {safeReconDate:yyyy-MM-dd}. Rader: {rows}.";
    }

    private Task<OperationResult<JeevesRuntimeContext>> ResolveRuntimeAsync(UserSession? user, CancellationToken cancellationToken)
        => _runtimeContextService.ResolveAsync(user, cancellationToken);

    private ModuleBannerViewModel? BuildRuntimeBanner(SpeedreconProbeResult probe)
    {
        if (!probe.RuntimeAvailable)
            return BuildBanner("Speedrecon kunde inte nå Jeeves.", probe.RuntimeMessage ?? "Aktiv Jeeves-koppling saknas.", "warning");

        if (!probe.IsEnabledInJeeves)
        {
            return BuildBanner(
                "Speedrecon är inte aktiverad i Jeeves.",
                "Parametern CUSTOM_SPEEDRECON_01 är inte 1 för valt bolag.",
                "warning");
        }

        var missingTables = probe.Objects
            .Where(item => item.ObjectType == "Table" && !item.Exists)
            .Where(item => item.ObjectName is "q_zu_speedrecon" or "q_zu_speedrecon_result")
            .Select(item => item.ObjectName)
            .ToList();

        if (missingTables.Count > 0)
        {
            return BuildBanner(
                "Speedrecon saknar tabeller i Jeeves.",
                $"Saknas: {string.Join(", ", missingTables)}.",
                "warning");
        }

        return null;
    }

    private static ModuleBannerViewModel BuildBanner(string title, string message, string tone)
        => new()
        {
            Title = title,
            Message = message,
            Tone = tone,
            IconClass = tone == "warning" ? "fa fa-exclamation-triangle" : "fa fa-info-circle"
        };

    private string LogAndSanitize(Exception exception, string fallback)
    {
        var diagnostic = IntegrationLogSanitizer.Diagnostic(exception.Message);
        var supportId = HttpContextSupportId();
        _logger.LogWarning(exception, "{Message} SupportId={SupportId} Detail={Detail}", fallback, supportId, diagnostic);
        return $"{fallback} Support-id: {supportId}.";
    }

    private string HttpContextSupportId()
        => _httpContextAccessor.HttpContext?.TraceIdentifier ?? Guid.NewGuid().ToString("N");

    private static DateTime NormalizeReconDate(DateTime? reconDate)
        => (reconDate ?? DateTime.Today.AddDays(-1)).Date;
}
