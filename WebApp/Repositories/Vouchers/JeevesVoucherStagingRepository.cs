using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using Microsoft.Data.SqlClient;
using WebApp.Models.Voucher;
using WebApp.Services.ExcelImport;

namespace WebApp.Repositories.Vouchers
{
    public class JeevesVoucherStagingRepository : IVoucherStagingRepository
    {
        private readonly IExcelImportConnectionResolver _connectionResolver;
        public JeevesVoucherStagingRepository(IExcelImportConnectionResolver connectionResolver)
        {
            _connectionResolver = connectionResolver;
        }

        public async Task BulkInsertAsync(IEnumerable<PortalVoucherStagingRow> rows, CancellationToken cancellationToken = default)
        {
            var rowList = rows as IList<PortalVoucherStagingRow> ?? rows.ToList();
            if (rowList.Count == 0)
            {
                return;
            }

            await using var conn = new SqlConnection(_connectionResolver.ResolveConnectionString());
            await conn.OpenAsync(cancellationToken);

            await using var tx = (SqlTransaction)await conn.BeginTransactionAsync(IsolationLevel.ReadCommitted);
            try
            {
                using var table = BuildDataTable(rowList);
                using var bulk = new SqlBulkCopy(conn, SqlBulkCopyOptions.CheckConstraints, tx)
                {
                    DestinationTableName = "dbo.q_zu_StagingVoucher",
                    BatchSize = 1000,
                    BulkCopyTimeout = 120
                };

                AddMappings(bulk);
                await bulk.WriteToServerAsync(table, cancellationToken);
                await ExecuteVoucherCreationAsync(conn, tx, rowList, cancellationToken);

                await tx.CommitAsync();
            }
            catch
            {
                await tx.RollbackAsync();
                throw;
            }
        }

        private static DataTable BuildDataTable(IEnumerable<PortalVoucherStagingRow> rows)
        {
            var table = new DataTable();

            table.Columns.Add("Account", typeof(string));
            table.Columns.Add("Amount", typeof(string));
            table.Columns.Add("Autoregel", typeof(string));
            table.Columns.Add("Currency", typeof(string));
            table.Columns.Add("CurrencyRate", typeof(string));
            table.Columns.Add("Debbel", typeof(string));
            table.Columns.Add("ImportBatchId", typeof(System.Guid));
            table.Columns.Add("ImportedAt", typeof(System.DateTime));
            table.Columns.Add("ImportedBy", typeof(string));
            table.Columns.Add("K4", typeof(string));
            table.Columns.Add("K5", typeof(string));
            table.Columns.Add("K6", typeof(string));
            table.Columns.Add("K7", typeof(string));
            table.Columns.Add("Kostbar", typeof(string));
            table.Columns.Add("Koststallekod", typeof(string));
            table.Columns.Add("Krebel", typeof(string));
            table.Columns.Add("Ktonr", typeof(string));
            table.Columns.Add("Momskod", typeof(string));
            table.Columns.Add("Projcode", typeof(string));
            table.Columns.Add("Rate", typeof(string));
            table.Columns.Add("RowNo", typeof(int));
            table.Columns.Add("Valkod", typeof(string));
            table.Columns.Add("VoucherText", typeof(string));
            table.Columns.Add("Vbbelopp", typeof(string));
            table.Columns.Add("RawJson", typeof(string));
            table.Columns.Add("CompanyId", typeof(System.Guid));
            table.Columns.Add("ForetagKod", typeof(int));
            table.Columns.Add("UserId", typeof(string));
            table.Columns.Add("PostingDate", typeof(System.DateTime));
            table.Columns.Add("AterBokfDat", typeof(System.DateTime));

            foreach (var row in rows)
            {
                var dataRow = table.NewRow();
                dataRow["Account"] = ToDbValue(row.Account);
                dataRow["Amount"] = ToDbValue(row.Amount);
                dataRow["Autoregel"] = ToDbValue(row.Autoregel);
                dataRow["Currency"] = ToDbValue(row.Currency);
                dataRow["CurrencyRate"] = ToDbValue(row.CurrencyRate);
                dataRow["Debbel"] = ToDbValue(row.Debbel);
                dataRow["ImportBatchId"] = row.ImportBatchId;
                dataRow["ImportedAt"] = row.ImportedAt;
                dataRow["ImportedBy"] = ToDbValue(row.ImportedBy);
                dataRow["K4"] = ToDbValue(row.K4);
                dataRow["K5"] = ToDbValue(row.K5);
                dataRow["K6"] = ToDbValue(row.K6);
                dataRow["K7"] = ToDbValue(row.K7);
                dataRow["Kostbar"] = ToDbValue(row.Kostbar);
                dataRow["Koststallekod"] = ToDbValue(row.Koststallekod);
                dataRow["Krebel"] = ToDbValue(row.Krebel);
                dataRow["Ktonr"] = ToDbValue(row.Ktonr);
                dataRow["Momskod"] = ToDbValue(row.Momskod);
                dataRow["Projcode"] = ToDbValue(row.Projcode);
                dataRow["Rate"] = ToDbValue(row.Rate);
                dataRow["RowNo"] = row.RowNo;
                dataRow["Valkod"] = ToDbValue(row.Valkod);
                dataRow["VoucherText"] = ToDbValue(row.VoucherText);
                dataRow["Vbbelopp"] = ToDbValue(row.Vbbelopp);
                dataRow["RawJson"] = ToDbValue(row.RawJson);
                dataRow["CompanyId"] = row.CompanyId.HasValue ? row.CompanyId.Value : System.DBNull.Value;
                dataRow["ForetagKod"] = row.ForetagKod.HasValue ? row.ForetagKod.Value : System.DBNull.Value;
                dataRow["UserId"] = ToDbValue(row.UserId);
                dataRow["PostingDate"] = row.PostingDate.HasValue ? row.PostingDate.Value : System.DBNull.Value;
                dataRow["AterBokfDat"] = row.AterBokfDat.HasValue ? row.AterBokfDat.Value : System.DBNull.Value;
                table.Rows.Add(dataRow);
            }

            return table;
        }

        private static void AddMappings(SqlBulkCopy bulk)
        {
            bulk.ColumnMappings.Add("Account", "Account");
            bulk.ColumnMappings.Add("Amount", "Amount");
            bulk.ColumnMappings.Add("Autoregel", "Autoregel");
            bulk.ColumnMappings.Add("Currency", "Currency");
            bulk.ColumnMappings.Add("CurrencyRate", "CurrencyRate");
            bulk.ColumnMappings.Add("Debbel", "Debbel");
            bulk.ColumnMappings.Add("ImportBatchId", "ImportBatchId");
            bulk.ColumnMappings.Add("ImportedAt", "ImportedAt");
            bulk.ColumnMappings.Add("ImportedBy", "ImportedBy");
            bulk.ColumnMappings.Add("K4", "K4");
            bulk.ColumnMappings.Add("K5", "K5");
            bulk.ColumnMappings.Add("K6", "K6");
            bulk.ColumnMappings.Add("K7", "K7");
            bulk.ColumnMappings.Add("Kostbar", "Kostbar");
            bulk.ColumnMappings.Add("Koststallekod", "Koststallekod");
            bulk.ColumnMappings.Add("Krebel", "Krebel");
            bulk.ColumnMappings.Add("Ktonr", "Ktonr");
            bulk.ColumnMappings.Add("Momskod", "Momskod");
            bulk.ColumnMappings.Add("Projcode", "Projcode");
            bulk.ColumnMappings.Add("Rate", "Rate");
            bulk.ColumnMappings.Add("RowNo", "RowNo");
            bulk.ColumnMappings.Add("Valkod", "Valkod");
            bulk.ColumnMappings.Add("VoucherText", "VoucherText");
            bulk.ColumnMappings.Add("Vbbelopp", "Vbbelopp");
            bulk.ColumnMappings.Add("RawJson", "RawJson");
            bulk.ColumnMappings.Add("CompanyId", "CompanyId");
            bulk.ColumnMappings.Add("ForetagKod", "ForetagKod");
            bulk.ColumnMappings.Add("UserId", "UserId");
            bulk.ColumnMappings.Add("PostingDate", "PostingDate");
            bulk.ColumnMappings.Add("AterBokfDat", "AterBokfDat");
        }

        private static Task ExecuteVoucherCreationAsync(
            SqlConnection connection,
            SqlTransaction transaction,
            IList<PortalVoucherStagingRow> rows,
            CancellationToken cancellationToken)
        {
            var firstRow = rows[0];
            var command = new CommandDefinition(
                "dbo.Q_zu_create_mvr_fromstaging",
                new
                {
                    ImportBatchId = firstRow.ImportBatchId,
                    CompanyId = firstRow.CompanyId,
                    c_zLanguage = (short)0,
                    c_debug = (string?)null
                },
                transaction,
                commandType: CommandType.StoredProcedure,
                commandTimeout: 120,
                cancellationToken: cancellationToken);

            return connection.ExecuteAsync(command);
        }

        private static object ToDbValue(string? value)
        {
            return string.IsNullOrWhiteSpace(value) ? System.DBNull.Value : value;
        }
    }
}
