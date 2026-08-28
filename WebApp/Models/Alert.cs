namespace WebApp.Models
{
    public static class Alert
    {
        /// <summary>
        /// Success
        /// </summary>
        public const string SUCCESS = "success";

        /// <summary>
        /// Attention
        /// </summary>
        public const string ATTENTION = "attention";

        /// <summary>
        /// Error
        /// </summary>
        public const string DANGER = "danger";

        /// <summary>
        /// Userfriendly error message.
        /// </summary>
        public const string USERFRIENDLYERRORMESSAGE = "error";

        /// <summary>
        /// Information
        /// </summary>
        public const string INFORMATION = "info";

        /// <summary>
        /// View all.
        /// </summary>
        public static string[] ALL
        {
            get { return new[] { SUCCESS, ATTENTION, INFORMATION, USERFRIENDLYERRORMESSAGE, DANGER }; }
        }
    }
}
