namespace WebApp.Models.Integration
{
    public class IntegrationSourceError
    {
        public IntegrationSource Source { get; set; }
        public int? StatusCode { get; set; }
        public string Message { get; set; } = string.Empty;
    }
}
