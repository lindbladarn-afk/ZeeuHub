using System;

namespace WebApp.Models.Integration
{
    public class IntegrationFetchRequest
    {
        public Guid CompanyId { get; set; }
        public string? ExternalOrderId { get; set; }
        public DateTime? FromUtc { get; set; }
        public DateTime? ToUtc { get; set; }
        public string? Cursor { get; set; }
        public int? JeevesCompanyCode { get; set; }
    }
}
