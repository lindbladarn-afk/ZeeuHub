using Dapper;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Repository.Execution;

namespace WebApp.Repositories.Jeeves
{
    public class JeevesUserRepository : IJeevesUserRepository
    {
        private readonly IJeevesSqlExecutor _jeevesSqlExecutor;

        public JeevesUserRepository(IJeevesSqlExecutor jeevesSqlExecutor)
        {
            _jeevesSqlExecutor = jeevesSqlExecutor;
        }

        public async Task<IReadOnlyList<string>> GetPersSignsAsync(string jeevesConnectionString, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(jeevesConnectionString))
                throw new ArgumentNullException(nameof(jeevesConnectionString));

            const string sql = @"
SELECT DISTINCT LTRIM(RTRIM(perssign)) AS PersSign
FROM sy2 WITH (READUNCOMMITTED)
WHERE perssign IS NOT NULL AND LTRIM(RTRIM(perssign)) <> ''
ORDER BY LTRIM(RTRIM(perssign));";

            var rows = await _jeevesSqlExecutor.QueryAsync<string>(
                jeevesConnectionString,
                sql,
                operationName: "JeevesUserRepository.GetPersSigns",
                cancellationToken: ct);
            return rows.Where(x => !string.IsNullOrWhiteSpace(x)).ToList();
        }

        public async Task<bool> PersSignExistsAsync(string jeevesConnectionString, string persSign, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(jeevesConnectionString))
                throw new ArgumentNullException(nameof(jeevesConnectionString));

            if (string.IsNullOrWhiteSpace(persSign))
                return false;

            const string sql = @"
SELECT TOP 1 1
FROM sy2 WITH (READUNCOMMITTED)
WHERE perssign = @persSign;";

            var found = await _jeevesSqlExecutor.ExecuteScalarAsync<int?>(
                jeevesConnectionString,
                sql,
                new { persSign },
                operationName: "JeevesUserRepository.PersSignExists",
                cancellationToken: ct);

            return found.HasValue;
        }
    }
}
