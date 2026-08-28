using Entities.Contracts;
using Entities.ZeeuDashboard;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using NotificationService;
using Repository.Contracts;
using WebApp.Data;
using WebApp.Helpers;
using WebApp.Models.Identity;
using WebApp.Services.Application;
using WebApp.ViewModels.Shared;

namespace WebApp.Controllers;

[Authorize(Roles = "Administrator,User,Dashboard")]
public class ZeeuDashboardController : BaseController
{
    private readonly IZeeuDashboardRepository _dashboardRepository;
    private readonly ILogger _logger;

    public ZeeuDashboardController(IHttpContextAccessor contextAccessor
                                    ,IApplicationUserRepository applicationUserRepository
                                    ,INotificationManager notificationManager
                                    ,IZeeuDashboardRepository dashboardRepository
                                    ,ILoggerFactory loggerFactory
                                    ,IApplicationHelper applicationHelper
                                    ,ApplicationDbContext context)
        : base(contextAccessor, 
              applicationUserRepository,
              notificationManager,
              applicationHelper,
              context)
    {
        _logger = loggerFactory.CreateLogger("ZeeuDashboardController");
        _dashboardRepository = dashboardRepository;
    }


    private sealed class DashboardRequestContext
    {
        public required string ConnectionString { get; init; }
        public int? CompanyCode { get; init; }
    }

    private async Task<OperationResult<DashboardRequestContext>> BuildRequestContextAsync()
    {
        var userObject = await GetCurrentUserAsync()
            ?? throw new InvalidOperationException("The user could not be loaded");

        var runtimeContext = await ResolveCurrentRuntimeContextAsync();
        if (runtimeContext is null)
        {
            return OperationResult<DashboardRequestContext>.Fail("Produktionsdashboarden kräver just nu en aktiv Jeeves-koppling.");
        }

        userObject.JeevesActiveCompany = runtimeContext.CompanyCode;
        userObject.CompanyId ??= runtimeContext.CompanyId;
        userObject.Email ??= runtimeContext.Email;
        userObject.PersSign ??= runtimeContext.PersSign;

        return OperationResult<DashboardRequestContext>.Ok(new DashboardRequestContext
        {
            ConnectionString = runtimeContext.ConnectionString,
            CompanyCode = userObject.JeevesActiveCompany
        });
    }

    public IActionResult ZeeuDashboard()
    {
        return View();
    }

    [Authorize(Roles = "Administrator,User,Dashboard")]
    public async Task<IActionResult> ProductionDashboard()
    {
        var context = await BuildRequestContextAsync();
        if (!context.Success || context.Value is null)
        {
            return View("ModuleUnavailable", BuildModuleUnavailableViewModel(
                "Dashboard",
                "Produktionsdashboarden är tillfälligt otillgänglig",
                "Produktionsdata läses från Jeeves i aktiv tenant.",
                context.Error,
                Url.Action(nameof(ProductionDashboard), "ZeeuDashboard")));
        }

        var list = _dashboardRepository.GetProductionPersonal(context.Value.ConnectionString, context.Value.CompanyCode);
        list = ZeeuDashboardHelper.CalculateRemainingOperationTime(list);
        var sortedList = list.OrderByDescending(u => u.Present).ThenBy(u => u.WorkOrder == null).ThenBy(u => u.Name).ToList();
        return View(sortedList);
    }


    [HttpGet]
    [Authorize(Roles = "Administrator,User,Dashboard")]
    public async Task<PartialViewResult> _CttProduction(string sortOrder)
    {
        var context = await BuildRequestContextAsync();
        if (!context.Success || context.Value is null)
        {
            return PartialView("~/Views/Shared/_ModuleState.cshtml", BuildRuntimeUnavailableState(context.Error));
        }

        var listOfAvailablePersonal = await FetchProductionData(context.Value);
        listOfAvailablePersonal = ZeeuDashboardHelper.CalculateRemainingOperationTime(listOfAvailablePersonal);
        var sortedList = listOfAvailablePersonal.OrderByDescending(u => u.Present).ThenBy(u => u.WorkOrder == null).ThenBy(u => u.Name).ToList();
        return PartialView("_CttProduction", sortedList);
    }

    [HttpGet]
    [Authorize(Roles = "Administrator,User")]
    public async Task<IActionResult> ProductionDashboardAdmin()
	{
        var context = await BuildRequestContextAsync();
        if (!context.Success || context.Value is null)
        {
            return View("ModuleUnavailable", BuildModuleUnavailableViewModel(
                "Dashboard",
                "Produktionsdashboarden är tillfälligt otillgänglig",
                "Produktionsdata läses från Jeeves i aktiv tenant.",
                context.Error,
                Url.Action(nameof(ProductionDashboardAdmin), "ZeeuDashboard")));
        }

        var list = _dashboardRepository.GetProductionPersonal(context.Value.ConnectionString, context.Value.CompanyCode);
        list = ZeeuDashboardHelper.CalculateRemainingOperationTime(list);
        var sortedList = list.OrderByDescending(u => u.Present).ThenBy(u => u.Name).ToList();
        return View(sortedList);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Administrator,User")]
    public async Task<IActionResult> _CttProductionAdmin(List<ProductionDashboardVM> model)
    {
        var context = await BuildRequestContextAsync();
        if (!context.Success || context.Value is null)
        {
            return View("ModuleUnavailable", BuildModuleUnavailableViewModel(
                "Dashboard",
                "Produktionsdashboarden är tillfälligt otillgänglig",
                "Produktionsdata läses från Jeeves i aktiv tenant.",
                context.Error,
                Url.Action(nameof(ProductionDashboardAdmin), "ZeeuDashboard")));
        }

        foreach (var item in model)
        {
            _dashboardRepository.UpdateNextWorkOrder(context.Value.ConnectionString, item.PersSign, context.Value.CompanyCode, item.NextWorkOrder, item.NextProductionGroup);
        }
        return RedirectToAction(nameof(ProductionDashboardAdmin));
	}

    [Authorize(Roles = "Administrator,User,Dashboard")]
    private Task<IEnumerable<ProductionDashboardVM>> FetchProductionData(DashboardRequestContext context)
    {
        IEnumerable<ProductionDashboardVM> listOfAvailabelPersonal = _dashboardRepository.GetProductionPersonal(context.ConnectionString, context.CompanyCode);
        return Task.FromResult(listOfAvailabelPersonal);
    }

    private ModuleUnavailableViewModel BuildModuleUnavailableViewModel(
        string moduleLabel,
        string title,
        string subtitle,
        string? detail,
        string? actionUrl)
    {
        return new ModuleUnavailableViewModel
        {
            ModuleLabel = moduleLabel,
            Title = title,
            Subtitle = subtitle,
            State = BuildRuntimeUnavailableState(detail, actionUrl)
        };
    }

    private ModuleStateViewModel BuildRuntimeUnavailableState(string? detail, string? actionUrl = null)
    {
        return new ModuleStateViewModel
        {
            Title = "Jeeves är tillfälligt otillgängligt",
            Message = "Du är fortfarande inloggad i portalen, men dashboarden kan inte läsa tenantdata just nu.",
            Note = string.IsNullOrWhiteSpace(detail)
                ? "Försök igen om en stund eller byt till en modul som inte behöver live-data från Jeeves."
                : detail,
            Tone = "warning",
            IconClass = "fa fa-plug",
            ActionText = string.IsNullOrWhiteSpace(actionUrl) ? null : "Försök igen",
            ActionUrl = actionUrl
        };
    }
}
