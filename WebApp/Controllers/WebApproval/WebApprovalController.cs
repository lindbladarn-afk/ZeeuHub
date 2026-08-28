// Owns the WebApproval shell and shared runtime setup for the approval partial controllers.
using Entities.Application;
using Entities.Contracts;
using LoggerService;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using NotificationService;
using Repository.Contracts;
using WebApp.Models.Identity;
using WebApp.Models.Application;
using WebApp.Data;
using WebApp.Helpers;
using WebApp.Services.Admin.ApprovalChains;
using WebApp.Services;
using WebApp.Services.Application;
using WebApp.Observability;
using WebApp.Services.Integration;
using WebApp.ViewModels.Shared;

namespace WebApp.Controllers
{
    [Authorize(Roles = "Administrator,User,SuperUser")]
    public partial class WebApprovalController : BaseController
    {
        private readonly IWebApprovalOrderRepository _orderRepository;
        private readonly IWebApprovalPurchaseRepository _purchaseRepository;
        private readonly IWebApprovalPriceListRepository _priceListRepository;
        private readonly ILoggerManager _loggerManager;
        private readonly INotificationManager _notificationManager;
        private readonly ITechnicalErrorNotificationService _technicalErrorNotificationService;
        private readonly IStringLocalizer<SharedResources> _sharedLocalizer;
        private readonly IApprovalChainDesignerService _approvalChainDesignerService;

        private string? _sqlConnectionString;
        private IUser? _userObject;
        private UserSession? _sessionUser;
        private JeevesRuntimeContext? _runtimeContext;
        private IUser CurrentUser => _userObject ?? throw new InvalidOperationException("WebApproval user is not initialized");
        private string SqlConnectionString => _sqlConnectionString ?? throw new InvalidOperationException("WebApproval connection string is not initialized");
        private string CurrentUserEmail => !string.IsNullOrWhiteSpace(CurrentUser.Email)
            ? CurrentUser.Email
            : throw new InvalidOperationException("WebApproval user email is missing");
        private string CurrentUserPersSign => !string.IsNullOrWhiteSpace(CurrentUser.PersSign)
            ? CurrentUser.PersSign
            : throw new InvalidOperationException("WebApproval user PersSign is missing");

        public WebApprovalController(
            IHttpContextAccessor contextAccessor,
            IApplicationUserRepository applicationUserRepository,
            IWebApprovalOrderRepository orderRepository,
            IWebApprovalPurchaseRepository purchaseRepository,
            IWebApprovalPriceListRepository priceListRepository,
            ILoggerManager loggerManager,
            INotificationManager notificationManager,
            ITechnicalErrorNotificationService technicalErrorNotificationService,
            IStringLocalizer<SharedResources> sharedLocalizer,
            IApprovalChainDesignerService approvalChainDesignerService,
            IApplicationHelper applicationHelper,
            ApplicationDbContext context)
            : base(contextAccessor, applicationUserRepository, notificationManager, applicationHelper, context)
        {
            _orderRepository = orderRepository;
            _purchaseRepository = purchaseRepository;
            _priceListRepository = priceListRepository;
            _loggerManager = loggerManager;
            _notificationManager = notificationManager;
            _technicalErrorNotificationService = technicalErrorNotificationService;
            _sharedLocalizer = sharedLocalizer;
            _approvalChainDesignerService = approvalChainDesignerService;
        }

        // Minimal session-context. Approval-vyerna kör nu på runtime-resolved tenant context i stället för lagrad session-connection.
        private async Task InitializeAsync()
        {
            _sessionUser = HttpContext?.Session?.Get<UserSession>("UserObject");

            _userObject = await GetCurrentUserAsync();
            if (_userObject is null)
            {
                _loggerManager.LogError("WebApproval InitializeAsync: GetCurrentUserAsync returned null");
                throw new InvalidOperationException("The user could not be loaded");
            }

            _runtimeContext = await ResolveCurrentRuntimeContextAsync();
            if (_runtimeContext is null)
            {
                _loggerManager.LogError($"WebApproval InitializeAsync: Runtime context missing for user {_userObject.Email}");
                throw new InvalidOperationException("Company context could not be resolved");
            }

            if (_userObject.CompanyId is null)
                _userObject.CompanyId = _runtimeContext.CompanyId;

            if (string.IsNullOrWhiteSpace(_userObject.Email))
                _userObject.Email = _runtimeContext.Email;

            if (string.IsNullOrWhiteSpace(_userObject.PersSign))
                _userObject.PersSign = _runtimeContext.PersSign;

            if (string.IsNullOrWhiteSpace(_userObject.FirstName))
                _userObject.FirstName = _runtimeContext.FirstName;

            if (string.IsNullOrWhiteSpace(_userObject.LastName))
                _userObject.LastName = _runtimeContext.LastName;

            _userObject.JeevesActiveCompany = _runtimeContext.CompanyCode;
            _sqlConnectionString = _runtimeContext.ConnectionString;
        }

        public IActionResult WebApproval()
        {
            return View();
        }

        [HttpGet]
        [Route("WebApproval/AttestChains")]
        public async Task<IActionResult> AttestChains(CancellationToken cancellationToken)
        {
            await InitializeAsync();
            var companyCode = ResolveApprovalChainCompanyCode();
            var model = await _approvalChainDesignerService.BuildAsync(companyCode, cancellationToken);
            return View(model);
        }

        [AllowAnonymous]
        [Route("WebApproval/ThankYou")]
        public IActionResult ThankYou()
        {
            return View();
        }

        private Task NotifyWebApprovalFailureAsync(string header, string details, Exception exception, string? summary = null)
        {
            return _technicalErrorNotificationService.NotifyAsync(
                new TechnicalErrorNotificationRequest
                {
                    Module = "WebApproval",
                    Header = header,
                    Summary = summary,
                    Details = details,
                    CompanyId = _runtimeContext?.CompanyId ?? _userObject?.CompanyId,
                    JeevesCompanyCode = _runtimeContext?.CompanyCode ?? _userObject?.JeevesActiveCompany,
                    UserId = _sessionUser?.UserId,
                    UserEmail = _runtimeContext?.Email ?? _userObject?.Email ?? _sessionUser?.Email,
                    RequestPath = HttpContext?.Request?.Path.Value,
                    Exception = exception
                });
        }

        protected string GetOrCreateSupportId()
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

        protected void LogWebApprovalError(string operation, Exception exception)
        {
            var supportId = GetOrCreateSupportId();
            _loggerManager.LogError($"{operation}. SupportId={supportId} {IntegrationLogSanitizer.Diagnostic(exception.Message)}");
        }

        private short ResolveApprovalChainCompanyCode()
        {
            var companyCode = _runtimeContext?.CompanyCode ?? CurrentUser.JeevesActiveCompany;
            if (companyCode is null)
                throw new InvalidOperationException("Jeeves company code is missing for approval chains.");

            if (companyCode is < short.MinValue or > short.MaxValue)
                throw new InvalidOperationException($"Jeeves company code {companyCode} cannot be used for approval chains.");

            return (short)companyCode.Value;
        }

        private IActionResult RenderModuleUnavailable(string title, string message)
        {
            var companyName = _runtimeContext?.CompanyName
                ?? _sessionUser?.CompanyName
                ?? _userObject?.Company?.Name;

            return View("ModuleUnavailable", new ModuleUnavailableViewModel
            {
                ModuleLabel = "WebApproval",
                Title = title,
                Subtitle = string.IsNullOrWhiteSpace(companyName) ? null : $"Visar data för: {companyName}",
                State = new ModuleStateViewModel
                {
                    Title = title,
                    Message = message,
                    Note = "Portalen fungerar, men approvals från den aktiva datakällan kunde inte laddas just nu.",
                    Tone = "warning",
                    IconClass = "fa fa-check-square-o",
                    ActionText = "Ladda om sidan",
                    ActionUrl = string.Empty
                }
            });
        }
    }
}
