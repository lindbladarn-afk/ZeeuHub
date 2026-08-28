using System;

namespace WebApp.Models.Integration
{
    public class IntegrationOrder
    {
        public string ExternalId { get; set; } = string.Empty;
        public string? OrderNo { get; set; }
        public string? CustomerNo { get; set; }
        public string? CustomerName { get; set; }
        public string? Status { get; set; }
        public DateTime? OrderDate { get; set; }
        public decimal? TotalAmount { get; set; }
        public string? Currency { get; set; }
        public string? RawJson { get; set; }
    }
}
