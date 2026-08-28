using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Entities.Application;
using WebApp.Models.ActionCenter;
using WebApp.Services.Application;

namespace WebApp.Services.ActionCenter;

/// <summary>
/// Demo/mock-insikter för att visa Action Center-kraft.
/// </summary>
public sealed class MockInsightProvider : IInsightProvider
{
    public string ProviderKey => "customer-mock";
    public ActionCenterAudience Audience => ActionCenterAudience.Customer;

    public Task<IEnumerable<ActionCenterInsight>> GetInsightsAsync(UserSession user, JeevesRuntimeContext? runtimeContext, CancellationToken cancellationToken)
    {
        // Stabil bas så "new" inte flimrar.
        var detectedBase = DateTime.Now.Date.AddHours(9);

        var insights = new List<ActionCenterInsight>
        {
            new()
            {
                Key = "margin-drop",
                Audience = ActionCenterAudience.Customer,
                Category = "Marginal",
                Status = ActionCenterStatus.Open,
                Title = "Bruttomarginal avviker på topp-säljare",
                Description = "En produkt visar ovanligt låg bruttomarginal jämfört med senaste 30 dagarna. Kontrollera pris, rabatt eller inköpskostnad. (Mockup)",
                Priority = ActionCenterPriority.Medium,
                DetectedAt = detectedBase.AddMinutes(12),
                DueAt = detectedBase.AddDays(1),
                IsMock = true,
                LinkText = "Öppna order",
                LinkUrl = "/Orders/Index"
            },
            new()
            {
                Key = "stockout-risk",
                Audience = ActionCenterAudience.Customer,
                Category = "Lager",
                Status = ActionCenterStatus.Open,
                Title = "Risk för lagerbrist inom 7 dagar",
                Description = "Efterfrågan överstiger normal takt för en artikel i pågående ordrar. Föreslå inköp/omplanering. (Mockup)",
                Priority = ActionCenterPriority.High,
                DetectedAt = detectedBase.AddMinutes(22),
                DueAt = detectedBase.AddDays(1),
                IsMock = true,
                LinkText = "Gå till inköp",
                LinkUrl = "/Purchase/PurchaseOrders"
            },
            new()
            {
                Key = "customer-anomaly",
                Audience = ActionCenterAudience.Customer,
                Category = "Kund",
                Status = ActionCenterStatus.Open,
                Title = "Kundmönster: ovanligt stor order",
                Description = "En kund har lagt en order som avviker kraftigt från sin normala orderstorlek. Bra läge för uppföljning. (Mockup)",
                Priority = ActionCenterPriority.Low,
                DetectedAt = detectedBase.AddMinutes(35),
                IsMock = true,
                LinkText = "Visa ordrar",
                LinkUrl = "/Orders/Index"
            },
            new()
            {
                Key = "vendor-delay",
                Audience = ActionCenterAudience.Customer,
                Category = "Inköp",
                Status = ActionCenterStatus.Open,
                Title = "Leverantören har skjutit upp leveransen 2 veckor",
                Description = "En beställd order har fått ny leveranstid (+14 dagar). Meddela drabbade kunder och planera om leveranser. (Mockup)",
                Priority = ActionCenterPriority.High,
                DetectedAt = detectedBase.AddMinutes(45),
                DueAt = detectedBase.AddDays(1),
                IsMock = true,
                LinkText = "Visa order",
                LinkUrl = "/Orders/Index"
            },
            new()
            {
                Key = "catalog-update",
                Audience = ActionCenterAudience.Customer,
                Category = "Info",
                Status = ActionCenterStatus.Open,
                Title = "Ny produktkatalog uppladdad",
                Description = "En ny prislista/produktkatalog finns tillgänglig. Säkerställ att säljteamet använder den senaste versionen. (Mockup)",
                Priority = ActionCenterPriority.Info,
                DetectedAt = detectedBase.AddMinutes(55),
                IsMock = true,
                LinkText = "Visa katalog",
                LinkUrl = "/Files/Catalog"
            },
            new()
            {
                Key = "overdue-maintenance",
                Audience = ActionCenterAudience.Customer,
                Category = "Drift",
                Status = ActionCenterStatus.Open,
                Title = "Låg prioritet: Planerat underhåll väntar",
                Description = "Ett icke-kritiskt underhållsfönster är försenat. Planera in när det passar utan att störa kunder. (Mockup)",
                Priority = ActionCenterPriority.Info,
                DetectedAt = detectedBase.AddMinutes(65),
                IsMock = true,
                LinkText = "Öppna planering",
                LinkUrl = "/Maintenance/Index"
            }
        };

        return Task.FromResult<IEnumerable<ActionCenterInsight>>(insights);
    }
}
