using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using WebApp.Models.Invoices;
using WebApp.Services.Application.AI;
using WebApp.Services.Invoices;
using WebApp.ViewModels.Invoices;

namespace WebApp.Tests;

// Verifies that invoice-related AI questions reuse the invoice module instead of schema guesses.
public sealed class AiInvoiceQuestionServiceTests
{
    [Fact]
    public async Task TryAnswerAsync_ReturnsOpenInvoicesFromInvoiceModule()
    {
        var invoices = new List<InvoiceItem>
        {
            new()
            {
                InvoiceNo = "#100036",
                Customer = "10012",
                SalesPerson = "JIS",
                DueDate = DateTime.Today.AddDays(-2),
                AmountSek = 1545m,
                RemainingAmount = 1545m,
                Status = "Förfallen",
                IsPaid = false
            },
            new()
            {
                InvoiceNo = "#100037",
                Customer = "10034",
                SalesPerson = "AAA",
                DueDate = DateTime.Today.AddDays(10),
                AmountSek = 2500m,
                RemainingAmount = 2500m,
                Status = "Öppen",
                IsPaid = false
            }
        };

        var invoicesService = new FakeInvoicesService(new InvoiceListViewModel
        {
            UnpaidInvoices = invoices,
            TotalCount = invoices.Count,
            TotalUnpaidSek = 4045m,
            UnpaidCount = invoices.Count,
            OverdueCount = 1,
            UsesHistoricalFactSource = false
        });

        var service = new AiInvoiceQuestionService(invoicesService);
        var response = await service.TryAnswerAsync("Visa öppna fakturor", "conn-str", 1001, CancellationToken.None);

        Assert.NotNull(response);
        Assert.True(response!.Success);
        Assert.Contains("2 öppna fakturor", response.Answer);
        Assert.Contains("1 av dem är förfallna", response.Answer);
        Assert.Equal(6, response.Columns.Count);
        Assert.Equal(2, response.RowCount);
        Assert.Equal(2, response.Rows.Count);

        Assert.Equal("unpaid", invoicesService.CapturedQuery!.ActiveTab);
        Assert.Equal(1001, invoicesService.CapturedQuery.CompanyCode);
        Assert.Equal(1, invoicesService.CapturedQuery.Page);
        Assert.Equal(50, invoicesService.CapturedQuery.PageSize);
        Assert.Equal(new DateTime(1990, 1, 1), invoicesService.CapturedQuery.FromDate);
        Assert.Equal(DateTime.Today, invoicesService.CapturedQuery.ToDate);
        Assert.False(invoicesService.CapturedQuery.UsesDefaultPeriod);
    }

    [Fact]
    public async Task TryAnswerAsync_IgnoresUnrelatedQuestions()
    {
        var invoicesService = new FakeInvoicesService(new InvoiceListViewModel());
        var service = new AiInvoiceQuestionService(invoicesService);

        var response = await service.TryAnswerAsync("Visa orderstatus", "conn-str", 1001, CancellationToken.None);

        Assert.Null(response);
        Assert.Null(invoicesService.CapturedQuery);
    }

    [Fact]
    public async Task TryAnswerAsync_HandlesGenericInvoiceListWithoutAiSql()
    {
        var invoices = new List<InvoiceItem>
        {
            new()
            {
                InvoiceNo = "#100040",
                Customer = "10012",
                DueDate = DateTime.Today.AddDays(10),
                AmountSek = 900m,
                RemainingAmount = 900m,
                Status = "Obetald",
                IsPaid = false
            }
        };
        var invoicesService = new FakeInvoicesService(new InvoiceListViewModel
        {
            UnpaidInvoices = invoices,
            TotalUnpaidSek = 900m,
            UnpaidCount = 1
        });
        var service = new AiInvoiceQuestionService(invoicesService);

        var response = await service.TryAnswerAsync("Visa mina fakturor", "conn-str", 1001, CancellationToken.None);

        Assert.NotNull(response);
        Assert.True(response!.Success);
        Assert.Contains("1 öppen faktura", response.Answer);
        Assert.Contains("tolkade", response.Warning, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("unpaid", invoicesService.CapturedQuery!.ActiveTab);
        Assert.Equal(1001, invoicesService.CapturedQuery.CompanyCode);
    }

    [Fact]
    public async Task TryAnswerAsync_LeavesInvoiceChartsForAnalyticalFlow()
    {
        var invoicesService = new FakeInvoicesService(new InvoiceListViewModel());
        var service = new AiInvoiceQuestionService(invoicesService);

        var response = await service.TryAnswerAsync(
            "Visa våra fakturor i ett diagram",
            "conn-str",
            1001,
            CancellationToken.None);

        Assert.Null(response);
        Assert.Null(invoicesService.CapturedQuery);
    }

    private sealed class FakeInvoicesService : IInvoicesService
    {
        private readonly InvoiceListViewModel _response;

        public FakeInvoicesService(InvoiceListViewModel response)
        {
            _response = response;
        }

        public GetInvoicesQuery? CapturedQuery { get; private set; }

        public Task<InvoiceListViewModel> GetInvoiceListAsync(string connectionString, GetInvoicesQuery query)
        {
            CapturedQuery = query;
            return Task.FromResult(_response);
        }

        public Task<InvoiceItem?> GetInvoiceAsync(string connectionString, int? companyCode, string invoiceNo)
            => Task.FromResult<InvoiceItem?>(null);

        public Task<InvoiceListViewModel> GetDashboardSummaryAsync(string connectionString, int? companyCode)
            => Task.FromResult(new InvoiceListViewModel());
    }
}
