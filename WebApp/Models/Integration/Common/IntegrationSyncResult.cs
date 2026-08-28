using System;
using System.Collections.Generic;

namespace WebApp.Models.Integration
{
    public class IntegrationSyncResult
    {
        public Guid CompanyId { get; set; }
        public DateTime StartedAtUtc { get; set; } = DateTime.UtcNow;
        public DateTime? FinishedAtUtc { get; set; }
        public List<string> Warnings { get; set; } = new();
        public int CentraCount { get; set; }
        public int JeevesCount { get; set; }
        public int OngoingCount { get; set; }
        public int MissingInJeevesCount { get; set; }
        public int MissingInOngoingCount { get; set; }
        public List<IntegrationOrder> CentraOrders { get; set; } = new();
        public List<IntegrationOrder> JeevesOrders { get; set; } = new();
        public List<IntegrationOrder> OngoingOrders { get; set; } = new();
        public List<string> MatchedExternalIds { get; set; } = new();
        public List<string> MatchedOngoingOrderNos { get; set; } = new();
        public List<IntegrationSourceError> Errors { get; set; } = new();
    }
}
