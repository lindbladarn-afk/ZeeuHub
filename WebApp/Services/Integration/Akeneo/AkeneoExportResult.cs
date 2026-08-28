namespace WebApp.Services.Integration.Akeneo
{
    public class AkeneoExportResult
    {
        public string Scope { get; set; } = "all";
        public string FileName { get; set; } = "PIM2Jvs.xml";
        public string Xml { get; set; } = string.Empty;
        public int Count { get; set; }
        public IReadOnlyList<string> RequestedSkus { get; set; } = Array.Empty<string>();
        public IReadOnlyList<string> NotFoundSkus { get; set; } = Array.Empty<string>();
    }
}
