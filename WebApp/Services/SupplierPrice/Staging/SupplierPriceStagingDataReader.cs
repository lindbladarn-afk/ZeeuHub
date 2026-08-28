using System.Data;
using WebApp.Models.SupplierPrice;

namespace WebApp.Services.SupplierPrice;

// Exposes normalized supplier price rows to SqlBulkCopy with stable staging column mappings.
public sealed class SupplierPriceStagingDataReader : IDataReader
{
    public static readonly IReadOnlyList<(string Name, Type Type, Func<PortalSupplierPriceStagingRow, object> Value)> Columns =
        new List<(string, Type, Func<PortalSupplierPriceStagingRow, object>)>
        {
            ("ImportBatchId", typeof(Guid), row => row.ImportBatchId),
            ("RowNo", typeof(int), row => row.RowNo),
            ("Supplier", typeof(string), row => ToDbValue(row.Supplier)),
            ("SupplierArticleNo", typeof(string), row => ToDbValue(row.SupplierArticleNo)),
            ("CustomerArticleNo", typeof(string), row => ToDbValue(row.CustomerArticleNo)),
            ("Description", typeof(string), row => ToDbValue(row.Description)),
            ("CurrencyCode", typeof(string), row => ToDbValue(row.CurrencyCode)),
            ("ListPrice", typeof(decimal), row => ToDbValue(row.ListPrice)),
            ("NetPrice", typeof(decimal), row => ToDbValue(row.NetPrice)),
            ("DiscountPercent", typeof(decimal), row => ToDbValue(row.DiscountPercent)),
            ("Uom", typeof(string), row => ToDbValue(row.Uom)),
            ("MinimumOrderQuantity", typeof(decimal), row => ToDbValue(row.MinimumOrderQuantity)),
            ("PackageQuantity", typeof(decimal), row => ToDbValue(row.PackageQuantity)),
            ("WeightKg", typeof(decimal), row => ToDbValue(row.WeightKg)),
            ("CountryOfOrigin", typeof(string), row => ToDbValue(row.CountryOfOrigin)),
            ("TariffCode", typeof(string), row => ToDbValue(row.TariffCode)),
            ("ValidFrom", typeof(DateTime), row => ToDbValue(row.ValidFrom)),
            ("ValidTo", typeof(DateTime), row => ToDbValue(row.ValidTo)),
            ("Category1", typeof(string), row => ToDbValue(row.Category1)),
            ("Category2", typeof(string), row => ToDbValue(row.Category2)),
            ("Category3", typeof(string), row => ToDbValue(row.Category3)),
            ("Category4", typeof(string), row => ToDbValue(row.Category4)),
            ("Category5", typeof(string), row => ToDbValue(row.Category5)),
            ("SourceFileName", typeof(string), row => ToDbValue(row.SourceFileName)),
            ("SourceSheetName", typeof(string), row => ToDbValue(row.SourceSheetName)),
            ("SourceRowNo", typeof(int), row => ToDbValue(row.SourceRowNo)),
            ("RawJson", typeof(string), row => ToDbValue(row.RawJson)),
            ("ImportedAt", typeof(DateTime), row => row.ImportedAt),
            ("ImportedBy", typeof(string), row => ToDbValue(row.ImportedBy)),
            ("CompanyId", typeof(Guid), row => row.CompanyId.HasValue ? row.CompanyId.Value : DBNull.Value),
            ("ForetagKod", typeof(int), row => row.ForetagKod.HasValue ? row.ForetagKod.Value : DBNull.Value),
            ("UserId", typeof(string), row => ToDbValue(row.UserId))
        };

    private readonly IEnumerator<PortalSupplierPriceStagingRow> _rows;

    public SupplierPriceStagingDataReader(IEnumerable<PortalSupplierPriceStagingRow> rows)
    {
        _rows = rows.GetEnumerator();
    }

    public int FieldCount => Columns.Count;
    public int RecordsRead { get; private set; }
    public bool Read()
    {
        if (!_rows.MoveNext())
            return false;

        RecordsRead++;
        return true;
    }
    public string GetName(int i) => Columns[i].Name;
    public Type GetFieldType(int i) => Columns[i].Type;
    public object GetValue(int i) => Columns[i].Value(_rows.Current);
    public int GetOrdinal(string name)
    {
        for (var i = 0; i < Columns.Count; i++)
        {
            if (string.Equals(Columns[i].Name, name, StringComparison.OrdinalIgnoreCase))
                return i;
        }

        return -1;
    }

    public void Dispose() => _rows.Dispose();
    public string GetDataTypeName(int i) => GetFieldType(i).Name;
    public int GetValues(object[] values)
    {
        var count = Math.Min(values.Length, FieldCount);
        for (var i = 0; i < count; i++)
        {
            values[i] = GetValue(i);
        }

        return count;
    }

    public bool IsDBNull(int i) => GetValue(i) == DBNull.Value;
    public object this[int i] => GetValue(i);
    public object this[string name] => GetValue(GetOrdinal(name));
    public int Depth => 0;
    public bool IsClosed => false;
    public int RecordsAffected => -1;
    public void Close() { }
    public DataTable? GetSchemaTable() => null;
    public bool NextResult() => false;
    public bool GetBoolean(int i) => (bool)GetValue(i);
    public byte GetByte(int i) => (byte)GetValue(i);
    public long GetBytes(int i, long fieldOffset, byte[]? buffer, int bufferoffset, int length) => throw new NotSupportedException();
    public char GetChar(int i) => (char)GetValue(i);
    public long GetChars(int i, long fieldoffset, char[]? buffer, int bufferoffset, int length) => throw new NotSupportedException();
    public IDataReader GetData(int i) => throw new NotSupportedException();
    public DateTime GetDateTime(int i) => (DateTime)GetValue(i);
    public decimal GetDecimal(int i) => (decimal)GetValue(i);
    public double GetDouble(int i) => (double)GetValue(i);
    public float GetFloat(int i) => (float)GetValue(i);
    public Guid GetGuid(int i) => (Guid)GetValue(i);
    public short GetInt16(int i) => (short)GetValue(i);
    public int GetInt32(int i) => (int)GetValue(i);
    public long GetInt64(int i) => (long)GetValue(i);
    public string GetString(int i) => (string)GetValue(i);

    private static object ToDbValue(string? value) =>
        string.IsNullOrWhiteSpace(value) ? DBNull.Value : value.Trim();

    private static object ToDbValue(decimal? value) =>
        value.HasValue ? value.Value : DBNull.Value;

    private static object ToDbValue(DateTime? value) =>
        value.HasValue ? value.Value.Date : DBNull.Value;

    private static object ToDbValue(int? value) =>
        value.HasValue ? value.Value : DBNull.Value;
}
