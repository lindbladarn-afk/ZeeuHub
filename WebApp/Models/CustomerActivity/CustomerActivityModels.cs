using System;
using System.Collections.Generic;
using WebApp.ViewModels.Shared;

namespace WebApp.Models.CustomerActivity
{
    public class CustomerActivityItem
    {
        public string Customer { get; set; } = string.Empty;
        public string CustomerName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public DateTime OccurredAt { get; set; }
    }

    public class CustomerActivityViewModel
    {
        public IReadOnlyList<CustomerActivityItem> Items { get; set; } = Array.Empty<CustomerActivityItem>();
        public ModuleStateViewModel? AvailabilityState { get; set; }
    }
}
