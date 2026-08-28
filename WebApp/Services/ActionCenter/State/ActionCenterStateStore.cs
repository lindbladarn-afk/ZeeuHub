using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using WebApp.Data;
using WebApp.Models.ActionCenter;

namespace WebApp.Services.ActionCenter;

/// <summary>
/// Persisterar status/dismiss av insikter per användare/bolag.
/// Fail-safe: fångar DB-fel och no-op:ar så UI inte kraschar om tabellen saknas.
/// </summary>
public sealed class ActionCenterStateStore : IActionCenterStateStore
{
    private readonly IDbContextFactory<ApplicationDbContext> _dbContextFactory;
    private readonly ILogger<ActionCenterStateStore> _logger;

    public ActionCenterStateStore(IDbContextFactory<ApplicationDbContext> dbContextFactory, ILogger<ActionCenterStateStore> logger)
    {
        _dbContextFactory = dbContextFactory;
        _logger = logger;
    }

    public async Task<IReadOnlyList<ActionCenterItemState>> GetStatesAsync(Guid? companyId, string userId, CancellationToken cancellationToken)
    {
        try
        {
            await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

            var q = db.ActionCenterItemStates!.AsNoTracking()
                .Where(x => x.UserId == userId);

            if (companyId != null)
            {
                q = q.Where(x => x.CompanyId == companyId);
            }

            return await q.ToListAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "ActionCenterStateStore: failed to read states (table missing or DB error).");
            return Array.Empty<ActionCenterItemState>();
        }
    }

    public async Task UpsertAsync(string externalId, ActionCenterItemStatus status, Guid? companyId, string userId, ActionCenterUpdateRequest snapshot, CancellationToken cancellationToken)
    {
        try
        {
            await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

            var existing = await db.ActionCenterItemStates!
                .FirstOrDefaultAsync(x => x.ExternalId == externalId && x.UserId == userId && x.CompanyId == companyId, cancellationToken);

            if (existing == null)
            {
                existing = new ActionCenterItemState
                {
                    ExternalId = externalId,
                    CompanyId = companyId,
                    UserId = userId
                };
                db.ActionCenterItemStates!.Add(existing);
            }

            existing.Status = status;
            existing.UpdatedAtUtc = DateTime.UtcNow;
            existing.Comment = Trim(snapshot.Comment ?? existing.Comment, 256);
            existing.Title = Trim(snapshot.Title ?? existing.Title, 256);
            existing.Description = Trim(snapshot.Description ?? existing.Description, 512);
            existing.Category = Trim(snapshot.Category ?? existing.Category, 64);
            existing.Priority = snapshot.Priority ?? existing.Priority;
            existing.DetectedAtUtc = snapshot.DetectedAt ?? existing.DetectedAtUtc;
            if (status == ActionCenterItemStatus.Completed)
            {
                existing.CompletedAtUtc = DateTime.UtcNow;
            }
            else if (status == ActionCenterItemStatus.Active)
            {
                existing.CompletedAtUtc = null; // reopen clears completion timestamp
            }

            await db.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "ActionCenterStateStore: failed to upsert state for {ExternalId}", externalId);
        }
    }

    public async Task<IReadOnlyList<ActionCenterItemState>> GetHistoryAsync(Guid? companyId, string userId, int take, CancellationToken cancellationToken)
    {
        try
        {
            await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

            var q = db.ActionCenterItemStates!.AsNoTracking()
                .Where(x => x.UserId == userId)
                .Where(x => x.Status == ActionCenterItemStatus.Completed || x.Status == ActionCenterItemStatus.Dismissed)
                .OrderByDescending(x => x.CompletedAtUtc ?? x.UpdatedAtUtc)
                .Take(take);

            if (companyId != null)
            {
                q = q.Where(x => x.CompanyId == companyId);
            }

            return await q.ToListAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "ActionCenterStateStore: failed to read history");
            return Array.Empty<ActionCenterItemState>();
        }
    }

    private static string? Trim(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var trimmed = value.Trim();
        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
    }
}
