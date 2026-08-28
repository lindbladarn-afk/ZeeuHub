using WebApp.Models.Integration;
using WebApp.Services.Integration.BankReconciliation;
using WebApp.Services.Integration.BankReconciliation.Bundles;
using WebApp.Services.Integration.BankReconciliation.Commands;
using WebApp.Services.Integration.BankReconciliation.CodingRules;
using WebApp.Services.Integration.BankReconciliation.DemoSession;
using WebApp.Services.Integration.BankReconciliation.Invoices;
using WebApp.Services.Integration.BankReconciliation.Imports;
using WebApp.Services.Integration.BankReconciliation.Presentation;
using WebApp.Services.Integration.BankReconciliation.Queries;
using WebApp.Services.Integration.BankReconciliation.SupplierInvoices;
using WebApp.Services.Integration.BankReconciliation.Upload;
using WebApp.Services.Integration.BankReconciliation.UploadFlow;
using WebApp.Services.Integration.BankReconciliation.Validation;
using WebApp.Services.Integration.BankReconciliation.Workspace;
using WebApp.Services.Integration.CustomerSync;
using WebApp.Services.Integration.FlowEngine;
using WebApp.Services.Integration.Speedrecon;
using WebApp.Services.Integration.Speedrecon.Modules;

namespace WebApp.Services.Integration.Infrastructure;

// Registers external integration clients, options, matching, and FlowEngine-facing services.
public static class IntegrationServiceExtensions
{
        public static void AddIntegrationServices(this IServiceCollection services, IConfiguration configuration)
        {
            services.Configure<WebApp.Models.Integration.IntegrationOptions>(configuration.GetSection("Integration"));
            services.PostConfigure<WebApp.Models.Integration.IntegrationOptions>(options =>
            {
                var namedCompanies = ReadNamedIntegrationCompanies(configuration.GetSection("IntegrationNamedCompanies"));
                if (namedCompanies.Count == 0)
                    return;

                foreach (var (_, company) in namedCompanies)
                {
                    if (company.CompanyId == Guid.Empty)
                        continue;

                    var targetCompany = options.Companies.FirstOrDefault(x => x.CompanyId == company.CompanyId);
                    if (targetCompany is null)
                    {
                        targetCompany = new WebApp.Models.Integration.IntegrationCompanyConfig
                        {
                            CompanyId = company.CompanyId
                        };
                        options.Companies.Add(targetCompany);
                    }

                    targetCompany.JeevesCompanyCode = company.JeevesCompanyCode;
                    targetCompany.Enabled = company.Enabled;

                    if (company.Sources is null || company.Sources.Count == 0)
                        continue;

                    foreach (var (sourceKey, source) in company.Sources)
                    {
                        if (!Enum.TryParse<WebApp.Models.Integration.IntegrationSource>(sourceKey, true, out var parsedSource))
                            continue;

                        var targetSource = targetCompany.Sources.FirstOrDefault(x => x.Source == parsedSource);
                        if (targetSource is null)
                        {
                            targetSource = new WebApp.Models.Integration.IntegrationSourceConfig
                            {
                                Source = parsedSource
                            };
                            targetCompany.Sources.Add(targetSource);
                        }

                        targetSource.BaseUrl = source.BaseUrl;
                        targetSource.Token = source.Token;
                        targetSource.TestBaseUrl = source.TestBaseUrl;
                        targetSource.TestToken = source.TestToken;
                        targetSource.AuthUrl = source.AuthUrl;
                        targetSource.AppId = source.AppId;
                        targetSource.AppSecret = source.AppSecret;
                        targetSource.TestAuthUrl = source.TestAuthUrl;
                        targetSource.TestAppId = source.TestAppId;
                        targetSource.TestAppSecret = source.TestAppSecret;
                        targetSource.Username = source.Username;
                        targetSource.Password = source.Password;
                        targetSource.TestUsername = source.TestUsername;
                        targetSource.TestPassword = source.TestPassword;
                        targetSource.GoodsOwnerId = source.GoodsOwnerId;
                        targetSource.TestGoodsOwnerId = source.TestGoodsOwnerId;
                        targetSource.Enabled = source.Enabled;
                    }
                }
            });

            services.Configure<WebApp.Models.Integration.AkeneoOptions>(configuration.GetSection("Akeneo"));
            services.Configure<FlowEngineModuleOptions>(configuration.GetSection(FlowEngineModuleOptions.SectionName));
            services.Configure<BankReconciliationAiSuggestionOptions>(configuration.GetSection(BankReconciliationAiSuggestionOptions.SectionName));
            services.Configure<BankReconciliationPaymentBundleOptions>(configuration.GetSection(BankReconciliationPaymentBundleOptions.SectionName));
            services.Configure<BankReconciliationCamtValidationOptions>(configuration.GetSection(BankReconciliationCamtValidationOptions.SectionName));
            services.AddCustomerSyncServices(configuration);
            services.AddHttpClient("Integration.Centra");
            services.AddHttpClient("Integration.Jeeves");
            services.AddHttpClient("Integration.Jeeves.Auth");
            services.AddHttpClient("Integration.Ongoing");
            services.AddHttpClient("Integration.Akeneo");
            services.AddHttpClient("Integration.Shopify");
            services.AddScoped<WebApp.Repositories.Integration.IIntegrationRepository, WebApp.Repositories.Integration.NoopIntegrationRepository>();
            services.AddScoped<WebApp.Services.Integration.IIntegrationMatcher, WebApp.Services.Integration.IntegrationMatcher>();
            services.AddScoped<WebApp.Services.Integration.IIntegrationSyncService, WebApp.Services.Integration.IntegrationSyncService>();
            services.AddScoped<IBankReconciliationStateService, BankReconciliationStateService>();
            services.AddScoped<IBankReconciliationCodingRuleService, BankReconciliationCodingRuleService>();
            services.AddScoped<IBankReconciliationDemoDataService, BankReconciliationDemoDataService>();
            services.AddScoped<IBankReconciliationSupplierInvoiceRepository, BankReconciliationSupplierInvoiceRepository>();
            services.AddScoped<IBankReconciliationSupplierInvoiceService, BankReconciliationSupplierInvoiceService>();
            services.AddScoped<IBankReconciliationCamtParser, BankReconciliationCamtParser>();
            services.AddScoped<IBankReconciliationCamtValidationService, BankReconciliationCamtValidationService>();
            services.AddScoped<IBankReconciliationImportRegistry, BankReconciliationImportRegistry>();
            services.AddScoped<IBankReconciliationCamtUploadService, BankReconciliationCamtUploadService>();
            services.AddScoped<IBankReconciliationUploadFlowService, BankReconciliationUploadFlowService>();
            services.AddScoped<IBankReconciliationAiSuggestionVerifier, BankReconciliationAiSuggestionVerifier>();
            services.AddScoped<IBankReconciliationAiSuggestionService, OpenAiBankReconciliationSuggestionService>();
            services.AddScoped<IBankReconciliationMatchEligibilityService, BankReconciliationMatchEligibilityService>();
            services.AddScoped<IBankReconciliationService, BankReconciliationService>();
            services.AddScoped<IBankReconciliationPaymentBundleMatcher, BankReconciliationPaymentBundleMatcher>();
            services.AddScoped<IBankReconciliationPaymentBundleService, BankReconciliationPaymentBundleService>();
            services.AddScoped<IBankReconciliationInvoiceCandidateService, BankReconciliationInvoiceCandidateService>();
            services.AddScoped<IBankReconciliationTransactionPageService, BankReconciliationTransactionPageService>();
            services.AddScoped<IBankReconciliationWorkspaceService, BankReconciliationWorkspaceService>();
            services.AddScoped<IBankReconciliationMatchCommandService, BankReconciliationMatchCommandService>();
            services.AddScoped<IBankReconciliationLifecycleCommandService, BankReconciliationLifecycleCommandService>();
            services.AddScoped<IBankReconciliationCodingRuleCommandService, BankReconciliationCodingRuleCommandService>();
            services.AddScoped<IBankReconciliationRecommendationQueryService, BankReconciliationRecommendationQueryService>();
            services.AddScoped<IBankReconciliationStateQueryService, BankReconciliationStateQueryService>();
            services.AddScoped<IBankReconciliationPageQueryService, BankReconciliationPageQueryService>();
            services.AddScoped<IBankReconciliationInvoiceDetailPageQueryService, BankReconciliationInvoiceDetailPageQueryService>();
            services.AddScoped<IBankReconciliationPageTempDataService, BankReconciliationPageTempDataService>();
            services.AddScoped<IBankReconciliationInvoicePageQueryService, BankReconciliationInvoicePageQueryService>();
            services.AddScoped<IBankReconciliationDemoSessionService, BankReconciliationDemoSessionService>();
            services.AddScoped<ISpeedreconRepository, SpeedreconRepository>();
            services.AddScoped<ISpeedreconRunService, SpeedreconRunService>();
            services.AddScoped<ISpeedreconModuleRunner, KundreskontraSpeedreconModuleRunner>();
            services.AddScoped<ISpeedreconModuleRunner, LeverantorsreskontraSpeedreconModuleRunner>();
            services.AddScoped<ISpeedreconModuleRunner, AnlaggningSpeedreconModuleRunner>();
            services.AddScoped<ISpeedreconModuleRunner, InlevereratEjFaktureratSpeedreconModuleRunner>();
            services.AddScoped<ISpeedreconModuleRunner, LegoSpeedreconModuleRunner>();
            services.AddScoped<ISpeedreconModuleRunner, InternLeverantorsreskontraSpeedreconModuleRunner>();
            services.AddScoped<ISpeedreconModuleRunner, LagervardeSpeedreconModuleRunner>();
            services.AddScoped<ISpeedreconModuleRunner, LagerflyttSpeedreconModuleRunner>();
            services.AddScoped<ISpeedreconModuleRunner, OrderunikSpeedreconModuleRunner>();
            services.AddScoped<ISpeedreconModuleRunner, PeriodiseringSpeedreconModuleRunner>();
            services.AddScoped<ISpeedreconModuleRunner, PiaSpeedreconModuleRunner>();
            services.AddScoped<ISpeedreconModuleRunner, UtlevereratEjFaktureratSpeedreconModuleRunner>();
            services.AddScoped<ISpeedreconPageService, SpeedreconPageService>();
            services.AddScoped<WebApp.Services.Integration.IOrderSourceClient, WebApp.Services.Integration.Sources.CentraOrderSource>();
            services.AddScoped<WebApp.Services.Integration.IOrderSourceClient, WebApp.Services.Integration.Sources.JeevesApiOrderSource>();
            services.AddScoped<WebApp.Services.Integration.IOrderSourceClient, WebApp.Services.Integration.Sources.OngoingOrderSource>();
            services.AddSingleton<WebApp.Services.Integration.IJeevesAuthService, WebApp.Services.Integration.JeevesAuthService>();
            services.AddScoped<WebApp.Services.Integration.Akeneo.IAkeneoClient, WebApp.Services.Integration.Akeneo.AkeneoClient>();
            services.AddScoped<WebApp.Services.Integration.Akeneo.IAkeneoExportService, WebApp.Services.Integration.Akeneo.AkeneoExportService>();
            services.AddScoped<IFlowEngineJobStore, FlowEngineDbJobStore>();
            services.AddScoped<IFlowEngineExecutionService, FlowEngineExecutionService>();
            services.AddScoped<FlowEngineQueuedJobProcessor>();
            services.AddScoped<IFlowEngineCommandLineBuilder, FlowEngineCommandLineBuilder>();
            services.AddScoped<IFlowEngineOperationDispatcher, FlowEngineOperationDispatcher>();
            services.AddScoped<IFlowEngineRequestNormalizer, FlowEngineRequestNormalizer>();
            services.AddScoped<IFlowEngineCentraCommandFactory>(_ => new FlowEngineCentraCommandFactory(TimeProvider.System));
            services.AddScoped<IFlowEngineImportOrderWorkflowService, FlowEngineImportOrderWorkflowService>();
            services.AddScoped<IFlowEngineHealthProbeService, FlowEngineHealthProbeService>();
            services.AddScoped<IFlowEngineOperationCatalog, FlowEngineOperationCatalog>();
            services.AddScoped<IFlowEngineModuleService, FlowEngineModuleService>();
            services.AddScoped<IFlowEngineOrderDocumentExtractionService, FlowEngineOrderDocumentExtractionService>();
            services.AddScoped<IFlowEngineConfigValidationService, FlowEngineConfigValidationService>();
            services.AddScoped<IFlowEngineCentraConnectionService, FlowEngineCentraConnectionService>();
            services.AddScoped<IFlowEngineCentraGraphQlClient, FlowEngineCentraGraphQlClient>();
            services.AddScoped<IFlowEngineCentraJeevesBridgeService, FlowEngineCentraJeevesBridgeService>();
            services.AddScoped<IFlowEngineCentraQueryCatalog, FlowEngineCentraQueryCatalog>();
            services.AddScoped<FlowEngineCentraReadSelectionService>();
            services.AddScoped<FlowEngineCentraPagedReadCollector>();
            services.AddScoped<FlowEngineCentraShipmentLookupService>();
            services.AddScoped<FlowEngineCentraShipmentJeevesStatusService>();
            services.AddScoped<FlowEngineCentraShipmentWorkflowService>();
            services.AddScoped<FlowEngineCentraShipmentMutationPayloadFactory>();
            services.AddScoped<FlowEngineCentraShipmentMutationResultParser>();
            services.AddScoped<FlowEngineCentraShipmentMutationService>();
            services.AddScoped<IFlowEngineCentraReadResultFactory, FlowEngineCentraReadResultFactory>();
            services.AddScoped<IFlowEngineCentraSendOrdersResultFactory, FlowEngineCentraSendOrdersResultFactory>();
            services.AddScoped<IFlowEngineCentraSendReturnsResultFactory, FlowEngineCentraSendReturnsResultFactory>();
            services.AddScoped<IFlowEngineCentraReadService, FlowEngineCentraReadService>();
            services.AddScoped<IFlowEngineCentraCheckOrdersService, FlowEngineCentraCheckOrdersService>();
            services.AddScoped<IFlowEngineCentraCreateShipmentsService, FlowEngineCentraCreateShipmentsService>();
            services.AddScoped<IFlowEngineCentraSendOrdersService, FlowEngineCentraSendOrdersService>();
            services.AddScoped<IFlowEngineCentraSendReturnsService, FlowEngineCentraSendReturnsService>();
            services.AddScoped<IFlowEngineShopifyConnectionService, FlowEngineShopifyConnectionService>();
            services.AddScoped<IFlowEngineShopifyGraphQlClient, FlowEngineShopifyGraphQlClient>();
            services.AddScoped<IFlowEngineShopifyFulfillmentService, FlowEngineShopifyFulfillmentService>();
            services.AddScoped<IFlowEngineShopifyQueryCatalog, FlowEngineShopifyQueryCatalog>();
            services.AddScoped<IFlowEngineShopifyScopeProbeService, FlowEngineShopifyScopeProbeService>();
            services.AddScoped<IFlowEngineShopifySelectionService, FlowEngineShopifySelectionService>();
            services.AddScoped<IFlowEngineShopifyReadResultFactory, FlowEngineShopifyReadResultFactory>();
            services.AddScoped<IFlowEngineShopifyCompleteOrdersResultFactory, FlowEngineShopifyCompleteOrdersResultFactory>();
            services.AddScoped<IFlowEngineShopifyOrderValidator, FlowEngineShopifyOrderValidator>();
            services.AddScoped<IFlowEngineShopifyOrderMapper, FlowEngineShopifyOrderMapper>();
            services.AddScoped<IFlowEngineJeevesApiClient, FlowEngineJeevesApiClient>();
            services.AddScoped<IFlowEngineJeevesBridgeService, FlowEngineJeevesBridgeService>();
            services.AddScoped<IFlowEngineShopifyReadService, FlowEngineShopifyReadService>();
            services.AddScoped<IFlowEngineShopifyCompleteOrdersService, FlowEngineShopifyCompleteOrdersService>();
            services.AddScoped<IFlowEngineAkeneoExportService, FlowEngineAkeneoExportService>();
            services.AddScoped<IFlowEngineAkeneoSendToShopifyService, FlowEngineAkeneoSendToShopifyService>();
            services.AddScoped<IFlowEngineAkeneoSendToCentraService, FlowEngineAkeneoSendToCentraService>();
            services.AddScoped<IFlowEngineJeevesReadService, FlowEngineJeevesReadService>();
            services.AddScoped<IFlowEngineJeevesImportOrderService, FlowEngineJeevesImportOrderService>();
        }

        private static Dictionary<string, WebApp.Models.Integration.NamedIntegrationCompanyConfig> ReadNamedIntegrationCompanies(IConfigurationSection section)
        {
            var companies = new Dictionary<string, WebApp.Models.Integration.NamedIntegrationCompanyConfig>(StringComparer.OrdinalIgnoreCase);

            foreach (var companySection in section.GetChildren())
            {
                var company = new WebApp.Models.Integration.NamedIntegrationCompanyConfig
                {
                    CompanyId = companySection.GetValue<Guid?>("CompanyId") ?? Guid.Empty,
                    JeevesCompanyCode = companySection.GetValue<int?>("JeevesCompanyCode"),
                    Enabled = companySection.GetValue<bool?>("Enabled") ?? true,
                    Sources = new Dictionary<string, WebApp.Models.Integration.NamedIntegrationSourceConfig>(StringComparer.OrdinalIgnoreCase)
                };

                foreach (var sourceSection in companySection.GetSection("Sources").GetChildren())
                {
                    company.Sources[sourceSection.Key] = new WebApp.Models.Integration.NamedIntegrationSourceConfig
                    {
                        BaseUrl = sourceSection["BaseUrl"],
                        Token = sourceSection["Token"],
                        TestBaseUrl = sourceSection["TestBaseUrl"],
                        TestToken = sourceSection["TestToken"],
                        AuthUrl = sourceSection["AuthUrl"],
                        AppId = sourceSection["AppId"],
                        AppSecret = sourceSection["AppSecret"],
                        TestAuthUrl = sourceSection["TestAuthUrl"],
                        TestAppId = sourceSection["TestAppId"],
                        TestAppSecret = sourceSection["TestAppSecret"],
                        Username = sourceSection["Username"],
                        Password = sourceSection["Password"],
                        TestUsername = sourceSection["TestUsername"],
                        TestPassword = sourceSection["TestPassword"],
                        GoodsOwnerId = sourceSection.GetValue<int?>("GoodsOwnerId"),
                        TestGoodsOwnerId = sourceSection.GetValue<int?>("TestGoodsOwnerId"),
                        Enabled = sourceSection.GetValue<bool?>("Enabled") ?? true
                    };
                }

                companies[companySection.Key] = company;
            }

            return companies;
        }
}
