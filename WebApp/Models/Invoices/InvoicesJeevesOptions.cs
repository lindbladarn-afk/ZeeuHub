using System;
using System.Collections.Generic;

namespace WebApp.Models.Invoices
{
    /// <summary>
    /// Per-company Jeeves-koppling. Mappa portalns CompanyId till Jeeves bolagskod.
    /// </summary>
    public class InvoicesJeevesOptions
    {
        /// <summary>
        /// Exakt nyckel: CompanyId (Guid) som string -> JeevesCompanyCode (t.ex. "001").
        /// </summary>
        public Dictionary<string, string> CompanyCodeMap { get; set; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    }
}
