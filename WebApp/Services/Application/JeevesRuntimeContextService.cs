using Entities.Application;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using WebApp.Data;
using WebApp.Helpers;

namespace WebApp.Services.Application;

public sealed class JeevesRuntimeContextService : IJeevesRuntimeContextService
{
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(2);

    private readonly IDbContextFactory<ApplicationDbContext> _dbContextFactory;
    private readonly IApplicationUserContextService _userContextService;
    private readonly IApplicationCompanyContextService _companyContextService;
    private readonly IApplicationConnectionContextService _connectionContextService;
    private readonly IConnectionStringResolver _connectionStringResolver;
    private readonly IJeevesCompanyAccessService _jeevesCompanyAccessService;
    private readonly IMemoryCache _cache;
    private readonly ILogger<JeevesRuntimeContextService> _logger;

    public JeevesRuntimeContextService(
        IDbContextFactory<ApplicationDbContext> dbContextFactory,
        IApplicationUserContextService userContextService,
        IApplicationCompanyContextService companyContextService,
        IApplicationConnectionContextService connectionContextService,
        IConnectionStringResolver connectionStringResolver,
        IJeevesCompanyAccessService jeevesCompanyAccessService,
        IMemoryCache cache,
        ILogger<JeevesRuntimeContextService> logger)
    {
        _dbContextFactory = dbContextFactory;
        _userContextService = userContextService;
        _companyContextService = companyContextService;
        _connectionContextService = connectionContextService;
        _connectionStringResolver = connectionStringResolver;
        _jeevesCompanyAccessService = jeevesCompanyAccessService;
        _cache = cache;
        _logger = logger;
    }

    public async Task<OperationResult<JeevesRuntimeContext>> ResolveAsync(UserSession? sessionUser, CancellationToken cancellationToken = default)
    {
        if (sessionUser is null || string.IsNullOrWhiteSpace(sessionUser.UserId))
            return OperationResult<JeevesRuntimeContext>.Fail("User session is missing.");

        var cacheKey = BuildCacheKey(sessionUser);
        if (_cache.TryGetValue(cacheKey, out JeevesRuntimeContext? cached) && cached is not null)
            return OperationResult<JeevesRuntimeContext>.Ok(cached);

        try
        {
            await using var context = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

            var applicationUser = await _userContextService.GetUserByIdAsync(context, sessionUser.UserId);
            if (applicationUser?.CompanyId is not Guid companyId || companyId == Guid.Empty)
            {
                _logger.LogWarning("Could not resolve company for runtime context. UserId: {UserId}", sessionUser.UserId);
                return OperationResult<JeevesRuntimeContext>.Fail("User company is missing.");
            }

            if (applicationUser.ActiveConnectionStringId is not Guid activeConnectionStringId || activeConnectionStringId == Guid.Empty)
            {
                _logger.LogWarning("Could not resolve active connection mapping. UserId: {UserId}", sessionUser.UserId);
                return OperationResult<JeevesRuntimeContext>.Fail("Active connection string mapping is missing.");
            }

            var company = await _companyContextService.GetCompanyAsync(context, companyId);
            if (company is null)
            {
                _logger.LogWarning("Could not resolve company entity for runtime context. UserId: {UserId}, CompanyId: {CompanyId}", sessionUser.UserId, companyId);
                return OperationResult<JeevesRuntimeContext>.Fail("Company could not be resolved.");
            }

            var connectionStrings = await _connectionContextService.GetConnectionStringsAsync(context, companyId);
            var resolvedConnection = await _connectionStringResolver.ResolveAsync(connectionStrings, activeConnectionStringId, companyId);
            if (!resolvedConnection.Success || string.IsNullOrWhiteSpace(resolvedConnection.Value))
            {
                _logger.LogWarning("Could not resolve Jeeves connection string for runtime context. UserId: {UserId}, CompanyId: {CompanyId}", sessionUser.UserId, companyId);
                return OperationResult<JeevesRuntimeContext>.Fail(resolvedConnection.Error ?? "Connection string could not be resolved.");
            }

            var runtimeUser = BuildRuntimeSessionUser(sessionUser, applicationUser, company.Name, companyId);
            var companyCode = await ResolveCompanyCodeAsync(runtimeUser, cancellationToken);
            if (companyCode is null || companyCode <= 0)
            {
                _logger.LogWarning("Could not resolve an allowed Jeeves company for runtime context. UserId: {UserId}", sessionUser.UserId);
                return OperationResult<JeevesRuntimeContext>.Fail("No allowed Jeeves company could be resolved.");
            }

            var companyName = await _jeevesCompanyAccessService.ResolveCompanyNameAsync(runtimeUser, companyCode, cancellationToken);
            var runtimeContext = new JeevesRuntimeContext
            {
                UserId = applicationUser.Id,
                CompanyId = companyId,
                CompanyCode = companyCode.Value,
                ConnectionString = resolvedConnection.Value,
                CompanyName = string.IsNullOrWhiteSpace(companyName) ? company.Name ?? string.Empty : companyName,
                Email = applicationUser.Email,
                PersSign = applicationUser.PersSign,
                FirstName = applicationUser.FirstName,
                LastName = applicationUser.LastName
            };

            _cache.Set(cacheKey, runtimeContext, CacheDuration);
            return OperationResult<JeevesRuntimeContext>.Ok(runtimeContext);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to resolve Jeeves runtime context for user {UserId}", sessionUser.UserId);
            return OperationResult<JeevesRuntimeContext>.Fail("Runtime context resolution failed.");
        }
    }

    private async Task<int?> ResolveCompanyCodeAsync(UserSession runtimeUser, CancellationToken cancellationToken)
    {
        if (runtimeUser.JeevesActiveCompany is int requestedCompanyCode
            && requestedCompanyCode > 0
            && await _jeevesCompanyAccessService.HasCompanyAccessAsync(runtimeUser, requestedCompanyCode, cancellationToken))
        {
            return requestedCompanyCode;
        }

        return await _jeevesCompanyAccessService.ResolveCompanyCodeAsync(runtimeUser, cancellationToken);
    }

    private static UserSession BuildRuntimeSessionUser(
        UserSession sessionUser,
        Models.Identity.ApplicationUser applicationUser,
        string? companyName,
        Guid companyId)
    {
        return new UserSession
        {
            UserId = applicationUser.Id,
            Email = applicationUser.Email,
            FirstName = applicationUser.FirstName,
            LastName = applicationUser.LastName,
            Language = applicationUser.Language,
            CompanyId = companyId,
            CompanyName = companyName,
            PersSign = applicationUser.PersSign,
            JeevesActiveCompany = sessionUser.JeevesActiveCompany
        };
    }

    private static string BuildCacheKey(UserSession sessionUser)
        => $"JeevesRuntimeContext:{sessionUser.UserId}:{sessionUser.JeevesActiveCompany?.ToString() ?? "default"}";
}
