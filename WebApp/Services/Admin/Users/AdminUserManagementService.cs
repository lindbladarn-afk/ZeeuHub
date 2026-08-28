using Entities.Application;
using Entities.ViewModels.Admin;
using LoggerService;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Routing;
using WebApp.Data;
using WebApp.Helpers;
using WebApp.Mapping;
using WebApp.Models.Identity;
using WebApp.Repositories.Jeeves;
using WebApp.Services.Application;
using WebApp.Services.Admin.Users;
using WebApp.ViewModels.Admin;
using MailService;
using Entities.Mail;
using System.Text;
using Microsoft.AspNetCore.WebUtilities;

namespace WebApp.Services.Admin;

// This service owns the ManageUser use case for the admin area.
// It centralizes loading, validation, and persistence so the controller only handles HTTP concerns.
public class AdminUserManagementService : IAdminUserManagementService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly Repository.Contracts.IAdminCompanyRepository _adminCompanyRepository;
    private readonly Repository.Contracts.IAdminUserLookupRepository _adminUserLookupRepository;
    private readonly IApplicationConnectionContextService _applicationConnectionContextService;
    private readonly IConnectionStringResolver _connectionStringResolver;
    private readonly ApplicationDbContext _context;
    private readonly IJeevesUserRepository _jeevesUserRepository;
    private readonly Repository.Contracts.IUserRepository _userRepository;
    private readonly IUserWhitelistService _userWhitelistService;
    private readonly ILoggerManager _loggerManager;
    private readonly IMailManager _mailManager;
    private readonly IUserStore<ApplicationUser> _userStore;
    private readonly IUserEmailStore<ApplicationUser> _emailStore;
    private readonly LinkGenerator _linkGenerator;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public AdminUserManagementService(
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole> roleManager,
        Repository.Contracts.IAdminCompanyRepository adminCompanyRepository,
        Repository.Contracts.IAdminUserLookupRepository adminUserLookupRepository,
        IApplicationConnectionContextService applicationConnectionContextService,
        IConnectionStringResolver connectionStringResolver,
        ApplicationDbContext context,
        IJeevesUserRepository jeevesUserRepository,
        Repository.Contracts.IUserRepository userRepository,
        IUserWhitelistService userWhitelistService,
        ILoggerManager loggerManager,
        IMailManager mailManager,
        IUserStore<ApplicationUser> userStore,
        LinkGenerator linkGenerator,
        IHttpContextAccessor httpContextAccessor)
    {
        _userManager = userManager;
        _roleManager = roleManager;
        _adminCompanyRepository = adminCompanyRepository;
        _adminUserLookupRepository = adminUserLookupRepository;
        _applicationConnectionContextService = applicationConnectionContextService;
        _connectionStringResolver = connectionStringResolver;
        _context = context;
        _jeevesUserRepository = jeevesUserRepository;
        _userRepository = userRepository;
        _userWhitelistService = userWhitelistService;
        _loggerManager = loggerManager;
        _mailManager = mailManager;
        _userStore = userStore;
        _emailStore = GetEmailStore();
        _linkGenerator = linkGenerator;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task<IReadOnlyCollection<AdminUserViewModel>> GetUsersAsync()
    {
        var users = await _userManager.Users.ToListAsync();
        var rolesAvailable = await _roleManager.Roles.ToListAsync();
        var userRolesLookup = await GetUserRolesLookupAsync(users);
        var companyLookup = await GetUserCompaniesLookupAsync();

        return users.Select(user => new AdminUserViewModel
        {
            UserId = user.Id,
            FirstName = user.FirstName ?? string.Empty,
            LastName = user.LastName ?? string.Empty,
            Email = user.Email ?? string.Empty,
            EmailValidated = user.EmailConfirmed,
            PersSign = user.PersSign ?? string.Empty,
            Roles = GetUserRoles(user, rolesAvailable, userRolesLookup),
            CompanyName = companyLookup.TryGetValue(user.Id, out var companyName) ? companyName : "-"
        }).ToList();
    }

    public async Task<AdminManageUserLoadResult> BuildManageUserViewModelAsync(string userId)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user is null)
        {
            return new AdminManageUserLoadResult
            {
                ErrorMessage = "Could not find the user"
            };
        }

        if (user.CompanyId is null)
        {
            return new AdminManageUserLoadResult
            {
                ErrorMessage = "User has no CompanyId set"
            };
        }

        var phoneNumber = await _userManager.GetPhoneNumberAsync(user);
        var company = await _adminCompanyRepository.GetCompanyByIdAsync(user.CompanyId.Value);
        if (company is null)
        {
            return new AdminManageUserLoadResult
            {
                ErrorMessage = "There was an issue when fetching the company"
            };
        }

        var userViewModel = user.ToAdminUserViewModel();
        userViewModel.PhoneNumber = phoneNumber;
        userViewModel.Company = company;
        userViewModel.CompanyId = company.Id;
        userViewModel.CompanyName = company.Name;
        userViewModel.Roles = (await GetUserRolesAsync(user)).ToList();
        userViewModel.ProfilePicture = user.ProfilePicture;
        await PopulateManageUserModelAsync(userViewModel, user);

        return new AdminManageUserLoadResult
        {
            Success = true,
            Model = userViewModel
        };
    }

    public async Task<AdminCreateUserViewModel> BuildCreateUserViewModelAsync()
    {
        return new AdminCreateUserViewModel
        {
            AllCompanies = await _adminCompanyRepository.GetAllCompaniesForSelectList()
        };
    }

    public async Task<AdminManageUserUpdateResult> UpdateUserAsync(AdminUserViewModel model, string? actingUserId)
    {
        var user = await _userManager.FindByIdAsync(model.UserId);
        if (user is null)
        {
            return new AdminManageUserUpdateResult
            {
                UserNotFound = true,
                NotFoundMessage = "Unable to load user"
            };
        }

        await PopulateManageUserModelAsync(model, user);

        var validationErrors = await ValidateManageUserAsync(model, user);
        if (validationErrors.Count > 0)
        {
            return new AdminManageUserUpdateResult
            {
                Model = model,
                ShouldReturnView = true,
                ValidationErrors = validationErrors
            };
        }

        ApplyBasicUserChanges(model, user, out var userUpdated);

        if (userUpdated)
        {
            var updateResult = await UpdateUserSafelyAsync(user);
            if (!updateResult.Succeeded)
                model.StatusMessage += "Unexpected error when trying to update user.";
        }

        string? warningMessage = null;
        string? errorMessage = null;
        if (model.CompanyId != user.CompanyId || model.ActiveConnectionStringId != user.ActiveConnectionStringId)
        {
            if (model.ActiveConnectionStringId.HasValue)
            {
                var selected = model.AllConnectionStrings?
                    .FirstOrDefault(x => x.Id == model.ActiveConnectionStringId.Value);

                if (selected is null || selected.CompanyId != model.CompanyId)
                {
                    errorMessage = "Cannot set ConnectionString belonging to another company";
                    return new AdminManageUserUpdateResult
                    {
                        Model = model,
                        ShouldReturnView = true,
                        ErrorMessage = errorMessage
                    };
                }
            }

            if (model.CompanyId != user.CompanyId)
                user.CompanyId = model.CompanyId;

            if (model.ActiveConnectionStringId != user.ActiveConnectionStringId)
                user.ActiveConnectionStringId = model.ActiveConnectionStringId;

            var updateResult = await UpdateUserSafelyAsync(user);
            if (!updateResult.Succeeded)
                model.StatusMessage += "Unexpected error when trying to update user.";

            model.ConnectionStrings = await _adminCompanyRepository.GetCompanyConnectionStringsForSelectListAsync(user.CompanyId);
            warningMessage = "Make sure the PersSign is correct if you change the Company or Active environment";
        }

        var restrictionErrors = await SyncUserAllowedCompanyCodesAsync(user.Id, model);
        if (restrictionErrors.Count > 0)
        {
            return new AdminManageUserUpdateResult
            {
                Model = model,
                ShouldReturnView = true,
                WarningMessage = warningMessage,
                ValidationErrors = restrictionErrors
            };
        }

        var roleErrors = await ManageUserRolesAsync(model.Roles ?? new List<AdminUserRolesViewModel>(), user);
        await UpdateWhitelistStateAsync(user.Email, user.Id, user.CompanyId, model.IsWhitelisted, actingUserId);

        return new AdminManageUserUpdateResult
        {
            Model = model,
            WarningMessage = warningMessage,
            SuccessMessage = "User information updated",
            NotificationErrors = roleErrors
        };
    }

    public async Task<AdminCreateUserResult> CreateUserAsync(AdminCreateUserViewModel model, string? actingUserId)
    {
        var validationErrors = new List<AdminManageUserValidationError>();
        // New admin-created users are auto-whitelisted below, so PersSign must not block creation.
        var isWhitelisted = true;
        if (!isWhitelisted && string.IsNullOrWhiteSpace(model.PersSign))
        {
            validationErrors.Add(new AdminManageUserValidationError
            {
                Key = nameof(model.PersSign),
                Message = "PerssignRequired"
            });
        }

        if (!string.IsNullOrWhiteSpace(model.Email))
        {
            var normalized = _userManager.NormalizeEmail(model.Email);
            var exists = await _context.Users.AsNoTracking()
                .AnyAsync(x => x.NormalizedEmail == normalized);
            if (exists)
            {
                validationErrors.Add(new AdminManageUserValidationError
                {
                    Key = nameof(model.Email),
                    Message = "Email already exists."
                });
            }
        }

        if (validationErrors.Count > 0)
        {
            model.AllCompanies = await _adminCompanyRepository.GetAllCompaniesForSelectList();
            return new AdminCreateUserResult
            {
                Model = model,
                ShouldReturnPartialView = true,
                ValidationErrors = validationErrors
            };
        }

        var connectionStrings = await _applicationConnectionContextService.GetConnectionStringsAsync(_context, model.CompanyId);
        var active = connectionStrings.FirstOrDefault(x => x.IsActive);
        if (active is null)
        {
            model.AllCompanies = await _adminCompanyRepository.GetAllCompaniesForSelectList();
            return new AdminCreateUserResult
            {
                Model = model,
                ShouldReturnPartialView = true,
                ValidationErrors = new List<AdminManageUserValidationError>
                {
                    new() { Key = nameof(model.CompanyId), Message = "No active environment found for this company." }
                }
            };
        }

        if (!isWhitelisted)
        {
            var persSignCheck = await ValidatePersSignAsync(model.CompanyId, active.Id, model.PersSign ?? string.Empty);
            if (!persSignCheck.Success)
            {
                model.AllCompanies = await _adminCompanyRepository.GetAllCompaniesForSelectList();
                return new AdminCreateUserResult
                {
                    Model = model,
                    ShouldReturnPartialView = true,
                    ValidationErrors = new List<AdminManageUserValidationError>
                    {
                        new() { Key = nameof(model.PersSign), Message = persSignCheck.Error ?? "PersSign could not be validated." }
                    }
                };
            }
        }

        var user = CreateNewUser();
        await _userStore.SetUserNameAsync(user, model.Email, CancellationToken.None);
        await _emailStore.SetEmailAsync(user, model.Email, CancellationToken.None);

        user.FirstName = model.FirstName;
        user.LastName = model.LastName;
        user.CompanyId = model.CompanyId;
        user.ActiveConnectionStringId = active.Id;
        user.Language = model.Language;
        user.PersSign = string.IsNullOrWhiteSpace(model.PersSign) ? null : model.PersSign;
        user.PhoneNumber = model.PhoneNumber;

        IdentityResult result;
        try
        {
            result = await _userManager.CreateAsync(user);
        }
        catch (InvalidOperationException)
        {
            model.AllCompanies = await _adminCompanyRepository.GetAllCompaniesForSelectList();
            return new AdminCreateUserResult
            {
                Model = model,
                ShouldReturnPartialView = true,
                ValidationErrors = new List<AdminManageUserValidationError>
                {
                    new() { Key = nameof(model.Email), Message = "Email already exists." }
                }
            };
        }

        if (!result.Succeeded)
        {
            return new AdminCreateUserResult
            {
                Model = model,
                RedirectToUsers = true,
                ValidationErrors = result.Errors
                    .Select(x => new AdminManageUserValidationError { Key = string.Empty, Message = x.Description })
                    .ToList()
            };
        }

        _loggerManager.LogInfo($"created user {user.UserName}");
        await _userManager.AddToRoleAsync(user, "User");
        await EnsureWhitelistEntryAsync(user.Email, user.Id, user.CompanyId, actingUserId);

        var mail = await BuildVerificationMailAsync(user);

        await _mailManager.SendVerificationMailAsync(mail);

        return new AdminCreateUserResult
        {
            RedirectToUsers = true,
            SuccessMessage = $"Added user {user.Email}"
        };
    }

    public async Task<AdminDeleteUserResult> DeleteUserAsync(string? userId, string? actingUserId)
    {
        if (string.IsNullOrWhiteSpace(userId))
            return new AdminDeleteUserResult();

        if (userId == actingUserId)
        {
            return new AdminDeleteUserResult
            {
                ErrorMessage = "You cannot delete your own user."
            };
        }

        var user = await _userManager.FindByIdAsync(userId);
        if (user is null)
            return new AdminDeleteUserResult();

        var whitelistEntries = await _context.UserWhitelists!
            .Where(x => x.UserId == userId && x.IsActive)
            .ToListAsync();

        foreach (var entry in whitelistEntries)
            entry.IsActive = false;

        if (whitelistEntries.Count > 0)
            await _context.SaveChangesAsync();

        var result = await _userManager.DeleteAsync(user);
        if (!result.Succeeded)
        {
            return new AdminDeleteUserResult
            {
                ErrorMessage = "Could not delete user."
            };
        }

        return new AdminDeleteUserResult
        {
            SuccessMessage = $"Deleted user {user.Email}"
        };
    }

    public async Task<AdminSimpleUserActionResult> ResendEmailVerificationTokenAsync(string userId)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user is null)
        {
            return new AdminSimpleUserActionResult
            {
                Message = "Could not find the user"
            };
        }

        var mail = await BuildVerificationMailAsync(user);

        await _mailManager.SendVerificationMailAsync(mail);
        return new AdminSimpleUserActionResult
        {
            Success = true,
            Message = $"Sent verification email to {user.Email}"
        };
    }

    public async Task<AdminMailPreviewResult> BuildVerificationTestMailPreviewAsync(string userId)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user is null)
        {
            return new AdminMailPreviewResult
            {
                Message = "Could not find the user"
            };
        }

        var mail = await BuildVerificationMailAsync(user);

        var redirectRecipient = VerificationMailRouting.ResolveRecipient(mail.To);
        var redirectNotice = string.Equals(redirectRecipient, mail.To, StringComparison.OrdinalIgnoreCase)
            ? "Testläget är inte aktivt. Mailet visas som det skulle skickas."
            : $"I utvecklingsläge dirigeras utskicket till {redirectRecipient}.";

        return new AdminMailPreviewResult
        {
            Success = true,
            Model = new VerificationMailPreviewViewModel
            {
                Success = true,
                Recipient = mail.To,
                Subject = mail.Subject,
                RedirectNotice = redirectNotice,
                BodyHtml = VerificationMailTemplateRenderer.RenderBody(mail)
            }
        };
    }

    public async Task<AdminSimpleUserActionResult> ResetUserPasswordAsync(string userId)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user is null)
        {
            return new AdminSimpleUserActionResult
            {
                Message = "Could not find the user"
            };
        }

        var tempPassword = $"Tmp-{Guid.NewGuid():N}".Substring(0, 12) + "aA1!";
        var token = await _userManager.GeneratePasswordResetTokenAsync(user);
        var result = await _userManager.ResetPasswordAsync(user, token, tempPassword);

        if (!result.Succeeded)
        {
            return new AdminSimpleUserActionResult
            {
                Message = "Failed to reset password"
            };
        }

        return new AdminSimpleUserActionResult
        {
            Success = true,
            Message = "Temporary password created",
            Email = user.Email,
            TemporaryPassword = tempPassword
        };
    }

    private async Task PopulateManageUserModelAsync(AdminUserViewModel model, ApplicationUser user)
    {
        model.Company = (await _adminUserLookupRepository.GetUserCompany(model.UserId))!;
        model.Companies = await _adminCompanyRepository.GetAllCompaniesForSelectList();
        model.AllConnectionStrings = await _adminCompanyRepository.GetCompanyConnectionStringsForSelectListAsync(null);
        model.ConnectionStrings = await _adminCompanyRepository.GetCompanyConnectionStringsForSelectListAsync(user.CompanyId);
        model.IsWhitelisted = await _userWhitelistService.IsWhitelistedAsync(user.Email, user.Id, user.CompanyId);
        model.AllowedJeevesCompanyCodes = await GetAllowedCompanyCodesSafeAsync(user.Id);
        model.RestrictToAllowedJeevesCompanies = model.AllowedJeevesCompanyCodes.Count > 0;
        model.JeevesCompanies = await GetJeevesCompaniesForManageUserAsync(model.CompanyId, model.ActiveConnectionStringId, model.PersSign);
    }

    private async Task<MailModel> BuildVerificationMailAsync(ApplicationUser user)
    {
        var callbackUrl = await BuildConfirmAccountUrlAsync(user) ?? string.Empty;
        return VerificationMailFactory.Create(
            user.Email ?? string.Empty,
            user.FirstName ?? string.Empty,
            callbackUrl);
    }

    private async Task<Dictionary<string, List<string>>> GetUserRolesLookupAsync(IList<ApplicationUser> users)
    {
        var lookup = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (var user in users)
        {
            var userRoles = (await _userManager.GetRolesAsync(user)).ToList();
            lookup[user.Id] = userRoles;
        }
        return lookup;
    }

    private static List<AdminUserRolesViewModel> GetUserRoles(
        ApplicationUser user,
        IList<IdentityRole> rolesAvailable,
        Dictionary<string, List<string>> userRolesLookup)
    {
        var userRoleNames = userRolesLookup.TryGetValue(user.Id, out var roles)
            ? roles
            : new List<string>();

        return rolesAvailable.Select(role => new AdminUserRolesViewModel
        {
            RoleId = role.Id,
            RoleName = role.Name ?? string.Empty,
            Selected = role.Name != null && userRoleNames.Contains(role.Name)
        }).ToList();
    }

    private async Task<Dictionary<string, string>> GetUserCompaniesLookupAsync()
    {
        var userCompanies = await _adminUserLookupRepository.GetUserCompaniesLookup();
        return userCompanies.ToDictionary(uc => uc.UserId, uc => uc.CompanyName);
    }

    private async Task<List<AdminManageUserValidationError>> ValidateManageUserAsync(AdminUserViewModel model, ApplicationUser user)
    {
        var validationErrors = new List<AdminManageUserValidationError>();
        var isWhitelisted = model.IsWhitelisted;

        if (!isWhitelisted && string.IsNullOrWhiteSpace(model.PersSign))
        {
            validationErrors.Add(new AdminManageUserValidationError
            {
                Key = nameof(model.PersSign),
                Message = "PerssignRequired"
            });
        }

        if (!isWhitelisted &&
            !string.IsNullOrWhiteSpace(model.PersSign) &&
            (model.PersSign != user.PersSign ||
             model.ActiveConnectionStringId != user.ActiveConnectionStringId ||
             model.CompanyId != user.CompanyId))
        {
            if (!model.ActiveConnectionStringId.HasValue)
            {
                validationErrors.Add(new AdminManageUserValidationError
                {
                    Key = nameof(model.ActiveConnectionStringId),
                    Message = "Select an active environment to validate PersSign."
                });
            }
            else
            {
                var persSignCheck = await ValidatePersSignAsync(
                    model.CompanyId,
                    model.ActiveConnectionStringId.Value,
                    model.PersSign);
                if (!persSignCheck.Success)
                {
                    validationErrors.Add(new AdminManageUserValidationError
                    {
                        Key = nameof(model.PersSign),
                        Message = persSignCheck.Error ?? "PersSign could not be validated."
                    });
                }
            }
        }

        return validationErrors;
    }

    private static void ApplyBasicUserChanges(AdminUserViewModel model, ApplicationUser user, out bool updated)
    {
        updated = false;

        if (model.PhoneNumber != user.PhoneNumber)
        {
            user.PhoneNumber = model.PhoneNumber;
            updated = true;
        }

        if (model.FirstName != user.FirstName)
        {
            user.FirstName = model.FirstName;
            updated = true;
        }

        if (model.LastName != user.LastName)
        {
            user.LastName = model.LastName;
            updated = true;
        }

        if (model.PersSign != user.PersSign)
        {
            user.PersSign = model.PersSign;
            updated = true;
        }

        if (model.EmailValidated != user.EmailConfirmed && model.EmailValidated)
        {
            user.EmailConfirmed = true;
            updated = true;
        }
    }

    private async Task<IEnumerable<AdminUserRolesViewModel>> GetUserRolesAsync(ApplicationUser user)
    {
        var roles = new List<AdminUserRolesViewModel>();
        var rolesAvailable = _roleManager.Roles.ToList();

        foreach (var role in rolesAvailable)
        {
            var selected = role.Name != null && await _userManager.IsInRoleAsync(user, role.Name);
            roles.Add(new AdminUserRolesViewModel
            {
                RoleId = role.Id,
                RoleName = role.Name ?? string.Empty,
                Selected = selected
            });
        }

        return roles;
    }

    private async Task<OperationResult<string>> ResolveCompanyConnectionAsync(Guid companyId, Guid connectionStringId)
    {
        var connStrings = await _applicationConnectionContextService.GetConnectionStringsAsync(_context, companyId);
        return await _connectionStringResolver.ResolveAsync(connStrings, connectionStringId, companyId);
    }

    private async Task<OperationResult<bool>> ValidatePersSignAsync(Guid companyId, Guid connectionStringId, string persSign)
    {
        if (string.IsNullOrWhiteSpace(persSign))
            return OperationResult<bool>.Fail("PersSign is required.");

        var resolved = await ResolveCompanyConnectionAsync(companyId, connectionStringId);
        if (!resolved.Success || string.IsNullOrWhiteSpace(resolved.Value))
            return OperationResult<bool>.Fail(resolved.Error ?? "Could not resolve connection string.");

        try
        {
            var exists = await _jeevesUserRepository.PersSignExistsAsync(resolved.Value, persSign.Trim());
            return exists
                ? OperationResult<bool>.Ok(true)
                : OperationResult<bool>.Fail($"PersSign '{persSign}' not found in Jeeves (SY2).");
        }
        catch (Exception ex)
        {
            return OperationResult<bool>.Fail(ex.Message);
        }
    }

    private async Task<IdentityResult> UpdateUserSafelyAsync(ApplicationUser user)
    {
        if (await HasDuplicateEmailAsync(user))
        {
            user.ConcurrencyStamp = Guid.NewGuid().ToString();
            _context.Users.Update(user);
            await _context.SaveChangesAsync();
            return IdentityResult.Success;
        }

        return await _userManager.UpdateAsync(user);
    }

    private async Task<bool> HasDuplicateEmailAsync(ApplicationUser user)
    {
        if (string.IsNullOrWhiteSpace(user.Email))
            return false;

        var normalized = _userManager.NormalizeEmail(user.Email);
        if (string.IsNullOrWhiteSpace(normalized))
            return false;

        var count = await _context.Users.CountAsync(u => u.NormalizedEmail == normalized);
        return count > 1;
    }

    private async Task<List<Entities.User.JeevesCompanyVM>> GetJeevesCompaniesForManageUserAsync(Guid companyId, Guid? connectionStringId, string persSign)
    {
        if (companyId == Guid.Empty || !connectionStringId.HasValue || string.IsNullOrWhiteSpace(persSign))
            return new List<Entities.User.JeevesCompanyVM>();

        var resolved = await ResolveCompanyConnectionAsync(companyId, connectionStringId.Value);
        if (!resolved.Success || string.IsNullOrWhiteSpace(resolved.Value))
            return new List<Entities.User.JeevesCompanyVM>();

        try
        {
            return (await _userRepository.GetJeevesCompaniesAsync(resolved.Value, persSign.Trim()))?.ToList()
                ?? new List<Entities.User.JeevesCompanyVM>();
        }
        catch
        {
            return new List<Entities.User.JeevesCompanyVM>();
        }
    }

    private async Task<List<AdminManageUserValidationError>> SyncUserAllowedCompanyCodesAsync(string userId, AdminUserViewModel model)
    {
        var validationErrors = new List<AdminManageUserValidationError>();

        List<ApplicationUserCompanyAccess> existing;
        try
        {
            existing = await _context.UserCompanyAccesses!
                .Where(x => x.UserId == userId)
                .ToListAsync();
        }
        catch (SqlException ex) when (ex.Number == 208)
        {
            return validationErrors;
        }

        if (!model.RestrictToAllowedJeevesCompanies)
        {
            if (existing.Count > 0)
            {
                _context.UserCompanyAccesses!.RemoveRange(existing);
                await _context.SaveChangesAsync();
            }
            return validationErrors;
        }

        var selectedCodes = (model.AllowedJeevesCompanyCodes ?? new List<int>())
            .Distinct()
            .ToHashSet();

        if (selectedCodes.Count == 0)
        {
            validationErrors.Add(new AdminManageUserValidationError
            {
                Key = nameof(model.AllowedJeevesCompanyCodes),
                Message = "Välj minst ett Jeeves-bolag när begränsning är aktiverad."
            });
            return validationErrors;
        }

        var availableCodes = model.JeevesCompanies.Select(x => x.CompanyCode).ToHashSet();
        if (availableCodes.Count == 0 || selectedCodes.Except(availableCodes).Any())
        {
            validationErrors.Add(new AdminManageUserValidationError
            {
                Key = nameof(model.AllowedJeevesCompanyCodes),
                Message = "Valda företagskoder matchar inte tillgängliga Jeeves-bolag för angiven PersSign."
            });
            return validationErrors;
        }

        var existingCodes = existing.Select(x => x.CompanyCode).ToHashSet();
        var toRemove = existing.Where(x => !selectedCodes.Contains(x.CompanyCode)).ToList();
        var toAdd = selectedCodes.Where(x => !existingCodes.Contains(x)).ToList();

        if (toRemove.Count > 0)
            _context.UserCompanyAccesses!.RemoveRange(toRemove);

        foreach (var code in toAdd)
        {
            _context.UserCompanyAccesses!.Add(new ApplicationUserCompanyAccess
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                CompanyCode = code,
                CreatedAtUtc = DateTime.UtcNow
            });
        }

        if (toRemove.Count > 0 || toAdd.Count > 0)
            await _context.SaveChangesAsync();

        return validationErrors;
    }

    private async Task<List<int>> GetAllowedCompanyCodesSafeAsync(string userId)
    {
        try
        {
            return await _context.UserCompanyAccesses!
                .AsNoTracking()
                .Where(x => x.UserId == userId)
                .Select(x => x.CompanyCode)
                .ToListAsync();
        }
        catch (SqlException ex) when (ex.Number == 208)
        {
            return new List<int>();
        }
    }

    private async Task<List<string>> ManageUserRolesAsync(List<AdminUserRolesViewModel> model, ApplicationUser user)
    {
        var errors = new List<string>();
        var roles = await _userManager.GetRolesAsync(user);

        var remove = await _userManager.RemoveFromRolesAsync(user, roles);
        if (!remove.Succeeded)
        {
            errors.Add("Could not remove roles from user");
            return errors;
        }

        var add = await _userManager.AddToRolesAsync(user, model.Where(x => x.Selected).Select(y => y.RoleName));
        if (!add.Succeeded)
        {
            errors.Add("Cannot add selected roles to user");
            return errors;
        }

        return errors;
    }

    private async Task UpdateWhitelistStateAsync(string? email, string? userId, Guid? companyId, bool isWhitelisted, string? createdByUserId)
    {
        if (companyId is null)
            return;

        if (isWhitelisted)
        {
            await EnsureWhitelistEntryAsync(email, userId, companyId, createdByUserId);
            return;
        }

        var trimmedEmail = email?.Trim().ToLowerInvariant();
        var entries = await _context.UserWhitelists!
            .Where(x => x.CompanyId == companyId &&
                        x.IsActive &&
                        ((trimmedEmail != null && x.Email == trimmedEmail) ||
                         (userId != null && x.UserId == userId)))
            .ToListAsync();

        if (entries.Count == 0)
            return;

        foreach (var entry in entries)
            entry.IsActive = false;

        await _context.SaveChangesAsync();
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
            throw new NotSupportedException("The default UI requires a user store with email support.");

        return (IUserEmailStore<ApplicationUser>)_userStore;
    }

    private async Task<string?> BuildConfirmAccountUrlAsync(ApplicationUser user)
    {
        var code = await _userManager.GeneratePasswordResetTokenAsync(user);
        code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(code));

        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext is null)
            return null;

        return _linkGenerator.GetUriByPage(
            httpContext,
            page: "/Account/ConfirmAccount",
            values: new { area = "Identity", code, email = user.Email },
            scheme: httpContext.Request.Scheme);
    }
}
