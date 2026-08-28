namespace WebApp.Models.Integration
{
    public class AkeneoProduct
    {
        public string? Identifier { get; set; }
        public bool? Enabled { get; set; }
        public string? Family { get; set; }
        public string? Updated { get; set; }
        public string? Name { get; set; }
        public string? ArtNr { get; set; }
        public string? ArtBeskr { get; set; }
        public string? ArtBeskrSpec { get; set; }
        public string? ArtKat { get; set; }
        public string? ArtNrEan { get; set; }
        public string? ArtRitnNr { get; set; }
        public string? VaruGruppKod { get; set; }
        public string? ShopifySync { get; set; }
        public string? Directive { get; set; }
        public string? MainImage { get; set; }
        public string? WebBeskr { get; set; }
        public string? DescriptionLong { get; set; }
        public string? title { get; set; }
        public string? vendor { get; set; }
        public string? handle { get; set; }
        public string? status { get; set; }
        public string? productType { get; set; }
        public string? descriptionHtml { get; set; }
        public List<string> images { get; set; } = new();
        public Dictionary<string, string> Attributes { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    }
}
