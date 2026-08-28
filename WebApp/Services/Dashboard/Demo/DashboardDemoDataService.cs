// Supplies realistic, isolated demo payloads for dashboard cards without querying company data.
using Entities.Application;
using Microsoft.Extensions.Options;
using WebApp.Models.ActionCenter;
using WebApp.Models.Dashboard;
using WebApp.Models.DocumentSigning;
using WebApp.Models.Orders;
using WebApp.ViewModels.NotifyMe;

namespace WebApp.Services.Dashboard.Demo;

public sealed class DashboardDemoDataService : IDashboardDemoDataService
{
    private readonly DashboardDemoOptions _options;

    public DashboardDemoDataService(IOptions<DashboardDemoOptions> options)
    {
        _options = options.Value;
    }

    public bool ShouldUseDemoData(UserSession? user)
    {
        if (!_options.Enabled || user is null)
        {
            return false;
        }

        var companyName = user.CompanyName?.Trim();
        return !string.IsNullOrWhiteSpace(companyName)
            && string.Equals(companyName, _options.AllowedCompanyName.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    public NotifyMeOverviewVm BuildNotifyMeOverview()
    {
        var recentEntries = new[]
        {
            new NotifyMeLogItemVm
            {
                LogId = 90121,
                NotificationId = 204,
                NotificationDescription = "Förfallna kundfakturor över 50 tkr",
                SentAt = DateTime.Today.AddHours(7).AddMinutes(12),
                Subject = "3 förfallna fakturor kräver uppföljning",
                ExecutionStatus = "Skickad",
                ExecutionStatusTone = "success",
                Recipients = "ekonomi@zeeu.se",
                SchemaCode = "10",
                HtmlPreviewText = "Tre större kundfakturor passerade förfallodatum under natten."
            },
            new NotifyMeLogItemVm
            {
                LogId = 90122,
                NotificationId = 188,
                NotificationDescription = "Låg lagernivå toppsäljare",
                SentAt = DateTime.Today.AddHours(6).AddMinutes(40),
                Subject = "2 artiklar når kritisk nivå inom 48 timmar",
                ExecutionStatus = "Skickad",
                ExecutionStatusTone = "success",
                Recipients = "lager@zeeu.se",
                SchemaCode = "40",
                HtmlPreviewText = "Påfyllnad behöver prioriteras innan dagens slut."
            },
            new NotifyMeLogItemVm
            {
                LogId = 90123,
                NotificationId = 231,
                NotificationDescription = "Inköp som väntar attest för länge",
                SentAt = DateTime.Today.AddHours(6).AddMinutes(5),
                Subject = "4 inköp har fastnat i attestflödet",
                ExecutionStatus = "Delvis skickad",
                ExecutionStatusTone = "warning",
                Recipients = "inkop@zeeu.se",
                SchemaCode = "30",
                HtmlPreviewText = "Tre attestanter behöver följa upp inköp innan leverans påverkas."
            },
            new NotifyMeLogItemVm
            {
                LogId = 90124,
                NotificationId = 252,
                NotificationDescription = "Order med leveransavvikelse",
                SentAt = DateTime.Today.AddHours(5).AddMinutes(50),
                Subject = "1 order kräver manuell kontroll",
                ExecutionStatus = "Skickad",
                ExecutionStatusTone = "success",
                Recipients = "order@zeeu.se",
                SchemaCode = "20",
                HtmlPreviewText = "En större kundorder avviker från planerad leveransvecka."
            },
            new NotifyMeLogItemVm
            {
                LogId = 90125,
                NotificationId = 273,
                NotificationDescription = "Dokumentsignering väntar intern signatur",
                SentAt = DateTime.Today.AddHours(5).AddMinutes(15),
                Subject = "2 signeringar väntar slutligt godkännande",
                ExecutionStatus = "Skickad",
                ExecutionStatusTone = "success",
                Recipients = "juridik@zeeu.se",
                SchemaCode = "50",
                HtmlPreviewText = "Signerade av motparten men väntar fortfarande på intern signatur."
            }
        };

        var notifications = new[]
        {
            new NotifyMeListItemVm
            {
                NotificationId = 204,
                Description = "Förfallna kundfakturor över 50 tkr",
                WarningText = "Betalning har passerat förfallodatum och behöver följas upp.",
                TypeLabel = "Ekonomi",
                PriorityLabel = "Hög",
                ScheduleLabel = "Dagligen kl. 07:00",
                HasAutomation = true,
                AutomationHint = "Skickas till ekonomi och säljledning",
                IsActive = true,
                NextExecutionAt = DateTime.Today.AddDays(1).AddHours(7),
                NextExecutionDisplay = "Imorgon 07:00",
                LastWarningAt = DateTime.Today.AddDays(-1).AddHours(7),
                WarningCount = 17,
                EscalateAfterCount = 2,
                IsDueNow = true,
                LatestExecutionStatus = "Skickad",
                LatestExecutionStatusTone = "success",
                LatestExecutionAt = DateTime.Today.AddHours(7).AddMinutes(12),
                LatestExecutionSummary = "3 fakturor träffade reglerna"
            },
            new NotifyMeListItemVm
            {
                NotificationId = 188,
                Description = "Låg lagernivå toppsäljare",
                WarningText = "Artiklar med hög omsättning har nått kritisk täckning.",
                TypeLabel = "Lager",
                PriorityLabel = "Hög",
                ScheduleLabel = "Varje timme",
                HasAutomation = true,
                AutomationHint = "Skickas till inköp och lager",
                IsActive = true,
                NextExecutionAt = DateTime.Today.AddHours(1),
                NextExecutionDisplay = "Om 1 timme",
                LastWarningAt = DateTime.Today.AddHours(-1),
                WarningCount = 8,
                EscalateAfterCount = 1,
                IsDueNow = false,
                LatestExecutionStatus = "Skickad",
                LatestExecutionStatusTone = "success",
                LatestExecutionAt = DateTime.Today.AddHours(6).AddMinutes(40),
                LatestExecutionSummary = "2 artiklar kräver påfyllnad"
            },
            new NotifyMeListItemVm
            {
                NotificationId = 231,
                Description = "Inköp som väntar attest för länge",
                WarningText = "Fastnade approval-flöden riskerar att fördröja leveranser.",
                TypeLabel = "Inköp",
                PriorityLabel = "Medel",
                ScheduleLabel = "Dagligen kl. 06:00",
                HasAutomation = true,
                AutomationHint = "Backup-attestant aktiveras efter 48 timmar",
                IsActive = true,
                NextExecutionAt = DateTime.Today.AddDays(1).AddHours(6),
                NextExecutionDisplay = "Imorgon 06:00",
                LastWarningAt = DateTime.Today.AddHours(-3),
                WarningCount = 5,
                EscalateAfterCount = 1,
                IsDueNow = true,
                LatestExecutionStatus = "Delvis skickad",
                LatestExecutionStatusTone = "warning",
                LatestExecutionAt = DateTime.Today.AddHours(6).AddMinutes(5),
                LatestExecutionSummary = "4 inköp väntar attest"
            },
            new NotifyMeListItemVm
            {
                NotificationId = 252,
                Description = "Order med leveransavvikelse",
                WarningText = "Planerad leverans ligger utanför den normala leveranshorisonten.",
                TypeLabel = "Order",
                PriorityLabel = "Medel",
                ScheduleLabel = "Veckovis",
                HasAutomation = true,
                AutomationHint = "Visas för logistik och kundansvarig",
                IsActive = true,
                NextExecutionAt = DateTime.Today.AddDays(4).AddHours(8),
                NextExecutionDisplay = "På torsdag 08:00",
                LastWarningAt = DateTime.Today.AddDays(-2).AddHours(8),
                WarningCount = 3,
                EscalateAfterCount = 2,
                IsDueNow = false,
                LatestExecutionStatus = "Skickad",
                LatestExecutionStatusTone = "success",
                LatestExecutionAt = DateTime.Today.AddHours(5).AddMinutes(50),
                LatestExecutionSummary = "1 order kräver kontroll"
            },
            new NotifyMeListItemVm
            {
                NotificationId = 273,
                Description = "Dokumentsignering väntar intern signatur",
                WarningText = "Motparten är klar men intern signatur saknas.",
                TypeLabel = "Juridik",
                PriorityLabel = "Låg",
                ScheduleLabel = "Vid varje ändring",
                HasAutomation = false,
                AutomationHint = "Manuell kontroll",
                IsActive = true,
                NextExecutionDisplay = "-",
                LastWarningAt = DateTime.Today.AddDays(-1).AddHours(11),
                WarningCount = 2,
                IsDueNow = false,
                LatestExecutionStatus = "Skickad",
                LatestExecutionStatusTone = "success",
                LatestExecutionAt = DateTime.Today.AddHours(5).AddMinutes(15),
                LatestExecutionSummary = "2 signeringar väntar"
            }
        };

        return new NotifyMeOverviewVm
        {
            IsInstalled = true,
            CompanyCode = 700,
            StatusMessage = null,
            TotalNotifications = notifications.Length,
            ActiveNotifications = 12,
            DueNowCount = 4,
            EscalationConfiguredCount = 9,
            FilteredNotificationsCount = notifications.Length,
            Notifications = notifications,
            RecentLogEntries = recentEntries,
            Pagination = new NotifyMePaginationVm
            {
                Page = 1,
                PageSize = notifications.Length,
                TotalItems = notifications.Length
            }
        };
    }

    public OrderDeliveryForecastViewModel BuildDeliveryForecast()
    {
        var today = DateTime.Today;
        var timeline = new[]
        {
            CreateBucket(today.AddMonths(0), "Maj", 6, 210000m),
            CreateBucket(today.AddMonths(1), "Jun", 8, 275000m),
            CreateBucket(today.AddMonths(2), "Jul", 11, 332000m),
            CreateBucket(today.AddMonths(3), "Aug", 7, 265000m),
            CreateBucket(today.AddMonths(4), "Sep", 5, 198000m),
            CreateBucket(today.AddMonths(5), "Okt", 5, 214000m)
        };

        var upcomingOrders = new[]
        {
            CreateOrder(450812, "Northwind Retail", today.AddDays(4), 184500m, "SEK", "Leverans vecka 23"),
            CreateOrder(450913, "Berg & Marin AB", today.AddDays(9), 228000m, "SEK", "Leverans vecka 24"),
            CreateOrder(451044, "Atlas Components", today.AddDays(13), 162500m, "SEK", "Leverans vecka 25")
        };

        return new OrderDeliveryForecastViewModel
        {
            MonthsAhead = 6,
            FutureOrderCount = 42,
            FutureAmountTotal = timeline.Sum(x => x.AmountTotal),
            EarliestDeliveryDate = today.AddDays(4),
            LatestDeliveryDate = today.AddDays(142),
            TopMonthLabel = "Jul",
            TopMonthOrderCount = 11,
            Timeline = timeline,
            UpcomingOrders = upcomingOrders,
            TotalCount = upcomingOrders.Length,
            TotalPages = 1,
            Page = 1,
            PageSize = upcomingOrders.Length
        };
    }

    public ActionCenterViewModel BuildActionCenter()
    {
        var insights = new[]
        {
            new ActionCenterInsight
            {
                Key = "orders-overdue-delivery",
                Audience = ActionCenterAudience.Customer,
                Title = "8 orderrader ligger efter leveransplan",
                Description = "Tre större kundorder och fem pågående restorder har passerat planerat leveransdatum.",
                Category = "Leverans",
                Status = ActionCenterStatus.Open,
                Priority = ActionCenterPriority.High,
                DetectedAt = DateTime.Now.AddHours(-2),
                DueAt = DateTime.Now.AddHours(18),
                AssignedTo = "Logistikteamet",
                LinkText = "Öppna leveransöversikt",
                LinkUrl = "/Orders/DeliveryForecast",
                Metrics = new[]
                {
                    new ActionCenterMetric { Label = "Försenade order", Value = "8" },
                    new ActionCenterMetric { Label = "Belopp", Value = "1,42 Mkr" }
                },
                Timeline = new[]
                {
                    new ActionCenterTimelinePoint { Label = "v.22", Count = 2, Amount = 248000m },
                    new ActionCenterTimelinePoint { Label = "v.23", Count = 3, Amount = 394000m },
                    new ActionCenterTimelinePoint { Label = "v.24", Count = 3, Amount = 782000m }
                }
            },
            new ActionCenterInsight
            {
                Key = "notifyme-due-now",
                Audience = ActionCenterAudience.Customer,
                Title = "NotifyMe: 4 notifieringar körs idag",
                Description = "Ekonomi, lager och inköp genererar flera signaler som behöver ses över.",
                Category = "NotifyMe",
                Status = ActionCenterStatus.InProgress,
                Priority = ActionCenterPriority.Medium,
                DetectedAt = DateTime.Now.AddHours(-4),
                DueAt = DateTime.Now.AddHours(6),
                AssignedTo = "Systemägare",
                LinkText = "Öppna NotifyMe",
                LinkUrl = "/NotifyMe/NotifyMe",
                Metrics = new[]
                {
                    new ActionCenterMetric { Label = "Körningar idag", Value = "14" },
                    new ActionCenterMetric { Label = "Träffar", Value = "6" }
                },
                Timeline = new[]
                {
                    new ActionCenterTimelinePoint { Label = "07", Count = 3, Amount = 0m },
                    new ActionCenterTimelinePoint { Label = "09", Count = 4, Amount = 0m },
                    new ActionCenterTimelinePoint { Label = "12", Count = 6, Amount = 0m }
                }
            },
            new ActionCenterInsight
            {
                Key = "purchase-approvals",
                Audience = ActionCenterAudience.Customer,
                Title = "6 inköp väntar attest",
                Description = "Två större beställningar ligger över attestgräns och behöver hanteras innan lunch.",
                Category = "Inköp",
                Status = ActionCenterStatus.Open,
                Priority = ActionCenterPriority.High,
                DetectedAt = DateTime.Now.AddHours(-1),
                DueAt = DateTime.Now.AddHours(3),
                AssignedTo = "Inköpschefen",
                LinkText = "Öppna inköpskö",
                LinkUrl = "/Purchase/PurchaseOrders",
                Metrics = new[]
                {
                    new ActionCenterMetric { Label = "Väntar attest", Value = "6" },
                    new ActionCenterMetric { Label = "Värde", Value = "874 tkr" }
                },
                Timeline = new[]
                {
                    new ActionCenterTimelinePoint { Label = "09", Count = 2, Amount = 152000m },
                    new ActionCenterTimelinePoint { Label = "11", Count = 2, Amount = 280000m },
                    new ActionCenterTimelinePoint { Label = "13", Count = 2, Amount = 442000m }
                }
            },
            new ActionCenterInsight
            {
                Key = "stockout-risk",
                Audience = ActionCenterAudience.Customer,
                Title = "3 artiklar når kritisk lagernivå",
                Description = "Toppsäljare i två produktgrupper behöver prioriterad påfyllnad.",
                Category = "Lager",
                Status = ActionCenterStatus.Open,
                Priority = ActionCenterPriority.High,
                DetectedAt = DateTime.Now.AddHours(-3),
                DueAt = DateTime.Now.AddHours(24),
                AssignedTo = "Lagerplanering",
                LinkText = "Öppna lagerstatus",
                LinkUrl = "/Member/Dashboard",
                Metrics = new[]
                {
                    new ActionCenterMetric { Label = "Artiklar", Value = "3" },
                    new ActionCenterMetric { Label = "Täckning", Value = "2.1 dagar" }
                },
                Timeline = new[]
                {
                    new ActionCenterTimelinePoint { Label = "v.22", Count = 1, Amount = 0m },
                    new ActionCenterTimelinePoint { Label = "v.23", Count = 2, Amount = 0m },
                    new ActionCenterTimelinePoint { Label = "v.24", Count = 3, Amount = 0m }
                }
            },
            new ActionCenterInsight
            {
                Key = "document-signing",
                Audience = ActionCenterAudience.Customer,
                Title = "2 dokument väntar intern signatur",
                Description = "Motparten har signerat, men interna signaturer saknas fortfarande.",
                Category = "Dokument",
                Status = ActionCenterStatus.InProgress,
                Priority = ActionCenterPriority.Medium,
                DetectedAt = DateTime.Now.AddHours(-5),
                DueAt = DateTime.Now.AddDays(1),
                AssignedTo = "Jurist",
                LinkText = "Öppna signeringar",
                LinkUrl = "/Integration/DocumentSigning",
                Metrics = new[]
                {
                    new ActionCenterMetric { Label = "Väntar", Value = "2" },
                    new ActionCenterMetric { Label = "Signerade", Value = "11" }
                },
                Timeline = new[]
                {
                    new ActionCenterTimelinePoint { Label = "mån", Count = 1, Amount = 0m },
                    new ActionCenterTimelinePoint { Label = "tis", Count = 1, Amount = 0m },
                    new ActionCenterTimelinePoint { Label = "ons", Count = 2, Amount = 0m }
                }
            },
            new ActionCenterInsight
            {
                Key = "vendor-delay",
                Audience = ActionCenterAudience.Customer,
                Title = "Leverantörsorder ligger sent",
                Description = "En leverantör har inte bekräftat ett större inköp inom överenskommen SLA-tid.",
                Category = "Leverantör",
                Status = ActionCenterStatus.Open,
                Priority = ActionCenterPriority.Low,
                DetectedAt = DateTime.Now.AddHours(-8),
                DueAt = DateTime.Now.AddDays(2),
                AssignedTo = "Inköp",
                LinkText = "Öppna leverantörsstatus",
                LinkUrl = "/Purchase/PurchaseOrders",
                Metrics = new[]
                {
                    new ActionCenterMetric { Label = "Order", Value = "1" },
                    new ActionCenterMetric { Label = "Försenad", Value = "14 h" }
                },
                Timeline = new[]
                {
                    new ActionCenterTimelinePoint { Label = "tors", Count = 1, Amount = 114000m },
                    new ActionCenterTimelinePoint { Label = "fre", Count = 0, Amount = 0m },
                    new ActionCenterTimelinePoint { Label = "mån", Count = 1, Amount = 114000m }
                }
            },
            new ActionCenterInsight
            {
                Key = "inventory-replenishment-window",
                Audience = ActionCenterAudience.Customer,
                Title = "Påfyllnad måste läggas inom 48 timmar",
                Description = "Två artiklar har mindre än två dagars täckning och riskerar att påverka försäljningen.",
                Category = "Lager",
                Status = ActionCenterStatus.Open,
                Priority = ActionCenterPriority.High,
                DetectedAt = DateTime.Now.AddHours(-1),
                DueAt = DateTime.Now.AddHours(20),
                AssignedTo = "Lagerplanering",
                LinkText = "Öppna påfyllnadsläge",
                LinkUrl = "/Orders/DeliveryForecast",
                Metrics = new[]
                {
                    new ActionCenterMetric { Label = "Artiklar", Value = "2" },
                    new ActionCenterMetric { Label = "Täckning", Value = "1.7 dagar" }
                },
                Timeline = new[]
                {
                    new ActionCenterTimelinePoint { Label = "v.22", Count = 1, Amount = 0m },
                    new ActionCenterTimelinePoint { Label = "v.23", Count = 2, Amount = 0m },
                    new ActionCenterTimelinePoint { Label = "v.24", Count = 2, Amount = 0m }
                }
            },
            new ActionCenterInsight
            {
                Key = "cycle-count-queue",
                Audience = ActionCenterAudience.Customer,
                Title = "Inventering väntar på bekräftelse",
                Description = "En planerad inventering står i kö och behöver prioriteras tillsammans med lagerteamet.",
                Category = "Lager",
                Status = ActionCenterStatus.InProgress,
                Priority = ActionCenterPriority.Medium,
                DetectedAt = DateTime.Now.AddHours(-6),
                DueAt = DateTime.Now.AddDays(1),
                AssignedTo = "Lagerchef",
                LinkText = "Öppna lagerstatus",
                LinkUrl = "/Member/Dashboard",
                Metrics = new[]
                {
                    new ActionCenterMetric { Label = "Planerade", Value = "1" },
                    new ActionCenterMetric { Label = "Över tid", Value = "36 h" }
                },
                Timeline = new[]
                {
                    new ActionCenterTimelinePoint { Label = "mån", Count = 0, Amount = 0m },
                    new ActionCenterTimelinePoint { Label = "tis", Count = 1, Amount = 0m },
                    new ActionCenterTimelinePoint { Label = "ons", Count = 1, Amount = 0m }
                }
            }
        };

        return new ActionCenterViewModel
        {
            Audience = ActionCenterAudience.Customer,
            TotalCount = insights.Length,
            IsDegraded = false,
            Insights = insights,
            History = new[]
            {
                new ActionCenterHistoryItem
                {
                    Key = "stockout-risk",
                    Audience = ActionCenterAudience.Customer,
                    Title = "3 artiklar når kritisk lagernivå",
                    Description = "Påminnelse skickad till lagerplanering.",
                    Category = "Lager",
                    Priority = ActionCenterPriority.High,
                    DetectedAt = DateTime.Now.AddHours(-3),
                    CompletedAt = DateTime.Now.AddHours(-1)
                },
                new ActionCenterHistoryItem
                {
                    Key = "notifyme-due-now",
                    Audience = ActionCenterAudience.Customer,
                    Title = "NotifyMe: 4 notifieringar körs idag",
                    Description = "Systemägaren följer upp dagens signaler.",
                    Category = "NotifyMe",
                    Priority = ActionCenterPriority.Medium,
                    DetectedAt = DateTime.Now.AddHours(-4),
                    CompletedAt = null
                }
            },
            ProviderFailures = Array.Empty<ActionCenterProviderFailure>()
        };
    }

    public InventoryStatusCardViewModel BuildInventoryStatus()
    {
        var signals = BuildActionCenter().Insights
            .Where(x => string.Equals(x.Category, "Lager", StringComparison.OrdinalIgnoreCase)
                || x.Key.Contains("stock", StringComparison.OrdinalIgnoreCase)
                || x.Key.Contains("vendor", StringComparison.OrdinalIgnoreCase))
            .Take(5)
            .ToList();

        return new InventoryStatusCardViewModel
        {
            TotalSignals = 9,
            HighPriorityCount = 4,
            WarningCount = 7,
            Signals = signals,
            StatusMessage = null
        };
    }

    public PurchaseAcknowledgementCardViewModel BuildPurchaseAcknowledgement()
    {
        var orders = new[]
        {
            new PurchaseAcknowledgementOrderVm
            {
                OrderNumber = 450812,
                SupplierName = "Nordic Components AB",
                OrderStatusId = 20,
                StatusLabel = "Beställd",
                DeliveryDate = DateTime.Today.AddDays(4),
                OrderValue = 184500m,
                Currency = "SEK",
                IsOverdue = false
            },
            new PurchaseAcknowledgementOrderVm
            {
                OrderNumber = 450913,
                SupplierName = "Berg & Marin AB",
                OrderStatusId = 20,
                StatusLabel = "Beställd",
                DeliveryDate = DateTime.Today.AddDays(9),
                OrderValue = 228000m,
                Currency = "SEK",
                IsOverdue = false
            },
            new PurchaseAcknowledgementOrderVm
            {
                OrderNumber = 451044,
                SupplierName = "Atlas Components",
                OrderStatusId = 40,
                StatusLabel = "Delvis levererad",
                DeliveryDate = DateTime.Today.AddDays(-1),
                OrderValue = 162500m,
                Currency = "SEK",
                IsOverdue = true
            },
            new PurchaseAcknowledgementOrderVm
            {
                OrderNumber = 451107,
                SupplierName = "Origo Packaging",
                OrderStatusId = 10,
                StatusLabel = "Godkänd",
                DeliveryDate = DateTime.Today.AddDays(6),
                OrderValue = 91000m,
                Currency = "SEK",
                IsOverdue = false
            },
            new PurchaseAcknowledgementOrderVm
            {
                OrderNumber = 451188,
                SupplierName = "Svensk IndustriLogik",
                OrderStatusId = 20,
                StatusLabel = "Beställd",
                DeliveryDate = DateTime.Today.AddDays(12),
                OrderValue = 340000m,
                Currency = "SEK",
                IsOverdue = false
            }
        };

        return new PurchaseAcknowledgementCardViewModel
        {
            TotalOrders = 24,
            AwaitingAcknowledgementCount = 6,
            OrderedCount = 15,
            OverdueCount = 3,
            RecentOrders = orders
        };
    }

    public DocumentSigningCardViewModel BuildDocumentSigning()
    {
        var signings = new[]
        {
            new DocumentSigningListItem
            {
                Id = Guid.Parse("8d3d27ec-b7f0-4d7d-8b58-3a57462b97d1"),
                OrderNo = 450812,
                DocumentTitle = "Leveransavtal Q2",
                DocumentId = "DOC-9012",
                PortalStatus = "waitinginternal",
                ProviderStatus = "pending",
                SignerName = "Anna Berg",
                SignerEmail = "anna.berg@zeeu.se",
                MainFileName = "leveransavtal-q2.pdf",
                AttachmentCount = 2,
                CreatedAtUtc = DateTime.UtcNow.AddHours(-10),
                StartedAtUtc = DateTime.UtcNow.AddHours(-9),
                LastSyncedAtUtc = DateTime.UtcNow.AddHours(-1),
                SignedAndSealed = false,
                IsTerminal = false
            },
            new DocumentSigningListItem
            {
                Id = Guid.Parse("2c2d7b2f-3c1b-40d9-8c42-0d0f17947a01"),
                OrderNo = 450913,
                DocumentTitle = "Prisjustering 2026",
                DocumentId = "DOC-9013",
                PortalStatus = "signed",
                ProviderStatus = "completed",
                SignerName = "Erik Holm",
                SignerEmail = "erik.holm@zeeu.se",
                MainFileName = "prisjustering-2026.pdf",
                AttachmentCount = 1,
                CreatedAtUtc = DateTime.UtcNow.AddDays(-1),
                StartedAtUtc = DateTime.UtcNow.AddDays(-1).AddHours(1),
                CompletedAtUtc = DateTime.UtcNow.AddHours(-6),
                LastSyncedAtUtc = DateTime.UtcNow.AddHours(-6),
                SignedAndSealed = true,
                IsTerminal = true
            },
            new DocumentSigningListItem
            {
                Id = Guid.Parse("c5d7a1dd-5a9c-4b36-8a8f-0e9c7af5a0d2"),
                OrderNo = 451044,
                DocumentTitle = "NDA med ny leverantör",
                DocumentId = "DOC-9014",
                PortalStatus = "sent",
                ProviderStatus = "draft",
                SignerName = "Sara Lind",
                SignerEmail = "sara.lind@zeeu.se",
                MainFileName = "nda-leverantor.pdf",
                AttachmentCount = 0,
                CreatedAtUtc = DateTime.UtcNow.AddHours(-20),
                StartedAtUtc = DateTime.UtcNow.AddHours(-18),
                LastSyncedAtUtc = DateTime.UtcNow.AddHours(-2),
                SignedAndSealed = false,
                IsTerminal = false
            },
            new DocumentSigningListItem
            {
                Id = Guid.Parse("6d5d3d9e-b2d4-4386-a9cb-6d0df54d1e7f"),
                OrderNo = 451107,
                DocumentTitle = "Intern attest order 450913",
                DocumentId = "DOC-9015",
                PortalStatus = "preparing",
                ProviderStatus = "draft",
                SignerName = "Johan Ek",
                SignerEmail = "johan.ek@zeeu.se",
                MainFileName = "attest-order-450913.pdf",
                AttachmentCount = 1,
                CreatedAtUtc = DateTime.UtcNow.AddHours(-6),
                StartedAtUtc = DateTime.UtcNow.AddHours(-5),
                LastSyncedAtUtc = DateTime.UtcNow.AddMinutes(-40),
                SignedAndSealed = false,
                IsTerminal = false
            },
            new DocumentSigningListItem
            {
                Id = Guid.Parse("0e2e8a43-cc84-4fd8-b9ce-0c9e77c0fb15"),
                OrderNo = 451188,
                DocumentTitle = "Ramavtal 2026",
                DocumentId = "DOC-9016",
                PortalStatus = "rejected",
                ProviderStatus = "failed",
                SignerName = "Maja Sjöberg",
                SignerEmail = "maja.sjoberg@zeeu.se",
                MainFileName = "ramavtal-2026.pdf",
                AttachmentCount = 3,
                CreatedAtUtc = DateTime.UtcNow.AddDays(-2),
                StartedAtUtc = DateTime.UtcNow.AddDays(-2).AddHours(1),
                CompletedAtUtc = DateTime.UtcNow.AddDays(-1).AddHours(2),
                LastSyncedAtUtc = DateTime.UtcNow.AddHours(-8),
                SignedAndSealed = false,
                IsTerminal = true
            }
        };

        return new DocumentSigningCardViewModel
        {
            IsConfigured = true,
            StatusMessage = null,
            TotalSignings = 14,
            ActiveCount = 7,
            SignedCount = 5,
            NeedsAttentionCount = 2,
            RecentSignings = signings
        };
    }

    private static OrderDeliveryForecastBucket CreateBucket(DateTime periodStart, string label, int orderCount, decimal amountTotal)
        => new()
        {
            PeriodStart = periodStart,
            Label = label,
            OrderCount = orderCount,
            AmountTotal = amountTotal
        };

    private static OrderHeader CreateOrder(long orderNo, string customerName, DateTime plannedDelivery, decimal amountInclVat, string currency, string description)
        => new()
        {
            OrderNo = orderNo,
            OrderNoAlfa = orderNo.ToString(),
            CustomerName = customerName,
            PlannedDelivery = plannedDelivery,
            AmountInclVat = amountInclVat,
            Currency = currency,
            Description = description,
            StatusCode = "40",
            OrderType = "Normal",
            SalesPerson = "ZeeU Demo",
            CompanyCode = "700",
            IsClosed = false
        };
}
