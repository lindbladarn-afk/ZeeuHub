using Microsoft.AspNetCore.Mvc;
using Entities.ViewModels.Admin;
using WebApp.ViewModels.Admin;

namespace WebApp.Controllers;

// This file owns user administration inside the admin area.
// It keeps user CRUD, whitelist handling, Jeeves validation, and role updates together.
public partial class AdminController
{
    [HttpGet]
    public async Task<IActionResult> Users()
    {
        var users = await _adminUserManagementService.GetUsersAsync();
        return View("~/Views/Admin/Users/Users.cshtml", users);
    }

    [HttpGet]
    [Route("/Admin/ManageUser/{userId}")]
    public async Task<IActionResult> ManageUser(string userId)
    {
        var result = await _adminUserManagementService.BuildManageUserViewModelAsync(userId);
        if (!result.Success || result.Model is null)
        {
            await _notificationManager.Error(result.ErrorMessage ?? "Could not find the user");
            return View("~/Views/Admin/Users/ManageUser.cshtml");
        }
        return View("~/Views/Admin/Users/ManageUser.cshtml", result.Model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ManageUser([FromForm] AdminUserViewModel model)
    {
        var result = await _adminUserManagementService.UpdateUserAsync(model, _userManager.GetUserId(User));
        if (result.UserNotFound)
            return NotFound(result.NotFoundMessage ?? "Unable to load user");

        foreach (var validationError in result.ValidationErrors)
            ModelState.AddModelError(validationError.Key, validationError.Message);

        foreach (var notificationError in result.NotificationErrors)
            await _notificationManager.Error(notificationError);

        if (!string.IsNullOrWhiteSpace(result.ErrorMessage))
            await _notificationManager.Error(result.ErrorMessage);

        if (!string.IsNullOrWhiteSpace(result.WarningMessage))
            await _notificationManager.Warning(result.WarningMessage);

        if (result.ShouldReturnView && result.Model is not null)
            return View("~/Views/Admin/Users/ManageUser.cshtml", result.Model);

        if (!string.IsNullOrWhiteSpace(result.SuccessMessage))
            await _notificationManager.Success(result.SuccessMessage);

        return View("~/Views/Admin/Users/ManageUser.cshtml", result.Model ?? model);
    }

    [HttpGet]
    public async Task<IActionResult> CreateUser()
    {
        var model = await _adminUserManagementService.BuildCreateUserViewModelAsync();
        return PartialView("~/Views/Admin/Users/_CreateUser.cshtml", model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateUser(AdminCreateUserViewModel model)
    {
        var result = await _adminUserManagementService.CreateUserAsync(model, _userManager.GetUserId(User));
        foreach (var validationError in result.ValidationErrors)
            ModelState.AddModelError(validationError.Key, validationError.Message);

        if (result.ShouldReturnPartialView || !ModelState.IsValid)
            return PartialView("~/Views/Admin/Users/_CreateUser.cshtml", result.Model);

        return Json(new
        {
            success = true,
            redirectUrl = Url.Action("Users"),
            message = result.SuccessMessage ?? "Användaren skapades."
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteUser(string userId)
    {
        var result = await _adminUserManagementService.DeleteUserAsync(userId, _userManager.GetUserId(User));
        if (!string.IsNullOrWhiteSpace(result.ErrorMessage))
            await _notificationManager.Error(result.ErrorMessage);
        if (!string.IsNullOrWhiteSpace(result.SuccessMessage))
            await _notificationManager.Success(result.SuccessMessage);
        return RedirectToAction(nameof(Users));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResendEmailVerificationToken(string userId)
    {
        var result = await _adminUserManagementService.ResendEmailVerificationTokenAsync(userId);
        if (result.Success)
            await _notificationManager.Success(result.Message ?? "Sent verification email");
        else
            await _notificationManager.Error(result.Message ?? "Could not find the user");

        return RedirectToAction(nameof(Users));
    }

    [HttpGet]
    public async Task<IActionResult> PreviewVerificationTestMail(string userId)
    {
        var result = await _adminUserManagementService.BuildVerificationTestMailPreviewAsync(userId);
        if (!result.Success || result.Model is null)
        {
            return PartialView("~/Views/Admin/Users/_VerificationMailPreview.cshtml", new VerificationMailPreviewViewModel
            {
                Success = false,
                ErrorMessage = result.Message ?? "Could not find the user"
            });
        }

        return PartialView("~/Views/Admin/Users/_VerificationMailPreview.cshtml", result.Model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetUserPassword(string userId)
    {
        var result = await _adminUserManagementService.ResetUserPasswordAsync(userId);
        if (result.Success)
        {
            if (!string.IsNullOrWhiteSpace(result.Email) && !string.IsNullOrWhiteSpace(result.TemporaryPassword))
                await _notificationManager.TemporaryPassword(result.Email, result.TemporaryPassword);
            else
                await _notificationManager.Success(result.Message ?? "Password reset");
        }
        else
            await _notificationManager.Error(result.Message ?? "Failed to reset password");
        return RedirectToAction(nameof(ManageUser), new { userId });
    }
}
