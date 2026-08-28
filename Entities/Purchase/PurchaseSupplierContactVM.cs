namespace Entities.Purchase;

public class PurchaseSupplierContactVM : IPurchaseSupplierContactVM
{
    [Column(name: "FtgPerson")]
    public string? ContactName { get; set; }

    [Column(name: "ComNr")]
    public string ContactNumber { get; set; }

    [Column(name: "ComBeskr")]
    public string? ContactNumberDescription { get; set; }

    [Column(name: "FtgNr")]
    public string SupplierNumber { get; set; }
}
