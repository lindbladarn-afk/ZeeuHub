using System.Collections.Generic;

namespace WebApp.ViewModels.Admin
{
    public class AdminHealthDetailViewModel
    {
        public List<CompanyConnectionHealthItem> Items { get; set; } = new();

        public class CompanyConnectionHealthItem
        {
            public Guid CompanyId { get; set; }
            public string CompanyName { get; set; } = string.Empty;
            public Guid ConnectionId { get; set; }
            public string ConnectionName { get; set; } = string.Empty;
            public string? DatabaseName { get; set; }
            public bool IsActive { get; set; }
            public bool IsHealthy { get; set; }
            public string Message { get; set; } = string.Empty;
        }
    }
}
