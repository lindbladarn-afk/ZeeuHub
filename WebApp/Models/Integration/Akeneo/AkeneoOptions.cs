namespace WebApp.Models.Integration
{
    public class AkeneoOptions
    {
        public string? BaseUrl { get; set; }
        public string? ClientId { get; set; }
        public string? ClientSecret { get; set; }
        public string? Username { get; set; }
        public string? Password { get; set; }
        public int PageSize { get; set; } = 100;
        public bool Enabled { get; set; } = true;
    }
}
