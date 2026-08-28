// Handles Flow Engine pages, commands, status polling and workbench state.
using Entities.Application;
using Microsoft.AspNetCore.Mvc;
using System.Text;
using WebApp.Models;
using WebApp.Models.Integration;
using WebApp.Services;
using WebApp.Services.Integration.FlowEngine;

namespace WebApp.Controllers
{
    public partial class IntegrationController
    {
        private static readonly Guid SubModuleFlowEngineId = Guid.Parse("d9ad45b1-5cf3-4c12-a260-33f7733918d4");
        private const string FlowEngineWorkbenchSettingsSessionKey = "FlowEngine.WorkbenchSettings";
        private readonly IFlowEngineExecutionService _flowEngineExecutionService;
        private readonly IFlowEngineRequestNormalizer _flowEngineRequestNormalizer;
        private readonly IFlowEngineCentraCommandFactory _flowEngineCentraCommandFactory;
        private readonly IFlowEngineImportOrderWorkflowService _flowEngineImportOrderWorkflowService;
        private readonly IFlowEngineOrderDocumentExtractionService _flowEngineOrderDocumentExtractionService;
        private readonly IFlowEngineModuleService _flowEngineModuleService;
        private readonly IFlowEngineHealthProbeService _flowEngineHealthProbeService;

        [HttpGet]
        public Task<IActionResult> FlowEngine(
            Guid? selectedJobId,
            int historyPage = 1,
            string? historySystem = null,
            string? historyOperation = null,
            string? historyStatus = null,
            string? historyDateStart = null,
            string? historyDateEnd = null,
            CancellationToken cancellationToken = default)
            => RenderFlowEngineAsync(
                FlowEngineSectionKeys.Dashboard,
                selectedJobId,
                historyPage,
                new FlowEngineHistoryFilterState
                {
                    System = historySystem,
                    Operation = historyOperation,
                    Status = historyStatus,
                    DateStart = historyDateStart,
                    DateEnd = historyDateEnd
                },
                cancellationToken);

        [HttpGet]
        public Task<IActionResult> FlowEngineJeeves(Guid? selectedJobId, int historyPage = 1, CancellationToken cancellationToken = default)
            => RenderFlowEngineAsync(FlowEngineSectionKeys.Jeeves, selectedJobId, historyPage, null, cancellationToken);

        [HttpGet]
        public Task<IActionResult> FlowEngineCentra(Guid? selectedJobId, int historyPage = 1, CancellationToken cancellationToken = default)
            => RenderFlowEngineAsync(FlowEngineSectionKeys.Centra, selectedJobId, historyPage, null, cancellationToken);

        [HttpGet]
        public Task<IActionResult> FlowEngineShopify(Guid? selectedJobId, int historyPage = 1, CancellationToken cancellationToken = default)
            => RenderFlowEngineAsync(FlowEngineSectionKeys.Shopify, selectedJobId, historyPage, null, cancellationToken);

        [HttpGet]
        public Task<IActionResult> FlowEngineAkeneo(Guid? selectedJobId, int historyPage = 1, CancellationToken cancellationToken = default)
            => RenderFlowEngineAsync(FlowEngineSectionKeys.Akeneo, selectedJobId, historyPage, null, cancellationToken);

        [HttpGet]
        public Task<IActionResult> FlowEngineJobs(Guid? selectedJobId, int historyPage = 1, CancellationToken cancellationToken = default)
            => RenderFlowEngineAsync(FlowEngineSectionKeys.Jobs, selectedJobId, historyPage, null, cancellationToken);

        [HttpGet]
        public Task<IActionResult> FlowEngineConfig(Guid? selectedJobId, int historyPage = 1, CancellationToken cancellationToken = default)
            => RenderFlowEngineAsync(FlowEngineSectionKeys.Config, selectedJobId, historyPage, null, cancellationToken);

        [HttpGet]
        public async Task<IActionResult> FlowEngineJobModal(Guid jobId)
        {
            if (!await HasCompanyPermissionAsync(SubModuleFlowEngineId))
                return Forbid();

            var user = GetFlowEngineSessionUser();
            if (user?.CompanyId is not Guid companyId || companyId == Guid.Empty)
                return BadRequest();

            var job = _flowEngineExecutionService.Get(companyId, jobId);
            if (job is null)
                return NotFound();

            return PartialView("~/Views/Integration/FlowEngine/Partials/_FlowEngineHistoryJobModal.cshtml", job);
        }

        [HttpGet]
        public async Task<IActionResult> FlowEngineHealthStatus(string? section, bool testMode = false, CancellationToken cancellationToken = default)
        {
            if (!await HasCompanyPermissionAsync(SubModuleFlowEngineId))
                return Forbid();

            var user = GetFlowEngineSessionUser();
            var runtimeResult = await _jeevesRuntimeContextService.ResolveAsync(user, cancellationToken);
            var runtimeContext = runtimeResult.Success ? runtimeResult.Value : null;

            var statuses = await _flowEngineHealthProbeService.ProbeAsync(
                user,
                FlowEngineSectionKeys.Normalize(section),
                runtimeContext,
                testMode,
                cancellationToken);

            return Json(statuses.Select(status => new
            {
                key = status.Key,
                label = status.Label,
                isReady = status.IsReady,
                statusText = status.StatusText
            }));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> FlowEngineSaveWorkbenchSettings(FlowEngineWorkbenchSettingsState input)
        {
            if (!await HasCompanyPermissionAsync(SubModuleFlowEngineId))
                return Forbid();

            SaveFlowEngineWorkbenchSettingsState(NormalizeFlowEngineWorkbenchSettings(input));
            return Ok();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> FlowEngineRunCheckOrders(FlowEngineRunCheckOrdersInput input, CancellationToken cancellationToken)
        {
            return await ExecuteFlowEngineJobAsync(
                _flowEngineCentraCommandFactory.BuildCheckOrders(input),
                "FlowEngine-korning klar: Centra check orders sparad i jobbhistoriken.",
                cancellationToken);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> FlowEngineRunConfigValidate(CancellationToken cancellationToken)
        {
            return await ExecuteFlowEngineJobAsync(
                new FlowEngineExecuteJobRequest
                {
                    Name = "flowengine-config-validate",
                    UiLabel = "FlowEngine config validate",
                    Operation = FlowEngineOperationType.ConfigValidate
                },
                "FlowEngine-korning klar: config validate sparades i jobbhistoriken.",
                cancellationToken);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> FlowEngineRunCentraFetchOrder(FlowEngineRunCentraFetchOrderInput input, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(input.OrderId))
            {
                SetFlowEngineAlert(Alert.DANGER, _sharedLocalizer["Integration_OrderIdRequiredForCentraFetchOrder"]);
                return RedirectToFlowEngineSection();
            }

            return await ExecuteFlowEngineJobAsync(
                _flowEngineCentraCommandFactory.BuildFetchOrder(input),
                "FlowEngine-korning klar: Centra fetch order sparades i jobbhistoriken.",
                cancellationToken);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> FlowEngineRunCentraFetchOrders(FlowEngineRunCentraFetchOrdersInput input, CancellationToken cancellationToken)
        {
            return await ExecuteFlowEngineJobAsync(
                _flowEngineCentraCommandFactory.BuildFetchOrders(input),
                "FlowEngine-korning klar: Centra fetch orders sparades i jobbhistoriken.",
                cancellationToken);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> FlowEngineRunCentraFetchReturn(FlowEngineRunCentraFetchReturnInput input, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(input.ReturnId))
            {
                SetFlowEngineAlert(Alert.DANGER, _sharedLocalizer["Integration_ReturnIdRequiredForCentraFetchReturn"]);
                return RedirectToFlowEngineSection();
            }

            return await ExecuteFlowEngineJobAsync(
                _flowEngineCentraCommandFactory.BuildFetchReturn(input),
                "FlowEngine-korning klar: Centra fetch return sparades i jobbhistoriken.",
                cancellationToken);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> FlowEngineRunCentraFetchReturns(FlowEngineRunCentraFetchReturnsInput input, CancellationToken cancellationToken)
        {
            return await ExecuteFlowEngineJobAsync(
                _flowEngineCentraCommandFactory.BuildFetchReturns(input),
                "FlowEngine-korning klar: Centra fetch returns sparades i jobbhistoriken.",
                cancellationToken);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> FlowEngineRunCreateShipments(FlowEngineRunCreateShipmentsInput input, CancellationToken cancellationToken)
        {
            return await ExecuteFlowEngineJobAsync(
                _flowEngineCentraCommandFactory.BuildCreateShipments(input),
                input.DryRun
                    ? "FlowEngine-korning klar: Centra create shipments dry run sparad i jobbhistoriken."
                    : "FlowEngine-korning klar: Centra create shipments batch kor klart och sparades i jobbhistoriken.",
                cancellationToken);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> FlowEngineRunCreateShipmentsPending(FlowEngineRunCreateShipmentsPendingInput input, CancellationToken cancellationToken)
        {
            return await ExecuteFlowEngineJobAsync(
                _flowEngineCentraCommandFactory.BuildCreateShipmentsPending(input),
                input.DryRun
                    ? "FlowEngine-korning klar: Centra create shipments pending dry run sparad i jobbhistoriken."
                    : "FlowEngine-korning klar: Centra create shipments pending batch kor klart och sparades i jobbhistoriken.",
                cancellationToken);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> FlowEngineRunCreateShipment(FlowEngineRunCreateShipmentInput input, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(input.OrderId))
            {
                SetFlowEngineAlert(Alert.DANGER, _sharedLocalizer["Integration_OrderIdRequiredForCentraCreateShipment"]);
                return RedirectToFlowEngineSection();
            }

            return await ExecuteFlowEngineJobAsync(
                _flowEngineCentraCommandFactory.BuildCreateShipment(input),
                input.DryRun
                    ? "FlowEngine-korning klar: Centra create shipment dry run sparad i jobbhistoriken."
                    : "FlowEngine-korning klar: Centra create shipment korning sparades i jobbhistoriken.",
                cancellationToken);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> FlowEngineRunAkeneoProducts(FlowEngineRunAkeneoProductsInput input, CancellationToken cancellationToken)
        {
            var skus = ParseFlowEngineAkeneoSkus(input.Skus);
            if (skus.Count == 0)
            {
                SetFlowEngineAlert(Alert.DANGER, _sharedLocalizer["Integration_AtLeastOneSkuRequiredForAkeneoExport"]);
                return RedirectToFlowEngineSection();
            }

            return await ExecuteFlowEngineJobAsync(
                new FlowEngineExecuteJobRequest
                {
                    Name = "akeneo-products",
                    UiLabel = "Akeneo export",
                    Operation = FlowEngineOperationType.AkeneoProducts,
                    Params = new FlowEngineExecutionParams
                    {
                        AkeneoSkus = skus,
                        UseLimit = input.Limit.HasValue && input.Limit.Value > 0,
                        Limit = input.Limit
                    }
                },
                "FlowEngine-korning klar: Akeneo export for valda SKU:er sparades i jobbhistoriken.",
                cancellationToken);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> FlowEngineRunAkeneoAllProducts(FlowEngineRunAkeneoAllProductsInput input, CancellationToken cancellationToken)
        {
            return await ExecuteFlowEngineJobAsync(
                new FlowEngineExecuteJobRequest
                {
                    Name = "akeneo-all-products",
                    UiLabel = "Akeneo all products",
                    Operation = FlowEngineOperationType.AkeneoAllProducts,
                    Params = new FlowEngineExecutionParams
                    {
                        UseLimit = input.Limit.HasValue && input.Limit.Value > 0,
                        Limit = input.Limit
                    }
                },
                "FlowEngine-korning klar: Akeneo all products sparades i jobbhistoriken.",
                cancellationToken);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> FlowEngineRunShopifyScopesCheck(CancellationToken cancellationToken)
        {
            return await ExecuteFlowEngineJobAsync(
                new FlowEngineExecuteJobRequest
                {
                    Name = "shopify-scopes-check",
                    UiLabel = "Shopify scopes-check",
                    Operation = FlowEngineOperationType.ShopifyScopesCheck
                },
                "FlowEngine-korning klar: Shopify scopes-check sparades i jobbhistoriken.",
                cancellationToken);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> FlowEngineRunShopifyGetProducts(FlowEngineRunShopifyGetProductsInput input, CancellationToken cancellationToken)
        {
            return await ExecuteFlowEngineJobAsync(
                new FlowEngineExecuteJobRequest
                {
                    Name = "shopify-get-products",
                    UiLabel = "Shopify get-products",
                    Operation = FlowEngineOperationType.ShopifyGetProducts,
                    Params = new FlowEngineExecutionParams
                    {
                        ShopifyQuery = string.IsNullOrWhiteSpace(input.Query) ? null : input.Query.Trim(),
                        ShopifyUpdatedSince = string.IsNullOrWhiteSpace(input.UpdatedSince) ? null : input.UpdatedSince.Trim(),
                        UseLimit = input.Limit.HasValue && input.Limit.Value > 0,
                        Limit = input.Limit
                    }
                },
                "FlowEngine-korning klar: Shopify get-products sparades i jobbhistoriken.",
                cancellationToken);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> FlowEngineRunShopifyFetchOrder(FlowEngineRunShopifyFetchOrderInput input, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(input.OrderId))
            {
                SetFlowEngineAlert(Alert.DANGER, "Shopify order-id maste anges for fetch order.");
                return RedirectToFlowEngineSection();
            }

            return await ExecuteFlowEngineJobAsync(
                new FlowEngineExecuteJobRequest
                {
                    Name = "shopify-fetch-order",
                    UiLabel = "Shopify fetch order",
                    Operation = FlowEngineOperationType.ShopifyFetchOrder,
                    Params = new FlowEngineExecutionParams
                    {
                        OrderId = input.OrderId.Trim()
                    }
                },
                "FlowEngine-korning klar: Shopify fetch order sparades i jobbhistoriken.",
                cancellationToken);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> FlowEngineRunShopifyFetchOrders(FlowEngineRunShopifyFetchOrdersInput input, CancellationToken cancellationToken)
        {
            return await ExecuteFlowEngineJobAsync(
                new FlowEngineExecuteJobRequest
                {
                    Name = "shopify-fetch-orders",
                    UiLabel = "Shopify fetch orders",
                    Operation = FlowEngineOperationType.ShopifyFetchOrders,
                    Flags = new FlowEngineExecutionFlags
                    {
                        ForceRange = input.ForceRange
                    },
                    Params = new FlowEngineExecutionParams
                    {
                        DateUtc = string.IsNullOrWhiteSpace(input.DateUtc) ? null : input.DateUtc.Trim(),
                        SinceUtc = string.IsNullOrWhiteSpace(input.SinceUtc) ? null : input.SinceUtc.Trim(),
                        UntilUtc = string.IsNullOrWhiteSpace(input.UntilUtc) ? null : input.UntilUtc.Trim(),
                        UseLatestDay = input.UseLatestDay,
                        UseLimit = input.Limit.HasValue && input.Limit.Value > 0,
                        Limit = input.Limit
                    }
                },
                "FlowEngine-korning klar: Shopify fetch orders sparades i jobbhistoriken.",
                cancellationToken);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> FlowEngineRunShopifyValidateOrder(FlowEngineRunShopifyValidateOrderInput input, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(input.OrderId))
            {
                SetFlowEngineAlert(Alert.DANGER, "Shopify order-id maste anges for validate order.");
                return RedirectToFlowEngineSection();
            }

            return await ExecuteFlowEngineJobAsync(
                new FlowEngineExecuteJobRequest
                {
                    Name = "shopify-validate-order",
                    UiLabel = "Shopify validate order",
                    Operation = FlowEngineOperationType.ShopifyValidateOrder,
                    Params = new FlowEngineExecutionParams
                    {
                        OrderId = input.OrderId.Trim()
                    }
                },
                "FlowEngine-korning klar: Shopify validate order sparades i jobbhistoriken.",
                cancellationToken);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> FlowEngineRunShopifyValidateOrders(FlowEngineRunShopifyValidateOrdersInput input, CancellationToken cancellationToken)
        {
            return await ExecuteFlowEngineJobAsync(
                new FlowEngineExecuteJobRequest
                {
                    Name = "shopify-validate-orders",
                    UiLabel = "Shopify validate orders",
                    Operation = FlowEngineOperationType.ShopifyValidateOrders,
                    Flags = new FlowEngineExecutionFlags
                    {
                        ForceRange = input.ForceRange
                    },
                    Params = new FlowEngineExecutionParams
                    {
                        DateUtc = string.IsNullOrWhiteSpace(input.DateUtc) ? null : input.DateUtc.Trim(),
                        SinceUtc = string.IsNullOrWhiteSpace(input.SinceUtc) ? null : input.SinceUtc.Trim(),
                        UntilUtc = string.IsNullOrWhiteSpace(input.UntilUtc) ? null : input.UntilUtc.Trim(),
                        UseLatestDay = input.UseLatestDay,
                        UseLimit = input.Limit.HasValue && input.Limit.Value > 0,
                        Limit = input.Limit
                    }
                },
                "FlowEngine-korning klar: Shopify validate orders sparades i jobbhistoriken.",
                cancellationToken);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> FlowEngineRunShopifyCheckOrders(FlowEngineRunShopifyCheckOrdersInput input, CancellationToken cancellationToken)
        {
            return await ExecuteFlowEngineJobAsync(
                new FlowEngineExecuteJobRequest
                {
                    Name = "shopify-check-orders",
                    UiLabel = "Shopify check orders",
                    Operation = FlowEngineOperationType.ShopifyCheckOrders,
                    Flags = new FlowEngineExecutionFlags
                    {
                        ForceRange = input.ForceRange
                    },
                    Params = new FlowEngineExecutionParams
                    {
                        DateUtc = string.IsNullOrWhiteSpace(input.DateUtc) ? null : input.DateUtc.Trim(),
                        SinceUtc = string.IsNullOrWhiteSpace(input.SinceUtc) ? null : input.SinceUtc.Trim(),
                        UntilUtc = string.IsNullOrWhiteSpace(input.UntilUtc) ? null : input.UntilUtc.Trim(),
                        UseLatestDay = input.UseLatestDay,
                        UseLimit = input.Limit.HasValue && input.Limit.Value > 0,
                        Limit = input.Limit
                    }
                },
                "FlowEngine-korning klar: Shopify check orders sparades i jobbhistoriken.",
                cancellationToken);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> FlowEngineRunShopifySendOrder(FlowEngineRunShopifySendOrderInput input, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(input.OrderId))
            {
                SetFlowEngineAlert(Alert.DANGER, "Shopify order-id maste anges for send order.");
                return RedirectToFlowEngineSection();
            }

            return await ExecuteFlowEngineJobAsync(
                new FlowEngineExecuteJobRequest
                {
                    Name = "shopify-send-order",
                    UiLabel = "Shopify send order",
                    Operation = FlowEngineOperationType.ShopifySendOrder,
                    Flags = new FlowEngineExecutionFlags
                    {
                        DryRun = input.DryRun,
                        SkipJeevesCheck = input.SkipJeevesCheck
                    },
                    Params = new FlowEngineExecutionParams
                    {
                        OrderId = input.OrderId.Trim()
                    }
                },
                input.DryRun
                    ? "FlowEngine-korning klar: Shopify send order dry run sparades i jobbhistoriken."
                    : "FlowEngine-korning klar: Shopify send order korning sparades i jobbhistoriken.",
                cancellationToken);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> FlowEngineRunShopifySendOrders(FlowEngineRunShopifySendOrdersInput input, CancellationToken cancellationToken)
        {
            return await ExecuteFlowEngineJobAsync(
                new FlowEngineExecuteJobRequest
                {
                    Name = "shopify-send-orders",
                    UiLabel = "Shopify send orders",
                    Operation = FlowEngineOperationType.ShopifySendOrders,
                    Flags = new FlowEngineExecutionFlags
                    {
                        DryRun = input.DryRun,
                        SkipJeevesCheck = input.SkipJeevesCheck,
                        ForceRange = input.ForceRange
                    },
                    Params = new FlowEngineExecutionParams
                    {
                        DateUtc = string.IsNullOrWhiteSpace(input.DateUtc) ? null : input.DateUtc.Trim(),
                        SinceUtc = string.IsNullOrWhiteSpace(input.SinceUtc) ? null : input.SinceUtc.Trim(),
                        UntilUtc = string.IsNullOrWhiteSpace(input.UntilUtc) ? null : input.UntilUtc.Trim(),
                        UseLatestDay = input.UseLatestDay,
                        UseLimit = input.Limit.HasValue && input.Limit.Value > 0,
                        Limit = input.Limit
                    }
                },
                input.DryRun
                    ? "FlowEngine-korning klar: Shopify send orders dry run sparades i jobbhistoriken."
                    : "FlowEngine-korning klar: Shopify send orders korning sparades i jobbhistoriken.",
                cancellationToken);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> FlowEngineRunAkeneoSendToShopify(FlowEngineRunAkeneoSendToShopifyInput input, CancellationToken cancellationToken)
        {
            var skus = ParseFlowEngineAkeneoSkus(input.Sku);

            return await ExecuteFlowEngineJobAsync(
                new FlowEngineExecuteJobRequest
                {
                    Name = "akeneo-send-to-shopify",
                    UiLabel = "Akeneo send-to-shopify",
                    Operation = FlowEngineOperationType.AkeneoSendToShopify,
                    Flags = new FlowEngineExecutionFlags
                    {
                        DryRun = true
                    },
                    Params = new FlowEngineExecutionParams
                    {
                        AkeneoSkus = skus,
                        UseLimit = input.Limit.HasValue && input.Limit.Value > 0,
                        Limit = input.Limit
                    }
                },
                "FlowEngine-korning klar: Akeneo send-to-shopify dry run sparades i jobbhistoriken.",
                cancellationToken);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> FlowEngineRunAkeneoSendToCentra(FlowEngineRunAkeneoSendToCentraInput input, CancellationToken cancellationToken)
        {
            var skus = ParseFlowEngineAkeneoSkus(input.Sku);

            return await ExecuteFlowEngineJobAsync(
                new FlowEngineExecuteJobRequest
                {
                    Name = "akeneo-send-to-centra",
                    UiLabel = "Akeneo send-to-centra",
                    Operation = FlowEngineOperationType.AkeneoSendToCentra,
                    Flags = new FlowEngineExecutionFlags
                    {
                        DryRun = true
                    },
                    Params = new FlowEngineExecutionParams
                    {
                        AkeneoSkus = skus,
                        UseLimit = input.Limit.HasValue && input.Limit.Value > 0,
                        Limit = input.Limit
                    }
                },
                "FlowEngine-korning klar: Akeneo send-to-centra dry run sparades i jobbhistoriken.",
                cancellationToken);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> FlowEngineRunCompleteOrder(FlowEngineRunCompleteOrderInput input, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(input.OrderId))
            {
                SetFlowEngineAlert(Alert.DANGER, "Shopify order-id maste anges for complete order.");
                return RedirectToFlowEngineSection();
            }

            return await ExecuteFlowEngineJobAsync(
                new FlowEngineExecuteJobRequest
                {
                    Name = "shopify-complete-order",
                    UiLabel = "Shopify complete order",
                    Operation = FlowEngineOperationType.CompleteOrder,
                    Flags = new FlowEngineExecutionFlags
                    {
                        DryRun = input.DryRun,
                        CloseOrder = input.CloseOrder
                    },
                    Params = new FlowEngineExecutionParams
                    {
                        OrderId = input.OrderId.Trim()
                    }
                },
                input.DryRun
                    ? "FlowEngine-korning klar: Shopify complete order dry run sparad i jobbhistoriken."
                    : "FlowEngine-korning klar: Shopify complete order korning sparades i jobbhistoriken.",
                cancellationToken);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> FlowEngineRunCompleteOrders(FlowEngineRunCompleteOrdersInput input, CancellationToken cancellationToken)
        {
            var hasRangeInput = !string.IsNullOrWhiteSpace(input.SinceUtc) || !string.IsNullOrWhiteSpace(input.UntilUtc);
            var normalizedDate = input.UseLatestDay || hasRangeInput
                ? null
                : string.IsNullOrWhiteSpace(input.DateUtc)
                    ? DateTime.UtcNow.ToString("yyyy-MM-dd")
                    : input.DateUtc.Trim();
            var normalizedSince = string.IsNullOrWhiteSpace(input.SinceUtc) ? null : input.SinceUtc.Trim();
            var normalizedUntil = string.IsNullOrWhiteSpace(input.UntilUtc) ? null : input.UntilUtc.Trim();

            return await ExecuteFlowEngineJobAsync(
                new FlowEngineExecuteJobRequest
                {
                    Name = "shopify-complete-orders",
                    UiLabel = "Shopify complete orders",
                    Operation = FlowEngineOperationType.CompleteOrders,
                    Flags = new FlowEngineExecutionFlags
                    {
                        DryRun = input.DryRun,
                        CloseOrder = input.CloseOrder,
                        ForceRange = input.ForceRange
                    },
                    Params = new FlowEngineExecutionParams
                    {
                        DateUtc = normalizedDate,
                        SinceUtc = normalizedSince,
                        UntilUtc = normalizedUntil,
                        UseLatestDay = input.UseLatestDay,
                        UseLimit = input.Limit.HasValue && input.Limit.Value > 0,
                        Limit = input.Limit
                    }
                },
                input.DryRun
                    ? "FlowEngine-korning klar: Shopify complete orders dry run sparad i jobbhistoriken."
                    : "FlowEngine-korning klar: Shopify complete orders korning sparades i jobbhistoriken.",
                cancellationToken);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> FlowEngineRunCompleteOrdersPending(FlowEngineRunCompleteOrdersPendingInput input, CancellationToken cancellationToken)
        {
            return await ExecuteFlowEngineJobAsync(
                new FlowEngineExecuteJobRequest
                {
                    Name = "shopify-complete-orders-pending",
                    UiLabel = "Shopify complete orders pending",
                    Operation = FlowEngineOperationType.CompleteOrdersPending,
                    Flags = new FlowEngineExecutionFlags
                    {
                        DryRun = input.DryRun,
                        CloseOrder = input.CloseOrder
                    },
                    Params = new FlowEngineExecutionParams
                    {
                        UseLimit = input.Limit.HasValue && input.Limit.Value > 0,
                        Limit = input.Limit
                    }
                },
                input.DryRun
                    ? "FlowEngine-korning klar: Shopify complete orders pending dry run sparad i jobbhistoriken."
                    : "FlowEngine-korning klar: Shopify complete orders pending korning sparades i jobbhistoriken.",
                cancellationToken);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> FlowEngineRunSendOrder(FlowEngineRunSendOrderInput input, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(input.OrderId))
            {
                SetFlowEngineAlert(Alert.DANGER, "Order ID maste anges for Centra send order.");
                return RedirectToFlowEngineSection();
            }

            return await ExecuteFlowEngineJobAsync(
                _flowEngineCentraCommandFactory.BuildSendOrder(input),
                input.DryRun
                    ? "FlowEngine-korning klar: Centra send order dry run sparad i jobbhistoriken."
                    : "FlowEngine-korning klar: Centra send order korning sparades i jobbhistoriken.",
                cancellationToken);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> FlowEngineRunSendOrders(FlowEngineRunSendOrdersInput input, CancellationToken cancellationToken)
        {
            return await ExecuteFlowEngineJobAsync(
                _flowEngineCentraCommandFactory.BuildSendOrders(input),
                input.DryRun
                    ? "FlowEngine-korning klar: Centra send orders dry run sparad i jobbhistoriken."
                    : "FlowEngine-korning klar: Centra send orders skicka till Jeeves eller markerades som existing/ineligible.",
                cancellationToken);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> FlowEngineRunSendReturn(FlowEngineRunSendReturnInput input, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(input.ReturnId))
            {
                SetFlowEngineAlert(Alert.DANGER, "Return ID maste anges for Centra send return.");
                return RedirectToFlowEngineSection();
            }

            return await ExecuteFlowEngineJobAsync(
                _flowEngineCentraCommandFactory.BuildSendReturn(input),
                input.DryRun
                    ? "FlowEngine-korning klar: Centra send return dry run sparad i jobbhistoriken."
                    : "FlowEngine-korning klar: Centra send return korning sparades i jobbhistoriken.",
                cancellationToken);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> FlowEngineRunSendReturns(FlowEngineRunSendReturnsInput input, CancellationToken cancellationToken)
        {
            return await ExecuteFlowEngineJobAsync(
                _flowEngineCentraCommandFactory.BuildSendReturns(input),
                input.DryRun
                    ? "FlowEngine-korning klar: Centra send returns dry run sparad i jobbhistoriken."
                    : "FlowEngine-korning klar: Centra send returns skicka till Jeeves eller markerades som existing/ineligible.",
                cancellationToken);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> FlowEngineRunCustomerAddresses(FlowEngineRunCustomerAddressesInput input, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(input.CustomerNumber))
            {
                SetFlowEngineAlert(Alert.DANGER, "Kundnummer maste anges for Get delivery address.");
                return RedirectToFlowEngineSection();
            }

            return await ExecuteFlowEngineJobAsync(
                new FlowEngineExecuteJobRequest
                {
                    Name = "jeeves-get-customer-addresses",
                    UiLabel = "Get delivery address",
                    Operation = FlowEngineOperationType.GetCustomerAddresses,
                    Params = new FlowEngineExecutionParams
                    {
                        JeevesCustomerNumber = input.CustomerNumber.Trim()
                    }
                },
                "FlowEngine-korning klar: delivery addresses hamtade.",
                cancellationToken);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> FlowEngineRunGetOrders(FlowEngineRunGetOrdersInput input, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(input.LookupValue))
            {
                SetFlowEngineAlert(Alert.DANGER, "Lookup-varde maste anges for Jeeves get-orders.");
                return RedirectToFlowEngineSection();
            }

            var lookupField = string.Equals(input.LookupField, "c_ordernr", StringComparison.OrdinalIgnoreCase)
                ? "c_ordernr"
                : "c_extordernr";

            return await ExecuteFlowEngineJobAsync(
                new FlowEngineExecuteJobRequest
                {
                    Name = "jeeves-get-orders",
                    UiLabel = "Jeeves get orders",
                    Operation = FlowEngineOperationType.GetOrders,
                    Params = new FlowEngineExecutionParams
                    {
                        JeevesCompanyCode = input.CompanyCode,
                        JeevesLookupField = lookupField,
                        JeevesLookupValue = input.LookupValue.Trim()
                    }
                },
                "FlowEngine-korning klar: Jeeves get-orders sparades i jobbhistoriken.",
                cancellationToken);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> FlowEngineRunOrderExists(FlowEngineRunOrderExistsInput input, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(input.OrderId))
            {
                SetFlowEngineAlert(Alert.DANGER, "Order ID maste anges for Jeeves order-exists.");
                return RedirectToFlowEngineSection();
            }

            return await ExecuteFlowEngineJobAsync(
                new FlowEngineExecuteJobRequest
                {
                    Name = "jeeves-order-exists",
                    UiLabel = "Jeeves order exists",
                    Operation = FlowEngineOperationType.OrderExists,
                    Params = new FlowEngineExecutionParams
                    {
                        OrderId = input.OrderId.Trim()
                    }
                },
                "FlowEngine-korning klar: Jeeves order-exists sparades i jobbhistoriken.",
                cancellationToken);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> FlowEngineRunProduct(FlowEngineRunProductInput input, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(input.ArticleNumber))
            {
                SetFlowEngineAlert(Alert.DANGER, "Artikelnummer maste anges for Get product.");
                return RedirectToFlowEngineSection();
            }

            return await ExecuteFlowEngineJobAsync(
                new FlowEngineExecuteJobRequest
                {
                    Name = "jeeves-get-product",
                    UiLabel = "Get product",
                    Operation = FlowEngineOperationType.GetProduct,
                    Params = new FlowEngineExecutionParams
                    {
                        JeevesProductArticleNumber = input.ArticleNumber.Trim()
                    }
                },
                "FlowEngine-korning klar: produktuppslag hamtat.",
                cancellationToken);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> FlowEngineRunArtStatus(FlowEngineRunArtStatusInput input, FlowEngineRunImportOrderInput importOrderInput, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(input.ArticleNumbers))
            {
                SetFlowEngineAlert(Alert.DANGER, "Minst ett artikelnummer maste anges for Get art status.");
                return RedirectToFlowEngineSection();
            }

            var currentState = _flowEngineImportOrderWorkflowService.LoadState();
            var hasImportOrderContext = !string.IsNullOrWhiteSpace(importOrderInput.CustomerNumber)
                || !string.IsNullOrWhiteSpace(importOrderInput.Lines)
                || !string.IsNullOrWhiteSpace(importOrderInput.CustomerReference)
                || !string.IsNullOrWhiteSpace(importOrderInput.ExternalOrderNumber)
                || !string.IsNullOrWhiteSpace(importOrderInput.DeliveryPlaceCode)
                || importOrderInput.OrderType > 0;

            if (hasImportOrderContext)
            {
                _flowEngineImportOrderWorkflowService.SaveState(
                    _flowEngineImportOrderWorkflowService.BuildState(
                        _flowEngineImportOrderWorkflowService.NormalizeInput(importOrderInput),
                        currentState,
                        documentReview: currentState?.DocumentReview,
                        artStatusRows: currentState?.ArtStatusRows));
            }

            var articleNumbers = input.ArticleNumbers
                .Split(new[] { '\r', '\n', ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToList();

            if (!await HasCompanyPermissionAsync(SubModuleFlowEngineId))
                return Forbid();

            var user = GetFlowEngineSessionUser();
            if (user?.CompanyId is null)
                return Forbid();

            try
            {
                var job = await ExecuteNormalizedFlowEngineJobAsync(
                    user,
                    new FlowEngineExecuteJobRequest
                    {
                        Name = "jeeves-get-art-status",
                        UiLabel = "Get art status",
                        Operation = FlowEngineOperationType.GetArtStatus,
                        Params = new FlowEngineExecutionParams
                        {
                            JeevesProductArticleNumbers = articleNumbers
                        }
                    },
                    cancellationToken);

                if (hasImportOrderContext)
                {
                    _flowEngineImportOrderWorkflowService.SaveState(
                        _flowEngineImportOrderWorkflowService.BuildState(
                            _flowEngineImportOrderWorkflowService.NormalizeInput(importOrderInput),
                            currentState,
                            documentReview: currentState?.DocumentReview,
                            artStatusRows: _flowEngineImportOrderWorkflowService.ParseArtStatusRowsFromJob(job)));
                }

                SetFlowEngineAlert(Alert.SUCCESS, "FlowEngine-korning klar: artikelstatus kontrollerad.");
                return RedirectToFlowEngineSection(job.Id);
            }
            catch (Exception ex)
            {
                SetFlowEngineAlert(Alert.DANGER, ex.Message);
                return RedirectToFlowEngineSection();
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> FlowEngineLoadDeliveryAddresses(FlowEngineRunImportOrderInput input, CancellationToken cancellationToken)
        {
            if (!await HasCompanyPermissionAsync(SubModuleFlowEngineId))
                return Forbid();

            var user = _contextAccessor.HttpContext?.Session.Get<UserSession>("UserObject");
            if (user?.CompanyId is null)
                return Forbid();

            var currentState = _flowEngineImportOrderWorkflowService.LoadState();
            _flowEngineImportOrderWorkflowService.SaveState(
                _flowEngineImportOrderWorkflowService.BuildState(
                    _flowEngineImportOrderWorkflowService.NormalizeInput(input),
                    currentState,
                    documentReview: currentState?.DocumentReview,
                    artStatusRows: currentState?.ArtStatusRows));

            if (string.IsNullOrWhiteSpace(input.CustomerNumber))
            {
                SetFlowEngineAlert(Alert.DANGER, "Kundnummer maste anges for att hamta leveransadresser.");
                return RedirectToFlowEngineSection();
            }

            try
            {
                var runtimeContextResult = await _jeevesRuntimeContextService.ResolveAsync(user, cancellationToken);
                if (!runtimeContextResult.Success || runtimeContextResult.Value is null)
                    throw new InvalidOperationException(runtimeContextResult.Error ?? "Jeeves runtime context kunde inte faststallas.");

                var runtimeContext = runtimeContextResult.Value;
                var job = await ExecuteNormalizedFlowEngineJobAsync(
                    user,
                    new FlowEngineExecuteJobRequest
                    {
                        Name = "jeeves-get-customer-addresses",
                        UiLabel = "Get delivery address",
                        Operation = FlowEngineOperationType.GetCustomerAddresses,
                        Params = new FlowEngineExecutionParams
                        {
                            JeevesCustomerNumber = input.CustomerNumber.Trim()
                        }
                    },
                    cancellationToken);

                var options = _flowEngineImportOrderWorkflowService.ParseDeliveryAddressOptionsFromJob(job);
                var normalizedInput = _flowEngineImportOrderWorkflowService.NormalizeInput(input);
                normalizedInput.DeliveryPlaceCode = options.Any(option => option.Code == normalizedInput.DeliveryPlaceCode)
                    ? normalizedInput.DeliveryPlaceCode
                    : string.Empty;

                _flowEngineImportOrderWorkflowService.SaveState(
                _flowEngineImportOrderWorkflowService.BuildState(
                    normalizedInput,
                    currentState,
                    deliveryAddressOptions: options,
                    addressLookupContext: new FlowEngineImportAddressLookupContext
                    {
                        CompanyCode = runtimeContext.CompanyCode,
                        CustomerNumber = normalizedInput.CustomerNumber
                    },
                    documentReview: currentState?.DocumentReview,
                    artStatusRows: currentState?.ArtStatusRows));

                SetFlowEngineAlert(
                    Alert.SUCCESS,
                    options.Count == 0
                        ? "Inga leveransadresser hittades for kunden."
                        : $"FlowEngine-korning klar: {options.Count} leveransadress(er) hamtade.");
                return RedirectToFlowEngineSection(job.Id);
            }
            catch (Exception ex)
            {
                SetFlowEngineAlert(Alert.DANGER, ex.Message);
                return RedirectToFlowEngineSection();
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> FlowEngineExtractImportDocument(FlowEngineRunImportOrderInput input, IFormFile? importDocument, CancellationToken cancellationToken)
        {
            if (!await HasCompanyPermissionAsync(SubModuleFlowEngineId))
                return Forbid();

            var normalizedInput = _flowEngineImportOrderWorkflowService.NormalizeInput(input);
            var currentState = _flowEngineImportOrderWorkflowService.LoadState();

            if (importDocument is null || importDocument.Length == 0)
            {
                _flowEngineImportOrderWorkflowService.SaveState(
                    _flowEngineImportOrderWorkflowService.BuildState(
                        normalizedInput,
                        currentState,
                        documentReview: _flowEngineImportOrderWorkflowService.BuildDocumentErrorReview(
                            null,
                            "Du måste välja en PDF för dokumentextraktion."),
                        artStatusRows: currentState?.ArtStatusRows));
                SetFlowEngineAlert(Alert.DANGER, _sharedLocalizer["Integration_NoPdfSelectedForExtraction"]);
                return RedirectToFlowEngineSection();
            }

            if (!IsPdf(importDocument))
            {
                _flowEngineImportOrderWorkflowService.SaveState(
                    _flowEngineImportOrderWorkflowService.BuildState(
                        normalizedInput,
                        currentState,
                        documentReview: _flowEngineImportOrderWorkflowService.BuildDocumentErrorReview(
                            importDocument.FileName,
                            "Endast PDF-filer stöds för dokumentextraktion."),
                        artStatusRows: currentState?.ArtStatusRows));
                SetFlowEngineAlert(Alert.DANGER, _sharedLocalizer["Integration_OnlyPdfSupportedForExtraction"]);
                return RedirectToFlowEngineSection();
            }

            try
            {
                await using var ms = new MemoryStream();
                await importDocument.CopyToAsync(ms, cancellationToken);

                var extractionResult = await _flowEngineOrderDocumentExtractionService.ExtractAsync(
                    new FlowEngineOrderDocumentInput
                    {
                        FileName = Path.GetFileName(importDocument.FileName),
                        MediaType = importDocument.ContentType,
                        Data = ms.ToArray()
                    },
                    cancellationToken);

                _flowEngineImportOrderWorkflowService.SaveState(
                    _flowEngineImportOrderWorkflowService.BuildState(
                        normalizedInput,
                        currentState,
                        documentReview: _flowEngineImportOrderWorkflowService.BuildDocumentReview(importDocument.FileName, extractionResult),
                        artStatusRows: currentState?.ArtStatusRows));

                SetFlowEngineAlert(Alert.SUCCESS, _sharedLocalizer["Integration_DocumentExtractedReadyForReview", extractionResult.Lines.Count]);
                return RedirectToFlowEngineSection();
            }
            catch (Exception ex)
            {
                _flowEngineImportOrderWorkflowService.SaveState(
                    _flowEngineImportOrderWorkflowService.BuildState(
                        normalizedInput,
                        currentState,
                        documentReview: _flowEngineImportOrderWorkflowService.BuildDocumentErrorReview(importDocument.FileName, ex.Message),
                        artStatusRows: currentState?.ArtStatusRows));

                SetFlowEngineAlert(Alert.DANGER, ex.Message);
                return RedirectToFlowEngineSection();
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> FlowEngineApplyImportDocument(FlowEngineRunImportOrderInput input)
        {
            if (!await HasCompanyPermissionAsync(SubModuleFlowEngineId))
                return Forbid();

            var currentState = _flowEngineImportOrderWorkflowService.LoadState();
            var review = currentState?.DocumentReview;
            if (review is null || review.Lines.Count == 0)
            {
                SetFlowEngineAlert(Alert.DANGER, _sharedLocalizer["Integration_NoExtractedDocumentRowsToAdd"]);
                return RedirectToFlowEngineSection();
            }

            var normalizedInput = _flowEngineImportOrderWorkflowService.NormalizeInput(input);
            normalizedInput.Lines = _flowEngineImportOrderWorkflowService.MergeDocumentLines(normalizedInput.Lines, review.Lines);

            _flowEngineImportOrderWorkflowService.SaveState(
                _flowEngineImportOrderWorkflowService.BuildState(
                    normalizedInput,
                    currentState,
                    documentReview: null,
                    artStatusRows: currentState?.ArtStatusRows));

            SetFlowEngineAlert(Alert.SUCCESS, _sharedLocalizer["Integration_ExtractedRowsAddedToImportTable", review.FileName]);
            return RedirectToFlowEngineSection();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> FlowEngineRunImportOrder(FlowEngineRunImportOrderInput input, CancellationToken cancellationToken)
        {
            if (!await HasCompanyPermissionAsync(SubModuleFlowEngineId))
                return Forbid();

            var user = _contextAccessor.HttpContext?.Session.Get<UserSession>("UserObject");
            if (user?.CompanyId is null)
                return Forbid();

            if (string.IsNullOrWhiteSpace(input.CustomerNumber) ||
                string.IsNullOrWhiteSpace(input.Lines))
            {
                SetFlowEngineAlert(Alert.DANGER, "Kundnummer och minst en rad maste anges for import order.");
                return RedirectToFlowEngineSection();
            }

            try
            {
                var normalizedInput = _flowEngineImportOrderWorkflowService.NormalizeInput(input);
                normalizedInput.DeliveryPlaceCode = _flowEngineImportOrderWorkflowService.ResolveDeliveryPlaceCode(
                    user.JeevesActiveCompany ?? 0,
                    normalizedInput.CustomerNumber,
                    normalizedInput.DeliveryPlaceCode) ?? string.Empty;
                var currentState = _flowEngineImportOrderWorkflowService.LoadState();
                _flowEngineImportOrderWorkflowService.SaveState(
                    _flowEngineImportOrderWorkflowService.BuildState(
                        normalizedInput,
                        currentState,
                        documentReview: currentState?.DocumentReview,
                        artStatusRows: currentState?.ArtStatusRows));

                var lines = _flowEngineImportOrderWorkflowService.ParseImportOrderLines(normalizedInput.Lines);
                var blockingArtStatusIssues = GetBlockingImportArtStatusIssues(lines, currentState?.ArtStatusRows);
                if (!input.DryRun && blockingArtStatusIssues.Count > 0)
                {
                    SetFlowEngineAlert(Alert.DANGER, $"Import order stoppad. Kör artikelstatuskontroll och åtgärda raderna först: {string.Join(", ", blockingArtStatusIssues.Take(5))}{(blockingArtStatusIssues.Count > 5 ? " ..." : string.Empty)}");
                    return RedirectToFlowEngineSection();
                }

                var job = await ExecuteNormalizedFlowEngineJobAsync(
                    user,
                    new FlowEngineExecuteJobRequest
                    {
                        Name = "jeeves-import-order",
                        UiLabel = input.DryRun ? "Import order dry run" : "Import order",
                        Operation = FlowEngineOperationType.ImportOrder,
                        Flags = new FlowEngineExecutionFlags
                        {
                            DryRun = input.DryRun
                        },
                        Params = new FlowEngineExecutionParams
                        {
                            JeevesImportOrder = new FlowEngineJeevesImportOrderInput
                            {
                                CustomerNumber = normalizedInput.CustomerNumber,
                                OrderType = normalizedInput.OrderType,
                                CustomerReference = normalizedInput.CustomerReference,
                                ExternalOrderNumber = normalizedInput.ExternalOrderNumber,
                                DeliveryPlaceCode = normalizedInput.DeliveryPlaceCode,
                                Lines = lines
                            }
                        }
                    },
                    cancellationToken);

                SetFlowEngineAlert(
                    Alert.SUCCESS,
                    input.DryRun
                        ? "FlowEngine-korning klar: import order mappad i dry-run-lage."
                        : "FlowEngine-korning klar: import order skickad till Jeeves.");
                return RedirectToFlowEngineSection(job.Id);
            }
            catch (Exception ex)
            {
                SetFlowEngineAlert(Alert.DANGER, ex.Message);
                return RedirectToFlowEngineSection();
            }
        }

        [HttpGet]
        public async Task<IActionResult> FlowEngineStatusSnapshot(string? section, Guid? selectedJobId, CancellationToken cancellationToken = default)
        {
            if (!await HasCompanyPermissionAsync(SubModuleFlowEngineId))
                return Forbid();

            var user = GetFlowEngineSessionUser();
            if (user?.CompanyId is not Guid companyId || companyId == Guid.Empty)
                return Forbid();

            var normalizedSection = FlowEngineSectionKeys.Normalize(section);
            var recentJobs = _flowEngineExecutionService.ListRecent(companyId, 8);
            var selectedJob = selectedJobId.HasValue
                ? _flowEngineExecutionService.Get(companyId, selectedJobId.Value)
                : null;
            var activeJobCount = recentJobs.Count(job =>
                job.Status == FlowEngineJobStatus.Queued || job.Status == FlowEngineJobStatus.Running);

            var versionParts = new List<string>
            {
                normalizedSection,
                $"active:{activeJobCount}"
            };

            if (selectedJob is not null)
                versionParts.Add($"selected:{selectedJob.Id:N}:{selectedJob.Status}:{selectedJob.CreatedAtUtc.UtcTicks}:{selectedJob.StartedAtUtc?.UtcTicks}:{selectedJob.FinishedAtUtc?.UtcTicks}:{selectedJob.ErrorMessage}");

            foreach (var job in recentJobs)
                versionParts.Add($"{job.Id:N}:{job.Status}:{job.CreatedAtUtc.UtcTicks}:{job.StartedAtUtc?.UtcTicks}:{job.FinishedAtUtc?.UtcTicks}:{job.ErrorMessage}");

            Response.Headers["Cache-Control"] = "no-store, no-cache, must-revalidate";
            Response.Headers["Pragma"] = "no-cache";

            return Json(new
            {
                version = string.Join("|", versionParts),
                hasActiveJobs = activeJobCount > 0
            });
        }

        private static List<string> ParseFlowEngineAkeneoSkus(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return new List<string>();

            return raw
                .Split(new[] { ',', ';', '\n', '\r', '\t', ' ' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(value => value.Trim())
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private async Task<IActionResult> ExecuteFlowEngineJobAsync(
            FlowEngineExecuteJobRequest request,
            string successMessage,
            CancellationToken cancellationToken)
        {
            if (!await HasCompanyPermissionAsync(SubModuleFlowEngineId))
                return Forbid();

            var user = GetFlowEngineSessionUser();
            if (user?.CompanyId is null)
                return Forbid();

            try
            {
                var job = await ExecuteNormalizedFlowEngineJobAsync(user, request, cancellationToken);
                SetFlowEngineAlert(Alert.SUCCESS, successMessage);

                return RedirectToFlowEngineSection(job.Id);
            }
            catch (Exception ex)
            {
                SetFlowEngineAlert(Alert.DANGER, ex.Message);
                return RedirectToFlowEngineSection();
            }
        }

        private Task<FlowEngineJobSnapshot> ExecuteNormalizedFlowEngineJobAsync(
            UserSession user,
            FlowEngineExecuteJobRequest request,
            CancellationToken cancellationToken)
        {
            var normalizedRequest = _flowEngineRequestNormalizer.Normalize(
                request,
                Request.HasFormContentType ? Request.Form : null);

            return _flowEngineExecutionService.ExecuteAsync(
                user,
                normalizedRequest,
                cancellationToken);
        }

        private void SetFlowEngineAlert(string level, string message)
            => SetScopedAlertForAction(GetFlowEngineTargetActionName(), level, message);

        private string GetFlowEngineTargetActionName()
            => GetFlowEngineTargetActionName(ControllerContext.ActionDescriptor.ActionName);

        private static string GetFlowEngineTargetActionName(string? actionName)
            => actionName switch
            {
                nameof(FlowEngineRunConfigValidate) => nameof(FlowEngineConfig),
                nameof(FlowEngineRunCheckOrders) => nameof(FlowEngineCentra),
                nameof(FlowEngineRunCentraFetchOrder) => nameof(FlowEngineCentra),
                nameof(FlowEngineRunCentraFetchOrders) => nameof(FlowEngineCentra),
                nameof(FlowEngineRunCentraFetchReturn) => nameof(FlowEngineCentra),
                nameof(FlowEngineRunCentraFetchReturns) => nameof(FlowEngineCentra),
                nameof(FlowEngineRunCreateShipments) => nameof(FlowEngineCentra),
                nameof(FlowEngineRunCreateShipmentsPending) => nameof(FlowEngineCentra),
                nameof(FlowEngineRunCreateShipment) => nameof(FlowEngineCentra),
                nameof(FlowEngineRunSendOrder) => nameof(FlowEngineCentra),
                nameof(FlowEngineRunSendOrders) => nameof(FlowEngineCentra),
                nameof(FlowEngineRunSendReturn) => nameof(FlowEngineCentra),
                nameof(FlowEngineRunSendReturns) => nameof(FlowEngineCentra),
                nameof(FlowEngineRunAkeneoProducts) => nameof(FlowEngineAkeneo),
                nameof(FlowEngineRunAkeneoAllProducts) => nameof(FlowEngineAkeneo),
                nameof(FlowEngineRunAkeneoSendToShopify) => nameof(FlowEngineAkeneo),
                nameof(FlowEngineRunAkeneoSendToCentra) => nameof(FlowEngineAkeneo),
                nameof(FlowEngineRunShopifyScopesCheck) => nameof(FlowEngineShopify),
                nameof(FlowEngineRunShopifyGetProducts) => nameof(FlowEngineShopify),
                nameof(FlowEngineRunShopifyFetchOrder) => nameof(FlowEngineShopify),
                nameof(FlowEngineRunShopifyFetchOrders) => nameof(FlowEngineShopify),
                nameof(FlowEngineRunShopifyValidateOrder) => nameof(FlowEngineShopify),
                nameof(FlowEngineRunShopifyValidateOrders) => nameof(FlowEngineShopify),
                nameof(FlowEngineRunShopifyCheckOrders) => nameof(FlowEngineShopify),
                nameof(FlowEngineRunShopifySendOrder) => nameof(FlowEngineShopify),
                nameof(FlowEngineRunShopifySendOrders) => nameof(FlowEngineShopify),
                nameof(FlowEngineRunCompleteOrder) => nameof(FlowEngineShopify),
                nameof(FlowEngineRunCompleteOrders) => nameof(FlowEngineShopify),
                nameof(FlowEngineRunCompleteOrdersPending) => nameof(FlowEngineShopify),
                nameof(FlowEngineRunCustomerAddresses) => nameof(FlowEngineJeeves),
                nameof(FlowEngineRunGetOrders) => nameof(FlowEngineJeeves),
                nameof(FlowEngineRunOrderExists) => nameof(FlowEngineJeeves),
                nameof(FlowEngineRunProduct) => nameof(FlowEngineJeeves),
                nameof(FlowEngineRunArtStatus) => nameof(FlowEngineJeeves),
                nameof(FlowEngineLoadDeliveryAddresses) => nameof(FlowEngineJeeves),
                nameof(FlowEngineExtractImportDocument) => nameof(FlowEngineJeeves),
                nameof(FlowEngineApplyImportDocument) => nameof(FlowEngineJeeves),
                nameof(FlowEngineRunImportOrder) => nameof(FlowEngineJeeves),
                _ => nameof(FlowEngine)
            };

        private async Task<IActionResult> RenderFlowEngineAsync(string activeSection, Guid? selectedJobId, int historyPage, FlowEngineHistoryFilterState? historyFilters, CancellationToken cancellationToken)
        {
            if (!await HasCompanyPermissionAsync(SubModuleFlowEngineId))
                return Forbid();

            var user = GetFlowEngineSessionUser();
            var workbenchSettings = NormalizeFlowEngineWorkbenchSettings(LoadFlowEngineWorkbenchSettingsState());
            var model = await _flowEngineModuleService.BuildModuleViewModelAsync(
                user,
                activeSection,
                selectedJobId,
                historyPage,
                historyFilters,
                workbenchSettings,
                cancellationToken);

            var importState = _flowEngineImportOrderWorkflowService.LoadState();
            if (importState is not null)
            {
                model.Forms.ImportOrder = importState.Form ?? new FlowEngineRunImportOrderInput();
                model.ImportDeliveryAddressOptions = importState.DeliveryAddressOptions ?? new List<FlowEngineDeliveryAddressOption>();
                model.ImportAddressLookupContext = importState.AddressLookupContext;
                model.ImportDocumentReview = importState.DocumentReview;
                model.ImportArtStatusRows = importState.ArtStatusRows ?? new List<FlowEngineJeevesArtStatusRow>();
            }

            return View("~/Views/Integration/FlowEngine/FlowEngine.cshtml", model);
        }

        private IActionResult RedirectToFlowEngineSection(Guid? selectedJobId = null)
        {
            var targetAction = GetFlowEngineTargetActionName();

            return selectedJobId.HasValue
                ? RedirectToAction(targetAction, new { selectedJobId })
                : RedirectToAction(targetAction);
        }

        private static List<string> GetBlockingImportArtStatusIssues(
            IReadOnlyCollection<FlowEngineJeevesImportLineInput> lines,
            IReadOnlyCollection<FlowEngineJeevesArtStatusRow>? artStatusRows)
        {
            var statusLookup = (artStatusRows ?? Array.Empty<FlowEngineJeevesArtStatusRow>())
                .Where(row => !string.IsNullOrWhiteSpace(row.ArticleNumber))
                .GroupBy(row => row.ArticleNumber.Trim(), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

            return lines
                .Select(line => line.ArticleNumber?.Trim())
                .Where(articleNumber => !string.IsNullOrWhiteSpace(articleNumber))
                .Select(articleNumber =>
                {
                    if (!statusLookup.TryGetValue(articleNumber!, out var row))
                        return $"{articleNumber} saknar statuskontroll";

                    return row.Importable ? null : $"{articleNumber} ({row.StatusDescription})";
                })
                .Where(issue => !string.IsNullOrWhiteSpace(issue))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList()!;
        }

        private FlowEngineWorkbenchSettingsState? LoadFlowEngineWorkbenchSettingsState()
            => _contextAccessor.HttpContext?.Session.Get<FlowEngineWorkbenchSettingsState>(FlowEngineWorkbenchSettingsSessionKey);

        private void SaveFlowEngineWorkbenchSettingsState(FlowEngineWorkbenchSettingsState state)
        {
            var session = _contextAccessor.HttpContext?.Session;
            if (session is null)
                return;

            session.Set(FlowEngineWorkbenchSettingsSessionKey, state);
        }

        private static FlowEngineWorkbenchSettingsState NormalizeFlowEngineWorkbenchSettings(FlowEngineWorkbenchSettingsState? state)
        {
            return new FlowEngineWorkbenchSettingsState
            {
                TestMode = state?.TestMode ?? true,
                DryRun = state?.DryRun ?? false,
                DebugHttp = state?.DebugHttp ?? false,
                SkipJeevesCheck = state?.SkipJeevesCheck ?? false,
                UseLimit = state?.UseLimit ?? true,
                Limit = Math.Clamp(state?.Limit ?? 10, 1, 500),
                CentraSchedulerEnabled = state?.CentraSchedulerEnabled ?? false,
                ShopifySchedulerEnabled = state?.ShopifySchedulerEnabled ?? false
            };
        }

    }
}
