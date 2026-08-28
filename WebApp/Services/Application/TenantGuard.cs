using Entities.Application;
using WebApp.Helpers;

namespace WebApp.Services.Application;

public class TenantGuard : ITenantGuard
{
    private readonly IJeevesCompanyAccessService _jeevesCompanyAccessService;
    private readonly ILogger<TenantGuard> _logger;

    public TenantGuard(IJeevesCompanyAccessService jeevesCompanyAccessService, ILogger<TenantGuard> logger)
    {
        _jeevesCompanyAccessService = jeevesCompanyAccessService;
        _logger = logger;
    }

    public OperationResult<bool> Validate(UserSession? session, int? requestedCompanyCode = null)
    {
        if (session is null)
        {
            _logger.LogWarning("Tenant validation failed: missing session user");
            return OperationResult<bool>.Fail("No session user");
        }

        if (session.JeevesActiveCompany is null)
        {
            _logger.LogWarning("Tenant validation failed: missing active company for user {UserId}", session.UserId);
            return OperationResult<bool>.Fail("No active company");
        }

        var companies = _jeevesCompanyAccessService.GetCompaniesAsync(session).GetAwaiter().GetResult();
        if (companies.Count == 0)
        {
            _logger.LogWarning("Tenant validation failed: user {UserId} has no Jeeves companies", session.UserId);
            return OperationResult<bool>.Fail("No companies");
        }

        var targetCompany = requestedCompanyCode ?? session.JeevesActiveCompany;
        if (!companies.Any(x => x.CompanyCode == targetCompany.Value))
        {
            _logger.LogWarning("Tenant validation failed: company {Company} not in allowed set for user {UserId}", targetCompany, session.UserId);
            return OperationResult<bool>.Fail("Unauthorized company");
        }

        return OperationResult<bool>.Ok(true);
    }
}
