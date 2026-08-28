// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
#nullable disable

using System;
using System.Threading.Tasks;
using Entities.Application;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;
using WebApp.Models.Identity;
using WebApp.Services.ExcelImport;
using WebApp.Services;

namespace WebApp.Areas.Identity.Pages.Account
{
    public class LogoutModel : PageModel
    {
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly IExcelImportTransientStatusStore _excelImportTransientStatusStore;
        private readonly ILogger<LogoutModel> _logger;

        public LogoutModel(
            SignInManager<ApplicationUser> signInManager,
            IExcelImportTransientStatusStore excelImportTransientStatusStore,
            ILogger<LogoutModel> logger)
        {
            _signInManager = signInManager;
            _excelImportTransientStatusStore = excelImportTransientStatusStore;
            _logger = logger;
        }

        public async Task<IActionResult> OnPost(string returnUrl = null)
        {
            var sessionUser = HttpContext.Session.Get<UserSession>("UserObject");
            if (sessionUser?.CompanyId is Guid companyId && companyId != Guid.Empty)
            {
                _excelImportTransientStatusStore.ClearCompany(companyId);
            }

            await _signInManager.SignOutAsync();
            HttpContext.Session.Clear();
            _logger.LogInformation("User logged out.");
            if (returnUrl != null)
            {
                return LocalRedirect(returnUrl);
            }
            else
            {
                // This needs to be a redirect so that the browser performs a new
                // request and the identity for the user gets updated.
                return RedirectToPage();
            }
        }
    }
}
