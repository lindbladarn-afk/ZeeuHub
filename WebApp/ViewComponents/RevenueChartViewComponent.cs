using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using WebApp.Models.Dashboard;

namespace WebApp.ViewComponents
{
    public class RevenueChartViewComponent : ViewComponent
    {
        public Task<IViewComponentResult> InvokeAsync(RevenueDataModel revenue)
        {
            return Task.FromResult((IViewComponentResult)View(revenue));
        }
    }
}
