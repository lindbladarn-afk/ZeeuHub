namespace Entities.ViewModels.WebApproval;

public class WebApprovalPurchaseOrderRowVM
{
    [Column(name:"BestRadNr")]
    public int OrderRowNumber { get; set; }

    [Column(name:"ArtNr")]
    public string ArticleNumber { get; set; }

    [Column(name:"ArtBeskr")]
    public string ArticleDescription { get; set; }

    [Column(name:"Vb_Inpris")]
    [DisplayFormat(DataFormatString = "{0:N2}")] // Will format like 1,000.00
    public decimal Price { get; set; }

    [Column(name: "BestAntExtQty")]
    [DisplayFormat(DataFormatString = "{0:N2}")] // Will format like 1,000.00
    public decimal Quantity { get; set; }

    [Column(name: "BestBerLevDat")]
    [DisplayFormat(DataFormatString = "{0:d}")] // Short date format based on location
    public DateTime DeliveryDate { get; set; }
}
