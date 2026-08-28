using System.Threading.Tasks;
using WebApp.Models.CustomerActivity;
using WebApp.Repositories.CustomerActivity;
using WebApp.Services.Application;

namespace WebApp.Services.CustomerActivity
{
    public class JeevesCustomerActivityService : ICustomerActivityService
    {
        private readonly ICustomerActivityRepository _repository;
        private readonly IJeevesConnectionResolver _jeevesConnectionResolver;

        public JeevesCustomerActivityService(
            ICustomerActivityRepository repository,
            IJeevesConnectionResolver jeevesConnectionResolver)
        {
            _repository = repository;
            _jeevesConnectionResolver = jeevesConnectionResolver;
        }

        public async Task<CustomerActivityViewModel> GetRecentAsync(string? connectionString, int? companyCode, int take = 5)
        {
            if (companyCode is null || companyCode <= 0)
                return new CustomerActivityViewModel();

            var resolvedConnection = string.IsNullOrWhiteSpace(connectionString)
                ? _jeevesConnectionResolver.ResolveConnectionString()
                : connectionString;

            if (string.IsNullOrWhiteSpace(resolvedConnection))
                return new CustomerActivityViewModel();

            var items = await _repository.GetRecentAsync(resolvedConnection, companyCode.Value, take);
            var mapped = items.Select(d => new CustomerActivityItem
            {
                Customer = d.Customer,
                CustomerName = d.CustomerName,
                Description = d.Description,
                OccurredAt = d.OccurredAt
            }).ToList();
            return new CustomerActivityViewModel { Items = mapped };
        }
    }
}
