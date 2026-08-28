#nullable disable
using LoggerService;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.WebUtilities;
using NotificationService;
using System.ComponentModel.DataAnnotations;
using System.Text;
using WebApp.Models;
using WebApp.Models.Identity;

namespace WebApp.Areas.Identity.Pages.Account
{
    [AllowAnonymous]
    public class ConfirmAccountModel : PageModel
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly INotificationManager _notificationManager;
        private readonly ILoggerManager _loggerManager;

        public ConfirmAccountModel(UserManager<ApplicationUser> userManager, 
                                    INotificationManager notificationManager,
                                    ILoggerManager loggerManager)
        {
            _userManager = userManager;
            _notificationManager = notificationManager;
            _loggerManager = loggerManager;
        }

        [BindProperty]
        public InputModel Input { get; set; }
        public string ReturnUrl { get; set; }

        public class InputModel
        {
            //[Required(ErrorMessage = "You must enter a First Name")]
            //[Display(Name = "First Name")]
            //[StringLength(100, ErrorMessage = "The maximum length is 100 characters")]
            //public string FirstName { get; set; }

            //[Required(ErrorMessage = "You must enter a Last Name")]
            //[Display(Name = "Last Name")]
            //[StringLength(100, ErrorMessage = "The maximum length is 100 characters")]
            //public string LastName { get; set; }

            [Required(ErrorMessage = "You must enter your Email Address")]
            [EmailAddress]
            [Display(Name = "Email")]
            public string Email { get; set; }

            [Required]
            [MinLength(10, ErrorMessage = "The Password must be at least 10 characters long.")]
            [DataType(DataType.Password)]
            [Display(Name = "Password")]
            public string Password { get; set; }

            [Required]
            [DataType(DataType.Password)]
            [Display(Name = "Confirm Password")]
            [Compare("Password", ErrorMessage = "The Password and confirmation Password do not match.")]
            public string ConfirmPassword { get; set; }

            [Required]
            public string Code { get; set; }
        }

        public IActionResult OnGet(string returnUrl = null, string code = null, string email = null)
        {   
            ReturnUrl = returnUrl;
            if (code == null)
            {
                return BadRequest("A code must be supplied for account confirmation.");
            }
            if (email == null)
            {
                return BadRequest("An email must be supplied for account confirmation.");
            }

			Input = new InputModel
			{
				Code = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(code)),
				Email = email
			};

            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                return Page();
            }


            var user = await _userManager.FindByEmailAsync(Input.Email);
            if (user == null)
            {
                // Don't reveal that the user does not exist
                return RedirectToPage("./ResetPasswordConfirmation");
            }

            
            var result = await _userManager.ResetPasswordAsync(user, Input.Code, Input.Password);
            if (result.Succeeded)
            {
                user.EmailConfirmed = true;
                await _userManager.UpdateAsync(user);
                await _notificationManager.Success("Account confirmed!");
                return RedirectToPage("./Login");
            }
            else
            {
                foreach (var error in result.Errors)
                {
                    _loggerManager.LogError(error.Description);


                    if (TempData[Alert.DANGER] != null)
                    {
                        TempData[Alert.DANGER] += error.Description + Environment.NewLine;
                    }
                    else
                    {
                        TempData.Add(Alert.DANGER, error.Description);
                    }
                }              
                return Page();
            }
        }
    }
}
