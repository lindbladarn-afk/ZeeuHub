using System;
using System.Collections.Generic;

namespace WebApp.Models.Orders
{
    public class OrderHeaderDto
    {
        public long OrderNo { get; set; }
        public string OrderNoAlfa { get; set; } = string.Empty;
        public string CustomerNo { get; set; } = string.Empty;
        public string CustomerName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public DateTime? OrderDate { get; set; }
        public DateTime? PlannedDelivery { get; set; }
        public DateTime? PromisedDate { get; set; }
        public DateTime? ActualDelivery { get; set; }
        public decimal AmountExclVat { get; set; }
        public decimal AmountInclVat { get; set; }
        public string Currency { get; set; } = string.Empty;
        public string StatusCode { get; set; } = string.Empty;
        public string OrderType { get; set; } = string.Empty;
        public string SalesPerson { get; set; } = string.Empty;
        public string CompanyCode { get; set; } = string.Empty;
        public bool IsClosed { get; set; }
    }

    public class OrderLineDto
    {
        public long OrderNo { get; set; }
        public int LineNo { get; set; }
        public string ArticleNo { get; set; } = string.Empty;
        public string ArticleDescription { get; set; } = string.Empty;
        public decimal OrderedQty { get; set; }
        public decimal DeliveredQty { get; set; }
        public decimal RestQty { get; set; }
        public string Unit { get; set; } = string.Empty;
        public decimal NetPrice { get; set; }
        public decimal LineAmountExclVat { get; set; }
        public decimal LineAmountInclVat { get; set; }
        public decimal DiscountPercent { get; set; }
        public decimal DiscountValue { get; set; }
        public string Currency { get; set; } = string.Empty;
    }

    public class OrderWithLinesDto
    {
        public OrderHeaderDto? Header { get; set; }
        public IReadOnlyList<OrderLineDto> Lines { get; set; } = Array.Empty<OrderLineDto>();
    }

    public class PagedOrdersPageResultDto
    {
        public IReadOnlyList<OrderHeaderDto> Orders { get; set; } = Array.Empty<OrderHeaderDto>();
        public int TotalCount { get; set; }
    }

    public class OrdersSummaryDto
    {
        public decimal PaidAmountTotal { get; set; }
        public decimal UnpaidAmountTotal { get; set; }
        public decimal GrandAmountTotal { get; set; }
    }

    public class OrderDeliveryInsightSummaryDto
    {
        public int OrderCount { get; set; }
        public decimal AmountTotal { get; set; }
        public DateTime? EarliestDate { get; set; }
        public DateTime? LatestDate { get; set; }
    }

    public class OrderDeliveryTimelineBucketDto
    {
        public DateTime PeriodStart { get; set; }
        public int OrderCount { get; set; }
        public decimal AmountTotal { get; set; }
    }
}
