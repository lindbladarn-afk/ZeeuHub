namespace Entities.ViewModels.WebApproval;

public class WebApprovalSaleOrderRowVM
{
    [Column(name: "OrdRadNr")]
    public int OrderRowNumber { get; set; }

    [Column(name: "ArtNr")]
    public string ArticleNumber { get; set; }

    [Column(name: "ArtBeskr")]
    public string ArticleDescription { get; set; }

    [Column(name: "OrdAntal")]
    [DisplayFormat(DataFormatString = "{0:N2}")] // Will format like 1,000.00
    public decimal Quantity { get; set; }

    
    [Column(name: "Vb_Pris")]
    [DisplayFormat(DataFormatString = "{0:N2}")] // Will format like 1,000.00
    public decimal Price { get; set; }

    [Column(name: "OrdRadRab")]
    [DisplayFormat(DataFormatString = "{0:N2}")] // Will format like 1,000.00
    public decimal Discount { get; set; }
    
    /// <summary>
    /// Will get the value from orp.VbOrdRadSumNetto
    /// </summary>
    [Column(name: "RowSum")]
    [DisplayFormat(DataFormatString = "{0:N2}")]  // Will format like 1,000.00
    public decimal RowSum { get; set; }
    

    [Column(name: "OrdBerLevDat")]
    [DisplayFormat(DataFormatString = "{0:d}")] // Short date format based on location
    public DateTime DeliveryDate { get; set; }
}
