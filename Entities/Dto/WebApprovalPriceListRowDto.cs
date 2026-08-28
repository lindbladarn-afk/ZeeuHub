namespace Entities.Dto;

public class WebApprovalPriceListRowDto
{
    public Guid Id { get; set; }

    // prl.PrisLista
    public int PriceListId { get; set; }

    // prl.ArtNr
    public string ArticleNumber { get; set; }

    // ar.ArtBeskr
    public string ArticleDescription { get; set; }

    // prl.AltEnhetKod
    public string? UnitOfMeasure { get; set; }

    // prl.LimitLowAnt
    public decimal LowLimit { get; set; }

    //prl.ForetagKod
    public int ForetagKod { get; set; }

    // prl.vb_pris
    [DisplayFormat(DataFormatString = "{0:N2}")] // Will format like 1,000.00
    public decimal? Price { get; set; }

    // prl.vb_PrisNytt
    [DisplayFormat(DataFormatString = "{0:N2}")] // Will format like 1,000.00
    public decimal? NewPrice { get; set; }

    //prl.vb_PrisNyttDatum
    [DisplayFormat(DataFormatString = "{0:d}")] // Short date format based on location
    public DateTime? NewPriceDate { get; set; }

    // prl.Proc1
    public float? Discount { get; set; }

    // prl.rabatt1
    public decimal? Discount1 { get; set; }

	// prl.rabatt2
	public decimal? Discount2 { get; set; }

	// prl.rabatt3
	public decimal? Discount3 { get; set; }

    public bool IsApproved { get; set; }
    public bool IsRejected { get; set; }

    /// <summary>
    /// If the price list row is active or not
    /// If true, the row is active
    /// If false, the row has been handled and is not active
    /// </summary>
    public bool IsActive { get; set; }
    public string? Message { get; set; }

    [Column(name: "ApprovedBy")]
    public string? ApprovedBy { get; set; }

    [Column(name: "ApprovedDate")]
    [DisplayFormat(DataFormatString = "{0:d}")] // Short date format based on location
    public DateTime? ApprovedDate { get; set; }

    public int? ApprovalStatus { get; set; }

    public string AttestantPersSign { get; set; }
}
