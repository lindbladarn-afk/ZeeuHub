// Defines how AI data sources are discovered, selected, and resolved for the active session.
using WebApp.Models.AI;
using WebApp.Services;

namespace WebApp.Services.Application.AI
{
    public interface IAiDataSourceResolver
    {
        IReadOnlyList<AiDataSourceInfo> GetConfiguredDataSources();

        Task<(string ConnectionString, AiDataSourceInfo Info)> ResolveAsync(string? requestedKey = null, CancellationToken ct = default);

        /// <summary>
        /// Sätter vald datakälla i session (för dropdownen).
        /// </summary>
        void SetSelected(string key);

        /// <summary>
        /// Hämtar vald datakälla från session (om satt).
        /// </summary>
        string? GetSelected();
    }
}
