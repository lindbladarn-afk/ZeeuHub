using System;

namespace WebApp.Models.Integration
{
    public class IntegrationMatch
    {
        public Guid CompanyId { get; set; }
        public string CentraOrderId { get; set; } = string.Empty;
        public string? JeevesOrderId { get; set; }
        public string MatchType { get; set; } = "Exact";
        public DateTime MatchedAtUtc { get; set; } = DateTime.UtcNow;
    }
}
