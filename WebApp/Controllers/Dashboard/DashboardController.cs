using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace WebApp.Controllers
{
    // Keeps the legacy /Dashboard entry point pointed at the shared member dashboard shell.
    [Authorize(Roles = "Administrator, User")]
    public class DashboardController : Controller
    {
        public IActionResult Index()
        {
            return RedirectToAction("Index", "Member");
        }
    }
}
