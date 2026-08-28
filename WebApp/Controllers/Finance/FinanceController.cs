using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace WebApp.Controllers
{
    [Authorize(Roles = "Administrator, User")]
    public class FinanceController : Controller
    {
        public IActionResult Finance()
        {
            return View();
        }
    }
}
