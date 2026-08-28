using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using WebApp.Models.Orders;
using WebApp.Repositories.Orders;
using WebApp.Services.Application;
using WebApp.Services.DocumentSigning;

namespace WebApp.Services.Orders
{
    /// <summary>
    /// Builds order list and detail view models from repository DTOs.
    /// Keeps paging and period fallback rules in one place for the orders UI.
    /// </summary>
    public class JeevesOrdersService : IOrdersService
    {
        private readonly IOrdersRepository _repository;
        private readonly IMemoryCache _cache;
        private readonly IDocumentSigningService _documentSigningService;

        public JeevesOrdersService(IOrdersRepository repository, IMemoryCache cache, IDocumentSigningService documentSigningService)
        {
            _repository = repository;
            _cache = cache;
            _documentSigningService = documentSigningService;
        }

        public async Task<OrdersListViewModel> GetOrdersAsync(string connectionString, GetOrdersQuery query)
        {
            var safePage = query.Page <= 0 ? 1 : query.Page;
            var safePageSize = query.PageSize <= 0 ? 50 : query.PageSize;

            // The list and KPI totals must agree on the same normalized/fallback period.
            var period = await ResolvePeriodAsync(connectionString, query.CompanyCode, query.FromDate, query.ToDate, query.SelectedYear, query.AvailableYears, query.UsesDefaultPeriod);
            var listQuery = CreateListQuery(query, period, safePage, safePageSize);

            var pageResult = await _repository.GetOrdersPageAsync(connectionString, listQuery);
            var normalizedFilter = NormalizePaymentFilter(query.PaymentFilter);

            // Cache only the summary aggregate so paging stays fresh while expensive totals are reused briefly.
            var summaryCacheKey = BuildSummaryCacheKey(connectionString, query.CompanyCode, query.Search, period.FromDate, period.ToDate, normalizedFilter);
            var summary = await _cache.GetOrCreateAsync(summaryCacheKey, async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(3);
                return await _repository.GetOrdersSummaryAsync(connectionString, CreateListQuery(query, period, safePage, safePageSize, normalizedFilter));
            }) ?? new OrdersSummaryDto();

            var rows = pageResult.Orders
                .Select(dto => new OrderHeader
                {
                    OrderNo = dto.OrderNo,
                    OrderNoAlfa = dto.OrderNoAlfa,
                    CustomerNo = dto.CustomerNo,
                    CustomerName = dto.CustomerName,
                    Description = dto.Description,
                    OrderDate = dto.OrderDate,
                    PlannedDelivery = dto.PlannedDelivery,
                    PromisedDate = dto.PromisedDate,
                    ActualDelivery = dto.ActualDelivery,
                    AmountExclVat = dto.AmountExclVat,
                    AmountInclVat = dto.AmountInclVat,
                    Currency = dto.Currency,
                    StatusCode = dto.StatusCode,
                    OrderType = dto.OrderType,
                    SalesPerson = dto.SalesPerson,
                    CompanyCode = dto.CompanyCode,
                    IsClosed = dto.IsClosed
                })
                .ToList();
            var totalPages = Math.Max(1, (int)Math.Ceiling(pageResult.TotalCount / (double)safePageSize));

            return new OrdersListViewModel
            {
                Orders = rows,
                CurrentSort = string.IsNullOrWhiteSpace(query.Sort) ? "date" : query.Sort.ToLower(),
                CurrentDir = query.Desc ? "desc" : "asc",
                Search = query.Search,
                FromDate = period.FromDate,
                ToDate = period.ToDate,
                PaymentFilter = normalizedFilter,
                PaidAmountTotal = summary.PaidAmountTotal,
                UnpaidAmountTotal = summary.UnpaidAmountTotal,
                GrandAmountTotal = summary.GrandAmountTotal,
                Page = safePage,
                PageSize = safePageSize,
                TotalCount = pageResult.TotalCount,
                TotalPages = totalPages,
                SelectedYear = period.SelectedYear,
                AvailableYears = period.AvailableYears,
                UsesDefaultPeriod = period.UsesDefaultPeriod
            };
        }

        public async Task<OrderDetailsViewModel?> GetOrderDetailsAsync(string connectionString, GetOrderDetailsQuery query)
        {
            var dto = await _repository.GetOrderWithLinesAsync(connectionString, query);
            if (dto?.Header == null) return null;

            var header = new OrderHeader
            {
                OrderNo = dto.Header.OrderNo,
                OrderNoAlfa = dto.Header.OrderNoAlfa,
                CustomerNo = dto.Header.CustomerNo,
                CustomerName = dto.Header.CustomerName,
                Description = dto.Header.Description,
                OrderDate = dto.Header.OrderDate,
                PlannedDelivery = dto.Header.PlannedDelivery,
                PromisedDate = dto.Header.PromisedDate,
                ActualDelivery = dto.Header.ActualDelivery,
                AmountExclVat = dto.Header.AmountExclVat,
                AmountInclVat = dto.Header.AmountInclVat,
                Currency = dto.Header.Currency,
                StatusCode = dto.Header.StatusCode,
                OrderType = dto.Header.OrderType,
                SalesPerson = dto.Header.SalesPerson,
                CompanyCode = dto.Header.CompanyCode,
                IsClosed = dto.Header.IsClosed
            };

            var lines = dto.Lines.Select(l => new OrderLine
            {
                OrderNo = l.OrderNo,
                LineNo = l.LineNo,
                ArticleNo = l.ArticleNo,
                ArticleDescription = l.ArticleDescription,
                OrderedQty = l.OrderedQty,
                DeliveredQty = l.DeliveredQty,
                RestQty = l.RestQty,
                Unit = l.Unit,
                NetPrice = l.NetPrice,
                LineAmountExclVat = l.LineAmountExclVat,
                LineAmountInclVat = l.LineAmountInclVat,
                DiscountPercent = ResolveDiscountPercent(l),
                DiscountValue = l.DiscountValue,
                Currency = l.Currency
            }).ToList();

            header = NormalizeDetailHeaderAmounts(header, lines);

            var model = new OrderDetailsViewModel
            {
                Header = header,
                Lines = lines
            };

            if (query.CompanyId.HasValue)
            {
                model.DocumentSigningEnabled = _documentSigningService.IsEnabledForCompany(query.CompanyId.Value);
                model.DocumentSignings = await _documentSigningService.ListForOrderAsync(query.CompanyId.Value, query.CompanyCode, query.OrderNo);
                model.DocumentSigningForm = new OrderDocumentSigningFormViewModel
                {
                    RelatedOrderNo = query.OrderNo,
                    DocumentTitle = $"Offert {query.OrderNo}",
                    InvitationMessage = $"Hej,\n\nvänligen signera bifogad offert.\n",
                };
            }

            return model;
        }

        private static decimal ResolveDiscountPercent(OrderLineDto line)
        {
            if (line.DiscountPercent > 0)
            {
                return line.DiscountPercent;
            }

            if (line.DiscountValue <= 0)
            {
                return 0;
            }

            var listAmount = line.OrderedQty > 0 && line.NetPrice > 0
                ? decimal.Abs(line.OrderedQty * line.NetPrice)
                : decimal.Abs(line.LineAmountExclVat) + decimal.Abs(line.DiscountValue);

            if (listAmount <= 0)
            {
                return 0;
            }

            return Math.Round(decimal.Abs(line.DiscountValue) / listAmount * 100m, 2, MidpointRounding.AwayFromZero);
        }

        private static OrderHeader NormalizeDetailHeaderAmounts(OrderHeader header, IReadOnlyList<OrderLine> lines)
        {
            if (lines.Count == 0)
            {
                return header;
            }

            var computedExclVat = lines
                .Where(line => line.OrderedQty > 0 && line.NetPrice > 0)
                .Sum(line => Math.Round(line.OrderedQty * line.NetPrice, 2, MidpointRounding.AwayFromZero));

            var computedInclVat = lines
                .Sum(line => line.LineAmountInclVat);

            if (computedExclVat <= 0 || computedInclVat <= 0)
            {
                return header;
            }

            if (header.AmountExclVat == header.AmountInclVat || header.AmountExclVat <= 0)
            {
                header.AmountExclVat = computedExclVat;
                header.AmountInclVat = computedInclVat;
            }

            return header;
        }

        public async Task<OrderDeliveryForecastViewModel> GetDeliveryForecastAsync(string connectionString, GetDeliveryForecastQuery query)
        {
            var safeMonthsAhead = query.MonthsAhead <= 0 ? 6 : query.MonthsAhead;
            var safePage = query.Page <= 0 ? 1 : query.Page;
            var safePageSize = query.PageSize <= 0 ? 25 : query.PageSize;
            var forecastQuery = CreateForecastQuery(query, safeMonthsAhead, safePage, safePageSize);

            var customerOptions = await _repository.GetFutureDeliveryCustomerOptionsAsync(connectionString, forecastQuery);
            var summary = await _repository.GetFutureDeliverySummaryAsync(connectionString, forecastQuery);
            var timeline = await _repository.GetFutureDeliveryTimelineAsync(connectionString, forecastQuery);
            var upcomingResult = await _repository.GetUpcomingOrdersPageAsync(connectionString, forecastQuery);

            var upcomingOrders = upcomingResult.Orders
                .Select(dto => new OrderHeader
                {
                    OrderNo = dto.OrderNo,
                    OrderNoAlfa = dto.OrderNoAlfa,
                    CustomerNo = dto.CustomerNo,
                    CustomerName = dto.CustomerName,
                    Description = dto.Description,
                    OrderDate = dto.OrderDate,
                    PlannedDelivery = dto.PlannedDelivery,
                    PromisedDate = dto.PromisedDate,
                    ActualDelivery = dto.ActualDelivery,
                    AmountExclVat = dto.AmountExclVat,
                    AmountInclVat = dto.AmountInclVat,
                    Currency = dto.Currency,
                    StatusCode = dto.StatusCode,
                    OrderType = dto.OrderType,
                    SalesPerson = dto.SalesPerson,
                    CompanyCode = dto.CompanyCode,
                    IsClosed = dto.IsClosed
                })
                .ToList();

            var buckets = timeline
                .Select(dto => new OrderDeliveryForecastBucket
                {
                    PeriodStart = dto.PeriodStart,
                    Label = dto.PeriodStart.ToString("MMM", System.Globalization.CultureInfo.GetCultureInfo("sv-SE")),
                    OrderCount = dto.OrderCount,
                    AmountTotal = dto.AmountTotal
                })
                .ToList();

            var topMonth = buckets
                .OrderByDescending(x => x.OrderCount)
                .ThenBy(x => x.PeriodStart)
                .FirstOrDefault();

            return new OrderDeliveryForecastViewModel
            {
                MonthsAhead = safeMonthsAhead,
                CustomerFilter = query.CustomerNo,
                CustomerOptions = customerOptions,
                FutureOrderCount = summary.OrderCount,
                FutureAmountTotal = summary.AmountTotal,
                EarliestDeliveryDate = summary.EarliestDate,
                LatestDeliveryDate = summary.LatestDate,
                TopMonthLabel = topMonth?.PeriodStart.ToString("MMMM yyyy", System.Globalization.CultureInfo.GetCultureInfo("sv-SE")) ?? "-",
                TopMonthOrderCount = topMonth?.OrderCount ?? 0,
                Page = safePage,
                PageSize = safePageSize,
                TotalCount = upcomingResult.TotalCount,
                TotalPages = Math.Max(1, (int)Math.Ceiling(upcomingResult.TotalCount / (double)safePageSize)),
                Timeline = buckets,
                UpcomingOrders = upcomingOrders
            };
        }

        private static string NormalizePaymentFilter(string? paymentFilter)
        {
            var normalizedFilter = string.IsNullOrWhiteSpace(paymentFilter)
                ? "all"
                : paymentFilter.Trim().ToLowerInvariant();

            return normalizedFilter is "paid" or "unpaid" ? normalizedFilter : "all";
        }

        // Build the repository query once so list and summary always use the same normalized period and paging inputs.
        private static GetOrdersQuery CreateListQuery(
            GetOrdersQuery query,
            ListPeriodSelection period,
            int page,
            int pageSize,
            string? paymentFilterOverride = null)
        {
            return new GetOrdersQuery
            {
                Sort = query.Sort,
                Desc = query.Desc,
                CompanyCode = query.CompanyCode,
                Search = query.Search,
                FromDate = period.FromDate,
                ToDate = period.ToDate,
                PaymentFilter = paymentFilterOverride ?? query.PaymentFilter,
                Page = page,
                PageSize = pageSize,
                SelectedYear = period.SelectedYear,
                AvailableYears = period.AvailableYears,
                UsesDefaultPeriod = period.UsesDefaultPeriod
            };
        }

        // Forecast queries share the same sanitizing rules across summary, timeline and paged upcoming rows.
        private static GetDeliveryForecastQuery CreateForecastQuery(GetDeliveryForecastQuery query, int monthsAhead, int page, int pageSize)
        {
            return new GetDeliveryForecastQuery
            {
                CompanyCode = query.CompanyCode,
                MonthsAhead = monthsAhead,
                CustomerNo = query.CustomerNo,
                Page = page,
                PageSize = pageSize
            };
        }

        private static string BuildSummaryCacheKey(
            string connectionString,
            int? companyCode,
            string? search,
            DateTime? fromDate,
            DateTime? toDate,
            string paymentFilter)
        {
            return string.Join("|",
                "orders-summary",
                companyCode?.ToString() ?? "null",
                paymentFilter,
                fromDate?.ToString("yyyy-MM-dd") ?? "null",
                toDate?.ToString("yyyy-MM-dd") ?? "null",
                search?.Trim().ToLowerInvariant() ?? string.Empty,
                connectionString?.GetHashCode().ToString() ?? "null");
        }

        private async Task<ListPeriodSelection> ResolvePeriodAsync(
            string connectionString,
            int? companyCode,
            DateTime? fromDate,
            DateTime? toDate,
            int? selectedYear,
            IReadOnlyList<int>? availableYears,
            bool usesDefaultPeriod)
        {
            return await ListPeriodSelection.ResolveWithLatestDataFallbackAsync(
                fromDate,
                toDate,
                selectedYear,
                availableYears,
                usesDefaultPeriod,
                () => _repository.GetLatestOrderDateAsync(connectionString, companyCode));
        }
    }
}
