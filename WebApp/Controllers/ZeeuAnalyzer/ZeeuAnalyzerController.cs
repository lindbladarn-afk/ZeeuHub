using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace WebApp.Controllers
{
    [Authorize(Roles = "Administrator, User")]
    public class ZeeuAnalyzerController : Controller
    {
        public IActionResult ZeeuAnalyzer()
        {
            return View();
        }
    }
}
