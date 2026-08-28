using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using WebApp.Models.DocumentSigning;

namespace WebApp.Models.Orders
{
    public class OrderHeader
    {
        public long OrderNo { get; set; }
        public string OrderNoAlfa { get; set; } = string.Empty;
        public string CustomerNo { get; set; } = string.Empty;
        public string CustomerName { get; set; } = string.Empty;
        public DateTime? OrderDate { get; set; }
        public DateTime? PromisedDate { get; set; }
        public DateTime? PlannedDelivery { get; set; }
        public DateTime? ActualDelivery { get; set; }
        public decimal AmountExclVat { get; set; }
        public decimal AmountInclVat { get; set; }
        public string Currency { get; set; } = string.Empty;
        public string StatusCode { get; set; } = string.Empty;
        public string OrderType { get; set; } = string.Empty;
        public string SalesPerson { get; set; } = string.Empty;
        public string CompanyCode { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public bool IsClosed { get; set; }
    }

    public class OrderLine
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

    public class OrdersListViewModel
    {
        public IReadOnlyList<OrderHeader> Orders { get; set; } = Array.Empty<OrderHeader>();
        public string CurrentSort { get; set; } = "date";
        public string CurrentDir { get; set; } = "desc";
        public string? Search { get; set; }
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }
        public string PaymentFilter { get; set; } = "all";
        public decimal PaidAmountTotal { get; set; }
        public decimal UnpaidAmountTotal { get; set; }
        public decimal GrandAmountTotal { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 50;
        public int TotalCount { get; set; }
        public int TotalPages { get; set; } = 1;
        public int? SelectedYear { get; set; }
        public IReadOnlyList<int> AvailableYears { get; set; } = Array.Empty<int>();
        public bool UsesDefaultPeriod { get; set; }
    }

    public class OrderDetailsViewModel
    {
        public OrderHeader? Header { get; set; }
        public IReadOnlyList<OrderLine> Lines { get; set; } = Array.Empty<OrderLine>();
        public bool DocumentSigningEnabled { get; set; }
        public IReadOnlyList<DocumentSigningListItem> DocumentSignings { get; set; } = Array.Empty<DocumentSigningListItem>();
        public OrderDocumentSigningFormViewModel DocumentSigningForm { get; set; } = new();
    }

    public class OrderDeliveryForecastViewModel
    {
        public int MonthsAhead { get; set; } = 6;
        public string? CustomerFilter { get; set; }
        public IReadOnlyList<int> AvailableMonthRanges { get; set; } = new[] { 3, 6, 12 };
        public IReadOnlyList<OrderCustomerOption> CustomerOptions { get; set; } = Array.Empty<OrderCustomerOption>();
        public int FutureOrderCount { get; set; }
        public decimal FutureAmountTotal { get; set; }
        public DateTime? EarliestDeliveryDate { get; set; }
        public DateTime? LatestDeliveryDate { get; set; }
        public string TopMonthLabel { get; set; } = "-";
        public int TopMonthOrderCount { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 25;
        public int TotalCount { get; set; }
        public int TotalPages { get; set; } = 1;
        public IReadOnlyList<OrderDeliveryForecastBucket> Timeline { get; set; } = Array.Empty<OrderDeliveryForecastBucket>();
        public IReadOnlyList<OrderHeader> UpcomingOrders { get; set; } = Array.Empty<OrderHeader>();
    }

    public class OrderCustomerOption
    {
        public string CustomerNo { get; set; } = string.Empty;
        public string CustomerName { get; set; } = string.Empty;
        public string DisplayName => string.IsNullOrWhiteSpace(CustomerName) ? CustomerNo : $"{CustomerNo} - {CustomerName}";
    }

    public class OrderDeliveryForecastBucket
    {
        public DateTime PeriodStart { get; set; }
        public string Label { get; set; } = string.Empty;
        public int OrderCount { get; set; }
        public decimal AmountTotal { get; set; }
    }

    public class OrderDocumentSigningFormViewModel
    {
        public long? RelatedOrderNo { get; set; }

        [Required(ErrorMessage = "Titel krävs.")]
        [StringLength(256)]
        public string DocumentTitle { get; set; } = string.Empty;

        [Required(ErrorMessage = "Förnamn krävs.")]
        [StringLength(100)]
        public string SignerFirstName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Efternamn krävs.")]
        [StringLength(100)]
        public string SignerLastName { get; set; } = string.Empty;

        [Required(ErrorMessage = "E-post krävs.")]
        [EmailAddress(ErrorMessage = "Ange en giltig e-postadress.")]
        [StringLength(256)]
        public string SignerEmail { get; set; } = string.Empty;

        [StringLength(64)]
        public string? SignerMobile { get; set; }

        [StringLength(1000)]
        public string? InvitationMessage { get; set; }
    }
}
