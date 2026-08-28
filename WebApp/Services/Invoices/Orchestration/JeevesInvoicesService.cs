using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using WebApp.Models.Invoices;
using WebApp.Repositories.Invoices;
using WebApp.Services.Application;
using WebApp.ViewModels.Invoices;

namespace WebApp.Services.Invoices
{
    /// <summary>
    /// Builds invoice list and dashboard snapshot view models from Jeeves invoice data.
    /// Keeps paging, tab handling, and period fallback rules in one place for the invoices UI.
    /// </summary>
    public class JeevesInvoicesService : IInvoicesService
    {
        private readonly ILegacyInvoicesRepository _legacyRepository;
        private readonly IBiInvoicesRepository _biRepository;
        private readonly IInvoiceSourceSelector _invoiceSourceSelector;
        private readonly IJeevesConnectionResolver _jeevesConnectionResolver;
        private readonly IMemoryCache _cache;

        public JeevesInvoicesService(
            ILegacyInvoicesRepository legacyRepository,
            IBiInvoicesRepository biRepository,
            IInvoiceSourceSelector invoiceSourceSelector,
            IJeevesConnectionResolver jeevesConnectionResolver,
            IMemoryCache cache)
        {
            _legacyRepository = legacyRepository;
            _biRepository = biRepository;
            _invoiceSourceSelector = invoiceSourceSelector;
            _jeevesConnectionResolver = jeevesConnectionResolver;
            _cache = cache;
        }

        public async Task<InvoiceListViewModel> GetInvoiceListAsync(string connectionString, GetInvoicesQuery query)
        {
            // The tabbed list and its KPI counters must agree on the same normalized/fallback period.
            var selectedRepository = await SelectRepositoryAsync(connectionString);
            var period = await ResolvePeriodAsync(selectedRepository, query.CompanyCode, query.FromDate, query.ToDate, query.SelectedYear, query.AvailableYears, query.UsesDefaultPeriod);
            var normalizedTab = NormalizeActiveTab(query.ActiveTab, selectedRepository.UsesHistoricalFactSource);

            if (query.Page.HasValue && query.PageSize.HasValue)
            {
                var safePage = query.Page.Value <= 0 ? 1 : query.Page.Value;
                var safePageSize = query.PageSize.Value <= 0 ? 50 : query.PageSize.Value;
                var paged = await selectedRepository.Repository.GetInvoicesPageAsync(
                    selectedRepository.ConnectionString,
                    BuildListQuery(query.CompanyCode, query.Search, period.FromDate, period.ToDate, normalizedTab, safePage, safePageSize));

                var pagedInvoices = MapInvoices(paged.Invoices);
                var dashboardSummary = await GetCachedDashboardSummaryAsync(selectedRepository, query.CompanyCode);

                return BuildPagedInvoiceListViewModel(
                    paged,
                    pagedInvoices,
                    MapInvoices(dashboardSummary.OverdueInvoices),
                    query.Search,
                    period,
                    normalizedTab,
                    safePage,
                    safePageSize);
            }

            var rows = (await selectedRepository.Repository.GetAllInvoicesAsync(
                selectedRepository.ConnectionString,
                BuildListQuery(query.CompanyCode, query.Search, period.FromDate, period.ToDate))).ToList();

            if (!rows.Any())
            {
                return BuildEmptyInvoiceListViewModel(query.Search, period, normalizedTab, selectedRepository.UsesHistoricalFactSource);
            }

            var allInvoices = MapInvoices(rows);
            var paid = allInvoices.Where(x => x.IsPaid).OrderByDescending(x => x.PaidDate ?? x.DueDate).ToList();
            var unpaid = allInvoices.Where(x => !x.IsPaid).OrderBy(x => x.DueDate).ToList();
            var monthly = BuildMonthlySummaries(allInvoices, DateTime.Today);

            return BuildFullInvoiceListViewModel(query.Search, period, normalizedTab, selectedRepository.UsesHistoricalFactSource, paid, unpaid, monthly);
        }

        public async Task<InvoiceItem?> GetInvoiceAsync(string connectionString, int? companyCode, string invoiceNo)
        {
            var selectedRepository = await SelectRepositoryAsync(connectionString);
            var invoice = await selectedRepository.Repository.GetInvoiceAsync(
                selectedRepository.ConnectionString,
                new GetInvoiceQuery
                {
                    CompanyCode = companyCode,
                    InvoiceNo = invoiceNo
                });
            return invoice == null ? null : MapInvoice(invoice);
        }

        public async Task<InvoiceListViewModel> GetDashboardSummaryAsync(string connectionString, int? companyCode)
        {
            var selectedRepository = await SelectRepositoryAsync(connectionString);
            var summary = await GetCachedDashboardSummaryAsync(selectedRepository, companyCode);

            var overdue = summary.OverdueInvoices.Select(MapInvoice).ToList();

            return new InvoiceListViewModel
            {
                PaidInvoices = Array.Empty<InvoiceItem>(),
                UnpaidInvoices = overdue,
                OverdueHighlights = overdue,
                Monthly = Array.Empty<InvoiceMonthlySummary>(),
                TotalPaidSek = summary.TotalPaidSek,
                TotalUnpaidSek = summary.TotalUnpaidSek,
                PaidCount = 0,
                UnpaidCount = summary.UnpaidCount,
                OverdueCount = overdue.Count,
                UsesHistoricalFactSource = summary.UsesHistoricalFactSource,
                DataSourceNotice = BuildDashboardDataSourceNotice(summary.UsesHistoricalFactSource)
            };
        }

        private async Task<InvoiceDashboardSummaryDto> GetCachedDashboardSummaryAsync(SelectedInvoicesRepository selectedRepository, int? companyCode)
        {
            var cacheKey = BuildDashboardSummaryCacheKey(selectedRepository.ConnectionString, companyCode);

            return await _cache.GetOrCreateAsync(cacheKey, async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(3);
                return await selectedRepository.Repository.GetDashboardSummaryAsync(selectedRepository.ConnectionString, companyCode);
            }) ?? new InvoiceDashboardSummaryDto();
        }

        private static string BuildDashboardSummaryCacheKey(string connectionString, int? companyCode)
        {
            return string.Join("|",
                "invoices-dashboard-summary",
                companyCode?.ToString() ?? "null",
                connectionString?.GetHashCode().ToString() ?? "null");
        }

        private static string NormalizeActiveTab(string? activeTab, bool usesHistoricalFactSource)
        {
            var normalizedTab = string.Equals(activeTab, "paid", StringComparison.OrdinalIgnoreCase) ? "paid" : "unpaid";

            if (usesHistoricalFactSource && normalizedTab == "unpaid")
            {
                return "paid";
            }

            return normalizedTab;
        }

        private static List<InvoiceItem> MapInvoices(IEnumerable<InvoiceDto> invoices)
        {
            return invoices.Select(MapInvoice).ToList();
        }

        private static InvoiceListViewModel BuildPagedInvoiceListViewModel(
            PagedInvoicesResultDto paged,
            IReadOnlyList<InvoiceItem> pagedInvoices,
            IReadOnlyList<InvoiceItem> overdueHighlights,
            string? search,
            ListPeriodSelection period,
            string normalizedTab,
            int page,
            int pageSize)
        {
            var totalPages = Math.Max(1, (int)Math.Ceiling(paged.TotalCount / (double)pageSize));

            return new InvoiceListViewModel
            {
                PaidInvoices = normalizedTab == "paid" ? pagedInvoices : Array.Empty<InvoiceItem>(),
                UnpaidInvoices = normalizedTab == "unpaid" ? pagedInvoices : Array.Empty<InvoiceItem>(),
                OverdueHighlights = overdueHighlights,
                Monthly = Array.Empty<InvoiceMonthlySummary>(),
                Search = search,
                FromDate = period.FromDate,
                ToDate = period.ToDate,
                ActiveTab = normalizedTab,
                Page = page,
                PageSize = pageSize,
                TotalCount = paged.TotalCount,
                TotalPages = totalPages,
                SelectedYear = period.SelectedYear,
                AvailableYears = period.AvailableYears,
                UsesDefaultPeriod = period.UsesDefaultPeriod,
                UsesHistoricalFactSource = paged.UsesHistoricalFactSource,
                DataSourceNotice = BuildListDataSourceNotice(paged.UsesHistoricalFactSource),
                TotalPaidSek = paged.TotalPaidSek,
                TotalUnpaidSek = paged.TotalUnpaidSek,
                PaidCount = paged.PaidCount,
                UnpaidCount = paged.UnpaidCount,
                OverdueCount = paged.OverdueCount
            };
        }

        private static InvoiceListViewModel BuildEmptyInvoiceListViewModel(
            string? search,
            ListPeriodSelection period,
            string normalizedTab,
            bool usesHistoricalFactSource)
        {
            return new InvoiceListViewModel
            {
                PaidInvoices = Array.Empty<InvoiceItem>(),
                UnpaidInvoices = Array.Empty<InvoiceItem>(),
                OverdueHighlights = Array.Empty<InvoiceItem>(),
                Monthly = Array.Empty<InvoiceMonthlySummary>(),
                Search = search,
                FromDate = period.FromDate,
                ToDate = period.ToDate,
                ActiveTab = normalizedTab,
                SelectedYear = period.SelectedYear,
                AvailableYears = period.AvailableYears,
                UsesDefaultPeriod = period.UsesDefaultPeriod,
                UsesHistoricalFactSource = usesHistoricalFactSource,
                DataSourceNotice = BuildListDataSourceNotice(usesHistoricalFactSource)
            };
        }

        private static InvoiceListViewModel BuildFullInvoiceListViewModel(
            string? search,
            ListPeriodSelection period,
            string normalizedTab,
            bool usesHistoricalFactSource,
            IReadOnlyList<InvoiceItem> paid,
            IReadOnlyList<InvoiceItem> unpaid,
            IReadOnlyList<InvoiceMonthlySummary> monthly)
        {
            return new InvoiceListViewModel
            {
                PaidInvoices = paid,
                UnpaidInvoices = unpaid,
                OverdueHighlights = unpaid.Where(x => x.IsOverdue).OrderBy(x => x.DueDate).Take(3).ToList(),
                Monthly = monthly,
                Search = search,
                FromDate = period.FromDate,
                ToDate = period.ToDate,
                ActiveTab = normalizedTab,
                SelectedYear = period.SelectedYear,
                AvailableYears = period.AvailableYears,
                UsesDefaultPeriod = period.UsesDefaultPeriod,
                UsesHistoricalFactSource = usesHistoricalFactSource,
                DataSourceNotice = BuildListDataSourceNotice(usesHistoricalFactSource),
                TotalPaidSek = paid.Sum(x => x.AmountSek),
                TotalUnpaidSek = unpaid.Sum(x => x.AmountSek),
                PaidCount = paid.Count,
                UnpaidCount = unpaid.Count,
                OverdueCount = unpaid.Count(x => x.IsOverdue)
            };
        }

        private static InvoiceItem MapInvoice(InvoiceDto r)
        {
            string invoiceNo = r.InvoiceNo;
            string customer = r.Customer;
            string sales = r.SalesPerson ?? string.Empty;
            DateTime invoiceDate = r.InvoiceDate;
            DateTime dueDate = r.DueDate ?? invoiceDate;
            DateTime? paidDate = r.PaidDate;
            decimal amountIncl = r.AmountInclVat;
            decimal amountExcl = r.AmountExclVat;
            decimal paidAmount = r.PaidAmount;
            decimal remaining = r.RemainingAmount;

            var isFullyPaid = remaining <= 0m && amountIncl > 0m;
            var isPartial = paidAmount > 0m && remaining > 0m;
            var status = isFullyPaid
                ? "Betald"
                : isPartial
                    ? "Delbetald"
                    : (dueDate < DateTime.Today ? "Förfallen" : "Obetald");

            return new InvoiceItem
            {
                InvoiceNo = invoiceNo,
                Customer = customer,
                SalesPerson = sales,
                DueDate = dueDate,
                PaidDate = paidDate,
                AmountSek = amountIncl,
                AmountExclVat = amountExcl,
                PaidAmount = paidAmount,
                RemainingAmount = remaining,
                Ocr = r.Ocr,
                CompanyCode = r.CompanyCode,
                IsPaid = isFullyPaid,
                Status = status
            };
        }

        private static List<InvoiceMonthlySummary> BuildMonthlySummaries(IEnumerable<InvoiceItem> invoices, DateTime today)
        {
            var invoiceList = invoices.ToList();
            var buckets = new List<(string Label, DateTime From, DateTime To)>
            {
                ("Denna månad",
                    new DateTime(today.Year, today.Month, 1),
                    new DateTime(today.Year, today.Month, DateTime.DaysInMonth(today.Year, today.Month))),

                ("Förra månaden",
                    new DateTime(today.AddMonths(-1).Year, today.AddMonths(-1).Month, 1),
                    new DateTime(today.AddMonths(-1).Year, today.AddMonths(-1).Month, DateTime.DaysInMonth(today.AddMonths(-1).Year, today.AddMonths(-1).Month))),

                ("Två månader sedan",
                    new DateTime(today.AddMonths(-2).Year, today.AddMonths(-2).Month, 1),
                    new DateTime(today.AddMonths(-2).Year, today.AddMonths(-2).Month, DateTime.DaysInMonth(today.AddMonths(-2).Year, today.AddMonths(-2).Month)))
            };

            var summaries = new List<InvoiceMonthlySummary>();

            foreach (var bucket in buckets)
            {
                var inRange = invoiceList.Where(x => x.DueDate.Date >= bucket.From && x.DueDate.Date <= bucket.To).ToList();
                var paid = inRange.Where(x => x.IsPaid).ToList();
                var unpaid = inRange.Where(x => !x.IsPaid).ToList();

                summaries.Add(new InvoiceMonthlySummary
                {
                    Label = bucket.Label,
                    PaidAmountSek = paid.Sum(x => x.AmountSek),
                    UnpaidAmountSek = unpaid.Sum(x => x.AmountSek),
                    PaidCount = paid.Count(),
                    UnpaidCount = unpaid.Count()
                });
            }

            return summaries;
        }

        private async Task<SelectedInvoicesRepository> SelectRepositoryAsync(string connectionString)
        {
            var effectiveConnectionString = ResolveConnectionString(connectionString);
            var source = await _invoiceSourceSelector.SelectAsync(effectiveConnectionString);

            return source switch
            {
                InvoiceDataSource.Bi => new SelectedInvoicesRepository
                {
                    ConnectionString = effectiveConnectionString,
                    Repository = _biRepository,
                    UsesHistoricalFactSource = true
                },
                _ => new SelectedInvoicesRepository
                {
                    ConnectionString = effectiveConnectionString,
                    Repository = _legacyRepository,
                    UsesHistoricalFactSource = false
                }
            };
        }

        private string ResolveConnectionString(string connectionString)
        {
            return string.IsNullOrWhiteSpace(connectionString)
                ? _jeevesConnectionResolver.ResolveConnectionString()
                : connectionString;
        }

        private static string? BuildListDataSourceNotice(bool usesHistoricalFactSource)
        {
            return usesHistoricalFactSource
                ? "Holdit läser här historisk fakturering från datalagret. Därför visas fakturahistoriken under Betalda, medan obetalda, förfallna och bankavstämning kräver reskontrafält som inte finns i q_zu_bi_fsg."
                : null;
        }

        private static string? BuildDashboardDataSourceNotice(bool usesHistoricalFactSource)
        {
            return usesHistoricalFactSource
                ? "Dashboarden läser historisk fakturering från datalagret. Öppna och förfallna fakturor kan därför inte beräknas fullt ut ännu."
                : null;
        }

        private static GetInvoicesQuery BuildListQuery(
            int? companyCode,
            string? search,
            DateTime? fromDate,
            DateTime? toDate,
            string activeTab = "unpaid",
            int page = 1,
            int pageSize = 50)
        {
            return new GetInvoicesQuery
            {
                CompanyCode = companyCode,
                Search = search,
                FromDate = fromDate,
                ToDate = toDate,
                ActiveTab = activeTab,
                Page = page,
                PageSize = pageSize
            };
        }

        private async Task<ListPeriodSelection> ResolvePeriodAsync(
            SelectedInvoicesRepository selectedRepository,
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
                () => selectedRepository.Repository.GetLatestInvoiceDateAsync(selectedRepository.ConnectionString, companyCode));
        }

        private sealed class SelectedInvoicesRepository
        {
            public string ConnectionString { get; set; } = string.Empty;
            public IInvoiceDataRepository Repository { get; set; } = null!;
            public bool UsesHistoricalFactSource { get; set; }
        }
    }
}
