namespace Entities.Purchase;

public class PurchaseArticleVM : IPurchaseArticleVM
{
    [Column(name: "ArtNr")]
    public string ArticleNumber { get; set; }

    [Column(name: "ArtBeskr")]
    public string ArticleDescription { get; set; }

    [Column(name: "EnhetsKod")]
    public string Unit { get; set; }

    // Is this the one?
    [Column(name: "VaruGruppKod")]
    public int ProductGroupCode { get; set; }

    [Column(name: "q_zu_default_acccount")]
    public string DefaultAccount { get; set; }

    [Column(name: "q_zu_default_costcenter")]
    public string DefaultCostCenter { get; set; }

    [Column(name: "q_zu_expence_item")]
    public bool ExpenceArticle { get; set; }



    // Kontrollera foreatgkod när de hämtas
}
