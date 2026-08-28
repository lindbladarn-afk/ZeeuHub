using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using Microsoft.Data.SqlClient;
using WebApp.Models.CustomerActivity;

namespace WebApp.Repositories.CustomerActivity
{
    public class JeevesCustomerActivityRepository : ICustomerActivityRepository
    {
        public JeevesCustomerActivityRepository()
        {
        }

        public async Task<IReadOnlyList<CustomerActivityDto>> GetRecentAsync(string connectionString, int companyCode, int take)
        {
            if (string.IsNullOrWhiteSpace(connectionString) || companyCode <= 0)
            {
                return new List<CustomerActivityDto>();
            }

            const string sql = @"
SELECT TOP(@Take)
    oh.FtgNr           AS Customer,
    COALESCE(fr.FtgNamn, '') AS CustomerName,
    oh.OrdSumInklMoms  AS Amount,
    oh.OrderNr         AS OrderNo,
    oh.OrdDatum        AS OrderDate
FROM dbo.oh oh
LEFT JOIN dbo.fr fr ON fr.ForetagKod = oh.ForetagKod AND fr.FtgNr = oh.FtgNr
WHERE oh.OrdDatum IS NOT NULL
  AND oh.ForetagKod = @CompanyCode
ORDER BY oh.OrdDatum DESC";

            await using var conn = new SqlConnection(connectionString);
            await conn.OpenAsync();

            var rows = (await conn.QueryAsync<dynamic>(sql, new { Take = take, CompanyCode = companyCode })).ToList();
            var culture = CultureInfo.GetCultureInfo("sv-SE");

            return rows.Select(r =>
            {
                var customer = (string)(r.Customer ?? string.Empty);
                var customerName = (string)(r.CustomerName ?? string.Empty);
                var amount = (decimal)(r.Amount ?? 0m);
                var orderNo = (long)(r.OrderNo ?? 0L);
                var date = (System.DateTime)(r.OrderDate ?? System.DateTime.MinValue);
                var description = $"Order #{orderNo} · {amount.ToString("N0", culture)} kr";
                return new CustomerActivityDto
                {
                    Customer = customer,
                    CustomerName = string.IsNullOrWhiteSpace(customerName) ? customer : customerName,
                    Description = description,
                    OccurredAt = date
                };
            }).ToList();
        }
    }
}
