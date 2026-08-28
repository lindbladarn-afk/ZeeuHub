// Central dashboard model for revenue, KPI and period context shown on the member dashboard.
using System.Collections.Generic;
using WebApp.ViewModels.Invoices;

namespace WebApp.Models.Dashboard
{
    public class OrdersKpiModel
    {
        public decimal AnnualRunRateMsek { get; set; }
        public decimal ForecastMsek { get; set; }
        public decimal AverageOrderValue { get; set; }
        public int OrdersCountPeriod { get; set; }
    }

    public class RevenueSeries
    {
        public List<string> Labels { get; set; } = new();
        public List<decimal> Values { get; set; } = new();
        public string XTitle { get; set; } = string.Empty;
    }

    public class RevenueDataModel
    {
        public RevenueSeries Week { get; set; } = new();
        public RevenueSeries Month { get; set; } = new();
        public RevenueSeries Quarter { get; set; } = new();
        public OrdersKpiModel Kpi { get; set; } = new();
        public List<string> AovLabels { get; set; } = new();
        public List<decimal> AovValues { get; set; } = new();
        public List<TopSellerItem> TopSellers { get; set; } = new();
        public AverageOrderValueDetails AverageOrderValueDetails { get; set; } = new();
        public AnnualRunRateDetails AnnualRunRateDetails { get; set; } = new();
        public RevenueAnalysisContext Analysis { get; set; } = new();
        public decimal TotalRevenueMsek { get; set; }
    }

    public class RevenueAnalysisContext
    {
        public DateTime? PeriodStart { get; set; }
        public DateTime? PeriodEnd { get; set; }
        public bool UsesFallbackPeriod { get; set; }
    }

    public class RevenueOrderDetail
    {
        public long OrderNumber { get; set; }
        public string OrderLabel { get; set; } = string.Empty;
        public DateTime OrderDate { get; set; }
        public decimal AmountInclVat { get; set; }
    }

    public class AverageOrderValueDetails
    {
        public DateTime? PeriodStart { get; set; }
        public DateTime? PeriodEnd { get; set; }
        public int OrdersCount { get; set; }
        public List<RevenueOrderDetail> Orders { get; set; } = new();
    }

    public class AnnualRunRateDetails
    {
        public DateTime? PeriodStart { get; set; }
        public DateTime? PeriodEnd { get; set; }
        public int OrdersCount { get; set; }
        public decimal RevenueMsek { get; set; }
        public List<RevenueOrderDetail> TopOrders { get; set; } = new();
    }

    public class TopSellerItem
    {
        public string ArticleNo { get; set; } = string.Empty;
        public string ArticleDescription { get; set; } = string.Empty;
        public decimal Revenue { get; set; }
        public decimal Quantity { get; set; }
    }

    public class MemberDashboardViewModel
    {
        public InvoiceListViewModel Invoices { get; set; } = new();
        public RevenueDataModel Revenue { get; set; } = new();
    }
}
