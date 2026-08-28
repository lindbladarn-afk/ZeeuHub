using WebApp.Models.DocumentSigning;

namespace WebApp.Repositories.DocumentSigning;

public interface IDocumentSigningRepository
{
    Task AddAsync(
        DocumentSigningRecord signing,
        IReadOnlyCollection<DocumentSigningParticipantRecord>? participants = null,
        CancellationToken cancellationToken = default);

    Task UpdateAsync(
        DocumentSigningRecord signing,
        IReadOnlyCollection<DocumentSigningParticipantRecord>? participants = null,
        CancellationToken cancellationToken = default);

    Task<DocumentSigningRecord?> GetByIdAsync(Guid companyId, Guid signingId, CancellationToken cancellationToken = default);
    Task<DocumentSigningRecord?> GetByIdWithParticipantsAsync(Guid companyId, Guid signingId, CancellationToken cancellationToken = default);
    Task<DocumentSigningRecord?> GetByDocumentIdAsync(Guid companyId, string documentId, CancellationToken cancellationToken = default);
    Task<DocumentSigningRecord?> GetByCorrelationKeyAsync(Guid companyId, string correlationKey, CancellationToken cancellationToken = default);
    Task<DocumentSigningRecord?> GetByPublicTokenAsync(string publicToken, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DocumentSigningRecord>> ListActiveAsync(int take = 100, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DocumentSigningRecord>> ListByOrderAsync(Guid companyId, int? jeevesCompanyCode, long orderNo, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DocumentSigningRecord>> ListRecentAsync(Guid companyId, int? jeevesCompanyCode, int take = 20, CancellationToken cancellationToken = default);
}
