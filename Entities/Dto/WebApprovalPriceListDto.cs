namespace Entities.Dto;

public class WebApprovalPriceListDto
{
    public int PriceListId { get; set; }
    public string PriceListDescription { get; set; }
    public int CompanyCode { get; set; }

    [DisplayFormat(DataFormatString = "{0:d}")] // Short date format based on location
    public DateTime? ValidFrom { get; set; }

    [DisplayFormat(DataFormatString = "{0:d}")] // Short date format based on location
    public DateTime? ValidTo { get; set; }
    public string Currency { get; set; }
    public bool? IsActive { get; set; }


    public List<WebApprovalPriceListRowDto> Rows { get; set; }
}
