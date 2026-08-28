namespace Entities.Models;

public class PriceList
{
	public int PriceListId { get; set; }
	public string PriceListDescription { get; set; }
	public int CompanyCode { get; set; }
	public DateTime? ValidFrom { get; set; }
	public DateTime? ValidTo { get; set; }
	public string Currency { get; set; }
}
