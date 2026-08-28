using System;

namespace WebApp.Models.CustomerActivity
{
    public class CustomerActivityDto
    {
        public string Customer { get; set; } = string.Empty;
        public string CustomerName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public DateTime OccurredAt { get; set; }
    }
}
