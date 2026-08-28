using Entities.ViewModels.Admin;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebApp.Services.Application.AI;
using WebApp.Services.Application.AI.Billing;
using WebApp.Services.Application.AI.Quota;
using WebApp.ViewModels.Admin;

namespace WebApp.Controllers;

// This file owns the AI control panel, quota administration, and billing exports.
// It groups AI-specific admin actions and helpers so they can evolve independently.
public partial class AdminController
{
    [HttpGet]
    public async Task<IActionResult> AiQueries(
        int latestPage = 1,
        int latestPageSize = 10,
        bool revenueDemo = false,
        string tab = "overview",
        int? quotaYear = null,
        int? quotaMonth = null)
    {
        latestPage = Math.Max(1, latestPage);
        latestPageSize = Math.Clamp(latestPageSize, 1, 100);
        tab = tab?.ToLowerInvariant() switch
        {
            "quota" => "quota",
            "billing" => "billing",
            _ => "overview"
        };
        var quotaPeriodStart = ResolveQuotaPeriodStartUtc(quotaYear, quotaMonth);

        var vm = await _telemetryService.GetAiQueriesAsync(latestPage: latestPage, latestPageSize: latestPageSize);
        var quota = await _aiQuotaAdminService.GetSnapshotAsync(quotaPeriodStart);
        vm.Quota = new AiQuotaAdminVm
        {
            GlobalEnabled = quota.GlobalEnabled,
            GlobalFreeTokensPerPeriod = quota.GlobalFreeTokensPerPeriod,
            GlobalWarningThresholdPercent = quota.GlobalWarningThresholdPercent,
            SurchargePercent = quota.SurchargePercent,
            TotalPaidExtraTokensCurrentPeriod = quota.TotalPaidExtraTokensCurrentPeriod,
            TotalPaidExtraBaseCostSekCurrentPeriod = quota.TotalPaidExtraBaseCostSekCurrentPeriod,
            TotalPaidExtraRevenueSekCurrentPeriod = quota.TotalPaidExtraRevenueSekCurrentPeriod,
            TotalPaidExtraBillableSekCurrentPeriod = quota.TotalPaidExtraBillableSekCurrentPeriod,
            Companies = quota.Companies.Select(x => new AiQuotaCompanyAdminVm
            {
                CompanyId = x.CompanyId,
                CompanyName = x.CompanyName,
                HasOverride = x.HasOverride,
                EnabledOverride = x.EnabledOverride,
                FreeTokensPerPeriodOverride = x.FreeTokensPerPeriodOverride,
                WarningThresholdPercentOverride = x.WarningThresholdPercentOverride,
                EffectiveEnabled = x.EffectiveEnabled,
                EffectiveFreeTokensPerPeriod = x.EffectiveFreeTokensPerPeriod,
                EffectiveWarningThresholdPercent = x.EffectiveWarningThresholdPercent,
                UsedTokensCurrentPeriod = x.UsedTokensCurrentPeriod,
                UsagePercentCurrentPeriod = x.UsagePercentCurrentPeriod,
                CurrentPeriodMode = x.CurrentPeriodMode,
                PaidUsersCount = x.PaidUsersCount,
                BlockedUsersCount = x.BlockedUsersCount,
                PaidExtraTokensCurrentPeriod = x.PaidExtraTokensCurrentPeriod,
                PaidExtraBaseCostSekCurrentPeriod = x.PaidExtraBaseCostSekCurrentPeriod,
                PaidExtraRevenueSekCurrentPeriod = x.PaidExtraRevenueSekCurrentPeriod,
                PaidExtraBillableSekCurrentPeriod = x.PaidExtraBillableSekCurrentPeriod
            }).ToList()
        };
        vm.QuotaSelectedYear = quota.PeriodYear;
        vm.QuotaSelectedMonth = quota.PeriodMonth;
        vm.QuotaSelectableYears = await BuildQuotaSelectableYearsAsync();
        vm.QuotaIsHistoricalPeriod = quota.IsHistoricalPeriod;

        vm.RevenueDemoEnabled = revenueDemo;
        vm.ActiveTab = tab;
        if (revenueDemo)
        {
            vm.Quota.Companies = ApplyRevenueDemoSimulation(vm.Quota.Companies);
            vm.Quota.Companies = ApplyRevenueDemoFloorScaling(vm.Quota.Companies);
            vm.Quota.TotalPaidExtraTokensCurrentPeriod = vm.Quota.Companies.Sum(x => x.PaidExtraTokensCurrentPeriod);
            vm.Quota.TotalPaidExtraBaseCostSekCurrentPeriod = Math.Round(vm.Quota.Companies.Sum(x => x.PaidExtraBaseCostSekCurrentPeriod), 2, MidpointRounding.AwayFromZero);
            vm.Quota.TotalPaidExtraRevenueSekCurrentPeriod = Math.Round(vm.Quota.Companies.Sum(x => x.PaidExtraRevenueSekCurrentPeriod), 2, MidpointRounding.AwayFromZero);
            vm.Quota.TotalPaidExtraBillableSekCurrentPeriod = Math.Round(vm.Quota.Companies.Sum(x => x.PaidExtraBillableSekCurrentPeriod), 2, MidpointRounding.AwayFromZero);
        }

        vm.InvoiceExport = BuildInvoiceExportVm(
            year: vm.QuotaSelectedYear,
            month: vm.QuotaSelectedMonth,
            companies: vm.Quota.Companies);

        var monthlySeries = await BuildMonthlyRevenueSeriesAsync(vm.Quota.Companies, vm.Quota.SurchargePercent, revenueDemo);
        vm.RevenueOverview = BuildRevenueOverview(vm.Quota, monthlySeries);
        return View("~/Views/Admin/Ai/AiQueries.cshtml", vm);
    }

    [HttpGet]
    public async Task<IActionResult> ExportAiQuotaInvoice(int? quotaYear = null, int? quotaMonth = null, bool revenueDemo = false, CancellationToken ct = default)
    {
        var periodStart = ResolveQuotaPeriodStartUtc(quotaYear, quotaMonth);
        if (revenueDemo)
        {
            var demoRows = await BuildDemoInvoiceRowsAsync(periodStart, ct);
            var bytes = _aiInvoiceExportService.BuildWorkbook(demoRows, includeTotalsRow: true);
            var demoFileName = $"ai-fakturaunderlag-demo-{periodStart:yyyy-MM}.xlsx";
            return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", demoFileName);
        }
        var (content, fileName) = await _aiInvoiceExportService.ExportAllAsync(periodStart, ct);
        return File(content, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
    }

    [HttpGet]
    public async Task<IActionResult> ExportAiQuotaInvoiceCompany(Guid companyId, int? quotaYear = null, int? quotaMonth = null, bool revenueDemo = false, CancellationToken ct = default)
    {
        if (companyId == Guid.Empty)
            return RedirectToAction(nameof(AiQueries), new { tab = "billing", quotaYear, quotaMonth, revenueDemo });

        var periodStart = ResolveQuotaPeriodStartUtc(quotaYear, quotaMonth);
        if (revenueDemo)
        {
            var demoRows = await BuildDemoInvoiceRowsAsync(periodStart, ct);
            var row = demoRows.FirstOrDefault(x => x.CompanyId == companyId);
            var exportRows = row is null ? Array.Empty<AiInvoiceExportRow>() : new[] { row };
            var bytes = _aiInvoiceExportService.BuildWorkbook(exportRows, includeTotalsRow: false);
            var demoFileName = row is null
                ? $"ai-fakturaunderlag-demo-bolag-{periodStart:yyyy-MM}.xlsx"
                : $"ai-fakturaunderlag-demo-{SanitizeFileNamePart(row.CompanyName)}-{periodStart:yyyy-MM}.xlsx";
            return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", demoFileName);
        }
        var (content, fileName) = await _aiInvoiceExportService.ExportCompanyAsync(periodStart, companyId, ct);
        return File(content, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
    }

    // Demo-lage for visual reviews: simulate higher usage and revenue without touching persisted data.
    private static IReadOnlyCollection<AiQuotaCompanyAdminVm> ApplyRevenueDemoSimulation(IReadOnlyCollection<AiQuotaCompanyAdminVm> source)
    {
        const decimal baseCostPerTokenSek = 0.09m;
        const decimal surchargeFactor = 0.20m;

        return source
            .Select((company, index) =>
            {
                var freeTokens = Math.Max(1, company.EffectiveFreeTokensPerPeriod);
                var seed = Math.Abs(HashCode.Combine(company.CompanyId, DateTime.UtcNow.Year, DateTime.UtcNow.Month, index));
                var usagePercent = 120 + (seed % 180);
                var usedTokens = Math.Max(company.UsedTokensCurrentPeriod, (int)Math.Round(freeTokens * (usagePercent / 100d)));
                var paidExtraTokens = Math.Max(0, usedTokens - freeTokens);
                var variability = 1m + ((seed % 9) / 20m);
                var baseCost = Math.Round(paidExtraTokens * baseCostPerTokenSek * variability, 2, MidpointRounding.AwayFromZero);
                var revenue = Math.Round(baseCost * surchargeFactor, 2, MidpointRounding.AwayFromZero);
                var billable = Math.Round(baseCost + revenue, 2, MidpointRounding.AwayFromZero);
                var paidUsers = paidExtraTokens > 0 ? 1 + (seed % 7) : 0;

                return new AiQuotaCompanyAdminVm
                {
                    CompanyId = company.CompanyId,
                    CompanyName = company.CompanyName,
                    HasOverride = company.HasOverride,
                    EnabledOverride = company.EnabledOverride,
                    FreeTokensPerPeriodOverride = company.FreeTokensPerPeriodOverride,
                    WarningThresholdPercentOverride = company.WarningThresholdPercentOverride,
                    EffectiveEnabled = company.EffectiveEnabled,
                    EffectiveFreeTokensPerPeriod = company.EffectiveFreeTokensPerPeriod,
                    EffectiveWarningThresholdPercent = company.EffectiveWarningThresholdPercent,
                    UsedTokensCurrentPeriod = usedTokens,
                    UsagePercentCurrentPeriod = Math.Clamp((int)Math.Round((usedTokens / (double)freeTokens) * 100d), 0, 999),
                    CurrentPeriodMode = paidExtraTokens > 0 ? "paid" : company.CurrentPeriodMode,
                    PaidUsersCount = paidExtraTokens > 0 ? paidUsers : company.PaidUsersCount,
                    BlockedUsersCount = paidExtraTokens > 0 ? 0 : company.BlockedUsersCount,
                    PaidExtraTokensCurrentPeriod = paidExtraTokens,
                    PaidExtraBaseCostSekCurrentPeriod = baseCost,
                    PaidExtraRevenueSekCurrentPeriod = revenue,
                    PaidExtraBillableSekCurrentPeriod = billable
                };
            })
            .ToList();
    }

    private static IReadOnlyCollection<AiQuotaCompanyAdminVm> ApplyRevenueDemoFloorScaling(IReadOnlyCollection<AiQuotaCompanyAdminVm> source)
    {
        var simulatedBillableTotal = source.Sum(x => x.PaidExtraBillableSekCurrentPeriod);
        if (simulatedBillableTotal <= 0m || simulatedBillableTotal >= 70000m)
            return source;

        var factor = 70000m / simulatedBillableTotal;
        return source
            .Select(x =>
            {
                var scaledBase = Math.Round(x.PaidExtraBaseCostSekCurrentPeriod * factor, 2, MidpointRounding.AwayFromZero);
                var scaledRevenue = Math.Round(x.PaidExtraRevenueSekCurrentPeriod * factor, 2, MidpointRounding.AwayFromZero);
                return new AiQuotaCompanyAdminVm
                {
                    CompanyId = x.CompanyId,
                    CompanyName = x.CompanyName,
                    HasOverride = x.HasOverride,
                    EnabledOverride = x.EnabledOverride,
                    FreeTokensPerPeriodOverride = x.FreeTokensPerPeriodOverride,
                    WarningThresholdPercentOverride = x.WarningThresholdPercentOverride,
                    EffectiveEnabled = x.EffectiveEnabled,
                    EffectiveFreeTokensPerPeriod = x.EffectiveFreeTokensPerPeriod,
                    EffectiveWarningThresholdPercent = x.EffectiveWarningThresholdPercent,
                    UsedTokensCurrentPeriod = x.UsedTokensCurrentPeriod,
                    UsagePercentCurrentPeriod = x.UsagePercentCurrentPeriod,
                    CurrentPeriodMode = x.CurrentPeriodMode,
                    PaidUsersCount = x.PaidUsersCount,
                    BlockedUsersCount = x.BlockedUsersCount,
                    PaidExtraTokensCurrentPeriod = x.PaidExtraTokensCurrentPeriod,
                    PaidExtraBaseCostSekCurrentPeriod = scaledBase,
                    PaidExtraRevenueSekCurrentPeriod = scaledRevenue,
                    PaidExtraBillableSekCurrentPeriod = Math.Round(scaledBase + scaledRevenue, 2, MidpointRounding.AwayFromZero)
                };
            })
            .ToList();
    }

    private static AiInvoiceExportVm BuildInvoiceExportVm(int year, int month, IReadOnlyCollection<AiQuotaCompanyAdminVm> companies)
    {
        return new AiInvoiceExportVm
        {
            SelectedYear = year,
            SelectedMonth = month,
            Rows = companies
                .OrderBy(x => x.CompanyName)
                .Select(x => new AiInvoiceExportRowVm
                {
                    CompanyId = x.CompanyId,
                    CompanyName = x.CompanyName,
                    UsedTokens = x.UsedTokensCurrentPeriod,
                    ExtraTokens = x.PaidExtraTokensCurrentPeriod,
                    BaseCostSek = x.PaidExtraBaseCostSekCurrentPeriod,
                    SurchargeSek = x.PaidExtraRevenueSekCurrentPeriod,
                    TotalBillableSek = x.PaidExtraBillableSekCurrentPeriod
                })
                .ToList()
        };
    }

    private async Task<IReadOnlyCollection<AiInvoiceExportRow>> BuildDemoInvoiceRowsAsync(DateTime periodStart, CancellationToken ct)
    {
        var quota = await _aiQuotaAdminService.GetSnapshotAsync(periodStart, ct);
        var companies = quota.Companies.Select(x => new AiQuotaCompanyAdminVm
        {
            CompanyId = x.CompanyId,
            CompanyName = x.CompanyName,
            HasOverride = x.HasOverride,
            EnabledOverride = x.EnabledOverride,
            FreeTokensPerPeriodOverride = x.FreeTokensPerPeriodOverride,
            WarningThresholdPercentOverride = x.WarningThresholdPercentOverride,
            EffectiveEnabled = x.EffectiveEnabled,
            EffectiveFreeTokensPerPeriod = x.EffectiveFreeTokensPerPeriod,
            EffectiveWarningThresholdPercent = x.EffectiveWarningThresholdPercent,
            UsedTokensCurrentPeriod = x.UsedTokensCurrentPeriod,
            UsagePercentCurrentPeriod = x.UsagePercentCurrentPeriod,
            CurrentPeriodMode = x.CurrentPeriodMode,
            PaidUsersCount = x.PaidUsersCount,
            BlockedUsersCount = x.BlockedUsersCount,
            PaidExtraTokensCurrentPeriod = x.PaidExtraTokensCurrentPeriod,
            PaidExtraBaseCostSekCurrentPeriod = x.PaidExtraBaseCostSekCurrentPeriod,
            PaidExtraRevenueSekCurrentPeriod = x.PaidExtraRevenueSekCurrentPeriod,
            PaidExtraBillableSekCurrentPeriod = x.PaidExtraBillableSekCurrentPeriod
        }).ToList();

        companies = ApplyRevenueDemoSimulation(companies).ToList();
        companies = ApplyRevenueDemoFloorScaling(companies).ToList();

        return companies
            .OrderBy(x => x.CompanyName)
            .Select(x => new AiInvoiceExportRow
            {
                CompanyId = x.CompanyId,
                CompanyName = x.CompanyName,
                UsedTokens = x.UsedTokensCurrentPeriod,
                ExtraTokens = x.PaidExtraTokensCurrentPeriod,
                BaseCostSek = x.PaidExtraBaseCostSekCurrentPeriod,
                SurchargeSek = x.PaidExtraRevenueSekCurrentPeriod,
                TotalBillableSek = x.PaidExtraBillableSekCurrentPeriod
            })
            .ToList();
    }

    private static string SanitizeFileNamePart(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "bolag";

        var chars = value
            .ToLowerInvariant()
            .Select(ch => char.IsLetterOrDigit(ch) ? ch : '-')
            .ToArray();
        var normalized = new string(chars);
        while (normalized.Contains("--", StringComparison.Ordinal))
            normalized = normalized.Replace("--", "-", StringComparison.Ordinal);
        normalized = normalized.Trim('-');
        return string.IsNullOrWhiteSpace(normalized) ? "bolag" : normalized;
    }

    private async Task<IReadOnlyCollection<AiQuotaRevenueMonthlyPointVm>> BuildMonthlyRevenueSeriesAsync(
        IReadOnlyCollection<AiQuotaCompanyAdminVm> companies,
        decimal surchargePercent,
        bool revenueDemo)
    {
        var now = DateTime.UtcNow;
        var currentMonthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var firstMonth = currentMonthStart.AddMonths(-5);
        var nextMonth = currentMonthStart.AddMonths(1);

        var freeTokenByCompany = companies.ToDictionary(x => x.CompanyId, x => Math.Max(1, x.EffectiveFreeTokensPerPeriod));

        var grouped = await _context.AiQueryLogs!
            .AsNoTracking()
            .Where(x => x.CompanyId.HasValue && x.CreatedAtUtc >= firstMonth && x.CreatedAtUtc < nextMonth)
            .GroupBy(x => new { x.CreatedAtUtc.Year, x.CreatedAtUtc.Month, CompanyId = x.CompanyId!.Value })
            .Select(g => new
            {
                g.Key.Year,
                g.Key.Month,
                g.Key.CompanyId,
                UsedTokens = g.Sum(x => (int?)x.TotalTokens) ?? 0,
                PromptTokens = g.Sum(x => (long?)x.PromptTokens) ?? 0,
                CompletionTokens = g.Sum(x => (long?)x.CompletionTokens) ?? 0,
                TotalTokens = g.Sum(x => (long?)x.TotalTokens) ?? 0
            })
            .ToListAsync();

        var monthRows = new List<AiQuotaRevenueMonthlyPointVm>(capacity: 6);
        var culture = new System.Globalization.CultureInfo("sv-SE");
        for (var i = 0; i < 6; i++)
        {
            var start = firstMonth.AddMonths(i);
            var monthGroups = grouped.Where(x => x.Year == start.Year && x.Month == start.Month);

            decimal monthBase = 0m;
            foreach (var row in monthGroups)
            {
                if (!freeTokenByCompany.TryGetValue(row.CompanyId, out var freeTokens))
                    continue;

                var paidExtraTokens = Math.Max(0, row.UsedTokens - freeTokens);
                if (paidExtraTokens <= 0 || row.UsedTokens <= 0)
                    continue;

                var totalCost = AiTokenPricing.CalculateTotalCostSek(
                    promptTokens: row.PromptTokens,
                    completionTokens: row.CompletionTokens,
                    totalTokens: row.TotalTokens) ?? 0m;
                var ratio = paidExtraTokens / (decimal)row.UsedTokens;
                monthBase += Math.Round(totalCost * ratio, 2, MidpointRounding.AwayFromZero);
            }

            var monthRevenue = Math.Round(monthBase * (surchargePercent / 100m), 2, MidpointRounding.AwayFromZero);
            var monthBillable = Math.Round(monthBase + monthRevenue, 2, MidpointRounding.AwayFromZero);

            monthRows.Add(new AiQuotaRevenueMonthlyPointVm
            {
                Label = start.ToString("MMM yyyy", culture),
                RevenueSek = monthRevenue,
                BillableSek = monthBillable
            });
        }

        if (revenueDemo)
        {
            var demoStart = new DateTime(now.Year, 8, 1, 0, 0, 0, DateTimeKind.Utc);
            if (now.Month < 8)
                demoStart = demoStart.AddYears(-1);

            var demoBillables = new[] { 22000m, 28000m, 35000m, 43000m, 52000m, 64000m, 79000m };
            monthRows = Enumerable.Range(0, demoBillables.Length)
                .Select(i =>
                {
                    var pointDate = demoStart.AddMonths(i);
                    var billable = demoBillables[i];
                    return new AiQuotaRevenueMonthlyPointVm
                    {
                        Label = pointDate.ToString("MMM yyyy", culture),
                        BillableSek = billable,
                        RevenueSek = Math.Round(billable * (surchargePercent / (100m + surchargePercent)), 2, MidpointRounding.AwayFromZero)
                    };
                })
                .ToList();
        }

        var maxBillable = monthRows.Any() ? monthRows.Max(x => x.BillableSek) : 0m;
        foreach (var row in monthRows)
        {
            row.WidthPercent = maxBillable > 0m
                ? Math.Clamp((double)(row.BillableSek / maxBillable * 100m), 0d, 100d)
                : 0d;
        }

        return monthRows;
    }

    private static AiQuotaRevenueOverviewVm BuildRevenueOverview(
        AiQuotaAdminVm quota,
        IReadOnlyCollection<AiQuotaRevenueMonthlyPointVm> monthlySeries)
    {
        var revenueCompanies = quota.Companies
            .Where(x => x.PaidExtraBillableSekCurrentPeriod > 0m)
            .OrderByDescending(x => x.PaidExtraBillableSekCurrentPeriod)
            .Take(8)
            .ToList();
        var maxRevenue = revenueCompanies.Any()
            ? revenueCompanies.Max(x => x.PaidExtraBillableSekCurrentPeriod)
            : 0m;

        return new AiQuotaRevenueOverviewVm
        {
            TotalBillableSekCurrentPeriod = quota.TotalPaidExtraBillableSekCurrentPeriod,
            ServiceMarginSekCurrentPeriod = quota.TotalPaidExtraRevenueSekCurrentPeriod,
            PaidModeCompaniesCount = quota.Companies.Count(x => string.Equals(x.CurrentPeriodMode, "paid", StringComparison.OrdinalIgnoreCase)),
            BlockedModeCompaniesCount = quota.Companies.Count(x => string.Equals(x.CurrentPeriodMode, "blocked", StringComparison.OrdinalIgnoreCase)),
            TotalPaidExtraTokensCurrentPeriod = quota.TotalPaidExtraTokensCurrentPeriod,
            MonthlySeries = monthlySeries,
            Companies = revenueCompanies.Select(x => new AiQuotaRevenueCompanyBarVm
            {
                CompanyName = x.CompanyName,
                BillableSekCurrentPeriod = x.PaidExtraBillableSekCurrentPeriod,
                WidthPercent = maxRevenue > 0m
                    ? Math.Clamp((double)(x.PaidExtraBillableSekCurrentPeriod / maxRevenue * 100m), 0d, 100d)
                    : 0d
            }).ToList()
        };
    }

    private static DateTime ResolveQuotaPeriodStartUtc(int? quotaYear, int? quotaMonth)
    {
        var now = DateTime.UtcNow;
        var year = quotaYear ?? now.Year;
        var month = quotaMonth ?? now.Month;
        year = Math.Clamp(year, 2020, now.Year + 1);
        month = Math.Clamp(month, 1, 12);
        return new DateTime(year, month, 1, 0, 0, 0, DateTimeKind.Utc);
    }

    private async Task<IReadOnlyCollection<int>> BuildQuotaSelectableYearsAsync()
    {
        var now = DateTime.UtcNow;
        var minCreatedAtUtc = await _context.AiQueryLogs!
            .AsNoTracking()
            .MinAsync(x => (DateTime?)x.CreatedAtUtc);

        var startYear = minCreatedAtUtc?.Year ?? now.Year;
        startYear = Math.Min(startYear, now.Year);

        return Enumerable.Range(startYear, (now.Year - startYear) + 1)
            .OrderByDescending(x => x)
            .ToList();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveAiQuotaGlobal(
        [FromForm] AiQuotaGlobalUpdateVm model,
        int latestPage = 1,
        int latestPageSize = 10,
        bool revenueDemo = false,
        string tab = "quota",
        int? quotaYear = null,
        int? quotaMonth = null,
        CancellationToken ct = default)
    {
        try
        {
            await _aiQuotaAdminService.SaveGlobalPolicyAsync(new AiQuotaGlobalPolicyInput
            {
                Enabled = model.Enabled,
                FreeTokensPerPeriod = model.FreeTokensPerPeriod,
                WarningThresholdPercent = model.WarningThresholdPercent
            }, _userManager.GetUserId(User), ct);

            await _notificationManager.Success("AI-kvot (global) sparad.");
        }
        catch (InvalidOperationException ex)
        {
            await _notificationManager.Error(ex.Message);
        }
        return RedirectToAction(nameof(AiQueries), new { latestPage, latestPageSize, revenueDemo, tab, quotaYear, quotaMonth });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveAiQuotaCompanyOverride(
        [FromForm] AiQuotaCompanyOverrideUpdateVm model,
        int latestPage = 1,
        int latestPageSize = 10,
        bool revenueDemo = false,
        string tab = "quota",
        int? quotaYear = null,
        int? quotaMonth = null,
        CancellationToken ct = default)
    {
        if (model.CompanyId == Guid.Empty)
        {
            await _notificationManager.Error("Ogiltigt bolag.");
            return RedirectToAction(nameof(AiQueries), new { latestPage, latestPageSize, revenueDemo, tab, quotaYear, quotaMonth });
        }

        try
        {
            await _aiQuotaAdminService.SaveCompanyOverrideAsync(new AiQuotaCompanyPolicyInput
            {
                CompanyId = model.CompanyId,
                EnabledOverrideSet = model.EnabledOverride.HasValue,
                EnabledOverride = model.EnabledOverride,
                FreeTokensPerPeriodOverride = model.FreeTokensPerPeriodOverride,
                WarningThresholdPercentOverride = model.WarningThresholdPercentOverride
            }, _userManager.GetUserId(User), ct);

            await _notificationManager.Success("AI-kvot override sparad för bolaget.");
        }
        catch (InvalidOperationException ex)
        {
            await _notificationManager.Error(ex.Message);
        }
        return RedirectToAction(nameof(AiQueries), new { latestPage, latestPageSize, revenueDemo, tab, quotaYear, quotaMonth });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveAiQuotaCompanyOverride(
        Guid companyId,
        int latestPage = 1,
        int latestPageSize = 10,
        bool revenueDemo = false,
        string tab = "quota",
        int? quotaYear = null,
        int? quotaMonth = null,
        CancellationToken ct = default)
    {
        if (companyId == Guid.Empty)
        {
            await _notificationManager.Error("Ogiltigt bolag.");
            return RedirectToAction(nameof(AiQueries), new { latestPage, latestPageSize, revenueDemo, tab, quotaYear, quotaMonth });
        }

        try
        {
            await _aiQuotaAdminService.RemoveCompanyOverrideAsync(companyId, ct);
            await _notificationManager.Success("Bolagets AI-kvot override borttagen.");
        }
        catch (InvalidOperationException ex)
        {
            await _notificationManager.Error(ex.Message);
        }
        return RedirectToAction(nameof(AiQueries), new { latestPage, latestPageSize, revenueDemo, tab, quotaYear, quotaMonth });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetAiQuotaCompanyMode(
        Guid companyId,
        int latestPage = 1,
        int latestPageSize = 10,
        bool revenueDemo = false,
        string tab = "quota",
        int? quotaYear = null,
        int? quotaMonth = null,
        CancellationToken ct = default)
    {
        if (companyId == Guid.Empty)
        {
            await _notificationManager.Error("Ogiltigt bolag.");
            return RedirectToAction(nameof(AiQueries), new { latestPage, latestPageSize, revenueDemo, tab, quotaYear, quotaMonth });
        }

        try
        {
            await _aiQuotaAdminService.ResetCompanyCurrentPeriodModeAsync(companyId, ct);
            await _notificationManager.Success("Bolaget återställt till standardläge för aktuell period.");
        }
        catch (InvalidOperationException ex)
        {
            await _notificationManager.Error(ex.Message);
        }

        return RedirectToAction(nameof(AiQueries), new { latestPage, latestPageSize, revenueDemo, tab, quotaYear, quotaMonth });
    }
}
