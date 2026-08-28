using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using WebApp.Models.Orders;
using WebApp.Services.Application;
using WebApp.Services.Orders;

namespace WebApp.Repositories.Orders
{
    // Thin facade kept for existing consumers.
    // It resolves the active Jeeves connection and delegates to the correct source-specific repository.
    public sealed class JeevesOrdersRepository : IOrdersRepository
    {
        private readonly ILegacyOrdersRepository _legacyRepository;
        private readonly IBiOrdersRepository _biRepository;
        private readonly IOrderSourceSelector _orderSourceSelector;
        private readonly IJeevesConnectionResolver _jeevesConnectionResolver;

        public JeevesOrdersRepository(
            ILegacyOrdersRepository legacyRepository,
            IBiOrdersRepository biRepository,
            IOrderSourceSelector orderSourceSelector,
            IJeevesConnectionResolver jeevesConnectionResolver)
        {
            _legacyRepository = legacyRepository;
            _biRepository = biRepository;
            _orderSourceSelector = orderSourceSelector;
            _jeevesConnectionResolver = jeevesConnectionResolver;
        }

        public async Task<PagedOrdersPageResultDto> GetOrdersPageAsync(string connectionString, GetOrdersQuery query)
        {
            var selected = await SelectRepositoryAsync(connectionString);
            return await selected.Repository.GetOrdersPageAsync(selected.ConnectionString, query);
        }

        public async Task<OrdersSummaryDto> GetOrdersSummaryAsync(string connectionString, GetOrdersQuery query)
        {
            var selected = await SelectRepositoryAsync(connectionString);
            return await selected.Repository.GetOrdersSummaryAsync(selected.ConnectionString, query);
        }

        public async Task<DateTime?> GetLatestOrderDateAsync(string connectionString, int? companyCode)
        {
            var selected = await SelectRepositoryAsync(connectionString);
            return await selected.Repository.GetLatestOrderDateAsync(selected.ConnectionString, companyCode);
        }

        public async Task<OrderDeliveryInsightSummaryDto> GetOverdueDeliverySummaryAsync(string connectionString, GetOrderDeliveryInsightQuery query)
        {
            var selected = await SelectRepositoryAsync(connectionString);
            return await selected.Repository.GetOverdueDeliverySummaryAsync(selected.ConnectionString, query);
        }

        public async Task<OrderDeliveryInsightSummaryDto> GetFutureDeliverySummaryAsync(string connectionString, GetDeliveryForecastQuery query)
        {
            var selected = await SelectRepositoryAsync(connectionString);
            return await selected.Repository.GetFutureDeliverySummaryAsync(selected.ConnectionString, query);
        }

        public async Task<IReadOnlyList<OrderDeliveryTimelineBucketDto>> GetFutureDeliveryTimelineAsync(string connectionString, GetDeliveryForecastQuery query)
        {
            var selected = await SelectRepositoryAsync(connectionString);
            return await selected.Repository.GetFutureDeliveryTimelineAsync(selected.ConnectionString, query);
        }

        public async Task<OrderWithLinesDto?> GetOrderWithLinesAsync(string connectionString, GetOrderDetailsQuery query)
        {
            var selected = await SelectRepositoryAsync(connectionString);
            return await selected.Repository.GetOrderWithLinesAsync(selected.ConnectionString, query);
        }

        public async Task<IReadOnlyList<OrderCustomerOption>> GetFutureDeliveryCustomerOptionsAsync(string connectionString, GetDeliveryForecastQuery query)
        {
            var selected = await SelectRepositoryAsync(connectionString);
            return await selected.Repository.GetFutureDeliveryCustomerOptionsAsync(selected.ConnectionString, query);
        }

        public async Task<PagedOrdersPageResultDto> GetUpcomingOrdersPageAsync(string connectionString, GetDeliveryForecastQuery query)
        {
            var selected = await SelectRepositoryAsync(connectionString);
            return await selected.Repository.GetUpcomingOrdersPageAsync(selected.ConnectionString, query);
        }

        private async Task<SelectedOrdersRepository> SelectRepositoryAsync(string connectionString)
        {
            var effectiveConnectionString = string.IsNullOrWhiteSpace(connectionString)
                ? _jeevesConnectionResolver.ResolveConnectionString()
                : connectionString;

            var source = await _orderSourceSelector.SelectAsync(effectiveConnectionString);
            return source switch
            {
                OrderDataSource.Bi => new SelectedOrdersRepository
                {
                    ConnectionString = effectiveConnectionString,
                    Repository = _biRepository
                },
                _ => new SelectedOrdersRepository
                {
                    ConnectionString = effectiveConnectionString,
                    Repository = _legacyRepository
                }
            };
        }

        private sealed class SelectedOrdersRepository
        {
            public string ConnectionString { get; set; } = string.Empty;
            public IOrderDataRepository Repository { get; set; } = null!;
        }
    }
}
