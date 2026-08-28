namespace WebApp.Models.AI
{
    /// <summary>
    /// Visningsinformation om vald datakälla (för UI)
    /// </summary>
    public sealed class AiDataSourceInfo
    {
        public string Key { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;

        public string? Server { get; set; }
        public string? Database { get; set; }

        /// <summary>
        /// True = använder tenantens Jeeves-connection
        /// </summary>
        public bool IsTenantConnection { get; set; }

        /// <summary>
        /// True om någon connection string finns (tenant eller extern)
        /// </summary>
        public bool HasConnectionString { get; set; }

        public string DataProfile { get; set; } = AiDataProfile.JeevesDirect;
    }
}
