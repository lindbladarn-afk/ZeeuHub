// Coordinates the portal's integration workflows and their company-scoped access checks.
using Entities.Application;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using WebApp.Helpers;
using WebApp.Observability;
using WebApp.Services;
using WebApp.Services.Application;
using WebApp.Services.DocumentSigning;
using WebApp.Services.Integration;
using WebApp.Services.Integration.Akeneo;
using WebApp.Services.Integration.CustomerSync.Application;
using WebApp.Services.Integration.CustomerSync.Presentation;
using WebApp.Services.Integration.FlowEngine;
using WebApp.Services.Orders;

namespace WebApp.Controllers
{
    // Composes integration dependencies and provides shared authorization and error-handling helpers.
    [Authorize(Roles = "Administrator, User, SuperUser, Dashboard")]
    public partial class IntegrationController : Controller
    {
        private readonly ICompanyPermissionGuard _companyPermissionGuard;
        private readonly IHttpContextAccessor _contextAccessor;
        private readonly IJeevesRuntimeContextService _jeevesRuntimeContextService;
        private readonly IStringLocalizer<SharedResources> _sharedLocalizer;

        public IntegrationController(
            ICompanyPermissionGuard companyPermissionGuard,
            IHttpContextAccessor contextAccessor,
            IAkeneoExportService akeneoExportService,
            IJeevesRuntimeContextService jeevesRuntimeContextService,
            IOrdersService ordersService,
            IDocumentSigningService documentSigningService,
            IFlowEngineExecutionService flowEngineExecutionService,
            IFlowEngineRequestNormalizer flowEngineRequestNormalizer,
            IFlowEngineCentraCommandFactory flowEngineCentraCommandFactory,
            IFlowEngineImportOrderWorkflowService flowEngineImportOrderWorkflowService,
            IFlowEngineOrderDocumentExtractionService flowEngineOrderDocumentExtractionService,
            IFlowEngineModuleService flowEngineModuleService,
            IFlowEngineHealthProbeService flowEngineHealthProbeService,
            CustomerSyncPagePresenter customerSyncPagePresenter,
            ICustomerSyncRuntimeConfigurationService customerSyncRuntimeConfigurationService,
            ICustomerSyncHubSpotImportService customerSyncHubSpotImportService,
            ISidebarRuntimeStatusService sidebarRuntimeStatusService,
            IStringLocalizer<SharedResources> sharedLocalizer)
        {
            _companyPermissionGuard = companyPermissionGuard;
            _contextAccessor = contextAccessor;
            _akeneoExportService = akeneoExportService;
            _jeevesRuntimeContextService = jeevesRuntimeContextService;
            _ordersService = ordersService;
            _documentSigningService = documentSigningService;
            _flowEngineExecutionService = flowEngineExecutionService;
            _flowEngineRequestNormalizer = flowEngineRequestNormalizer;
            _flowEngineCentraCommandFactory = flowEngineCentraCommandFactory;
            _flowEngineImportOrderWorkflowService = flowEngineImportOrderWorkflowService;
            _flowEngineOrderDocumentExtractionService = flowEngineOrderDocumentExtractionService;
            _flowEngineModuleService = flowEngineModuleService;
            _flowEngineHealthProbeService = flowEngineHealthProbeService;
            _customerSyncPagePresenter = customerSyncPagePresenter;
            _customerSyncRuntimeConfigurationService = customerSyncRuntimeConfigurationService;
            _customerSyncHubSpotImportService = customerSyncHubSpotImportService;
            _sidebarRuntimeStatusService = sidebarRuntimeStatusService;
            _sharedLocalizer = sharedLocalizer;
        }

        private void SetScopedAlertForAction(string targetAction, string level, string message)
        {
            ScopedAlertTempDataHelper.Add(
                TempData,
                level,
                message,
                ControllerContext.ActionDescriptor.ControllerName,
                targetAction);
        }

        private string GetSafeIntegrationFailureMessage(Exception exception, string fallbackMessage)
        {
            var supportId = GetOrCreateSupportId();
            var diagnostic = IntegrationLogSanitizer.Diagnostic(exception.Message);
            return string.IsNullOrWhiteSpace(diagnostic)
                ? $"{fallbackMessage}. Referens: {supportId}."
                : $"{fallbackMessage}. Referens: {supportId}. {diagnostic}";
        }

        private string GetOrCreateSupportId()
        {
            var supportId = HttpContext?.Items[PortalObservability.SupportIdItemKey]?.ToString();
            if (!string.IsNullOrWhiteSpace(supportId))
            {
                return supportId!;
            }

            supportId = Guid.NewGuid().ToString("N")[..8];
            if (HttpContext is { } httpContext)
            {
                httpContext.Items[PortalObservability.SupportIdItemKey] = supportId;
            }

            return supportId;
        }

        private async Task<bool> HasCompanyPermissionAsync(Guid subModuleId)
        {
            var user = GetFlowEngineSessionUser();
            if (user?.CompanyId is null) return false;
            return await _companyPermissionGuard.HasAccessAsync(user.CompanyId.Value, subModuleId);
        }

        private async Task<bool> HasCompanyPermissionAnyAsync(params Guid[] subModuleIds)
        {
            var user = GetFlowEngineSessionUser();
            if (user?.CompanyId is null) return false;

            foreach (var subModuleId in subModuleIds)
            {
                if (await _companyPermissionGuard.HasAccessAsync(user.CompanyId.Value, subModuleId))
                    return true;
            }

            return false;
        }

        private UserSession? GetFlowEngineSessionUser()
            => _contextAccessor.HttpContext?.Session.Get<UserSession>("UserObject");

        private async Task<OperationResult<JeevesRuntimeContext>> ResolveRuntimeContextAsync(UserSession? user, CancellationToken cancellationToken)
        {
            if (user is null)
                return OperationResult<JeevesRuntimeContext>.Fail("User session is missing.");

            return await _jeevesRuntimeContextService.ResolveAsync(user, cancellationToken);
        }
    }
}
