using System.Threading;
using System.Threading.Tasks;
using Entities.Application;
using Microsoft.AspNetCore.Mvc;
using WebApp.Services;
using WebApp.Models.ActionCenter;
using WebApp.Services.ActionCenter;

namespace WebApp.ViewComponents;

public sealed class ActionCenterViewComponent : ViewComponent
{
    private readonly IActionCenterService _actionCenterService;

    public ActionCenterViewComponent(IActionCenterService actionCenterService)
    {
        _actionCenterService = actionCenterService;
    }

    public async Task<IViewComponentResult> InvokeAsync(int take = 5, bool fullPage = false)
    {
        ViewData["ActionCenterFullPage"] = fullPage;

        var user = HttpContext?.Session.Get<UserSession>("UserObject");
        if (user == null)
        {
            return View("~/Views/Shared/Components/ActionCenter/Default.cshtml", new ActionCenterViewModel());
        }

        var model = await _actionCenterService.GetInsightsAsync(user, take, CancellationToken.None);
        return View("~/Views/Shared/Components/ActionCenter/Default.cshtml", model);
    }
}
