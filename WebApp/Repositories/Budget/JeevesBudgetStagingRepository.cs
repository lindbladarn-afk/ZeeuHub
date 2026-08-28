using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using Microsoft.Data.SqlClient;
using WebApp.Models.Budget;
using WebApp.Services.ExcelImport;

namespace WebApp.Repositories.Budget
{
    public class JeevesBudgetStagingRepository : IBudgetStagingRepository
    {
        private readonly IExcelImportConnectionResolver _connectionResolver;
        public JeevesBudgetStagingRepository(IExcelImportConnectionResolver connectionResolver)
        {
            _connectionResolver = connectionResolver;
        }

        public async Task BulkInsertAsync(IEnumerable<PortalBudgetStagingRow> rows, CancellationToken cancellationToken = default)
        {
            var rowList = rows as IList<PortalBudgetStagingRow> ?? rows.ToList();
            if (rowList.Count == 0)
            {
                return;
            }

            await using var conn = new SqlConnection(_connectionResolver.ResolveConnectionString());
            await conn.OpenAsync(cancellationToken);

            await using var tx = (SqlTransaction)await conn.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
            try
            {
                using var table = BuildDataTable(rowList);
                using var bulk = new SqlBulkCopy(conn, SqlBulkCopyOptions.CheckConstraints, tx)
                {
                    DestinationTableName = "dbo.q_zu_StagingBudget",
                    BatchSize = 1000,
                    BulkCopyTimeout = 120
                };

                bulk.ColumnMappings.Add("ImportBatchId", "ImportBatchId");
                bulk.ColumnMappings.Add("RowNo", "RowNo");
                bulk.ColumnMappings.Add("RawJson", "RawJson");
                bulk.ColumnMappings.Add("ImportedAt", "ImportedAt");
                bulk.ColumnMappings.Add("ImportedBy", "ImportedBy");
                bulk.ColumnMappings.Add("CompanyId", "CompanyId");
                bulk.ColumnMappings.Add("ForetagKod", "ForetagKod");
                bulk.ColumnMappings.Add("UserId", "UserId");

                await bulk.WriteToServerAsync(table, cancellationToken);
                await tx.CommitAsync(cancellationToken);
            }
            catch
            {
                await tx.RollbackAsync(CancellationToken.None);
                throw;
            }
        }

        private static DataTable BuildDataTable(IEnumerable<PortalBudgetStagingRow> rows)
        {
            var table = new DataTable();
            table.Columns.Add("ImportBatchId", typeof(System.Guid));
            table.Columns.Add("RowNo", typeof(int));
            table.Columns.Add("RawJson", typeof(string));
            table.Columns.Add("ImportedAt", typeof(System.DateTime));
            table.Columns.Add("ImportedBy", typeof(string));
            table.Columns.Add("CompanyId", typeof(System.Guid));
            table.Columns.Add("ForetagKod", typeof(int));
            table.Columns.Add("UserId", typeof(string));

            foreach (var row in rows)
            {
                var dataRow = table.NewRow();
                dataRow["ImportBatchId"] = row.ImportBatchId;
                dataRow["RowNo"] = row.RowNo;
                dataRow["RawJson"] = ToDbValue(row.RawJson);
                dataRow["ImportedAt"] = row.ImportedAt;
                dataRow["ImportedBy"] = ToDbValue(row.ImportedBy);
                dataRow["CompanyId"] = row.CompanyId.HasValue ? row.CompanyId.Value : System.DBNull.Value;
                dataRow["ForetagKod"] = row.ForetagKod.HasValue ? row.ForetagKod.Value : System.DBNull.Value;
                dataRow["UserId"] = ToDbValue(row.UserId);
                table.Rows.Add(dataRow);
            }

            return table;
        }

        private static object ToDbValue(string? value)
        {
            return string.IsNullOrWhiteSpace(value) ? System.DBNull.Value : value;
        }
    }
}
