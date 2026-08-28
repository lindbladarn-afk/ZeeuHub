// Builds document signing metrics and recent signing activity for the active company.
using WebApp.Models.Dashboard;
using WebApp.Models.DocumentSigning;
using WebApp.Services.Dashboard.Demo;
using WebApp.Services.DocumentSigning;

namespace WebApp.Services.Dashboard;

public sealed class DocumentSigningDashboardCardProvider : IDashboardCardProvider
{
    private readonly IDocumentSigningService _documentSigningService;
    private readonly IDashboardDemoDataService _demoData;
    private readonly DashboardCardViewModelFactory _cards;
    private readonly ILogger<DocumentSigningDashboardCardProvider> _logger;

    public DocumentSigningDashboardCardProvider(
        IDocumentSigningService documentSigningService,
        IDashboardDemoDataService demoData,
        DashboardCardViewModelFactory cards,
        ILogger<DocumentSigningDashboardCardProvider> logger)
    {
        _documentSigningService = documentSigningService;
        _demoData = demoData;
        _cards = cards;
        _logger = logger;
    }

    public IReadOnlyCollection<string> CardIds => [DashboardCardIds.DocumentSigning];

    public async Task<DashboardCardViewModel?> BuildAsync(
        DashboardCardDefinition definition,
        DashboardCardBuildContext context,
        CancellationToken cancellationToken)
    {
        if (context.UseDemoData)
        {
            return _cards.Create(definition, _demoData.BuildDocumentSigning());
        }

        var configured = context.RuntimeContext is not null
            && _documentSigningService.IsEnabledForCompany(context.RuntimeContext.CompanyId);
        if (!configured || context.RuntimeContext is null)
        {
            var unavailable = new DocumentSigningCardViewModel
            {
                IsConfigured = false,
                StatusMessage = "Dokumentsignering är inte konfigurerat för valt bolag ännu."
            };
            return _cards.Create(
                definition,
                unavailable,
                DashboardCardState.Empty,
                "Dokumentsignering är inte aktiverat",
                unavailable.StatusMessage);
        }

        List<DocumentSigningListItem> signings;
        try
        {
            signings = (await _documentSigningService.ListRecentAsync(
                context.RuntimeContext.CompanyId,
                context.RuntimeContext.CompanyCode,
                take: 20,
                cancellationToken)).ToList();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Failed to build document signing dashboard card for company {CompanyName} ({CompanyCode}).",
                context.RuntimeContext.CompanyName,
                context.RuntimeContext.CompanyCode);
            return _cards.Create(
                definition,
                new DocumentSigningCardViewModel { IsConfigured = true },
                DashboardCardState.Error,
                "Dokumentsignering kunde inte laddas",
                "Försök igen för att hämta aktuella signeringsärenden.");
        }

        var data = new DocumentSigningCardViewModel
        {
            IsConfigured = true,
            TotalSignings = signings.Count,
            ActiveCount = signings.Count(signing => !IsClosed(signing.PortalStatus)),
            SignedCount = signings.Count(signing => IsSigned(signing.PortalStatus)),
            NeedsAttentionCount = signings.Count(signing => NeedsAttention(signing.PortalStatus)),
            RecentSignings = signings.Take(5).ToList()
        };

        return signings.Count == 0
            ? _cards.Create(
                definition,
                data,
                DashboardCardState.Empty,
                "Inga signeringsärenden",
                "Det finns inga aktiva eller nyligen uppdaterade signeringar.")
            : _cards.Create(definition, data);
    }

    private static bool IsSigned(string? status)
        => string.Equals(status, "signed", StringComparison.OrdinalIgnoreCase)
            || string.Equals(status, "completed", StringComparison.OrdinalIgnoreCase);

    private static bool NeedsAttention(string? status)
        => string.Equals(status, "rejected", StringComparison.OrdinalIgnoreCase)
            || string.Equals(status, "cancelled", StringComparison.OrdinalIgnoreCase)
            || string.Equals(status, "timedout", StringComparison.OrdinalIgnoreCase)
            || string.Equals(status, "failed", StringComparison.OrdinalIgnoreCase);

    private static bool IsClosed(string? status)
        => IsSigned(status)
            || string.Equals(status, "cancelled", StringComparison.OrdinalIgnoreCase)
            || string.Equals(status, "rejected", StringComparison.OrdinalIgnoreCase);
}
