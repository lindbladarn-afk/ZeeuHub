using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace WebApp.Views.Home
{
    [AllowAnonymous]
    public class ErrorModel : PageModel
    {

        public IActionResult OnGet()
        {
            return Page();
        }
    }
}
