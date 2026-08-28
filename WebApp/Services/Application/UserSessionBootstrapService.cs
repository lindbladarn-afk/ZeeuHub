using System.Collections.Generic;
using System.Linq;
using Entities.Application;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using NotificationService;
using Repository.Contracts;
using WebApp.Data;
using WebApp.Models.Identity;
using WebApp.Services;

namespace WebApp.Services.Application;

// Orchestrates the post-login bootstrap for the current user:
// resolve tenant/company context, fetch Jeeves companies, and store the final session payload.
public sealed class UserSessionBootstrapService : IUserSessionBootstrapService
{
    private readonly IDbContextFactory<ApplicationDbContext> _dbContextFactory;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IUserRepository _userRepository;
    private readonly IApplicationUserContextService _userContextService;
    private readonly IApplicationCompanyContextService _companyContextService;
    private readonly IApplicationConnectionContextService _connectionContextService;
    private readonly IConnectionStringResolver _connectionStringResolver;
    private readonly ICompanyBuilder _companyBuilder;
    private readonly IApplicationSessionService _sessionService;
    private readonly IJeevesCompanyAccessService _jeevesCompanyAccessService;
    private readonly ILogger<UserSessionBootstrapService> _logger;
    private readonly INotificationManager _notificationManager;
    private readonly IUserWhitelistService _userWhitelistService;

    public UserSessionBootstrapService(
        IDbContextFactory<ApplicationDbContext> dbContextFactory,
        UserManager<ApplicationUser> userManager,
        IUserRepository userRepository,
        IApplicationUserContextService userContextService,
        IApplicationCompanyContextService companyContextService,
        IApplicationConnectionContextService connectionContextService,
        IConnectionStringResolver connectionStringResolver,
        ICompanyBuilder companyBuilder,
        IApplicationSessionService sessionService,
        IJeevesCompanyAccessService jeevesCompanyAccessService,
        ILogger<UserSessionBootstrapService> logger,
        INotificationManager notificationManager,
        IUserWhitelistService userWhitelistService)
    {
        _dbContextFactory = dbContextFactory;
        _userManager = userManager;
        _userRepository = userRepository;
        _userContextService = userContextService;
        _companyContextService = companyContextService;
        _connectionContextService = connectionContextService;
        _connectionStringResolver = connectionStringResolver;
        _companyBuilder = companyBuilder;
        _sessionService = sessionService;
        _jeevesCompanyAccessService = jeevesCompanyAccessService;
        _logger = logger;
        _notificationManager = notificationManager;
        _userWhitelistService = userWhitelistService;
    }

    public async Task<bool> AddUserToSessionAsync(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            _logger.LogWarning("Invalid input when adding user to session");
            return false;
        }

        await using var context = await _dbContextFactory.CreateDbContextAsync();

        var user = await _userManager.FindByEmailAsync(email) ?? await _userManager.FindByNameAsync(email);
        if (user is null)
        {
            _logger.LogWarning("User {Email} could not be found", email);
            return false;
        }

        var contextUser = await _userContextService.GetUserByIdAsync(context, user.Id)
            ?? await _userContextService.GetUserByEmailAsync(context, email);
        if (contextUser is null)
        {
            _logger.LogWarning("Context user for {Email} could not be found", email);
            return false;
        }

        if (contextUser.ActiveConnectionStringId is null || contextUser.CompanyId is null)
        {
            _logger.LogError("User {Email} is missing CompanyId or ActiveConnectionStringId", email);
            return false;
        }

        var contextCompany = await _companyContextService.GetCompanyAsync(context, contextUser.CompanyId.Value);
        if (contextCompany is null)
        {
            _logger.LogWarning("Company {CompanyId} could not be found for {Email}", contextUser.CompanyId.Value, email);
            return false;
        }

        var company = await _companyBuilder.BuildAsync(contextCompany, context);
        var connectionStrings = await _connectionContextService.GetConnectionStringsAsync(context, contextCompany.Id);
        var resolvedConnection = await _connectionStringResolver.ResolveAsync(connectionStrings, contextUser.ActiveConnectionStringId.Value, contextCompany.Id);
        if (!resolvedConnection.Success || string.IsNullOrWhiteSpace(resolvedConnection.Value))
        {
            _logger.LogWarning("Could not resolve Jeeves connection string for {Email}", email);
            await _notificationManager.Warning("Ingen datakälla är uppsatt ännu. Du loggas in i begränsat läge.");
            return CreateLimitedSession(user, contextUser, company, email);
        }

        var jeevesCompanies = await ResolveJeevesCompaniesAsync(context, contextUser, company, resolvedConnection.Value);
        if (jeevesCompanies.Count == 0)
        {
            _logger.LogWarning("No Jeeves companies found for {Email} ({PersSign}). Defaulting to current company.", user.Email, user.PersSign);
            await _notificationManager.Warning("Kunde inte hitta kopplade Jeeves-bolag. Vi sätter ditt nuvarande bolag som aktivt.");

            jeevesCompanies = BuildFallbackCompanies(company);
        }

        var activeCompanyCode = jeevesCompanies.FirstOrDefault(x => x.IsDefault)?.CompanyCode
                                ?? jeevesCompanies.FirstOrDefault()?.CompanyCode;
        if (activeCompanyCode is null)
        {
            await _notificationManager.Error("Could not set an active Jeeves company for the user");
            _logger.LogError("Company: {CompanyName}: Could not set an active Jeeves company for: {Email}. Please check [Sy2].[DefForetagKod] for {PersSign}", company.Name, user.Email, user.PersSign);
            return false;
        }

        var sessionUser = new UserSession
        {
            UserId = user.Id,
            Email = user.Email,
            FirstName = user.FirstName,
            LastName = user.LastName,
            Language = user.Language,
            CompanyName = company.Name,
            PersSign = user.PersSign,
            CompanyId = contextUser.CompanyId,
            JeevesActiveCompany = activeCompanyCode,
            HasDataAccess = true
        };

        _jeevesCompanyAccessService.Store(sessionUser, jeevesCompanies);

        if (!_sessionService.TrySetUserSession(sessionUser))
        {
            _logger.LogWarning("Failed to persist user session for {Email}", email);
            return false;
        }

        return true;
    }

    private bool CreateLimitedSession(ApplicationUser user, ApplicationUser contextUser, Company company, string email)
    {
        var limitedSessionUser = new UserSession
        {
            UserId = user.Id,
            Email = user.Email,
            FirstName = user.FirstName,
            LastName = user.LastName,
            Language = user.Language,
            CompanyName = company.Name,
            PersSign = user.PersSign,
            CompanyId = contextUser.CompanyId,
            JeevesActiveCompany = company.DefaultJeevesCompanyCode,
            HasDataAccess = false,
            DataAccessWarning = "Ingen datakälla är uppsatt ännu. Kontakta administratör för att koppla databasen."
        };

        _jeevesCompanyAccessService.Store(limitedSessionUser, BuildFallbackCompanies(company));

        if (!_sessionService.TrySetUserSession(limitedSessionUser))
        {
            _logger.LogWarning("Failed to persist limited user session for {Email}", email);
            return false;
        }

        return true;
    }

    private async Task<List<Entities.User.JeevesCompanyVM>> ResolveJeevesCompaniesAsync(
        ApplicationDbContext context,
        ApplicationUser contextUser,
        Company company,
        string resolvedConnectionString)
    {
        var configuredCompanies = await GetConfiguredCompanyCodesAsync(context, contextUser.CompanyId);
        if (configuredCompanies.Count > 0)
        {
            var configuredAllowedCodes = await GetAllowedCompanyCodesSafeAsync(context, contextUser.Id);
            if (configuredAllowedCodes.Count == 0)
                return configuredCompanies;

            var configuredAllowedSet = configuredAllowedCodes.ToHashSet();
            var filteredConfiguredCompanies = configuredCompanies
                .Where(x => configuredAllowedSet.Contains(x.CompanyCode))
                .ToList();

            if (filteredConfiguredCompanies.Count == 0)
            {
                _logger.LogWarning("User {Email} has configured company-code restrictions but none matched configured Jeeves companies.", contextUser.Email);
                await _notificationManager.Error("Din användare har begränsade företagskoder men inga matchar bolagets konfiguration.");
            }

            return filteredConfiguredCompanies;
        }

        var isWhitelisted = await _userWhitelistService.IsWhitelistedAsync(contextUser.Email, contextUser.Id, contextUser.CompanyId);
        if (isWhitelisted)
        {
            _logger.LogInformation("User {Email} is whitelisted; skipping Jeeves company lookup.", contextUser.Email);
            return BuildFallbackCompanies(company);
        }

        if (string.IsNullOrWhiteSpace(contextUser.PersSign))
        {
            _logger.LogWarning("User {Email} is missing PersSign during Jeeves company lookup. Falling back to current company.", contextUser.Email);
            await _notificationManager.Warning("Jeeves-användaren saknar PersSign. Du loggas in med standardbolag.");
            return BuildFallbackCompanies(company);
        }

        List<Entities.User.JeevesCompanyVM> jeevesCompanies;
        try
        {
            jeevesCompanies = (await _userRepository.GetJeevesCompaniesAsync(resolvedConnectionString, contextUser.PersSign))?.ToList()
                ?? new List<Entities.User.JeevesCompanyVM>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Jeeves company lookup failed for {Email}. Falling back to current company in session.", contextUser.Email);
            await _notificationManager.Warning("Jeeves kunde inte nås just nu. Du loggas in med standardbolag.");
            return BuildFallbackCompanies(company);
        }

        var allowedCodes = await GetAllowedCompanyCodesSafeAsync(context, contextUser.Id);
        if (allowedCodes.Count == 0)
        {
            return jeevesCompanies;
        }

        var allowedSet = allowedCodes.ToHashSet();
        jeevesCompanies = jeevesCompanies
            .Where(x => allowedSet.Contains(x.CompanyCode))
            .ToList();

        if (jeevesCompanies.Count == 0)
        {
            _logger.LogWarning("User {Email} has configured company-code restrictions but none matched PersSign {PersSign}.", contextUser.Email, contextUser.PersSign);
            await _notificationManager.Error("Din användare har begränsade företagskoder men inga matchar aktuell PersSign.");
        }

        return jeevesCompanies;
    }

    private async Task<List<Entities.User.JeevesCompanyVM>> GetConfiguredCompanyCodesAsync(ApplicationDbContext context, Guid? companyId)
    {
        if (!companyId.HasValue)
            return new List<Entities.User.JeevesCompanyVM>();

        return await context.CompanyJeevesCompanies!
            .AsNoTracking()
            .Where(x => x.CompanyId == companyId.Value && x.IsActive)
            .OrderBy(x => x.SortOrder)
            .ThenByDescending(x => x.IsDefault)
            .ThenBy(x => x.CompanyCode)
            .Select(x => new Entities.User.JeevesCompanyVM
            {
                CompanyCode = x.CompanyCode,
                Name = x.DisplayName,
                IsDefault = x.IsDefault
            })
            .ToListAsync();
    }

    private static List<Entities.User.JeevesCompanyVM> BuildFallbackCompanies(Company company)
    {
        var companyCode = company.DefaultJeevesCompanyCode ?? 0;
        return new List<Entities.User.JeevesCompanyVM>
        {
            new()
            {
                CompanyCode = companyCode,
                Name = company.Name ?? "Unknown company",
                IsDefault = true
            }
        };
    }

    private async Task<List<int>> GetAllowedCompanyCodesSafeAsync(ApplicationDbContext context, string userId)
    {
        try
        {
            return await context.UserCompanyAccesses!
                .AsNoTracking()
                .Where(x => x.UserId == userId)
                .Select(x => x.CompanyCode)
                .ToListAsync();
        }
        catch (SqlException ex) when (ex.Number == 208)
        {
            _logger.LogWarning(ex, "Identity.UserCompanyAccesses saknas. Faller tillbaka till alla Jeeves-bolag.");
            return new List<int>();
        }
    }
}
