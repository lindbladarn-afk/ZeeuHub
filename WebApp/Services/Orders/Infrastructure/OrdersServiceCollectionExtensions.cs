using WebApp.Repositories.Orders;

namespace WebApp.Services.Orders
{
    // Registers order repositories, analytics services, and source selection.
    public static class OrdersServiceCollectionExtensions
    {
        public static IServiceCollection AddOrderServices(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddScoped<ILegacyOrdersRepository, LegacyOrdersRepository>();
            services.AddScoped<IBiOrdersRepository, BiOrdersRepository>();
            services.AddScoped<IOrderSourceSelector, OrderSourceSelector>();
            services.AddScoped<IOrdersRepository, JeevesOrdersRepository>();
            services.AddScoped<IOrdersService, JeevesOrdersService>();
            services.AddScoped<IOrdersAnalyticsQueryService, OrdersAnalyticsQueryService>();
            services.AddScoped<IOrdersAnalyticsModelBuilder, OrdersAnalyticsModelBuilder>();
            services.AddScoped<IOrdersAnalyticsService, OrdersAnalyticsService>();

            return services;
        }
    }
}
