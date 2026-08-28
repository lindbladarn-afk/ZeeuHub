using Entities.Application;
using Entities.Contracts;
using Microsoft.AspNetCore.Mvc;
using NotificationService;
using Repository.Contracts;
using System.Linq;
using System.Security.Claims;
using WebApp.Data;
using WebApp.Helpers;
using WebApp.Models;
using WebApp.Models.Identity;
using WebApp.Services;
using WebApp.Services.Application;

namespace WebApp.Controllers
{
    public class BaseController : Controller
    {
        private readonly IHttpContextAccessor _contextAccessor;
        private readonly IApplicationUserRepository _applicationUserRepository;
        private readonly INotificationManager _notificationManager;
        private readonly IApplicationHelper _applicationHelper;
        public BaseController(
            IHttpContextAccessor contextAccessor,
            IApplicationUserRepository applicationUserRepository,
            INotificationManager notificationManager,
            IApplicationHelper applicationHelper,
            ApplicationDbContext context)
        {
            _contextAccessor = contextAccessor;
            _applicationUserRepository = applicationUserRepository;
            _notificationManager = notificationManager;
            _applicationHelper = applicationHelper;
        }

        public void Attention(string message)
        {
            TempData.Add(Alert.ATTENTION, message);
        }

        /// <summary>
        /// Displays a popup in the top right corner with the message provided. 
        /// The popup will disappear after 6 sec
        /// </summary>
        /// <param name="message"></param>
        public void SuccessPopup(string message)
        {
            _notificationManager.Success(message);
            //TempData.Add(Alert.SUCCESS, message);
        }

        /// <summary>
        /// Displays a popup in the top right corner with the message provided
        /// The popup will disappear after 30 sec
        /// </summary>
        /// <param name="message"></param>
        public void ErrorPopup(string message)
        {
            _notificationManager.Error(message);
        }

        public void Info(string message)
        {
            TempData.Add(Alert.INFORMATION, message);
        }

        protected void HubToast(string message)
        {
            _notificationManager.HubStatus(message);
        }

        public void Error(string message)
        {
            if (TempData[Alert.DANGER] != null)
            {
                TempData[Alert.DANGER] = message;
            }
            else
            {
                TempData.Add(Alert.DANGER, message);
            }
        }

        public void UserFriendlyError(string userFriendlyMessage)
        {
            if (TempData[Alert.USERFRIENDLYERRORMESSAGE] != null)
            {
                TempData[Alert.USERFRIENDLYERRORMESSAGE] = userFriendlyMessage;
            }
            else
            {
                TempData.Add(Alert.USERFRIENDLYERRORMESSAGE, userFriendlyMessage);
            }
        }

        public async Task<IUser> GetCurrentUserAsync()
        {
            var sessionUser = _contextAccessor.HttpContext?.Session.Get<UserSession>("UserObject");
            if (sessionUser != null)
                return await MapToUserAsync(sessionUser);

            var userEmail = User.FindFirstValue(ClaimTypes.Name) ?? User.FindFirstValue(ClaimTypes.Email);
            if (!string.IsNullOrWhiteSpace(userEmail))
            {
                await _applicationHelper.AddUserToSession(userEmail);
                sessionUser = _contextAccessor.HttpContext?.Session.Get<UserSession>("UserObject");
                if (sessionUser != null)
                    return await MapToUserAsync(sessionUser);
            }

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(userId))
                throw new InvalidOperationException("Authenticated user is missing name identifier claim.");

            return await _applicationUserRepository.GetUserAsync(userId);
        }

        protected async Task<bool> RefreshCurrentSessionAsync()
        {
            var userEmail = User.FindFirstValue(ClaimTypes.Name) ?? User.FindFirstValue(ClaimTypes.Email);
            if (string.IsNullOrWhiteSpace(userEmail))
                return false;

            return await _applicationHelper.AddUserToSession(userEmail);
        }

        protected async Task<JeevesRuntimeContext?> ResolveCurrentRuntimeContextAsync(CancellationToken cancellationToken = default)
        {
            var sessionUser = _contextAccessor.HttpContext?.Session.Get<UserSession>("UserObject");
            if (sessionUser is null)
            {
                var userEmail = User.FindFirstValue(ClaimTypes.Name) ?? User.FindFirstValue(ClaimTypes.Email);
                if (!string.IsNullOrWhiteSpace(userEmail))
                {
                    await _applicationHelper.AddUserToSession(userEmail);
                    sessionUser = _contextAccessor.HttpContext?.Session.Get<UserSession>("UserObject");
                }
            }

            if (sessionUser is null)
                return null;

            var runtimeContextService = HttpContext.RequestServices.GetRequiredService<IJeevesRuntimeContextService>();
            var runtimeContext = await runtimeContextService.ResolveAsync(sessionUser, cancellationToken);
            return runtimeContext.Success ? runtimeContext.Value : null;
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangeActiveCompany(int activeCompany, string actionName, string controllerName)
        {
            var httpContext = _contextAccessor.HttpContext;
            if (httpContext?.Session is null)
            {
                ErrorPopup("Ingen användarsession kunde hittas. Logga in igen.");
                return RedirectToAction(actionName, controllerName);
            }

            var user = httpContext.Session.Get<UserSession>("UserObject");
            if (user is null)
            {
                ErrorPopup("Ingen användarsession kunde hittas. Logga in igen.");
                return RedirectToAction(actionName, controllerName);
            }

            var companyAccessService = HttpContext.RequestServices.GetRequiredService<IJeevesCompanyAccessService>();
            if (!await companyAccessService.HasCompanyAccessAsync(user, activeCompany))
            {
                ErrorPopup("Du har inte behörighet att byta till det här företaget.");
                return RedirectToAction(actionName, controllerName);
            }

            user.JeevesActiveCompany = activeCompany;
            httpContext.Session.Set("UserObject", user);
            // ToDo: Maybe instead redirect to the dashboard instead
            return RedirectToAction(actionName, controllerName);
        }

        private async Task<User> MapToUserAsync(UserSession sessionUser)
        {
            var companyAccessService = HttpContext.RequestServices.GetRequiredService<IJeevesCompanyAccessService>();
            var runtimeContextService = HttpContext.RequestServices.GetRequiredService<IJeevesRuntimeContextService>();
            var runtimeContext = await runtimeContextService.ResolveAsync(sessionUser, HttpContext.RequestAborted);
            var resolvedContext = runtimeContext.Success ? runtimeContext.Value : null;

            return new User
            {
                Id = sessionUser.UserId,
                Email = resolvedContext?.Email ?? sessionUser.Email,
                FirstName = sessionUser.FirstName,
                LastName = sessionUser.LastName,
                Language = sessionUser.Language,
                Company = (resolvedContext?.CompanyName ?? sessionUser.CompanyName) is { Length: > 0 } companyName
                    ? new Company { Name = companyName }
                    : null,
                PersSign = resolvedContext?.PersSign ?? sessionUser.PersSign,
                CompanyId = resolvedContext?.CompanyId ?? sessionUser.CompanyId,
                JeevesActiveCompany = resolvedContext?.CompanyCode ?? sessionUser.JeevesActiveCompany,
                JeevesCompanies = await companyAccessService.GetCompaniesAsync(sessionUser)
            };
        }
    }
}
