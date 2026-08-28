using Entities.ViewModels.Admin;

namespace WebApp.Services.Admin;

public interface IAdminUserManagementService
{
    Task<IReadOnlyCollection<AdminUserViewModel>> GetUsersAsync();
    Task<AdminManageUserLoadResult> BuildManageUserViewModelAsync(string userId);
    Task<AdminCreateUserViewModel> BuildCreateUserViewModelAsync();
    Task<AdminManageUserUpdateResult> UpdateUserAsync(AdminUserViewModel model, string? actingUserId);
    Task<AdminCreateUserResult> CreateUserAsync(AdminCreateUserViewModel model, string? actingUserId);
    Task<AdminDeleteUserResult> DeleteUserAsync(string? userId, string? actingUserId);
    Task<AdminSimpleUserActionResult> ResendEmailVerificationTokenAsync(string userId);
    Task<AdminMailPreviewResult> BuildVerificationTestMailPreviewAsync(string userId);
    Task<AdminSimpleUserActionResult> ResetUserPasswordAsync(string userId);
}

public sealed class AdminManageUserLoadResult
{
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }
    public AdminUserViewModel? Model { get; init; }
}

public sealed class AdminManageUserUpdateResult
{
    public bool UserNotFound { get; init; }
    public string? NotFoundMessage { get; init; }
    public AdminUserViewModel? Model { get; init; }
    public bool ShouldReturnView { get; init; }
    public string? ErrorMessage { get; init; }
    public string? WarningMessage { get; init; }
    public string? SuccessMessage { get; init; }
    public List<AdminManageUserValidationError> ValidationErrors { get; init; } = new();
    public List<string> NotificationErrors { get; init; } = new();
}

public sealed class AdminManageUserValidationError
{
    public required string Key { get; init; }
    public required string Message { get; init; }
}

public sealed class AdminCreateUserResult
{
    public AdminCreateUserViewModel Model { get; init; } = new();
    public bool ShouldReturnPartialView { get; init; }
    public bool RedirectToUsers { get; init; }
    public string? SuccessMessage { get; init; }
    public List<AdminManageUserValidationError> ValidationErrors { get; init; } = new();
}

public sealed class AdminDeleteUserResult
{
    public bool RedirectToUsers { get; init; } = true;
    public string? SuccessMessage { get; init; }
    public string? ErrorMessage { get; init; }
}

public sealed class AdminSimpleUserActionResult
{
    public bool Success { get; init; }
    public string? Message { get; init; }
    public string? Email { get; init; }
    public string? TemporaryPassword { get; init; }
}

public sealed class AdminMailPreviewResult
{
    public bool Success { get; init; }
    public string? Message { get; init; }
    public WebApp.ViewModels.Admin.VerificationMailPreviewViewModel? Model { get; init; }
}
