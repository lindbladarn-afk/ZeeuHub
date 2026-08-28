using System.Security.Claims;
using Entities.Application;
using Repository.Contracts;
using WebApp.Helpers;
using WebApp.Services.Application;

namespace WebApp.Services.Purchase.Context;

// Resolves the active user/company context and loads shared Purchase lookup data.
public sealed class PurchaseContextService : IPurchaseContextService
{
    private readonly IHttpContextAccessor _contextAccessor;
    private readonly IApplicationHelper _applicationHelper;
    private readonly IJeevesRuntimeContextService _runtimeContextService;
    private readonly IPurchaseRepository _purchaseRepository;

    public PurchaseContextService(
        IHttpContextAccessor contextAccessor,
        IApplicationHelper applicationHelper,
        IJeevesRuntimeContextService runtimeContextService,
        IPurchaseRepository purchaseRepository)
    {
        _contextAccessor = contextAccessor;
        _applicationHelper = applicationHelper;
        _runtimeContextService = runtimeContextService;
        _purchaseRepository = purchaseRepository;
    }

    public async Task<PurchaseRequestContext> BuildAsync(CancellationToken cancellationToken = default)
    {
        var sessionUser = await ResolveSessionUserAsync();
        if (sessionUser is null)
            throw new InvalidOperationException("The user could not be loaded");

        var runtimeContext = await _runtimeContextService.ResolveAsync(sessionUser, cancellationToken);
        if (!runtimeContext.Success || runtimeContext.Value is null)
            throw new InvalidOperationException("Company context could not be resolved");

        var persSign = !string.IsNullOrWhiteSpace(runtimeContext.Value.PersSign)
            ? runtimeContext.Value.PersSign
            : sessionUser.PersSign;

        if (string.IsNullOrWhiteSpace(persSign))
            throw new InvalidOperationException("User PersSign is missing");

        var suppliers = (await _purchaseRepository
            .GetAllSuppliersAsync(runtimeContext.Value.ConnectionString, runtimeContext.Value.CompanyCode))
            .ToList();
        var articles = (await _purchaseRepository
            .GetPurchaseArticlesAsync(runtimeContext.Value.ConnectionString, runtimeContext.Value.CompanyCode))
            .ToList();
        var contacts = (await _purchaseRepository
            .GetAllContactsAsync(runtimeContext.Value.ConnectionString, runtimeContext.Value.CompanyCode))
            .ToList();

        return new PurchaseRequestContext
        {
            ConnectionString = runtimeContext.Value.ConnectionString,
            CompanyCode = runtimeContext.Value.CompanyCode,
            PersSign = persSign,
            FullName = $"{runtimeContext.Value.FirstName ?? sessionUser.FirstName} {runtimeContext.Value.LastName ?? sessionUser.LastName}".Trim(),
            Suppliers = suppliers,
            Articles = articles,
            Contacts = contacts
        };
    }

    private async Task<UserSession?> ResolveSessionUserAsync()
    {
        var httpContext = _contextAccessor.HttpContext;
        var sessionUser = httpContext?.Session.Get<UserSession>("UserObject");
        if (sessionUser is not null)
            return sessionUser;

        var userEmail = httpContext?.User.FindFirstValue(ClaimTypes.Name)
            ?? httpContext?.User.FindFirstValue(ClaimTypes.Email);

        if (string.IsNullOrWhiteSpace(userEmail))
            return null;

        await _applicationHelper.AddUserToSession(userEmail);
        return httpContext?.Session.Get<UserSession>("UserObject");
    }
}
