using WebApp.Repositories.CustomerActivity;

namespace WebApp.Services.CustomerActivity
{
    // Registers customer activity repository and application service.
    public static class CustomerActivityServiceCollectionExtensions
    {
        public static IServiceCollection AddCustomerActivityServices(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddScoped<ICustomerActivityRepository, JeevesCustomerActivityRepository>();
            services.AddScoped<ICustomerActivityService, JeevesCustomerActivityService>();

            return services;
        }
    }
}
