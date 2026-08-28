using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using WebApp.Models.AI;
using WebApp.Models.Invoices;
using WebApp.Services.Application;
using WebApp.Services.Invoices;
using WebApp.ViewModels.Invoices;

namespace WebApp.Services.Application.AI;

// Reuses the invoice service for open-invoice questions so AI does not guess from schema alone.
public sealed class AiInvoiceQuestionService : IAiInvoiceQuestionService
{
    private static readonly CultureInfo AmountCulture = CultureInfo.GetCultureInfo("sv-SE");
    private readonly IInvoicesService _invoicesService;

    public AiInvoiceQuestionService(IInvoicesService invoicesService)
    {
        _invoicesService = invoicesService;
    }

    public async Task<AiQueryResponse?> TryAnswerAsync(
        string question,
        string connectionString,
        int? companyCode,
        CancellationToken ct = default)
    {
        var classifiedIntent = ClassifyInvoiceListIntent(question);
        if (classifiedIntent is null)
            return null;
        var intent = classifiedIntent.Value;

        var activeTab = intent == InvoiceListIntent.Paid ? "paid" : "unpaid";
        var model = await _invoicesService.GetInvoiceListAsync(
            connectionString,
            new GetInvoicesQuery
            {
                CompanyCode = companyCode,
                ActiveTab = activeTab,
                Page = 1,
                PageSize = 50,
                FromDate = new DateTime(1990, 1, 1),
                ToDate = DateTime.Today,
                UsesDefaultPeriod = false
            });

        var invoices = intent == InvoiceListIntent.Paid
            ? (model.PaidInvoices ?? Array.Empty<InvoiceItem>()).ToList()
            : (model.UnpaidInvoices ?? Array.Empty<InvoiceItem>()).ToList();
        if (intent == InvoiceListIntent.Overdue)
            invoices = invoices.Where(invoice => invoice.IsOverdue).ToList();

        var totalCount = intent switch
        {
            InvoiceListIntent.Paid => model.PaidCount,
            InvoiceListIntent.Overdue => model.OverdueCount,
            _ => model.UnpaidCount
        };
        if (totalCount <= 0)
            totalCount = invoices.Count;

        var totalAmount = intent switch
        {
            InvoiceListIntent.Paid => model.TotalPaidSek,
            InvoiceListIntent.Overdue => invoices.Sum(invoice => invoice.AmountSek),
            _ => model.TotalUnpaidSek
        };
        if (totalAmount == 0m && invoices.Count > 0)
            totalAmount = invoices.Sum(invoice => invoice.AmountSek);

        if (invoices.Count == 0)
        {
            return new AiQueryResponse
            {
                Success = true,
                Answer = $"Jag hittade inga {GetInvoiceStateLabel(intent, plural: true)} fakturor i fakturalistan för aktivt bolag.",
                Warning = model.UsesHistoricalFactSource
                    ? "Den valda datakällan visar historisk fakturering och inte säkra öppna fakturor."
                    : null
            };
        }

        var rows = invoices.Select(x => new List<object?>
        {
            x.InvoiceNo,
            x.AmountSek,
            x.Customer,
            x.SalesPerson,
            x.DueDate.ToString("yyyy-MM-dd"),
            x.Status
        }).ToList();

        var stateLabel = GetInvoiceStateLabel(intent, plural: totalCount != 1);
        var invoiceNoun = totalCount == 1 ? "faktura" : "fakturor";
        var totalAmountText = FormatAmountSek(totalAmount);
        var overdueCount = invoices.Count(x => x.IsOverdue);
        var answer = $"Det finns {totalCount} {stateLabel} {invoiceNoun} med totalt {totalAmountText}.";
        if (intent is InvoiceListIntent.Unpaid or InvoiceListIntent.Generic && overdueCount > 0)
            answer += $" {overdueCount} av dem är förfallna.";

        return new AiQueryResponse
        {
            Success = true,
            Answer = answer,
            Columns = new List<string>
            {
                "Faktura",
                "Belopp",
                "Kund",
                "Säljare",
                "Förfallodatum",
                "Status"
            },
            Rows = rows,
            RowCount = rows.Count,
            Truncated = totalCount > rows.Count,
            Warning = BuildWarning(intent, model, rows.Count, totalCount)
        };
    }

    private static string FormatAmountSek(decimal value)
    {
        return string.Format(AmountCulture, "{0:N2} kr", value);
    }

    private static InvoiceListIntent? ClassifyInvoiceListIntent(string question)
    {
        if (string.IsNullOrWhiteSpace(question))
            return null;

        var q = question.ToLowerInvariant();
        var hasInvoice = Regex.IsMatch(q, @"(?is)\b(faktur(a|or)?|invoice(s)?)\b");
        if (!hasInvoice)
            return null;

        var asksForAnalysis = Regex.IsMatch(
            q,
            @"(?is)\b(diagram|graf|trend|utveckling|omsättning|summa|totalt|hur\s+många|antal|genomsnitt|snitt|jämför|per\s+(dag|vecka|månad|kvartal|år|kund|säljare)|störst|minst)\b");
        if (asksForAnalysis)
            return null;

        if (Regex.IsMatch(q, @"(?is)\b(förfallen|förfallna|overdue)\b"))
            return InvoiceListIntent.Overdue;

        if (Regex.IsMatch(q, @"(?is)\b(betald|betalda|paid)\b"))
            return InvoiceListIntent.Paid;

        if (Regex.IsMatch(q, @"(?is)\b(öppen|öppna|open|obetald|obetalda|unpaid)\b"))
            return InvoiceListIntent.Unpaid;

        var asksForList = Regex.IsMatch(q, @"(?is)\b(visa|lista|vilka|mina|våra|alla|senaste)\b");
        return asksForList ? InvoiceListIntent.Generic : null;
    }

    private static string GetInvoiceStateLabel(InvoiceListIntent intent, bool plural) =>
        (intent, plural) switch
        {
            (InvoiceListIntent.Paid, false) => "betald",
            (InvoiceListIntent.Overdue, false) => "förfallen",
            (InvoiceListIntent.Generic or InvoiceListIntent.Unpaid, false) => "öppen",
            (InvoiceListIntent.Paid, true) => "betalda",
            (InvoiceListIntent.Overdue, true) => "förfallna",
            _ => "öppna"
        };

    private static string? BuildWarning(
        InvoiceListIntent intent,
        InvoiceListViewModel model,
        int displayedCount,
        int totalCount)
    {
        if (model.UsesHistoricalFactSource)
            return "Den valda datakällan visar historisk fakturering och inte säkra öppna fakturor.";

        if (totalCount > displayedCount)
            return $"Visar {displayedCount} av {totalCount} {GetInvoiceStateLabel(intent, plural: true)} fakturor.";

        return intent == InvoiceListIntent.Generic
            ? "Jag tolkade “fakturor” som öppna fakturor för aktivt bolag."
            : null;
    }

    private enum InvoiceListIntent
    {
        Generic,
        Unpaid,
        Overdue,
        Paid
    }
}
