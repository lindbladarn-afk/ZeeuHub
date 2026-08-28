using Entities.Application;
using Microsoft.Extensions.Options;
using WebApp.Models.Integration;
using WebApp.Services.Application;
using WebApp.ViewModels.Shared;

namespace WebApp.Services.Integration.FlowEngine;

public sealed class FlowEngineModuleService : IFlowEngineModuleService
{
    private readonly IOptions<FlowEngineModuleOptions> _options;
    private readonly IJeevesRuntimeContextService _jeevesRuntimeContextService;
    private readonly IFlowEngineExecutionService _flowEngineExecutionService;
    private readonly IFlowEngineHealthProbeService _flowEngineHealthProbeService;
    private readonly IFlowEngineOperationCatalog _flowEngineOperationCatalog;

    public FlowEngineModuleService(
        IOptions<FlowEngineModuleOptions> options,
        IJeevesRuntimeContextService jeevesRuntimeContextService,
        IFlowEngineExecutionService flowEngineExecutionService,
        IFlowEngineHealthProbeService flowEngineHealthProbeService,
        IFlowEngineOperationCatalog flowEngineOperationCatalog)
    {
        _options = options;
        _jeevesRuntimeContextService = jeevesRuntimeContextService;
        _flowEngineExecutionService = flowEngineExecutionService;
        _flowEngineHealthProbeService = flowEngineHealthProbeService;
        _flowEngineOperationCatalog = flowEngineOperationCatalog;
    }

    public async Task<FlowEngineModuleViewModel> BuildModuleViewModelAsync(
        UserSession? sessionUser,
        string? activeSection,
        Guid? selectedJobId,
        int historyPage,
        FlowEngineHistoryFilterState? historyFilters,
        FlowEngineWorkbenchSettingsState? workbenchSettings,
        CancellationToken cancellationToken = default)
    {
        var options = _options.Value;
        var effectiveWorkbenchSettings = workbenchSettings ?? new FlowEngineWorkbenchSettingsState();
        JeevesRuntimeContext? runtimeContext = null;
        string? runtimeError = null;

        if (options.Enabled && sessionUser is not null)
        {
            var runtimeResult = await _jeevesRuntimeContextService.ResolveAsync(sessionUser, cancellationToken);
            if (runtimeResult.Success)
                runtimeContext = runtimeResult.Value;
            else
                runtimeError = runtimeResult.Error;
        }

        var companyId = sessionUser?.CompanyId;
        var recentJobs = companyId.HasValue
            ? _flowEngineExecutionService.ListRecent(companyId.Value, 12)
            : Array.Empty<FlowEngineJobSnapshot>();
        var dashboardHistory = companyId.HasValue
            ? _flowEngineExecutionService.ListPage(companyId.Value, historyPage, 15, null, historyFilters)
            : new FlowEngineHistoryPageResult();
        var jeevesHistory = companyId.HasValue
            ? _flowEngineExecutionService.ListPage(companyId.Value, historyPage, 15, "jeeves")
            : new FlowEngineHistoryPageResult();
        var centraHistory = companyId.HasValue
            ? _flowEngineExecutionService.ListPage(companyId.Value, historyPage, 15, "centra")
            : new FlowEngineHistoryPageResult();
        var shopifyHistory = companyId.HasValue
            ? _flowEngineExecutionService.ListPage(companyId.Value, historyPage, 15, "shopify")
            : new FlowEngineHistoryPageResult();
        var akeneoHistory = companyId.HasValue
            ? _flowEngineExecutionService.ListPage(companyId.Value, historyPage, 15, "akeneo")
            : new FlowEngineHistoryPageResult();
        return new FlowEngineModuleViewModel
        {
            Title = string.IsNullOrWhiteSpace(options.Title) ? "FlowEngine" : options.Title.Trim(),
            Subtitle = string.IsNullOrWhiteSpace(options.Subtitle)
                ? "Portal-native omskrivning av FlowEngine i C# och Razor."
                : options.Subtitle.Trim(),
            Summary = string.IsNullOrWhiteSpace(options.Summary)
                ? "ZeeU Portal ska aga UI, auth, jobb och integrationer utan extern Swift-app eller separat URL."
                : options.Summary.Trim(),
            MigrationPhase = string.IsNullOrWhiteSpace(options.MigrationPhase) ? "scaffold" : options.MigrationPhase.Trim(),
            ActiveSection = FlowEngineSectionKeys.Normalize(activeSection),
            Banner = BuildBanner(options, runtimeContext, runtimeError),
            CanRunReadOperations = options.Enabled && runtimeContext is not null,
            RuntimeCompanyName = runtimeContext?.CompanyName,
            RuntimeCompanyCode = runtimeContext?.CompanyCode,
            SystemStatuses = Array.Empty<FlowEngineSystemStatusViewModel>(),
            Operations = _flowEngineOperationCatalog.GetAll()
                .Select(operation => new FlowEngineOperationDescriptor
                {
                    Key = operation.Key,
                    Operation = operation.Operation,
                    Section = operation.Section,
                    Label = operation.Label,
                    Summary = operation.Summary,
                    Slice = operation.Slice,
                    Readiness = operation.Readiness
                })
                .ToArray(),
            Verticals = BuildVerticals(),
            RecentJobs = recentJobs,
            SelectedJob = null,
            WorkbenchSettings = effectiveWorkbenchSettings,
            DashboardHistory = dashboardHistory,
            JeevesHistory = jeevesHistory,
            CentraHistory = centraHistory,
            ShopifyHistory = shopifyHistory,
            AkeneoHistory = akeneoHistory
        };
    }

    private static ModuleBannerViewModel BuildBanner(
        FlowEngineModuleOptions options,
        JeevesRuntimeContext? runtimeContext,
        string? runtimeError)
    {
        if (!options.Enabled)
        {
            return new ModuleBannerViewModel
            {
                Title = "FlowEngine native rewrite ar avstangd",
                Message = "Aktivera FlowEngine i konfigurationen nar portalen ska exponera den nya C#-modulen for anvandare.",
                Tone = "warning",
                IconClass = "fa fa-power-off"
            };
        }

        if (runtimeContext is null)
        {
            return new ModuleBannerViewModel
            {
                Title = "FlowEngine saknar runtime context",
                Message = string.IsNullOrWhiteSpace(runtimeError)
                    ? "Portalen kunde inte faststalla aktivt Jeeves-bolag for FlowEngine."
                    : runtimeError,
                Note = "De forsta native read-operationerna kravs att portalens runtime context och Jeeves-koppling ar tillgangliga.",
                Tone = "warning",
                IconClass = "fa fa-circle-exclamation"
            };
        }

        return new ModuleBannerViewModel
        {
            Title = "FlowEngine byggs om inuti portalen",
            Message = $"Forsta native vertikalen kor nu mot {runtimeContext.CompanyName} ({runtimeContext.CompanyCode}) med portalens egen auth, jobbmodell och historik.",
            Note = "Jeeves-lasningar ar klara, Centra check orders samt de forsta send-orders, send-returns och shipment-batcherna kor nu native, och import-order kan skickas till Jeeves samt fyllas pa via deterministisk PDF-extraktion i portalen.",
            Tone = "info",
            IconClass = "fa fa-diagram-project"
        };
    }

    private static IReadOnlyList<FlowEngineNativeVerticalDescriptor> BuildVerticals()
    {
        return new List<FlowEngineNativeVerticalDescriptor>
        {
            new()
            {
                Title = "Fas 1: Jobbsubstrat och typed commands",
                Scope = "FlowEngineJobSnapshot, queue-status, kommandooversattning och portalhistorik i C#.",
                Why = "All annan funktionalitet behver samma jobbmodell, status och output-hantering."
            },
            new()
            {
                Title = "Fas 2: Jeeves lasfloden",
                Scope = "Get customer addresses, get product och art status via portalens egen integrationsstack.",
                Why = "Ger snabb affarsnytta med lag risk och delar mycket logik med befintlig portalarkitektur."
            },
            new()
            {
                Title = "Fas 3: Jeeves import order",
                Scope = "Import order-form, pre-validation, delivery address-resolution och importhistorik.",
                Why = "Det ar den mest portalnara FlowEngine-ytan och passar val in i befintliga formularmonster."
            },
            new()
            {
                Title = "Fas 4: Centra, Shopify och Akeneo skrivfloden",
                Scope = "Batchjobb, single-order actions, shipment-floden och policy-/mappningslogik.",
                Why = "De ar mest komplexa och bor flyttas sist nar jobbsubstrat och adapters redan ar stabila."
            }
        };
    }
}
