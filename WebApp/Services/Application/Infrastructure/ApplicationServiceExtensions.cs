using AspNetCoreHero.ToastNotification;
using Entities.Application;
using Entities.Contracts;
using Entities.Mail;
using LoggerService;
using MailService;
using Microsoft.AspNetCore.Localization;
using NotificationService;
using Repository;
using Repository.Contracts;
using Repository.Execution;
using System.Globalization;
using WebApp.Filters;
using WebApp.Helpers;
using WebApp.Models.Identity;
using WebApp.Repositories.Customers;
using WebApp.Repositories.Jeeves;
using WebApp.Repositories.NotifyMe;
using WebApp.Services.ActionCenter;
using WebApp.Services.Admin;
using WebApp.Services.Application;
using WebApp.Services.Application.BackgroundJobs;
using WebApp.Services.Budget;
using WebApp.Services.ControlPanel;
using WebApp.Services.CustomerActivity;
using WebApp.Services.Dashboard;
using WebApp.Services.ExcelImport;
using WebApp.Services.Invoices;
using WebApp.Services.Integration.FlowEngine;
using WebApp.Services.Integration.Infrastructure;
using WebApp.Services.NotifyMe;
using WebApp.Services.Orders;
using WebApp.Services.PriceUpdate;
using WebApp.Services.Purchase.Infrastructure;
using WebApp.Services.PurchasePrice;
using WebApp.Services.Telemetry;
using WebApp.Services.SuperUser;
using WebApp.Services.Vouchers;

namespace WebApp.Services.Application.Infrastructure;

// Registers portal, domain, UI, session, and repository services that are not external API integrations.
public static class ApplicationServiceExtensions
{
        public static void ConfigureLoggerService(this IServiceCollection services)
        {
            services.AddScoped<ILoggerManager, LoggerManager>();
        }

        public static IServiceCollection AddPortalApplicationServices(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddDependencyInjections(configuration);
            services.AddRepositoryInjections();

            services.AddAdminServices();
            services.AddTelemetryServices();
            services.AddInvoiceServices(configuration);
            services.AddOrderServices(configuration);
            services.AddDocumentSigningServices(configuration);
            services.AddDashboardServices(configuration);
            services.AddNotifyMeServices(configuration);
            services.AddControlPanelServices(configuration);
            services.AddCustomerActivityServices(configuration);
            services.AddVoucherServices(configuration);
            services.AddBudgetServices(configuration);
            services.AddPurchaseServices();
            services.AddPurchasePriceServices(configuration);
            services.AddPriceUpdateServices(configuration);
            services.AddActionCenterServices();
            services.AddIntegrationServices(configuration);
            services.AddScoped<DataRetentionService>();
            services.AddHostedService<DataRetentionWorker>();

            return services;
        }

        public static void AddDependencyInjections(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddTransient<ICompany, Company>();
            services.AddTransient<ICompanyLicense, CompanyLicense>();
            services.AddTransient<ICompanyPermission, CompanyPermission>();
            services.AddTransient<IModule, Module>();
            services.AddTransient<ISubModule, SubModule>();
            services.AddTransient<IUser, User>();
            services.AddTransient<IZeeuProduct, ZeeuProduct>();
            services.AddTransient<IMailModel, MailModel>();
            services.AddTransient<IApplicationHelper, ApplicationHelper>();
            services.AddScoped<IJeevesSqlExecutor, JeevesSqlExecutor>();
            services.AddScoped<IJeevesConnectionResolver, JeevesConnectionResolver>();
            services.AddScoped<IUserSessionBootstrapService, UserSessionBootstrapService>();
            services.AddScoped<IApplicationUserContextService, ApplicationUserContextService>();
            services.AddScoped<IApplicationCompanyContextService, ApplicationCompanyContextService>();
            services.AddScoped<IApplicationConnectionContextService, ApplicationConnectionContextService>();
            services.AddScoped<IApplicationContextService, ApplicationContextService>();
            services.AddScoped<IConnectionStringResolver, ConnectionStringResolver>();
            services.AddScoped<ICompanyBuilder, CompanyBuilder>();
            services.AddScoped<IApplicationSessionService, ApplicationSessionService>();
            services.AddScoped<IApplicationMenuService, ApplicationMenuService>();
            services.AddScoped<IUserPermissionAccessService, UserPermissionAccessService>();
            services.AddScoped<ISuperUserPermissionService, SuperUserPermissionService>();
            services.AddScoped<IPortalEventLogService, PortalEventLogService>();
            services.AddScoped<ITechnicalErrorNotificationService, TechnicalErrorNotificationService>();
            services.AddBackgroundJobServices(configuration);
            services.AddExcelImportServices();
            services.AddScoped<ISidebarRuntimeStatusService, SidebarRuntimeStatusService>();
            services.AddScoped<IJeevesCompanyAccessService, JeevesCompanyAccessService>();
            services.AddScoped<IJeevesRuntimeContextService, JeevesRuntimeContextService>();
            services.AddScoped<PortalCookieEvents>();
            services.AddSingleton<PortalAuthenticationTicketStore>();
            services.AddScoped<ITenantGuard, TenantGuard>();
            services.AddScoped<TenantValidationFilter>();
            services.AddScoped<ICompanyPermissionGuard, CompanyPermissionGuard>();
            services.AddScoped<IPersSignService, PersSignService>();
            services.AddScoped<IUserWhitelistService, UserWhitelistService>();
            services.AddScoped<IFeatureAccessService, FeatureAccessService>();
            services.AddTransient<INotificationManager, NotificationManager>();
            services.AddTransient<IMailManager, MailManager>();
        }

        public static void AddRepositoryInjections(this IServiceCollection services)
        {
            services.AddTransient<IAdminCompanyRepository, AdminCompanyRepository>();
            services.AddTransient<IAdminUserLookupRepository, AdminUserLookupRepository>();
            services.AddTransient<IAdminRepository, AdminRepository>();
            services.AddTransient<IApplicationMenuRepository, ApplicationMenuRepository>();
            services.AddTransient<IApplicationUserRepository, ApplicationUserRepository>();
            services.AddTransient<IApplicationRepository, ApplicationRepository>();
            services.AddTransient<IWebApprovalOrderRepository, WebApprovalOrderRepository>();
            services.AddTransient<IWebApprovalPriceListRepository, WebApprovalPriceListRepository>();
            services.AddTransient<IWebApprovalPurchaseRepository, WebApprovalPurchaseRepository>();
            services.AddTransient<IUserRepository, UserRepository>();
            services.AddTransient<ICustomerRepository, JeevesCustomerRepository>();
            services.AddTransient<IPurchaseRepository, PurchaseRepository>();
            services.AddTransient<IZeeuDashboardRepository, ZeeuDashboardRepository>();
            services.AddTransient<IJeevesUserRepository, JeevesUserRepository>();
        }

        public static void AddNotificationService(this IServiceCollection services)
        {
            services.AddNotyf(config =>
            {
                config.DurationInSeconds = 10;
                config.IsDismissable = true;
                config.Position = NotyfPosition.TopRight;
            });
        }

        public static void AddSession(this IServiceCollection services, IConfiguration configuration)
        {
            var authenticationSection = configuration.GetSection(PortalAuthenticationOptions.SectionName);
            var sessionIdleMinutes = Math.Max(
                10,
                authenticationSection.GetValue<int?>(nameof(PortalAuthenticationOptions.SessionIdleMinutes)) ?? 45);
            var enforceSecureCookies =
                authenticationSection.GetValue<bool?>(nameof(PortalAuthenticationOptions.EnforceSecureCookies)) ?? true;

            services.AddDistributedMemoryCache();
            services.AddSession(options =>
            {
                options.Cookie.Name = ".ZeeuCustomerPortal.Session";
                options.IdleTimeout = TimeSpan.FromMinutes(sessionIdleMinutes);

                options.Cookie.HttpOnly = true;
                options.Cookie.IsEssential = true;
                options.Cookie.SameSite = SameSiteMode.Strict;
                options.Cookie.SecurePolicy = enforceSecureCookies
                    ? CookieSecurePolicy.Always
                    : CookieSecurePolicy.SameAsRequest;
            });
        }

        public static void ConfigureLocalization(this IServiceCollection services)
        {
            services.AddLocalization(options =>
            {
                options.ResourcesPath = "Resources";
            });

            services.Configure<RequestLocalizationOptions>(options =>
            {
                var supportedCultures = new List<CultureInfo>
                {
                    new CultureInfo("en"),
                    new CultureInfo("sv")
                };
                options.DefaultRequestCulture = new RequestCulture("sv");
                options.SupportedCultures = supportedCultures;
                options.SupportedUICultures = supportedCultures;
            });
        }
}
