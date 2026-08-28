using System;
using System.Collections.Generic;

namespace WebApp.Models.Orders
{
    /// <summary>
    /// Carries the normalized filter, sorting and paging inputs for the orders list.
    /// Connection and source selection stay outside this object so the query only describes the read request itself.
    /// </summary>
    public sealed class GetOrdersQuery
    {
        public string Sort { get; set; } = "date";
        public bool Desc { get; set; } = true;
        public int? CompanyCode { get; set; }
        public string? Search { get; set; }
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }
        public string? PaymentFilter { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 50;
        public int? SelectedYear { get; set; }
        public IReadOnlyList<int>? AvailableYears { get; set; }
        public bool UsesDefaultPeriod { get; set; }
    }

    /// <summary>
    /// Describes the identity and tenant context needed to load one order and its lines.
    /// </summary>
    public sealed class GetOrderDetailsQuery
    {
        public long OrderNo { get; set; }
        public int? CompanyCode { get; set; }
        public Guid? CompanyId { get; set; }
    }

    /// <summary>
    /// Describes the company/customer scope for delivery insight reads that do not need paging.
    /// </summary>
    public sealed class GetOrderDeliveryInsightQuery
    {
        public int? CompanyCode { get; set; }
        public string? CustomerNo { get; set; }
    }

    /// <summary>
    /// Describes the filters for the upcoming deliveries/forecast view.
    /// </summary>
    public sealed class GetDeliveryForecastQuery
    {
        public int? CompanyCode { get; set; }
        public int MonthsAhead { get; set; } = 6;
        public string? CustomerNo { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 25;
    }
}
