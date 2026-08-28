using WebApp.Models.SupplierPrice;
using WebApp.Services.ExcelImport;
using WebApp.Services.SupplierPrice;

namespace WebApp.Tests;

// Verifies bounded supplier-price limits and lazy staging-row enumeration.
public sealed class SupplierPriceStagingDataReaderTests
{
    [Fact]
    public void Reader_Enumerates_Staging_Rows_Lazily()
    {
        var enumeratedRows = 0;

        IEnumerable<PortalSupplierPriceStagingRow> Rows()
        {
            enumeratedRows++;
            yield return new PortalSupplierPriceStagingRow { Supplier = "Supplier 1" };
            enumeratedRows++;
            yield return new PortalSupplierPriceStagingRow { Supplier = "Supplier 2" };
        }

        using var reader = new SupplierPriceStagingDataReader(Rows());

        Assert.Equal(0, enumeratedRows);
        Assert.True(reader.Read());
        Assert.Equal(1, enumeratedRows);
        Assert.Equal(1, reader.RecordsRead);
        Assert.True(reader.Read());
        Assert.Equal(2, enumeratedRows);
        Assert.Equal(2, reader.RecordsRead);
        Assert.False(reader.Read());
    }

    [Fact]
    public void Supplier_Price_Limit_Supports_One_Hundred_Thousand_Rows()
    {
        Assert.Equal(100_000, ExcelImportResourceLimits.MaxSupplierPriceRows);
    }
}
