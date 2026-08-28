// Persists a validated dashboard selection per user and company in the portal identity database.
using Entities.Application;
using Microsoft.EntityFrameworkCore;
using WebApp.Data;
using WebApp.Models.Dashboard;

namespace WebApp.Services.Dashboard;

public sealed class DashboardWidgetLayoutService : IDashboardWidgetLayoutService
{
    private const int MaximumVisibleWidgets = 8;
    private readonly IDbContextFactory<ApplicationDbContext> _dbContextFactory;
    private readonly TimeProvider _timeProvider;

    public DashboardWidgetLayoutService(
        IDbContextFactory<ApplicationDbContext> dbContextFactory,
        TimeProvider? timeProvider = null)
    {
        _dbContextFactory = dbContextFactory;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<IReadOnlyList<DashboardWidgetLayout>> GetLayoutAsync(
        UserSession? user,
        IReadOnlyList<DashboardWidgetLayout> defaultLayout,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetScope(user, out var userId, out var companyId))
        {
            return defaultLayout;
        }

        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var stored = await db.DashboardWidgetPreferences!
            .AsNoTracking()
            .Where(item => item.UserId == userId && item.CompanyId == companyId)
            .OrderBy(item => item.SortOrder)
            .ThenBy(item => item.WidgetId)
            .ToListAsync(cancellationToken);

        return stored.Count == 0
            ? defaultLayout
            : stored.Select(ToLayout).ToList();
    }

    public async Task SaveAsync(
        UserSession user,
        IReadOnlyList<DashboardWidgetLayout> widgets,
        IReadOnlyCollection<DashboardCardDefinition> allowedCards,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetScope(user, out var userId, out var companyId))
        {
            throw new InvalidOperationException("En giltig användare och ett aktivt bolag krävs för att spara startsidan.");
        }

        var definitionsById = allowedCards
            .Where(card => !string.IsNullOrWhiteSpace(card.Id))
            .ToDictionary(card => card.Id.Trim(), StringComparer.Ordinal);
        var normalizedWidgets = Normalize(widgets, definitionsById);

        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var existing = await db.DashboardWidgetPreferences!
            .Where(item => item.UserId == userId && item.CompanyId == companyId)
            .ToDictionaryAsync(item => item.WidgetId, StringComparer.Ordinal, cancellationToken);
        var updatedAtUtc = _timeProvider.GetUtcNow().UtcDateTime;

        foreach (var (widgetId, definition) in definitionsById)
        {
            var layout = normalizedWidgets.TryGetValue(widgetId, out var selected)
                ? selected
                : new DashboardWidgetLayout
                {
                    WidgetId = widgetId,
                    SortOrder = int.MaxValue,
                    Size = GetSafeDefaultSize(definition),
                    IsVisible = false
                };

            if (!existing.TryGetValue(widgetId, out var preference))
            {
                preference = new DashboardWidgetPreferenceRecord
                {
                    UserId = userId,
                    CompanyId = companyId,
                    WidgetId = widgetId
                };
                db.DashboardWidgetPreferences!.Add(preference);
            }

            preference.SortOrder = layout.SortOrder;
            preference.Size = layout.Size.ToString();
            preference.IsVisible = layout.IsVisible;
            preference.UpdatedAtUtc = updatedAtUtc;
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task ResetAsync(UserSession user, CancellationToken cancellationToken = default)
    {
        if (!TryGetScope(user, out var userId, out var companyId))
        {
            throw new InvalidOperationException("En giltig användare och ett aktivt bolag krävs för att återställa startsidan.");
        }

        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var preferences = await db.DashboardWidgetPreferences!
            .Where(item => item.UserId == userId && item.CompanyId == companyId)
            .ToListAsync(cancellationToken);

        if (preferences.Count == 0)
        {
            return;
        }

        db.DashboardWidgetPreferences!.RemoveRange(preferences);
        await db.SaveChangesAsync(cancellationToken);
    }

    private static Dictionary<string, DashboardWidgetLayout> Normalize(
        IReadOnlyList<DashboardWidgetLayout> widgets,
        IReadOnlyDictionary<string, DashboardCardDefinition> definitionsById)
    {
        if (widgets.Count > MaximumVisibleWidgets)
        {
            throw new ArgumentException($"Du kan som mest visa {MaximumVisibleWidgets} block på startsidan.");
        }

        var normalized = new Dictionary<string, DashboardWidgetLayout>(StringComparer.Ordinal);
        foreach (var widget in widgets)
        {
            var widgetId = widget.WidgetId?.Trim();
            if (string.IsNullOrWhiteSpace(widgetId)
                || !definitionsById.TryGetValue(widgetId, out var definition))
            {
                throw new ArgumentException("Ett ogiltigt block skickades för startsidan.");
            }

            if (!Enum.IsDefined(widget.Size) || !definition.SupportedSizes.Contains(widget.Size))
            {
                throw new ArgumentException("Den valda storleken stöds inte av blocket.");
            }

            if (!normalized.TryAdd(widgetId, new DashboardWidgetLayout
                {
                    WidgetId = widgetId,
                    SortOrder = Math.Clamp(widget.SortOrder, 0, MaximumVisibleWidgets * 10),
                    Size = widget.Size,
                    IsVisible = true
                }))
            {
                throw new ArgumentException("Samma block kan bara visas en gång på startsidan.");
            }
        }

        return normalized;
    }

    private static DashboardWidgetSize GetSafeDefaultSize(DashboardCardDefinition definition)
    {
        if (definition.SupportedSizes.Count == 0)
        {
            throw new InvalidOperationException($"Dashboardkortet '{definition.Id}' saknar tillåtna storlekar.");
        }

        return definition.SupportedSizes.Contains(definition.DefaultSize)
            ? definition.DefaultSize
            : definition.SupportedSizes[0];
    }

    private static DashboardWidgetLayout ToLayout(DashboardWidgetPreferenceRecord preference)
        => new()
        {
            WidgetId = preference.WidgetId,
            SortOrder = preference.SortOrder,
            Size = Enum.TryParse<DashboardWidgetSize>(preference.Size, ignoreCase: true, out var size)
                ? size
                : DashboardWidgetSize.Compact,
            IsVisible = preference.IsVisible
        };

    private static bool TryGetScope(UserSession? user, out string userId, out Guid companyId)
    {
        userId = user?.UserId?.Trim() ?? string.Empty;
        companyId = user?.CompanyId ?? Guid.Empty;
        return !string.IsNullOrWhiteSpace(userId) && companyId != Guid.Empty;
    }
}
