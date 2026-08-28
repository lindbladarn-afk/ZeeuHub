using Entities.Application;
using LoggerService;
using MailService;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using NotificationService;
using Repository.Contracts;
using WebApp.Data;
using WebApp.Helpers;
using WebApp.Models.Identity;
using WebApp.Repositories.Jeeves;
using WebApp.Services.Admin;
using WebApp.Services.Admin.ApprovalChains;
using WebApp.Services.Application;
using WebApp.Services.Application.AI.Billing;
using WebApp.Services.Application.AI.Quota;
using WebApp.Services.Operations;
using WebApp.Models.Application;

namespace WebApp.Controllers;

// AdminController is the portal's administrative entry point.
// This root file owns the shared dependencies and controller identity,
// while feature-specific actions live in partial files next to it.
[Authorize(Roles = "Administrator")]
public partial class AdminController : BaseController
{
    private static readonly Guid LocalDevelopmentTypeId =
        Guid.Parse("0e02e3cc-0fea-4aff-9311-204b4eb6c0d4");

    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly ILoggerManager _loggerManager;
    private readonly INotificationManager _notificationManager;
    private readonly IMailManager _mailManager;
    private readonly WebApp.Services.Telemetry.ITelemetryService _telemetryService;
    private readonly IAdminOverviewService _adminOverviewService;
    private readonly IAdminUserManagementService _adminUserManagementService;
    private readonly IAdminCompanyManagementService _adminCompanyManagementService;
    private readonly IApplicationContextService _applicationContextService;
    private readonly IConnectionStringResolver _connectionStringResolver;
    private readonly ApplicationDbContext _context;
    private readonly IJeevesUserRepository _jeevesUserRepository;
    private readonly IUserRepository _userRepository;
    private readonly IUserWhitelistService _userWhitelistService;
    private readonly IAiQuotaAdminService _aiQuotaAdminService;
    private readonly IAiInvoiceExportService _aiInvoiceExportService;
    private readonly IZeeuOperationsService _zeeuOperationsService;
    private readonly IAdminEventLogService _adminEventLogService;
    private readonly IPortalEventLogService _portalEventLogService;
    private readonly IApprovalChainPurchaseParityService _approvalChainPurchaseParityService;

    public AdminController(IHttpContextAccessor contextAccessor,
                            IApplicationUserRepository applicationUserRepository,
                            UserManager<ApplicationUser> userManager,
                            RoleManager<IdentityRole> roleManager,
                            ILoggerManager loggerManager,
                            INotificationManager notificationManager,
                            IMailManager mailManager,
                            WebApp.Services.Telemetry.ITelemetryService telemetryService,
                            IAdminOverviewService adminOverviewService,
                            IAdminUserManagementService adminUserManagementService,
                            IAdminCompanyManagementService adminCompanyManagementService,
                            IApplicationContextService applicationContextService,
                            IConnectionStringResolver connectionStringResolver,
                            ApplicationDbContext context,
                            IJeevesUserRepository jeevesUserRepository,
                            IUserRepository userRepository,
                            IUserWhitelistService userWhitelistService,
                            IAiQuotaAdminService aiQuotaAdminService,
                            IAiInvoiceExportService aiInvoiceExportService,
                            IAdminEventLogService adminEventLogService,
                            IPortalEventLogService portalEventLogService,
                            IApprovalChainPurchaseParityService approvalChainPurchaseParityService,
                            IZeeuOperationsService zeeuOperationsService,
                            IApplicationHelper applicationHelper)
        : base(contextAccessor, applicationUserRepository, notificationManager, applicationHelper, context)
    {
        _userManager = userManager;
        _roleManager = roleManager;
        _loggerManager = loggerManager;
        _notificationManager = notificationManager;
        _mailManager = mailManager;
        _telemetryService = telemetryService;
        _adminOverviewService = adminOverviewService;
        _adminUserManagementService = adminUserManagementService;
        _adminCompanyManagementService = adminCompanyManagementService;
        _applicationContextService = applicationContextService;
        _connectionStringResolver = connectionStringResolver;
        _context = context;
        _jeevesUserRepository = jeevesUserRepository;
        _userRepository = userRepository;
        _userWhitelistService = userWhitelistService;
        _aiQuotaAdminService = aiQuotaAdminService;
        _aiInvoiceExportService = aiInvoiceExportService;
        _adminEventLogService = adminEventLogService;
        _portalEventLogService = portalEventLogService;
        _approvalChainPurchaseParityService = approvalChainPurchaseParityService;
        _zeeuOperationsService = zeeuOperationsService;
    }
}
