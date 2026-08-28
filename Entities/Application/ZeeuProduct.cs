namespace Entities.Application;

public class ZeeuProduct : IZeeuProduct
{
	public Guid Id { get; set; }
	public string? Name { get; set; }
	public string? Description { get; set; }
	public byte[]? Image { get; set; }
	public decimal Price { get; set; }
	public string? Link { get; set; }
	public string? InternalLink { get; set; }

	public List<ICompanyLicense>? Licenses { get; set; }
}
