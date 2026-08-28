using System.Threading.Tasks;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Entities.Application;
using WebApp.Models.CustomerActivity;
using WebApp.Services.CustomerActivity;
using WebApp.Services;
using WebApp.Services.Application;
using WebApp.ViewModels.Shared;

namespace WebApp.ViewComponents
{
    public class CustomerActivityViewComponent : ViewComponent
    {
        private readonly ICustomerActivityService _service;
        private readonly IJeevesRuntimeContextService _jeevesRuntimeContextService;

        public CustomerActivityViewComponent(
            ICustomerActivityService service,
            IJeevesRuntimeContextService jeevesRuntimeContextService)
        {
            _service = service;
            _jeevesRuntimeContextService = jeevesRuntimeContextService;
        }

        public async Task<IViewComponentResult> InvokeAsync(int take = 5)
        {
            var sessionUser = HttpContext?.Session.Get<UserSession>("UserObject");
            var runtimeContextResult = await _jeevesRuntimeContextService.ResolveAsync(sessionUser);
            if (!runtimeContextResult.Success || runtimeContextResult.Value is null)
            {
                return View(new CustomerActivityViewModel
                {
                    AvailabilityState = new ModuleStateViewModel
                    {
                        Title = "Kundaktivitet är tillfälligt otillgänglig",
                        Message = "Senaste kundaktivitet kräver tenantdata från Jeeves för valt bolag.",
                        Note = string.IsNullOrWhiteSpace(runtimeContextResult.Error) ? null : runtimeContextResult.Error,
                        Tone = "warning",
                        IconClass = "fa fa-plug-circle-xmark",
                        Compact = true
                    }
                });
            }

            var model = await _service.GetRecentAsync(
                runtimeContextResult.Value.ConnectionString,
                runtimeContextResult.Value.CompanyCode,
                take);
            return View(model);
        }
    }
}
