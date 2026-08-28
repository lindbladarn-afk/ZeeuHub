using Entities.Application;
using Entities.Mail;
using Entities.ViewModels.Admin;
using LoggerService;
using MailService;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using NotificationService;
using Repository.Contracts;
using System.Text;
using System.Text.Encodings.Web;
using WebApp.Data;
using WebApp.Models.Identity;
using WebApp.Services.Application;
using WebApp.Helpers;
using WebApp.Services.SuperUser;
using WebApp.ViewModels.SuperUser;

namespace WebApp.Controllers
{
    /// <summary>
    /// Endpoints för SuperUser: kan skapa användare, men endast i sitt eget bolag.
    /// </summary>
    [Authorize(Roles = "SuperUser")]
    public class SuperUserController : BaseController
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IUserStore<ApplicationUser> _userStore;
        private readonly IUserEmailStore<ApplicationUser> _emailStore;
        private readonly IAdminCompanyRepository _adminCompanyRepository;
        private readonly ILoggerManager _loggerManager;
        private readonly INotificationManager _notificationManager;
        private readonly IMailManager _mailManager;
        private readonly IUserWhitelistService _userWhitelistService;
        private readonly ApplicationDbContext _context;
        private readonly IStringLocalizer<SharedResources> _sharedLocalizer;
        private readonly ISuperUserPermissionService _permissionService;

        public SuperUserController(
            IHttpContextAccessor contextAccessor,
            IApplicationUserRepository applicationUserRepository,
            UserManager<ApplicationUser> userManager,
            IUserStore<ApplicationUser> userStore,
            ILoggerManager loggerManager,
            INotificationManager notificationManager,
            IMailManager mailManager,
            IAdminCompanyRepository adminCompanyRepository,
            IUserWhitelistService userWhitelistService,
            ApplicationDbContext context,
            IStringLocalizer<SharedResources> sharedLocalizer,
            ISuperUserPermissionService permissionService,
            IApplicationHelper applicationHelper)
            : base(contextAccessor, applicationUserRepository, notificationManager, applicationHelper, context)
        {
            _userManager = userManager;
            _userStore = userStore;
            _loggerManager = loggerManager;
            _notificationManager = notificationManager;
            _mailManager = mailManager;
            _adminCompanyRepository = adminCompanyRepository;
            _emailStore = GetEmailStore();
            _userWhitelistService = userWhitelistService;
            _context = context;
            _sharedLocalizer = sharedLocalizer;
            _permissionService = permissionService;
        }

        [HttpGet]
        public async Task<IActionResult> ControlPanel()
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser?.CompanyId is null)
            {
                await _notificationManager.Error(_sharedLocalizer["SuperUser_MissingCompanyId"]);
                return Forbid();
            }
            return View("ControlPanel");
        }

        [HttpGet]
        public async Task<IActionResult> Users()
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser?.CompanyId is null)
            {
                await _notificationManager.Error(_sharedLocalizer["SuperUser_MissingCompanyId"]);
                return Forbid();
            }

            var users = await _userManager.Users
                .Where(u => u.CompanyId == currentUser.CompanyId)
                .OrderBy(u => u.FirstName)
                .ThenBy(u => u.LastName)
                .ToListAsync();

            var vm = users.Select(u => new AdminUserViewModel
            {
                UserId = u.Id,
                FirstName = u.FirstName ?? string.Empty,
                LastName = u.LastName ?? string.Empty,
                Email = u.Email ?? string.Empty,
                CompanyId = u.CompanyId ?? Guid.Empty,
                CompanyName = currentUser.CompanyId == u.CompanyId ? _sharedLocalizer["SuperUser_OwnCompany"] : string.Empty,
                EmailValidated = u.EmailConfirmed
            }).ToList();

            return View("Users", vm);
        }

        [HttpGet]
        public async Task<IActionResult> ManageUser(string userId)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser?.CompanyId is null)
            {
                await _notificationManager.Error(_sharedLocalizer["SuperUser_MissingCompanyId"]);
                return Forbid();
            }

            var user = await _userManager.FindByIdAsync(userId);
            if (user is null || user.CompanyId != currentUser.CompanyId)
            {
                await _notificationManager.Error(_sharedLocalizer["SuperUser_UserNotFoundInCompany"]);
                return Forbid();
            }

            var allowedRoles = new[] { "User", "SuperUser" };
            var userRoles = await _userManager.GetRolesAsync(user);
            var rolesVm = allowedRoles.Select(r => new AdminUserRolesViewModel
            {
                RoleName = r,
                Selected = userRoles.Contains(r)
            }).ToList();

            var vm = new AdminUserViewModel
            {
                UserId = user.Id,
                FirstName = user.FirstName ?? string.Empty,
                LastName = user.LastName ?? string.Empty,
                Email = user.Email ?? string.Empty,
                PhoneNumber = user.PhoneNumber,
                CompanyId = user.CompanyId.GetValueOrDefault(),
                Roles = rolesVm
            };

            return View("ManageUser", vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ManageUser(AdminUserViewModel model)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser?.CompanyId is null)
            {
                await _notificationManager.Error(_sharedLocalizer["SuperUser_MissingCompanyId"]);
                return Forbid();
            }

            var user = await _userManager.FindByIdAsync(model.UserId);
            if (user is null || user.CompanyId != currentUser.CompanyId)
            {
                await _notificationManager.Error(_sharedLocalizer["SuperUser_UserNotFoundInCompany"]);
                return Forbid();
            }

            if (!ModelState.IsValid)
            {
                return View("ManageUser", model);
            }

            // Update basic fields
            user.FirstName = model.FirstName;
            user.LastName = model.LastName;
            user.PhoneNumber = model.PhoneNumber;
            await _userManager.UpdateAsync(user);

            // Update roles, but only allowed ones
            var allowedRoles = new[] { "User", "SuperUser" };
            var currentRoles = await _userManager.GetRolesAsync(user);
            var toRemove = currentRoles.Where(r => allowedRoles.Contains(r)).ToList();
            if (toRemove.Any())
            {
                await _userManager.RemoveFromRolesAsync(user, toRemove);
            }

            var selectedRoles = model.Roles?.Where(r => r.Selected && allowedRoles.Contains(r.RoleName)).Select(r => r.RoleName).ToList() ?? new List<string>();
            if (selectedRoles.Any())
            {
                await _userManager.AddToRolesAsync(user, selectedRoles);
            }

            await _notificationManager.Success(_sharedLocalizer["SuperUser_UserUpdated"]);
            return RedirectToAction("Users");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetUserPassword(string userId)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser?.CompanyId is null)
            {
                await _notificationManager.Error(_sharedLocalizer["SuperUser_MissingCompanyId"]);
                return Forbid();
            }

            var user = await _userManager.FindByIdAsync(userId);
            if (user is null || user.CompanyId != currentUser.CompanyId)
            {
                await _notificationManager.Error(_sharedLocalizer["SuperUser_UserNotFoundInCompany"]);
                return Forbid();
            }

            var tempPassword = $"Tmp-{Guid.NewGuid():N}".Substring(0, 12) + "aA1!";
            var token = await _userManager.GeneratePasswordResetTokenAsync(user);
            var result = await _userManager.ResetPasswordAsync(user, token, tempPassword);

            if (!result.Succeeded)
            {
                await _notificationManager.Error(_sharedLocalizer["SuperUser_ResetPasswordFailed"]);
                return RedirectToAction("ManageUser", new { userId });
            }

            await _notificationManager.TemporaryPassword(user.Email ?? string.Empty, tempPassword);
            return RedirectToAction("ManageUser", new { userId });
        }

        [HttpGet]
        public async Task<IActionResult> Permissions(string userId, CancellationToken cancellationToken)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser?.CompanyId is not Guid companyId)
                return Forbid();

            var model = await _permissionService.GetEditorAsync(companyId, userId, cancellationToken);
            if (model is null)
                return Forbid();

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Permissions(UserPermissionsViewModel model, CancellationToken cancellationToken)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser?.CompanyId is not Guid companyId)
                return Forbid();

            if (!ModelState.IsValid || !await _permissionService.UpdateAsync(companyId, model, cancellationToken))
            {
                var modelErrors = string.Join(" | ",
                    ModelState.Values.SelectMany(state => state.Errors)
                        .Select(error => string.IsNullOrWhiteSpace(error.ErrorMessage) ? error.Exception?.Message : error.ErrorMessage)
                        .Where(message => !string.IsNullOrWhiteSpace(message)));

                if (!ModelState.IsValid)
                {
                    _loggerManager.LogError(
                        $"Permissions validation failed for company {companyId:N}, user {model.UserId}. Errors: {modelErrors}");
                }
                else
                {
                    _loggerManager.LogError(
                        $"Permissions save rejected for company {companyId:N}, user {model.UserId}. The requested permission set was outside the company scope or the user could not be updated.");
                }

                await _notificationManager.Error(_sharedLocalizer["SuperUser_PermissionsSaveFailed"]);

                var editorModel = await _permissionService.GetEditorAsync(companyId, model.UserId, cancellationToken);
                if (editorModel is null)
                {
                    _loggerManager.LogError($"Permissions editor could not be reloaded for company {companyId:N}, user {model.UserId} after a failed save.");
                    return RedirectToAction(nameof(Users));
                }

                editorModel.Groups ??= [];
                return View(editorModel);
            }

            await _notificationManager.Success(_sharedLocalizer["SuperUser_PermissionsSaved"]);
            return RedirectToAction(nameof(Permissions), new { model.UserId });
        }

        [HttpGet]
        public async Task<IActionResult> CreateUser()
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser?.CompanyId is null)
            {
                await _notificationManager.Error(_sharedLocalizer["SuperUser_MissingCompanyId"]);
                return Forbid();
            }

            var company = await _adminCompanyRepository.GetCompanyByIdAsync(currentUser.CompanyId.Value);

            var vm = new AdminCreateUserViewModel
            {
                CompanyId = currentUser.CompanyId.Value,
                AllCompanies = new List<AdminAllCompaniesForSelectListVM>
                {
                    new AdminAllCompaniesForSelectListVM
                    {
                        Id = company?.Id ?? currentUser.CompanyId.Value,
                        Name = company?.Name ?? _sharedLocalizer["SuperUser_CurrentCompany"]
                    }
                }
            };

            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateUser(AdminCreateUserViewModel model)
        {
            var isWhitelisted = await _userWhitelistService.IsWhitelistedAsync(model.Email, null, model.CompanyId);
            if (isWhitelisted)
                ModelState.Remove(nameof(model.PersSign));
            else if (string.IsNullOrWhiteSpace(model.PersSign))
                ModelState.AddModelError(nameof(model.PersSign), "PerssignRequired");

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser?.CompanyId is null)
            {
                await _notificationManager.Error(_sharedLocalizer["SuperUser_MissingCompanyId"]);
                return Forbid();
            }

            var user = CreateNewUser();
            await _userStore.SetUserNameAsync(user, model.Email, CancellationToken.None);
            await _emailStore.SetEmailAsync(user, model.Email, CancellationToken.None);

            user.FirstName = model.FirstName;
            user.LastName = model.LastName;
            user.CompanyId = currentUser.CompanyId;
            user.Language = model.Language;
            user.PersSign = string.IsNullOrWhiteSpace(model.PersSign) ? null : model.PersSign;
            user.PhoneNumber = model.PhoneNumber;

            var result = await _userManager.CreateAsync(user);

            if (result.Succeeded)
            {
                _loggerManager.LogInfo($"created user {user.UserName}");

                await _userManager.AddToRoleAsync(user, "User");
                await EnsureWhitelistEntryAsync(user.Email, user.Id, user.CompanyId, _userManager.GetUserId(User));

                var code = await _userManager.GeneratePasswordResetTokenAsync(user);
                code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(code));
                var callbackUrl = Url.Page(
                    "/Account/ConfirmAccount",
                    pageHandler: null,
                    values: new { area = "Identity", code, email = user.Email },
                    protocol: Request.Scheme);
                var mail = new MailModel
                {
                    Subject = "Confirm your ZeeU portal account!",
                    To = user.Email ?? string.Empty,
                    Header = $"Welcome {user.FirstName}",
                    Text = "Please confirm your account by clicking on the link below. The link is valid for 3 days.",
                    VerificationURL = callbackUrl,
                    VerificationUrlText = "Verify"
                };

                try
                {
                    await _mailManager.SendVerificationMailAsync(mail);
                    await _notificationManager.Success(_sharedLocalizer["SuperUser_AddedUser", user.Email ?? string.Empty]);
                }
                catch (Exception ex)
                {
                    _loggerManager.LogError($"User created but mail failed: {ex.Message}");
                    await _notificationManager.Warning(_sharedLocalizer["SuperUser_UserCreatedMailFailed", user.Email ?? string.Empty]);
                }
                return RedirectToAction("CreateUser", "SuperUser");
            }

            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }

            return View(model);
        }

        private async Task EnsureWhitelistEntryAsync(string? email, string? userId, Guid? companyId, string? createdByUserId)
        {
            if (companyId is null)
                return;

            var trimmedEmail = email?.Trim().ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(trimmedEmail) && string.IsNullOrWhiteSpace(userId))
                return;

            var exists = await _context.UserWhitelists!
                .AnyAsync(x => x.CompanyId == companyId &&
                               x.IsActive &&
                               ((trimmedEmail != null && x.Email == trimmedEmail) ||
                                (userId != null && x.UserId == userId)));

            if (exists)
                return;

            _context.UserWhitelists!.Add(new ApplicationUserWhitelist
            {
                Id = Guid.NewGuid(),
                CompanyId = companyId,
                Email = trimmedEmail,
                UserId = userId,
                CreatedByUserId = createdByUserId,
                CreatedAtUtc = DateTime.UtcNow,
                IsActive = true,
                Note = "Auto-whitelisted on user create/update."
            });

            await _context.SaveChangesAsync();
        }

        private ApplicationUser CreateNewUser()
        {
            try
            {
                return Activator.CreateInstance<ApplicationUser>();
            }
            catch
            {
                throw new InvalidOperationException($"Can't create an instance of '{nameof(ApplicationUser)}'. " +
                    $"Ensure that '{nameof(ApplicationUser)}' is not an abstract class and has a parameterless constructor, or alternatively " +
                    $"override the register page in /Areas/Identity/Pages/Account/Register.cshtml");
            }
        }

        private IUserEmailStore<ApplicationUser> GetEmailStore()
        {
            if (!_userManager.SupportsUserEmail)
            {
                throw new NotSupportedException("The default UI requires a user store with email support.");
            }
            return (IUserEmailStore<ApplicationUser>)_userStore;
        }
    }
}
