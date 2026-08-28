// Owns the member dashboard shell, lazy-loaded cards, and member-facing runtime status endpoints.
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Entities.Application;
using System.Collections.Generic;
using WebApp.Services.Invoices;
using WebApp.Services.Orders;
using WebApp.Services;
using WebApp.Services.Integration;
using Microsoft.Extensions.Options;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using WebApp.Models.Dashboard;
using WebApp.Models.Integration;
using WebApp.Services.Dashboard;
using WebApp.Services.Application;
using Microsoft.Extensions.Localization;

namespace WebApp.Controllers
{
    [Authorize(Roles = "Administrator, User, SuperUser, Dashboard")]
    public class MemberController : Controller
    {
        private readonly IMemberDashboardService _memberDashboardService;
        private readonly IDashboardWidgetLayoutService _dashboardWidgetLayoutService;
        private readonly IDashboardConfigurationService _dashboardConfigurationService;
        private readonly IIntegrationSyncService _integrationSyncService;
        private readonly IOptions<IntegrationOptions> _integrationOptions;
        private readonly IOptions<AkeneoOptions> _akeneoOptions;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IJeevesAuthService _jeevesAuthService;
        private readonly IHttpContextAccessor _contextAccessor;
        private readonly ISidebarRuntimeStatusService _sidebarRuntimeStatusService;
        private readonly IStringLocalizer<SharedResources> _sharedLocalizer;

        public MemberController(
            IMemberDashboardService memberDashboardService,
            IDashboardWidgetLayoutService dashboardWidgetLayoutService,
            IDashboardConfigurationService dashboardConfigurationService,
            IIntegrationSyncService integrationSyncService,
            IOptions<IntegrationOptions> integrationOptions,
            IOptions<AkeneoOptions> akeneoOptions,
            IHttpClientFactory httpClientFactory,
            IJeevesAuthService jeevesAuthService,
            IHttpContextAccessor contextAccessor,
            ISidebarRuntimeStatusService sidebarRuntimeStatusService,
            IStringLocalizer<SharedResources> sharedLocalizer)
        {
            _memberDashboardService = memberDashboardService;
            _dashboardWidgetLayoutService = dashboardWidgetLayoutService;
            _dashboardConfigurationService = dashboardConfigurationService;
            _integrationSyncService = integrationSyncService;
            _integrationOptions = integrationOptions;
            _akeneoOptions = akeneoOptions;
            _httpClientFactory = httpClientFactory;
            _jeevesAuthService = jeevesAuthService;
            _contextAccessor = contextAccessor;
            _sidebarRuntimeStatusService = sidebarRuntimeStatusService;
            _sharedLocalizer = sharedLocalizer;
        }

        public async Task<IActionResult> Index()
        {
            var vm = await _memberDashboardService.BuildAsync(HttpContext.RequestAborted);
            return View("~/Views/Member/MainDashboard.cshtml", vm);
        }

        [HttpGet]
        [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
        public async Task<IActionResult> DashboardGrid(CancellationToken cancellationToken)
        {
            var vm = await _memberDashboardService.BuildAsync(cancellationToken);
            return PartialView("~/Views/Member/Dashboard/_DashboardGrid.cshtml", vm.Cards);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveDashboardLayout([FromBody] DashboardLayoutUpdateRequest? request, CancellationToken cancellationToken)
        {
            var user = _contextAccessor.HttpContext?.Session.Get<UserSession>("UserObject");
            if (user?.CompanyId is not Guid companyId || companyId == Guid.Empty || string.IsNullOrWhiteSpace(user.UserId))
            {
                return BadRequest(new { message = "Ett aktivt användar- och bolagsval krävs för att spara startsidan." });
            }

            var allowedCards = (await _dashboardConfigurationService
                .GetAvailableCardsAsync(user, cancellationToken))
                .Where(card => card.Enabled)
                .Where(card => user.HasDataAccess || !card.RequiresDataAccess)
                .ToArray();
            var widgets = request?.Widgets?
                .Select(item => new DashboardWidgetLayout
                {
                    WidgetId = item.WidgetId?.Trim() ?? string.Empty,
                    SortOrder = item.SortOrder,
                    Size = item.Size,
                    IsVisible = true
                })
                .ToList() ?? [];

            try
            {
                await _dashboardWidgetLayoutService.SaveAsync(user, widgets, allowedCards, cancellationToken);
                return Ok(new { message = "Startsidan har sparats." });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetDashboardLayout(CancellationToken cancellationToken)
        {
            var user = _contextAccessor.HttpContext?.Session.Get<UserSession>("UserObject");
            if (user?.CompanyId is not Guid companyId || companyId == Guid.Empty || string.IsNullOrWhiteSpace(user.UserId))
            {
                return BadRequest(new { message = "Ett aktivt användar- och bolagsval krävs för att återställa startsidan." });
            }

            await _dashboardWidgetLayoutService.ResetAsync(user, cancellationToken);
            var allowedCardIds = (await _dashboardConfigurationService
                    .GetAvailableCardsAsync(user, cancellationToken))
                .Where(card => card.Enabled)
                .Where(card => user.HasDataAccess || !card.RequiresDataAccess)
                .Select(card => card.Id)
                .ToHashSet(StringComparer.Ordinal);
            var widgets = _dashboardConfigurationService
                .GetDefaultLayout(user)
                .Where(widget => widget.IsVisible && allowedCardIds.Contains(widget.WidgetId))
                .OrderBy(widget => widget.SortOrder)
                .Select(widget => new
                {
                    widgetId = widget.WidgetId,
                    sortOrder = widget.SortOrder,
                    size = widget.Size
                });

            return Ok(new { message = "Startsidan har återställts.", widgets });
        }

        [HttpGet]
        public IActionResult SalesMember()
        {
            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
        public async Task<IActionResult> CustomerActivityCard()
        {
            return await RenderDashboardCardAsync(DashboardCardIds.CustomerActivity, HttpContext.RequestAborted);
        }

        [HttpGet]
        [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
        public async Task<IActionResult> ActionCenterCard(CancellationToken cancellationToken)
        {
            return await RenderDashboardCardAsync(DashboardCardIds.ActionCenter, cancellationToken);
        }

        [HttpGet]
        [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
        public async Task<IActionResult> DashboardCard(string? cardId, CancellationToken cancellationToken)
        {
            var normalizedCardId = cardId?.Trim();
            if (string.IsNullOrWhiteSpace(normalizedCardId) || normalizedCardId.Length > 64)
            {
                return BadRequest();
            }

            return await RenderDashboardCardAsync(normalizedCardId, cancellationToken);
        }

        private async Task<IActionResult> RenderDashboardCardAsync(
            string cardId,
            CancellationToken cancellationToken)
        {
            var card = await _memberDashboardService.BuildCardAsync(cardId, cancellationToken);
            return card is null
                ? NotFound()
                : PartialView("~/Views/Member/Dashboard/_DashboardCardContent.cshtml", card);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult MarkSidebarNotificationsRead(string? returnUrl)
        {
            var user = _contextAccessor.HttpContext?.Session.Get<UserSession>("UserObject");
            if (user is not null)
                _sidebarRuntimeStatusService.MarkAllRead(user);

            if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
                return Redirect(returnUrl);

            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public async Task<IActionResult> SidebarRuntimeStatusSnapshot(CancellationToken cancellationToken)
        {
            var user = _contextAccessor.HttpContext?.Session.Get<UserSession>("UserObject");
            var status = await _sidebarRuntimeStatusService.GetStatusAsync(user, cancellationToken);
            var versionParts = new List<string>
            {
                $"{status.OverallTone}|{status.OverallLabel}|{status.RunningLabel}|{status.LatestLabel}|{status.NotificationCount}"
            };

            foreach (var item in status.NotificationItems.Take(10))
                versionParts.Add($"{item.AggregateKey}|{item.OccurredAtUtc:O}|{item.Title}|{item.StatusLabel}|{item.StatusTone}");

            if (status.ActionCenterSummaryItem is { } actionCenterSummaryItem)
                versionParts.Add($"action-center-summary:{actionCenterSummaryItem.AggregateKey}|{actionCenterSummaryItem.OccurredAtUtc:O}|{actionCenterSummaryItem.Title}|{actionCenterSummaryItem.StatusLabel}|{actionCenterSummaryItem.StatusTone}");

            if (status.LatestItem is { } latestItem)
                versionParts.Add($"latest:{latestItem.AggregateKey}|{latestItem.OccurredAtUtc:O}|{latestItem.Title}|{latestItem.StatusLabel}|{latestItem.StatusTone}");

            Response.Headers["Cache-Control"] = "no-store, no-cache, must-revalidate";
            Response.Headers["Pragma"] = "no-cache";

            return Json(new
            {
                version = string.Join(";", versionParts),
                overallLabel = status.OverallLabel,
                overallTone = status.OverallTone,
                runningLabel = status.RunningLabel,
                latestLabel = status.LatestLabel,
                notificationCount = status.NotificationCount,
                latestItem = status.LatestItem is null ? null : new
                {
                    title = status.LatestItem.Title,
                    source = status.LatestItem.Source,
                    summary = status.LatestItem.Summary,
                    statusTone = status.LatestItem.StatusTone,
                    timeLabel = status.LatestItem.TimeLabel,
                    iconClass = status.LatestItem.IconClass
                },
                actionCenterSummaryItem = status.ActionCenterSummaryItem is null ? null : new
                {
                    title = status.ActionCenterSummaryItem.Title,
                    source = status.ActionCenterSummaryItem.Source,
                    summary = status.ActionCenterSummaryItem.Summary,
                    statusLabel = LocalizeRuntimeStatusLabel(status.ActionCenterSummaryItem.StatusLabel),
                    statusTone = status.ActionCenterSummaryItem.StatusTone,
                    occurredAt = status.ActionCenterSummaryItem.OccurredAtUtc.ToLocalTime().ToString("d MMM yyyy HH:mm:ss"),
                    linkUrl = status.ActionCenterSummaryItem.LinkUrl,
                    iconClass = status.ActionCenterSummaryItem.IconClass
                },
                notificationItems = status.NotificationItems.Select(item => new
                {
                    title = item.Title,
                    source = item.Source,
                    summary = item.Summary,
                    statusLabel = LocalizeRuntimeStatusLabel(item.StatusLabel),
                    statusTone = item.StatusTone,
                    occurredAt = item.OccurredAtUtc.ToLocalTime().ToString("d MMM yyyy HH:mm:ss"),
                    linkUrl = item.LinkUrl
                }).ToList()
            });
        }

        private string LocalizeRuntimeStatusLabel(string? statusLabel) => statusLabel switch
        {
            "Queued" => _sharedLocalizer["RuntimeStatus_Queued"].Value,
            "Running" => _sharedLocalizer["RuntimeStatus_Running"].Value,
            "Completed" => _sharedLocalizer["RuntimeStatus_Completed"].Value,
            "Failed" => _sharedLocalizer["RuntimeStatus_Failed"].Value,
            "Canceled" or "Cancelled" => _sharedLocalizer["RuntimeStatus_Canceled"].Value,
            _ => string.IsNullOrWhiteSpace(statusLabel) ? _sharedLocalizer["RuntimeStatus_Unknown"].Value : statusLabel
        };

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RunIntegrationSync(string? externalOrderId, DateTime? createdFrom, DateTime? createdTo, CancellationToken ct)
        {
            var user = _contextAccessor.HttpContext?.Session.Get<UserSession>("UserObject");
            if (user?.CompanyId is null)
            {
                return BadRequest(new { message = "User is missing CompanyId." });
            }

            var (fromUtc, toUtc) = NormalizeDateRange(createdFrom, createdTo);
            var result = await _integrationSyncService.SyncCompanyAsync(user.CompanyId.Value, externalOrderId, fromUtc, toUtc, ct);
            return Ok(new
            {
                finishedAtUtc = result.FinishedAtUtc,
                centraCount = result.CentraCount,
                matchedCount = result.MatchedExternalIds.Count,
                ongoingCount = result.OngoingCount,
                missingInJeevesCount = result.MissingInJeevesCount,
                missingInOngoingCount = result.MissingInOngoingCount,
                errors = result.Errors.Select(e => new
                {
                    source = e.Source.ToString(),
                    statusCode = e.StatusCode,
                    message = e.Message
                }),
                centraOrders = result.CentraOrders.Select(o => new
                {
                    externalId = o.ExternalId,
                    orderNo = o.OrderNo,
                    orderDate = o.OrderDate,
                    customerName = o.CustomerName,
                    status = o.Status,
                    totalAmount = o.TotalAmount,
                    currency = o.Currency
                }),
                ongoingOrders = result.OngoingOrders.Select(o => new
                {
                    externalId = o.ExternalId,
                    orderNo = o.OrderNo,
                    orderDate = o.OrderDate,
                    customerName = o.CustomerName,
                    status = o.Status,
                    totalAmount = o.TotalAmount,
                    currency = o.Currency
                }),
                matchedExternalIds = result.MatchedExternalIds,
                matchedOngoingOrderNos = result.MatchedOngoingOrderNos,
                warnings = result.Warnings
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RunIntegrationHealth(CancellationToken ct)
        {
            var user = _contextAccessor.HttpContext?.Session.Get<UserSession>("UserObject");
            if (user?.CompanyId is null)
            {
                return BadRequest(new { message = "User is missing CompanyId." });
            }

            var config = _integrationOptions.Value.Companies.FirstOrDefault(c => c.CompanyId == user.CompanyId && c.Enabled);
            if (config == null)
            {
                return Ok(new
                {
                    message = "No integration config found for company.",
                    configuredCompanies = _integrationOptions.Value.Companies.Select(c => c.CompanyId.ToString()).ToList(),
                    centra = new { status = "missing_config" },
                    jeeves = new { status = "missing_config" }
                });
            }

            var centraResult = await CheckCentraAsync(config, ct);
            var jeevesResult = await CheckJeevesAsync(config, user.CompanyId.Value, ct);
            var ongoingResult = await CheckOngoingAsync(config, ct);
            var akeneoResult = await CheckAkeneoAsync(ct);

            return Ok(new
            {
                centra = centraResult,
                jeeves = jeevesResult,
                ongoing = ongoingResult,
                akeneo = akeneoResult,
                sources = config.Sources.Select(s => new
                {
                    source = s.Source.ToString(),
                    enabled = s.Enabled,
                    baseUrl = s.BaseUrl,
                    authUrl = s.AuthUrl
                })
            });
        }

        private async Task<object> CheckCentraAsync(IntegrationCompanyConfig config, CancellationToken ct)
        {
            var source = config.GetSource(IntegrationSource.Centra);
            if (source == null || string.IsNullOrWhiteSpace(source.BaseUrl))
            {
                return new { status = "missing_config" };
            }

            var client = _httpClientFactory.CreateClient("Integration.Centra");
            client.BaseAddress = new Uri(source.BaseUrl);
            if (!string.IsNullOrWhiteSpace(source.Token))
            {
                client.DefaultRequestHeaders.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", source.Token);
            }

            var payload = new { query = "{ __typename }" };
            try
            {
                using var response = await client.PostAsJsonAsync(string.Empty, payload, cancellationToken: ct);
                return new
                {
                    status = response.IsSuccessStatusCode ? "ok" : "error",
                    statusCode = (int)response.StatusCode
                };
            }
            catch (Exception)
            {
                return new { status = "error", message = "Kontrollen kunde inte genomföras just nu." };
            }
        }

        private async Task<object> CheckJeevesAsync(IntegrationCompanyConfig config, Guid companyId, CancellationToken ct)
        {
            var source = config.GetSource(IntegrationSource.Jeeves);
            if (source == null || string.IsNullOrWhiteSpace(source.BaseUrl) || string.IsNullOrWhiteSpace(source.AuthUrl))
            {
                return new { status = "missing_config" };
            }

            var token = await _jeevesAuthService.GetAccessTokenAsync(
                cacheKey: $"{companyId}:jeeves",
                authUrl: source.AuthUrl,
                appId: source.AppId ?? string.Empty,
                appSecret: source.AppSecret ?? string.Empty,
                ct: ct);

            if (string.IsNullOrWhiteSpace(token))
            {
                return new { status = "auth_failed" };
            }

            try
            {
                var client = _httpClientFactory.CreateClient("Integration.Jeeves");
                client.BaseAddress = new Uri(source.BaseUrl.TrimEnd('/') + "/");

                var healthQuery = $"orders?c_foretagkod={config.JeevesCompanyCode ?? 0}&c_pagesize=1&c_pagenumber=1";
                using var request = new HttpRequestMessage(HttpMethod.Get, healthQuery);
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
                request.Headers.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
                using var response = await client.SendAsync(request, ct);

                var reachable = (int)response.StatusCode < 500;
                return new
                {
                    status = reachable ? "ok" : "error",
                    statusCode = (int)response.StatusCode
                };
            }
            catch (Exception)
            {
                return new { status = "error", message = "Kontrollen kunde inte genomföras just nu." };
            }
        }

        private async Task<object> CheckOngoingAsync(IntegrationCompanyConfig config, CancellationToken ct)
        {
            var source = config.GetSource(IntegrationSource.Ongoing);
            if (source == null || string.IsNullOrWhiteSpace(source.BaseUrl))
            {
                return new
                {
                    status = "missing_config",
                    missing = new[] { "BaseUrl" }
                };
            }

            if (string.IsNullOrWhiteSpace(source.Username) || string.IsNullOrWhiteSpace(source.Password))
            {
                var missing = new List<string>();
                if (string.IsNullOrWhiteSpace(source.Username))
                    missing.Add("Username");
                if (string.IsNullOrWhiteSpace(source.Password))
                    missing.Add("Password");

                return new
                {
                    status = "missing_credentials",
                    missing
                };
            }

            if (source.GoodsOwnerId is null)
            {
                return new
                {
                    status = "missing_goods_owner_id",
                    missing = new[] { "GoodsOwnerId" }
                };
            }

            try
            {
                var client = _httpClientFactory.CreateClient("Integration.Ongoing");
                client.BaseAddress = new Uri(source.BaseUrl.TrimEnd('/') + "/");
                var authToken = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{source.Username}:{source.Password}"));
                client.DefaultRequestHeaders.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", authToken);
                client.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));

                var healthQuery = $"orders?goodsOwnerId={source.GoodsOwnerId}&orderNumber=__healthcheck__";
                using var response = await client.GetAsync(healthQuery, ct);
                var reachable = (int)response.StatusCode < 500;
                return new
                {
                    status = reachable ? "ok" : "error",
                    statusCode = (int)response.StatusCode
                };
            }
            catch (Exception)
            {
                return new { status = "error", message = "Kontrollen kunde inte genomföras just nu." };
            }
        }

        private async Task<object> CheckAkeneoAsync(CancellationToken ct)
        {
            var opt = _akeneoOptions.Value;
            if (!opt.Enabled || string.IsNullOrWhiteSpace(opt.BaseUrl))
            {
                return new { status = "missing_config" };
            }

            if (string.IsNullOrWhiteSpace(opt.ClientId) ||
                string.IsNullOrWhiteSpace(opt.ClientSecret) ||
                string.IsNullOrWhiteSpace(opt.Username) ||
                string.IsNullOrWhiteSpace(opt.Password))
            {
                return new { status = "missing_credentials" };
            }

            try
            {
                var client = _httpClientFactory.CreateClient("Integration.Akeneo");
                var tokenUrl = $"{opt.BaseUrl.TrimEnd('/')}/api/oauth/v1/token";

                var payload = new Dictionary<string, string>
                {
                    ["grant_type"] = "password",
                    ["client_id"] = opt.ClientId,
                    ["client_secret"] = opt.ClientSecret,
                    ["username"] = opt.Username,
                    ["password"] = opt.Password
                };

                using var response = await client.PostAsync(tokenUrl, new FormUrlEncodedContent(payload), ct);
                var reachable = (int)response.StatusCode < 500;
                return new
                {
                    status = reachable ? "ok" : "error",
                    statusCode = (int)response.StatusCode
                };
            }
            catch (Exception)
            {
                return new { status = "error", message = "Kontrollen kunde inte genomföras just nu." };
            }
        }

        private static (DateTime? FromUtc, DateTime? ToUtc) NormalizeDateRange(DateTime? from, DateTime? to)
        {
            if (from is null && to is null)
                return (null, null);

            DateTime? fromUtc = null;
            DateTime? toUtc = null;

            if (from is not null)
            {
                var localStart = DateTime.SpecifyKind(from.Value.Date, DateTimeKind.Local);
                fromUtc = localStart.ToUniversalTime();
            }

            if (to is not null)
            {
                var localEnd = DateTime.SpecifyKind(to.Value.Date.AddDays(1).AddTicks(-1), DateTimeKind.Local);
                toUtc = localEnd.ToUniversalTime();
            }

            return (fromUtc, toUtc);
        }
    }
}
