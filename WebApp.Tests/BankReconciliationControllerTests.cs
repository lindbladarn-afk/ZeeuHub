using Entities.Application;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using WebApp.Controllers;
using WebApp.Helpers;
using WebApp.Models.Invoices;
using WebApp.Models.Integration;
using WebApp.Observability;
using WebApp.Services;
using WebApp.Services.Application;
using WebApp.Services.Invoices;
using WebApp.Services.Integration.BankReconciliation.Commands;
using WebApp.Services.Integration.BankReconciliation.Bundles;
using WebApp.Services.Integration.BankReconciliation.CodingRules;
using WebApp.Services.Integration.BankReconciliation;
using WebApp.Services.Integration.BankReconciliation.DemoSession;
using WebApp.Services.Integration.BankReconciliation.Invoices;
using WebApp.Services.Integration.BankReconciliation.Presentation;
using WebApp.Services.Integration.BankReconciliation.Queries;
using WebApp.Services.Integration.BankReconciliation.SupplierInvoices;
using WebApp.Services.Integration.BankReconciliation.Upload;
using WebApp.Services.Integration.BankReconciliation.UploadFlow;
using WebApp.Services.Integration.BankReconciliation.Workspace;
using WebApp.ViewModels.Invoices;

namespace WebApp.Tests;

public sealed class BankReconciliationControllerTests
{
    [Fact]
    public async Task BankReconciliation_Returns_Forbid_When_User_Lacks_Access()
    {
        var controller = CreateController(new DenyingCompanyPermissionGuard());

        var result = await controller.BankReconciliation();

        Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    public async Task BankReconciliationUpload_AcceptsNdaFiles()
    {
        var uploadService = new CapturingCamtUploadService();
        var controller = CreateController(new AllowingCompanyPermissionGuard(), camtUploadService: uploadService);
        var file = CreateFormFile("""
            <?xml version="1.0" encoding="utf-8"?>
            <Document xmlns="urn:iso:std:iso:20022:tech:xsd:camt.053.001.02">
              <BkToCstmrStmt>
                <Stmt>
                  <Ntry>
                    <Amt Ccy="SEK">125.00</Amt>
                    <CdtDbtInd>CRDT</CdtDbtInd>
                  </Ntry>
                </Stmt>
              </BkToCstmrStmt>
            </Document>
            """, "statement.nda");

        var result = await controller.BankReconciliationUpload(file);

        Assert.IsType<RedirectToActionResult>(result);
        Assert.NotNull(uploadService.CapturedFile);
        Assert.Equal("statement.nda", uploadService.CapturedFile!.FileName);
    }

    [Fact]
    public async Task BankReconciliationInvoices_InDemoMode_HidesInvoices_ForSupplierClassification()
    {
        var controller = CreateController(
            new AllowingCompanyPermissionGuard(),
            demoDataService: new DemoDataServiceWithInvoices());

        controller.HttpContext!.Session.SetString("BankReconciliation.DemoMode.11111111-1111-1111-1111-111111111111", "1");

        var result = await controller.BankReconciliationInvoices(classificationFilter: "leverantorsbetalning");

        var json = Assert.IsType<JsonResult>(result);
        using var document = System.Text.Json.JsonDocument.Parse(System.Text.Json.JsonSerializer.Serialize(json.Value));
        var root = document.RootElement;
        Assert.Equal(0, root.GetProperty("totalCount").GetInt32());
        Assert.Empty(root.GetProperty("items").EnumerateArray());
    }

    [Fact]
    public async Task BankReconciliationInvoices_UsesPaging_ForCustomerClassification()
    {
        var invoicesService = new CapturingInvoicesService();
        var controller = CreateController(
            new AllowingCompanyPermissionGuard(),
            invoicesService: invoicesService,
            runtimeContextService: new SuccessfulRuntimeContextService());

        var result = await controller.BankReconciliationInvoices(page: 2, pageSize: 20, classificationFilter: "bankinbetalningar");

        var json = Assert.IsType<JsonResult>(result);
        using var document = System.Text.Json.JsonDocument.Parse(System.Text.Json.JsonSerializer.Serialize(json.Value));
        var root = document.RootElement;
        Assert.Equal(20, invoicesService.CapturedQuery!.PageSize);
        Assert.Equal(2, invoicesService.CapturedQuery.Page);
        Assert.Equal(7, root.GetProperty("totalCount").GetInt32());
    }

    [Fact]
    public async Task BankReconciliationInvoices_UsesPaging_ForSupplierClassification()
    {
        var supplierService = new CapturingSupplierInvoiceService();
        var controller = CreateController(
            new AllowingCompanyPermissionGuard(),
            supplierInvoiceService: supplierService,
            runtimeContextService: new SuccessfulRuntimeContextService());

        var result = await controller.BankReconciliationInvoices(page: 2, pageSize: 20, classificationFilter: "leverantorsbetalning");

        var json = Assert.IsType<JsonResult>(result);
        using var document = System.Text.Json.JsonDocument.Parse(System.Text.Json.JsonSerializer.Serialize(json.Value));
        var root = document.RootElement;
        Assert.Equal(20, supplierService.CapturedQuery!.PageSize);
        Assert.Equal(2, supplierService.CapturedQuery.Page);
        Assert.Equal(9, root.GetProperty("totalCount").GetInt32());
    }

    [Fact]
    public async Task BankReconciliationTransactions_ReturnsGlobalClassificationSummary_AndPagesWithinClassification()
    {
        var camtFilePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.xml");
        await File.WriteAllTextAsync(camtFilePath, "<Document />");

        var controller = CreateController(
            new AllowingCompanyPermissionGuard(),
            bankReconciliationService: new ManualReviewBankReconciliationService(),
            camtParser: new StaticCamtParser(BuildMixedTransactions()),
            demoDataService: new DemoDataServiceWithMixedTransactions());
        controller.HttpContext!.Session.SetString("BankRec.UploadedCamtFile", camtFilePath);

        var result = await controller.BankReconciliationTransactions(page: 1, pageSize: 1, classificationFilter: "all");

        var json = Assert.IsType<JsonResult>(result);
        using var document = System.Text.Json.JsonDocument.Parse(System.Text.Json.JsonSerializer.Serialize(json.Value));
        var root = document.RootElement;

        Assert.Equal(4, root.GetProperty("totalCount").GetInt32());
        Assert.Equal(4, root.GetProperty("totalPages").GetInt32());
        Assert.Single(root.GetProperty("items").EnumerateArray());

        var groupCounts = root.GetProperty("groupCounts");
        Assert.Equal(4, groupCounts.GetProperty("All").GetInt32());
        Assert.Equal(1, groupCounts.GetProperty("Kundinbetalningar").GetInt32());
        Assert.Equal(2, groupCounts.GetProperty("Leverantorsutbetalningar").GetInt32());
        Assert.Equal(1, groupCounts.GetProperty("Ovrigt").GetInt32());

        var summary = root.GetProperty("classificationSummary").EnumerateArray().ToArray();
        Assert.Contains(summary, item => string.Equals(item.GetProperty("Key").GetString(), "bankinbetalningar", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(summary, item => string.Equals(item.GetProperty("Key").GetString(), "leverantorsbetalning", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(summary, item => string.Equals(item.GetProperty("Key").GetString(), "bankavgift", StringComparison.OrdinalIgnoreCase));

        var manualReviewItems = root.GetProperty("manualReviewItems").EnumerateArray().ToArray();
        Assert.Single(manualReviewItems);

        var autoResultItems = root.GetProperty("autoResultItems").EnumerateArray().ToArray();
        Assert.Single(autoResultItems);
    }

    [Fact]
    public async Task BankReconciliationTransactions_Returns_Safe_Error_Message_When_Page_Build_Fails()
    {
        var logger = new CapturingLogger<BankReconciliationController>();
        var controller = CreateController(
            new AllowingCompanyPermissionGuard(),
            transactionPageService: new ThrowingTransactionPageService(),
            logger: logger);

        var result = await controller.BankReconciliationTransactions(page: 1, pageSize: 25, classificationFilter: "all");

        var json = Assert.IsType<JsonResult>(result);
        using var document = System.Text.Json.JsonDocument.Parse(System.Text.Json.JsonSerializer.Serialize(json.Value));
        var root = document.RootElement;

        var errorMessage = root.GetProperty("errorMessage").GetString() ?? string.Empty;
        Assert.Contains("Referens:", errorMessage);
        Assert.DoesNotContain("authorization=secret-value", errorMessage, StringComparison.OrdinalIgnoreCase);

        var errorLog = Assert.Single(logger.Entries, entry => entry.Level == LogLevel.Error);
        Assert.Contains("BankReconciliationTransactions failed", errorLog.Message);
        Assert.Contains("SupportId=", errorLog.Message);
        Assert.DoesNotContain("authorization=secret-value", errorLog.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static BankReconciliationController CreateController(
        ICompanyPermissionGuard guard,
        IInvoicesService? invoicesService = null,
        IJeevesRuntimeContextService? runtimeContextService = null,
        IBankReconciliationService? bankReconciliationService = null,
        IBankReconciliationSupplierInvoiceService? supplierInvoiceService = null,
        IBankReconciliationCamtUploadService? camtUploadService = null,
        IBankReconciliationCamtParser? camtParser = null,
        IBankReconciliationCodingRuleService? codingRuleService = null,
        IBankReconciliationDemoDataService? demoDataService = null,
        IBankReconciliationTransactionPageService? transactionPageService = null,
        ILogger<BankReconciliationController>? logger = null)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Features.Set<ISessionFeature>(new TestSessionFeature());
        httpContext.Session.Set("UserObject", new UserSession
        {
            UserId = "user-1",
            CompanyId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            CompanyName = "Demo AB"
        });

        var bankService = bankReconciliationService ?? new FakeBankReconciliationService();
        var contextAccessor = new HttpContextAccessor { HttpContext = httpContext };
        var demoService = demoDataService ?? new FakeDemoDataService();
        var runtimeService = runtimeContextService ?? new FakeRuntimeContextService();
        var invoiceCandidateService = new BankReconciliationInvoiceCandidateService(
            invoicesService ?? new FakeInvoicesService(),
            runtimeService,
            demoService,
            supplierInvoiceService ?? new FakeSupplierInvoiceService(),
            contextAccessor,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<BankReconciliationInvoiceCandidateService>.Instance);
        var workspaceService = new BankReconciliationWorkspaceService(
            camtParser ?? new FakeCamtParser(),
            codingRuleService ?? new FakeCodingRuleService(),
            demoService,
            bankService,
            new DummyStringLocalizer());
        var uploadFlowService = new BankReconciliationUploadFlowService(
            contextAccessor,
            camtUploadService ?? new FakeCamtUploadService(),
            new DummyStringLocalizer());
        var pageTempDataService = new BankReconciliationPageTempDataService();
        var paymentBundleMatcher = new BankReconciliationPaymentBundleMatcher(
            new BankReconciliationMatchingService(),
            Microsoft.Extensions.Options.Options.Create(new BankReconciliationPaymentBundleOptions()));
        var matchCommandService = new BankReconciliationMatchCommandService(
            bankService,
            invoiceCandidateService,
            paymentBundleMatcher,
            contextAccessor,
            new DummyStringLocalizer(),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<BankReconciliationMatchCommandService>.Instance);
        var codingRuleCommandService = new BankReconciliationCodingRuleCommandService(
            codingRuleService ?? new FakeCodingRuleService(),
            contextAccessor,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<BankReconciliationCodingRuleCommandService>.Instance);
        var recommendationQueryService = new BankReconciliationRecommendationQueryService(
            bankService,
            invoiceCandidateService,
            contextAccessor,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<BankReconciliationRecommendationQueryService>.Instance);
        var stateQueryService = new BankReconciliationStateQueryService(bankService);
        var demoSessionService = new BankReconciliationDemoSessionService(
            contextAccessor,
            demoService,
            workspaceService,
            new DummyStringLocalizer());
        var pageQueryService = new BankReconciliationPageQueryService(
            runtimeService,
            workspaceService,
            uploadFlowService,
            demoSessionService,
            new DummyStringLocalizer(),
            new HttpContextAccessor { HttpContext = httpContext },
            Microsoft.Extensions.Logging.Abstractions.NullLogger<BankReconciliationPageQueryService>.Instance);
        var invoiceDetailPageQueryService = new BankReconciliationInvoiceDetailPageQueryService(
            runtimeService,
            invoiceCandidateService,
            demoSessionService,
            new DummyStringLocalizer(),
            contextAccessor,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<BankReconciliationInvoiceDetailPageQueryService>.Instance);
        var invoicePageQueryService = new BankReconciliationInvoicePageQueryService(
            invoiceCandidateService,
            demoSessionService,
            new DummyStringLocalizer(),
            contextAccessor,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<BankReconciliationInvoicePageQueryService>.Instance);
        var paymentBundleService = new BankReconciliationPaymentBundleService(
            bankService,
            invoiceCandidateService,
            paymentBundleMatcher,
            contextAccessor,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<BankReconciliationPaymentBundleService>.Instance);

        var controller = new BankReconciliationController(
            guard,
            contextAccessor,
            bankService,
            invoiceCandidateService,
            pageQueryService,
            invoiceDetailPageQueryService,
            transactionPageService ?? new BankReconciliationTransactionPageService(bankService),
            uploadFlowService,
            workspaceService,
            matchCommandService,
            new FakeLifecycleCommandService(),
            codingRuleCommandService,
            recommendationQueryService,
            stateQueryService,
            invoicePageQueryService,
            demoSessionService,
            pageTempDataService,
            paymentBundleService,
            logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<BankReconciliationController>.Instance);

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = httpContext
        };
        controller.TempData = new TempDataDictionary(httpContext, new DummyTempDataProvider());

        return controller;
    }

    private sealed class FakeLifecycleCommandService
        : IBankReconciliationLifecycleCommandService
    {
        public Task<BankReconciliationLifecycleCommandResult> CloseAsync(
            BankReconciliationSourceContext source,
            UserSession? user,
            int? expectedVersion,
            CancellationToken cancellationToken)
            => Task.FromResult(new BankReconciliationLifecycleCommandResult
            {
                Success = true,
                IsClosed = true,
                Version = expectedVersion.GetValueOrDefault() + 1
            });

        public Task<BankReconciliationLifecycleCommandResult> ReopenAsync(
            BankReconciliationSourceContext source,
            UserSession? user,
            int? expectedVersion,
            string reason,
            CancellationToken cancellationToken)
            => Task.FromResult(new BankReconciliationLifecycleCommandResult
            {
                Success = true,
                IsClosed = false,
                Version = expectedVersion.GetValueOrDefault() + 1
            });
    }

    private sealed class DenyingCompanyPermissionGuard : ICompanyPermissionGuard
    {
        public Task<bool> HasAccessAsync(Guid companyId, Guid subModuleId, CancellationToken cancellationToken = default) => Task.FromResult(false);
    }

    private sealed class AllowingCompanyPermissionGuard : ICompanyPermissionGuard
    {
        public Task<bool> HasAccessAsync(Guid companyId, Guid subModuleId, CancellationToken cancellationToken = default) => Task.FromResult(true);
    }

    private sealed class ThrowingTransactionPageService : IBankReconciliationTransactionPageService
    {
        public BankReconciliationTransactionPageResult BuildPage(
            IReadOnlyList<BankReconciliationParsedTransaction> transactions,
            IReadOnlyList<InvoiceItem> invoices,
            int page,
            int pageSize,
            string? filter,
            string? groupFilter,
            string? classificationFilter)
            => throw new InvalidOperationException("authorization=secret-value");

        public BankReconciliationTransactionPageResult BuildEmptyPage(int page, int pageSize, string? errorMessage = null)
            => new()
            {
                Page = page,
                PageSize = pageSize,
                ErrorMessage = errorMessage
            };
    }

    private sealed class FakeInvoicesService : IInvoicesService
    {
        public Task<InvoiceListViewModel> GetInvoiceListAsync(string connectionString, GetInvoicesQuery query)
            => Task.FromResult(new InvoiceListViewModel());

        public Task<InvoiceItem?> GetInvoiceAsync(string connectionString, int? companyCode, string invoiceNo)
            => Task.FromResult<InvoiceItem?>(null);

        public Task<InvoiceListViewModel> GetDashboardSummaryAsync(string connectionString, int? companyCode)
            => Task.FromResult(new InvoiceListViewModel());
    }

    private sealed class FakeRuntimeContextService : IJeevesRuntimeContextService
    {
        public Task<OperationResult<JeevesRuntimeContext>> ResolveAsync(UserSession? sessionUser, CancellationToken cancellationToken = default)
            => Task.FromResult(OperationResult<JeevesRuntimeContext>.Fail("not used"));
    }

    private sealed class SuccessfulRuntimeContextService : IJeevesRuntimeContextService
    {
        public Task<OperationResult<JeevesRuntimeContext>> ResolveAsync(UserSession? sessionUser, CancellationToken cancellationToken = default)
            => Task.FromResult(OperationResult<JeevesRuntimeContext>.Ok(new JeevesRuntimeContext
            {
                UserId = "user-1",
                CompanyId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                CompanyCode = 5,
                ConnectionString = "Server=.;Database=Jeeves;",
                CompanyName = "Demo AB"
            }));
    }

    private sealed class FakeBankReconciliationService : IBankReconciliationService
    {
        public IReadOnlyList<BankReconciliationRecommendationItem> BuildRecommendations(BankReconciliationTransactionCandidate transaction, IReadOnlyList<InvoiceItem> invoices, IReadOnlyDictionary<string, decimal> allocatedAmountsByInvoiceId, int maxResults = 4)
            => Array.Empty<BankReconciliationRecommendationItem>();

        public BankReconciliationAutoMatchResult BuildAutoMatches(IReadOnlyList<BankReconciliationTransactionCandidate> transactions, IReadOnlyList<InvoiceItem> invoices)
            => new();

        public Task<BankReconciliationAiSuggestionResult> BuildAiSuggestionsAsync(BankReconciliationAiSuggestionRequest request, CancellationToken cancellationToken = default)
            => Task.FromResult(new BankReconciliationAiSuggestionResult());

        public Task<BankReconciliationPersistedState> LoadStateAsync(Guid companyId, string stateKey, CancellationToken cancellationToken = default)
            => Task.FromResult(new BankReconciliationPersistedState());

        public Task<BankReconciliationPersistedState> ReplaceMatchesAsync(Guid companyId, string stateKey, UserSession? user, IReadOnlyList<BankReconciliationSavedMatch> matches, string auditActionType, int? expectedVersion = null, string? note = null, CancellationToken cancellationToken = default)
            => Task.FromResult(new BankReconciliationPersistedState());

        public Task<BankReconciliationPersistedState> UpsertMatchAsync(Guid companyId, string stateKey, UserSession? user, BankReconciliationSavedMatch match, int? expectedVersion = null, string? note = null, CancellationToken cancellationToken = default)
            => Task.FromResult(new BankReconciliationPersistedState());

        public Task<BankReconciliationPersistedState> ReverseMatchAsync(Guid companyId, string stateKey, UserSession? user, string transactionId, string? allocationId = null, string? invoiceId = null, int? expectedVersion = null, string? reason = null, CancellationToken cancellationToken = default)
            => Task.FromResult(new BankReconciliationPersistedState());
    }

    private sealed class ManualReviewBankReconciliationService : IBankReconciliationService
    {
        public IReadOnlyList<BankReconciliationRecommendationItem> BuildRecommendations(BankReconciliationTransactionCandidate transaction, IReadOnlyList<InvoiceItem> invoices, IReadOnlyDictionary<string, decimal> allocatedAmountsByInvoiceId, int maxResults = 4)
        {
            if (!string.Equals(transaction.TransactionId, "TX-1", StringComparison.OrdinalIgnoreCase))
                return Array.Empty<BankReconciliationRecommendationItem>();

            return
            [
                new BankReconciliationRecommendationItem
                {
                    Invoice = new BankReconciliationRecommendationInvoice
                    {
                        Id = "INV-1",
                        InvoiceNo = "INV-1",
                        Amount = 1000m,
                        RemainingAmount = 1000m,
                        Currency = "SEK"
                    },
                    Confidence = new BankReconciliationConfidence
                    {
                        Level = "Medel",
                        Score = 72
                    },
                    RuleLabel = "Manual review needed",
                    RuleHelp = "Stub",
                    RuleKey = "stub",
                    RequiresManualConfirmation = true
                }
            ];
        }

        public BankReconciliationAutoMatchResult BuildAutoMatches(IReadOnlyList<BankReconciliationTransactionCandidate> transactions, IReadOnlyList<InvoiceItem> invoices)
            => new();

        public Task<BankReconciliationAiSuggestionResult> BuildAiSuggestionsAsync(BankReconciliationAiSuggestionRequest request, CancellationToken cancellationToken = default)
            => Task.FromResult(new BankReconciliationAiSuggestionResult());

        public Task<BankReconciliationPersistedState> LoadStateAsync(Guid companyId, string stateKey, CancellationToken cancellationToken = default)
            => Task.FromResult(new BankReconciliationPersistedState());

        public Task<BankReconciliationPersistedState> ReplaceMatchesAsync(Guid companyId, string stateKey, UserSession? user, IReadOnlyList<BankReconciliationSavedMatch> matches, string auditActionType, int? expectedVersion = null, string? note = null, CancellationToken cancellationToken = default)
            => Task.FromResult(new BankReconciliationPersistedState());

        public Task<BankReconciliationPersistedState> UpsertMatchAsync(Guid companyId, string stateKey, UserSession? user, BankReconciliationSavedMatch match, int? expectedVersion = null, string? note = null, CancellationToken cancellationToken = default)
            => Task.FromResult(new BankReconciliationPersistedState());

        public Task<BankReconciliationPersistedState> ReverseMatchAsync(Guid companyId, string stateKey, UserSession? user, string transactionId, string? allocationId = null, string? invoiceId = null, int? expectedVersion = null, string? reason = null, CancellationToken cancellationToken = default)
            => Task.FromResult(new BankReconciliationPersistedState());
    }

    private sealed class FakeDemoDataService : IBankReconciliationDemoDataService
    {
        public Task<BankReconciliationDemoData> LoadAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(new BankReconciliationDemoData());

        public Task<BankReconciliationDemoScenario> LoadScenarioAsync(string? scenarioKey, CancellationToken cancellationToken = default)
            => Task.FromResult(new BankReconciliationDemoScenario());

        public IReadOnlyList<BankReconciliationDemoScenarioOption> ListScenarios()
            => Array.Empty<BankReconciliationDemoScenarioOption>();
    }

    private sealed class DemoDataServiceWithInvoices : IBankReconciliationDemoDataService
    {
        public Task<BankReconciliationDemoData> LoadAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(new BankReconciliationDemoData());

        public Task<BankReconciliationDemoScenario> LoadScenarioAsync(string? scenarioKey, CancellationToken cancellationToken = default)
            => Task.FromResult(new BankReconciliationDemoScenario
            {
                Key = "overview",
                Title = "Demo",
                Data = new BankReconciliationDemoData
                {
                    Invoices =
                    [
                        new BankReconciliationDemoInvoice
                        {
                            Id = "INV-1",
                            InvoiceNo = "INV-1",
                            CustomerName = "Demo AB",
                            Amount = 100m,
                            DueDate = "2026-05-18"
                        }
                    ]
                }
            });

        public IReadOnlyList<BankReconciliationDemoScenarioOption> ListScenarios()
            => Array.Empty<BankReconciliationDemoScenarioOption>();
    }

    private sealed class DemoDataServiceWithMixedTransactions : IBankReconciliationDemoDataService
    {
        public Task<BankReconciliationDemoData> LoadAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(new BankReconciliationDemoData());

        public Task<BankReconciliationDemoScenario> LoadScenarioAsync(string? scenarioKey, CancellationToken cancellationToken = default)
            => Task.FromResult(new BankReconciliationDemoScenario
            {
                Key = "overview",
                Title = "Demo",
                Data = new BankReconciliationDemoData
                {
                    Transactions =
                    [
                        new BankReconciliationDemoTransaction
                        {
                            Id = "TX-1",
                            Date = "2026-05-07",
                            Amount = 1000m,
                            Currency = "SEK",
                            Reference = "123456",
                            DebtorName = "Kund AB",
                            Remittance = "Kundbetalning OCR 123456"
                        },
                        new BankReconciliationDemoTransaction
                        {
                            Id = "TX-2",
                            Date = "2026-05-07",
                            Amount = -200m,
                            Currency = "SEK",
                            Reference = "SUP-1",
                            DebtorName = "Leverantör AB",
                            Remittance = "Leverantörsfaktura 1"
                        },
                        new BankReconciliationDemoTransaction
                        {
                            Id = "TX-3",
                            Date = "2026-05-07",
                            Amount = -300m,
                            Currency = "SEK",
                            Reference = "SUP-2",
                            DebtorName = "Leverantör AB",
                            Remittance = "Leverantörsfaktura 2"
                        },
                        new BankReconciliationDemoTransaction
                        {
                            Id = "TX-4",
                            Date = "2026-05-07",
                            Amount = -50m,
                            Currency = "SEK",
                            Reference = "FEE-1",
                            DebtorName = "Bank AB",
                            Remittance = "Bankavgift mars"
                        }
                    ]
                }
            });

        public IReadOnlyList<BankReconciliationDemoScenarioOption> ListScenarios()
            => Array.Empty<BankReconciliationDemoScenarioOption>();
    }

    private static IReadOnlyList<BankReconciliationParsedTransaction> BuildMixedTransactions()
    {
        return
        [
            new BankReconciliationParsedTransaction
            {
                Id = "TX-1",
                Date = "2026-05-07",
                ValueDate = "2026-05-07",
                Amount = 1000m,
                Currency = "SEK",
                Reference = "123456",
                DebtorName = "Kund AB",
                Remittance = "Kundbetalning OCR 123456",
                Direction = "CRDT",
                Domn = "PMNT",
                Fmly = "RCDT",
                Classification = BankReconciliationTransactionClassifier.Classify("PMNT", "RCDT", null, "CRDT", null, "Kundbetalning OCR 123456", "Kund AB"),
                Group = "Kundinbetalningar",
                MatchType = "auto",
                MatchRule = "auto-ocr",
                MatchedAmount = 1000m,
                Allocations =
                [
                    new BankReconciliationParsedAllocation
                    {
                        AllocationId = "alloc-1",
                        InvoiceId = "INV-1",
                        MatchType = "auto",
                        MatchRule = "auto-ocr",
                        MatchedAmount = 1000m,
                        Currency = "SEK"
                    }
                ]
            },
            new BankReconciliationParsedTransaction
            {
                Id = "TX-2",
                Date = "2026-05-07",
                ValueDate = "2026-05-07",
                Amount = -200m,
                Currency = "SEK",
                Reference = "SUP-1",
                DebtorName = "Leverantör AB",
                Remittance = "Leverantörsfaktura 1",
                Direction = "DBIT",
                Domn = "PMNT",
                Fmly = "ICDT",
                Classification = BankReconciliationTransactionClassifier.Classify("PMNT", "ICDT", null, "DBIT", null, "Leverantörsfaktura 1", "Leverantör AB"),
                Group = "Leverantorsutbetalningar"
            },
            new BankReconciliationParsedTransaction
            {
                Id = "TX-3",
                Date = "2026-05-07",
                ValueDate = "2026-05-07",
                Amount = -300m,
                Currency = "SEK",
                Reference = "SUP-2",
                DebtorName = "Leverantör AB",
                Remittance = "Leverantörsfaktura 2",
                Direction = "DBIT",
                Domn = "PMNT",
                Fmly = "ICDT",
                Classification = BankReconciliationTransactionClassifier.Classify("PMNT", "ICDT", null, "DBIT", null, "Leverantörsfaktura 2", "Leverantör AB"),
                Group = "Leverantorsutbetalningar"
            },
            new BankReconciliationParsedTransaction
            {
                Id = "TX-4",
                Date = "2026-05-07",
                ValueDate = "2026-05-07",
                Amount = -50m,
                Currency = "SEK",
                Reference = "FEE-1",
                DebtorName = "Bank AB",
                Remittance = "Bankavgift mars",
                Direction = "DBIT",
                Domn = "PMNT",
                Fmly = "ICDT",
                Classification = BankReconciliationTransactionClassifier.Classify("PMNT", "ICDT", null, "DBIT", null, "Bankavgift mars", "Bank AB"),
                Group = "Ovrigt"
            }
        ];
    }

    private sealed class FakeSupplierInvoiceService : IBankReconciliationSupplierInvoiceService
    {
        public Task<(IReadOnlyList<InvoiceItem> Invoices, int TotalCount)> GetPaymentCandidatesAsync(
            string connectionString,
            BankReconciliationSupplierInvoiceQuery query,
            CancellationToken cancellationToken = default)
            => Task.FromResult(((IReadOnlyList<InvoiceItem>)Array.Empty<InvoiceItem>(), 0));
    }

    private sealed class CapturingInvoicesService : IInvoicesService
    {
        public GetInvoicesQuery? CapturedQuery { get; private set; }

        public Task<InvoiceListViewModel> GetInvoiceListAsync(string connectionString, GetInvoicesQuery query)
        {
            CapturedQuery = query;
            return Task.FromResult(new InvoiceListViewModel
            {
                UnpaidInvoices = new[]
                {
                    new InvoiceItem
                    {
                        InvoiceNo = "INV-1",
                        Customer = "Demo AB",
                        DueDate = new DateTime(2026, 5, 18),
                        AmountSek = 100m,
                        RemainingAmount = 100m
                    }
                },
                TotalCount = 7
            });
        }

        public Task<InvoiceItem?> GetInvoiceAsync(string connectionString, int? companyCode, string invoiceNo)
            => Task.FromResult<InvoiceItem?>(null);

        public Task<InvoiceListViewModel> GetDashboardSummaryAsync(string connectionString, int? companyCode)
            => Task.FromResult(new InvoiceListViewModel());
    }

    private sealed class CapturingSupplierInvoiceService : IBankReconciliationSupplierInvoiceService
    {
        public BankReconciliationSupplierInvoiceQuery? CapturedQuery { get; private set; }

        public Task<(IReadOnlyList<InvoiceItem> Invoices, int TotalCount)> GetPaymentCandidatesAsync(
            string connectionString,
            BankReconciliationSupplierInvoiceQuery query,
            CancellationToken cancellationToken = default)
        {
            CapturedQuery = query;
            return Task.FromResult(((IReadOnlyList<InvoiceItem>)new[]
            {
                new InvoiceItem
                {
                    InvoiceNo = "SUP-1",
                    Customer = "Supplier AB",
                    DueDate = new DateTime(2026, 5, 18),
                    AmountSek = 100m,
                    RemainingAmount = 100m
                }
            }, 9));
        }
    }

    private sealed class FakeCamtParser : IBankReconciliationCamtParser
    {
        public IReadOnlyList<BankReconciliationParsedTransaction> Parse(string filePath)
            => Array.Empty<BankReconciliationParsedTransaction>();
    }

    private sealed class StaticCamtParser : IBankReconciliationCamtParser
    {
        private readonly IReadOnlyList<BankReconciliationParsedTransaction> _transactions;

        public StaticCamtParser(IReadOnlyList<BankReconciliationParsedTransaction> transactions)
        {
            _transactions = transactions;
        }

        public IReadOnlyList<BankReconciliationParsedTransaction> Parse(string filePath)
            => _transactions;
    }

    private sealed class FakeCamtUploadService : IBankReconciliationCamtUploadService
    {
        public Task<BankReconciliationCamtUploadResult> PrepareUploadAsync(IFormFile file, Guid companyId, string sessionId, string? previousFilePath, CancellationToken cancellationToken = default)
            => Task.FromResult(new BankReconciliationCamtUploadResult());
    }

    private sealed class FakeCodingRuleService : IBankReconciliationCodingRuleService
    {
        public Task<BankReconciliationCodingRuleSet> LoadAsync(Guid companyId, string bankAccountKey, CancellationToken cancellationToken = default)
            => Task.FromResult(new BankReconciliationCodingRuleSet());

        public Task<BankReconciliationCodingRuleSet> SaveAsync(Guid companyId, string bankAccountKey, UserSession? user, IReadOnlyList<BankReconciliationCodingRuleRow> rows, string? bankAccountLabel = null, int? expectedVersion = null, CancellationToken cancellationToken = default)
            => Task.FromResult(new BankReconciliationCodingRuleSet());
    }

    private sealed class CapturingLogger<T> : ILogger<T>
    {
        public List<LogEntry> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            var properties = state is IEnumerable<KeyValuePair<string, object?>> values
                ? values.ToDictionary(item => item.Key, item => item.Value, StringComparer.Ordinal)
                : new Dictionary<string, object?>(StringComparer.Ordinal);

            Entries.Add(new LogEntry(logLevel, formatter(state, exception), exception, properties));
        }
    }

    private sealed record LogEntry(LogLevel Level, string Message, Exception? Exception, IReadOnlyDictionary<string, object?> Properties);

    private sealed class CapturingCamtUploadService : IBankReconciliationCamtUploadService
    {
        public IFormFile? CapturedFile { get; private set; }

        public Task<BankReconciliationCamtUploadResult> PrepareUploadAsync(IFormFile file, Guid companyId, string sessionId, string? previousFilePath, CancellationToken cancellationToken = default)
        {
            CapturedFile = file;
            return Task.FromResult(new BankReconciliationCamtUploadResult
            {
                Success = true,
                StoredFilePath = Path.Combine(Path.GetTempPath(), "bankrec-upload", "statement.xml"),
                TransactionCount = 1
            });
        }
    }

    private static IFormFile CreateFormFile(string content, string fileName)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(content);
        var stream = new MemoryStream(bytes);
        return new FormFile(stream, 0, bytes.Length, "file", fileName)
        {
            Headers = new HeaderDictionary(),
            ContentType = "application/xml"
        };
    }

    private sealed class DummyStringLocalizer : IStringLocalizer<SharedResources>
    {
        public LocalizedString this[string name] => new(name, name);

        public LocalizedString this[string name, params object[] arguments] => new(name, string.Format(name, arguments));

        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures)
            => Array.Empty<LocalizedString>();

        public IStringLocalizer WithCulture(System.Globalization.CultureInfo culture)
            => this;
    }

    private sealed class DummyTempDataProvider : ITempDataProvider
    {
        public IDictionary<string, object?> LoadTempData(HttpContext context)
            => new Dictionary<string, object?>();

        public void SaveTempData(HttpContext context, IDictionary<string, object?> values)
        {
        }
    }

    private sealed class TestSessionFeature : ISessionFeature
    {
        public ISession Session { get; set; } = new TestSession();
    }

    private sealed class TestSession : ISession
    {
        private readonly Dictionary<string, byte[]> _store = new(StringComparer.Ordinal);

        public bool IsAvailable => true;
        public string Id { get; } = Guid.NewGuid().ToString("N");
        public IEnumerable<string> Keys => _store.Keys;

        public void Clear() => _store.Clear();
        public Task CommitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task LoadAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public void Remove(string key) => _store.Remove(key);
        public void Set(string key, byte[] value) => _store[key] = value.ToArray();
        public bool TryGetValue(string key, out byte[] value) => _store.TryGetValue(key, out value!);
    }
}
