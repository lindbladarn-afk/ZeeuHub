using System.Threading.Tasks;

namespace WebApp.Services.Orders
{
    public enum OrderDataSource
    {
        Legacy = 0,
        Bi = 1
    }

    public interface IOrderSourceSelector
    {
        Task<OrderDataSource> SelectAsync(string connectionString);
    }
}
