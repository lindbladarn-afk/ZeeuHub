using Microsoft.EntityFrameworkCore;
using WebApp.Data;
using WebApp.Models.DocumentSigning;

namespace WebApp.Repositories.DocumentSigning;

public class DocumentSigningRepository : IDocumentSigningRepository
{
    private readonly IDbContextFactory<ApplicationDbContext> _dbContextFactory;

    public DocumentSigningRepository(IDbContextFactory<ApplicationDbContext> dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
    }

    public async Task AddAsync(
        DocumentSigningRecord signing,
        IReadOnlyCollection<DocumentSigningParticipantRecord>? participants = null,
        CancellationToken cancellationToken = default)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        db.DocumentSignings!.Add(signing);
        AddParticipants(db, participants);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(
        DocumentSigningRecord signing,
        IReadOnlyCollection<DocumentSigningParticipantRecord>? participants = null,
        CancellationToken cancellationToken = default)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        db.DocumentSignings!.Update(signing);
        if (participants != null)
            await ReplaceParticipantsAsync(db, signing.Id, participants, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<DocumentSigningRecord?> GetByIdAsync(Guid companyId, Guid signingId, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await db.DocumentSignings!
            .FirstOrDefaultAsync(x => x.CompanyId == companyId && x.Id == signingId, cancellationToken);
    }

    public async Task<DocumentSigningRecord?> GetByIdWithParticipantsAsync(Guid companyId, Guid signingId, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var signing = await db.DocumentSignings!
            .FirstOrDefaultAsync(x => x.CompanyId == companyId && x.Id == signingId, cancellationToken);

        if (signing == null)
            return null;

        signing.Participants = await db.DocumentSigningParticipants!
            .Where(x => x.SigningId == signing.Id)
            .OrderBy(x => x.IsMyParticipant)
            .ThenBy(x => x.SigningOrder)
            .ThenBy(x => x.Name)
            .ToListAsync(cancellationToken);

        return signing;
    }

    public async Task<DocumentSigningRecord?> GetByDocumentIdAsync(Guid companyId, string documentId, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await db.DocumentSignings!
            .FirstOrDefaultAsync(x => x.CompanyId == companyId && x.DocumentId == documentId, cancellationToken);
    }

    public async Task<DocumentSigningRecord?> GetByCorrelationKeyAsync(Guid companyId, string correlationKey, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await db.DocumentSignings!
            .FirstOrDefaultAsync(x => x.CompanyId == companyId && x.CorrelationKey == correlationKey, cancellationToken);
    }

    public async Task<DocumentSigningRecord?> GetByPublicTokenAsync(string publicToken, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await db.DocumentSignings!
            .FirstOrDefaultAsync(x => x.PublicToken == publicToken, cancellationToken);
    }

    public async Task<IReadOnlyList<DocumentSigningRecord>> ListActiveAsync(int take = 100, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await db.DocumentSignings!
            .Where(x =>
                x.PortalStatus == "sent" ||
                x.PortalStatus == "waitinginternal" ||
                x.PortalStatus == "preparing")
            .OrderByDescending(x => x.UpdatedAtUtc)
            .Take(Math.Max(1, take))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<DocumentSigningRecord>> ListByOrderAsync(Guid companyId, int? jeevesCompanyCode, long orderNo, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await db.DocumentSignings!
            .Where(x => x.CompanyId == companyId && x.OrderNo == orderNo && x.JeevesCompanyCode == jeevesCompanyCode)
            .OrderByDescending(x => x.CreatedAtUtc)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<DocumentSigningRecord>> ListRecentAsync(Guid companyId, int? jeevesCompanyCode, int take = 20, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await db.DocumentSignings!
            .Where(x => x.CompanyId == companyId && x.JeevesCompanyCode == jeevesCompanyCode)
            .OrderByDescending(x => x.CreatedAtUtc)
            .Take(Math.Max(1, take))
            .ToListAsync(cancellationToken);
    }

    private static void AddParticipants(ApplicationDbContext db, IReadOnlyCollection<DocumentSigningParticipantRecord>? participants)
    {
        if (participants == null || participants.Count == 0)
            return;

        db.DocumentSigningParticipants!.AddRange(participants);
    }

    private static async Task ReplaceParticipantsAsync(
        ApplicationDbContext db,
        Guid signingId,
        IReadOnlyCollection<DocumentSigningParticipantRecord> participants,
        CancellationToken cancellationToken)
    {
        var participantSet = db.DocumentSigningParticipants
            ?? throw new InvalidOperationException("DocumentSigningParticipants DbSet is not configured.");

        var existing = await participantSet
            .Where(x => x.SigningId == signingId)
            .ToListAsync(cancellationToken);

        if (existing.Count > 0)
            participantSet.RemoveRange(existing);

        if (participants.Count > 0)
            participantSet.AddRange(participants);
    }
}
