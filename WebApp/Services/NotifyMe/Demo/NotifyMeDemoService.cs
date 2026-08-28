using WebApp.ViewModels.NotifyMe;

namespace WebApp.Services.NotifyMe;

// Keeps demo library/statistics separate from the real NotifyMe SQL integration.
public sealed class NotifyMeDemoService : INotifyMeDemoService
{
    private readonly IReadOnlyList<NotifyMeTemplateVm> _templates = BuildTemplates();

    public Task<NotifyMeTemplateLibraryVm> GetTemplateLibraryAsync(
        int? companyCode,
        string? search = null,
        string? category = null,
        CancellationToken cancellationToken = default)
    {
        var filtered = _templates.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            filtered = filtered.Where(x =>
                x.Title.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                x.Category.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                x.Summary.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                x.BusinessValue.Contains(search, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(category))
            filtered = filtered.Where(x => string.Equals(x.Category, category, StringComparison.OrdinalIgnoreCase));

        var categoryOptions = _templates
            .Select(x => x.Category)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x)
            .Select(x => new NotifyMeLookupOptionVm { Value = x, Label = x })
            .ToArray();

        var result = filtered.ToArray();

        return Task.FromResult(new NotifyMeTemplateLibraryVm
        {
            CompanyCode = companyCode,
            Search = search,
            Category = category,
            TotalTemplates = result.Length,
            CategoryOptions = categoryOptions,
            Templates = result
        });
    }

    public Task<NotifyMeStatisticsVm> GetStatisticsAsync(int? companyCode, CancellationToken cancellationToken = default)
    {
        var trend = new[]
        {
            new NotifyMeStatsPointVm { Label = "okt", RunCount = 62, HitCount = 11 },
            new NotifyMeStatsPointVm { Label = "nov", RunCount = 64, HitCount = 15 },
            new NotifyMeStatsPointVm { Label = "dec", RunCount = 70, HitCount = 19 },
            new NotifyMeStatsPointVm { Label = "jan", RunCount = 78, HitCount = 24 },
            new NotifyMeStatsPointVm { Label = "feb", RunCount = 82, HitCount = 29 },
            new NotifyMeStatsPointVm { Label = "mar", RunCount = 91, HitCount = 33 }
        };

        var maxHits = Math.Max(1, trend.Max(x => x.HitCount));
        foreach (var point in trend)
            point.HeightPercent = Math.Max(14, (int)Math.Round(point.HitCount / (double)maxHits * 100d));

        var rows = new[]
        {
            new NotifyMeNotificationStatsRowVm { NotificationId = 141, Description = "Order som saknar checklista", Category = "Order", RunCount = 30, HitCount = 17, HitRatePercent = 56.7m, QualityLabel = "Bra signal", QualityTone = "success" },
            new NotifyMeNotificationStatsRowVm { NotificationId = 188, Description = "Låg lagernivå toppartiklar", Category = "Lager", RunCount = 30, HitCount = 8, HitRatePercent = 26.7m, QualityLabel = "Bra signal", QualityTone = "success" },
            new NotifyMeNotificationStatsRowVm { NotificationId = 204, Description = "Kunder utan aktivitet 30 dagar", Category = "Kund", RunCount = 30, HitCount = 2, HitRatePercent = 6.7m, QualityLabel = "Behöver trimmas", QualityTone = "warning" },
            new NotifyMeNotificationStatsRowVm { NotificationId = 219, Description = "Prislista väntar godkännande", Category = "Pris", RunCount = 30, HitCount = 27, HitRatePercent = 90.0m, QualityLabel = "För bullrig", QualityTone = "danger" },
            new NotifyMeNotificationStatsRowVm { NotificationId = 231, Description = "Förfallna kundfakturor > 50 tkr", Category = "Ekonomi", RunCount = 30, HitCount = 12, HitRatePercent = 40.0m, QualityLabel = "Bra signal", QualityTone = "success" }
        };

        var totalRuns = rows.Sum(x => x.RunCount);
        var totalHits = rows.Sum(x => x.HitCount);
        var hitRate = totalRuns == 0 ? 0m : Math.Round(totalHits * 100m / totalRuns, 1);

        var insights = new[]
        {
            new NotifyMeStatsInsightVm
            {
                Title = "Starkast affärsvärde just nu",
                Description = "Order- och fakturanotifieringar står för störst del av träffarna. Det är där demo-värdet syns tydligast för kunden.",
                Tone = "info"
            },
            new NotifyMeStatsInsightVm
            {
                Title = "Bullrig notifiering upptäckt",
                Description = "Prislistenotifieringen träffar i 90% av körningarna. Det är en bra kandidat att skärpa med fler villkor innan kund går live.",
                Tone = "warning"
            },
            new NotifyMeStatsInsightVm
            {
                Title = "Uppskattad tidsbesparing",
                Description = "Med nuvarande träffbild motsvarar demo-upplägget cirka 47 sparade timmar per månad i manuella kontroller och uppföljningar.",
                Tone = "success"
            }
        };

        return Task.FromResult(new NotifyMeStatisticsVm
        {
            CompanyCode = companyCode,
            PeriodLabel = "Senaste 6 månaderna (demo)",
            TotalRuns = totalRuns,
            TotalHits = totalHits,
            HitRatePercent = hitRate,
            EstimatedHoursSaved = 47m,
            EstimatedValueProtectedSek = 186000m,
            Trend = trend,
            NotificationRows = rows,
            Insights = insights
        });
    }

    public NotifyMeTemplateVm? GetTemplate(string? templateKey)
    {
        if (string.IsNullOrWhiteSpace(templateKey))
            return null;

        return _templates.FirstOrDefault(x => string.Equals(x.Key, templateKey, StringComparison.OrdinalIgnoreCase));
    }

    private static IReadOnlyList<NotifyMeTemplateVm> BuildTemplates()
    {
        return new[]
        {
            new NotifyMeTemplateVm
            {
                Key = "overdue-invoices-high-value",
                Title = "Förfallna kundfakturor över 50 tkr",
                Category = "Ekonomi",
                Summary = "Identifierar större kundfakturor som passerat förfallodatum och bör följas upp samma dag.",
                BusinessValue = "Driver snabbare cash collection och minskar risken att stora belopp glider vidare utan åtgärd.",
                ExampleFrequency = "Dagligen kl. 07:00",
                SuggestedPriority = "Åtgärda snarast",
                ComplexityLabel = "Låg",
                ParameterHints = new[] { "Beloppsgräns", "Dagar efter förfallodatum", "Kundkategori" },
                Draft = new NotifyMeDraftVm
                {
                    Description = "Förfallna kundfakturor över 50 tkr",
                    WarningText = "NotifyMe: Förfallen kundfaktura över gränsvärde",
                    Comment = "Kontakta kund och verifiera betalstatus innan kl. 12 samma dag.",
                    TypeCode = "20",
                    PriorityCode = "20",
                    SchemaCode = "10",
                    ScheduleCode = "10",
                    StartDate = DateTime.Today,
                    SqlPreview = "SELECT kundnummer, fakturanr, belopp, forfallodatum\nFROM kundfakturor\nWHERE belopp >= 50000 AND forfallodatum < GETDATE()"
                }
            },
            new NotifyMeTemplateVm
            {
                Key = "order-missing-checklist",
                Title = "Order som saknar checklista",
                Category = "Order",
                Summary = "Fångar ordrar som kommit för långt i flödet utan att obligatorisk checklista har markerats klar.",
                BusinessValue = "Minskar sena stopp och kvalitetsmissar innan leverans eller fakturering.",
                ExampleFrequency = "Dag och natt",
                SuggestedPriority = "Information",
                ComplexityLabel = "Låg",
                ParameterHints = new[] { "Orderstatus", "Åldersgräns i dagar", "Ansvarig grupp" },
                Draft = new NotifyMeDraftVm
                {
                    Description = "Order som saknar checklista",
                    WarningText = "NotifyMe: Order saknar checklista",
                    Comment = "Kontrollera att checklistan fyllts i innan ordern går vidare till nästa steg.",
                    TypeCode = "10",
                    PriorityCode = "10",
                    SchemaCode = "30",
                    ScheduleCode = "10",
                    StartDate = DateTime.Today,
                    SqlPreview = "SELECT ordernr, kund, status\nFROM orderhuvud\nWHERE checklista_klar = 0 AND status IN ('Plock', 'Pack')"
                }
            },
            new NotifyMeTemplateVm
            {
                Key = "inventory-low-top-sellers",
                Title = "Låg lagernivå på toppsäljare",
                Category = "Lager",
                Summary = "Visar artiklar med låg täckning där försäljningen fortfarande är stark och påfyllnad behöver prioriteras.",
                BusinessValue = "Minskar risken för att högmarginalartiklar går slut och skyddar försäljning.",
                ExampleFrequency = "Varje timma",
                SuggestedPriority = "Åtgärda snarast",
                ComplexityLabel = "Medel",
                ParameterHints = new[] { "Dagar av täckning", "Produktgrupp", "Minsta omsättning" },
                Draft = new NotifyMeDraftVm
                {
                    Description = "Låg lagernivå på toppsäljare",
                    WarningText = "NotifyMe: Toppartikel når kritisk lagernivå",
                    Comment = "Verifiera påfyllnad eller lägg om beställning innan dagens slut.",
                    TypeCode = "30",
                    PriorityCode = "20",
                    SchemaCode = "40",
                    ScheduleCode = "10",
                    StartDate = DateTime.Today,
                    SqlPreview = "SELECT artikel, disponibelt_lager, saljtempo\nFROM lagersaldo\nWHERE disponibelt_lager < min_niva AND senaste_30_dagar > 25"
                }
            },
            new NotifyMeTemplateVm
            {
                Key = "customer-inactive-30d",
                Title = "Kunder utan aktivitet 30 dagar",
                Category = "Kund",
                Summary = "Fångar kunder som historiskt varit aktiva men inte haft order, offert eller kontakt senaste 30 dagarna.",
                BusinessValue = "Bra för proaktiv försäljningsuppföljning och account management.",
                ExampleFrequency = "Veckovis",
                SuggestedPriority = "Information",
                ComplexityLabel = "Medel",
                ParameterHints = new[] { "Antal dagar utan aktivitet", "Kundsegment", "Ansvarig säljare" },
                Draft = new NotifyMeDraftVm
                {
                    Description = "Kunder utan aktivitet 30 dagar",
                    WarningText = "NotifyMe: Aktiv kund utan aktivitet",
                    Comment = "Planera uppföljning med kundansvarig och säkra nästa steg.",
                    TypeCode = "40",
                    PriorityCode = "10",
                    SchemaCode = "10",
                    ScheduleCode = "20",
                    StartDate = DateTime.Today,
                    SqlPreview = "SELECT kundnummer, kundnamn, senaste_orderdatum\nFROM kundaktivitet\nWHERE DATEDIFF(day, senaste_orderdatum, GETDATE()) >= 30"
                }
            },
            new NotifyMeTemplateVm
            {
                Key = "purchase-awaiting-approval",
                Title = "Inköp som väntar attest för länge",
                Category = "Inköp",
                Summary = "Visar inköp som passerat rimlig väntetid i attestflödet och riskerar att fördröja leverans eller lagerpåfyllnad.",
                BusinessValue = "Lyfter fastnade approval-flöden innan de påverkar produktion eller leveransprecision.",
                ExampleFrequency = "Dagligen kl. 06:00",
                SuggestedPriority = "Åtgärda snarast",
                ComplexityLabel = "Låg",
                ParameterHints = new[] { "Attestgräns i timmar", "Inköpskategori", "Ansvarig attestant" },
                Draft = new NotifyMeDraftVm
                {
                    Description = "Inköp som väntar attest för länge",
                    WarningText = "NotifyMe: Inköp väntar attest",
                    Comment = "Kontakta attestansvarig eller flytta ärendet till backup-ansvarig.",
                    TypeCode = "50",
                    PriorityCode = "20",
                    SchemaCode = "10",
                    ScheduleCode = "10",
                    StartDate = DateTime.Today,
                    SqlPreview = "SELECT ordernr, leverantor, attestansvarig\nFROM inkopsorder\nWHERE atteststatus = 'Väntar' AND DATEDIFF(hour, skapaddatum, GETDATE()) >= 24"
                }
            },
            new NotifyMeTemplateVm
            {
                Key = "price-list-needs-approval",
                Title = "Prislista väntar godkännande",
                Category = "Pris",
                Summary = "Markerar prislistor eller prisuppdateringar som fastnat och behöver snabbt beslut för att gå live.",
                BusinessValue = "Skyddar marginal och minskar risken att gamla priser fortsätter användas för länge.",
                ExampleFrequency = "Dagligen kl. 08:00",
                SuggestedPriority = "Åtgärda snarast",
                ComplexityLabel = "Låg",
                ParameterHints = new[] { "Prislista", "Ansvarig godkännare", "Tolerans i dagar" },
                Draft = new NotifyMeDraftVm
                {
                    Description = "Prislista väntar godkännande",
                    WarningText = "NotifyMe: Prislista väntar godkännande",
                    Comment = "Verifiera att ansvarig godkännare har allt underlag och följ upp samma dag.",
                    TypeCode = "60",
                    PriorityCode = "20",
                    SchemaCode = "10",
                    ScheduleCode = "10",
                    StartDate = DateTime.Today,
                    SqlPreview = "SELECT prislista, ansvarig, status\nFROM prisuppdateringar\nWHERE status = 'Väntar på godkännande'"
                }
            }
        };
    }
}
