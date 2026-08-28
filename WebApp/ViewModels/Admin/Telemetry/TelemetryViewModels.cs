using WebApp.Models.Identity;

namespace WebApp.ViewModels.Admin;

public class PortalSessionEntryVm
{
    public Guid Id { get; set; }
    public string? UserId { get; set; }
    public string? UserEmail { get; set; }
    public Guid? CompanyId { get; set; }
    public string? CompanyName { get; set; }
    public DateTime StartedAtUtc { get; set; }
    public DateTime LastSeenAtUtc { get; set; }
    public int DurationMinutes { get; set; }
}

public class PortalSessionSummaryVm
{
    public Guid? CompanyId { get; set; }
    public string? CompanyName { get; set; }
    public int Sessions { get; set; }
    public int TotalMinutes { get; set; }
}

public class PortalSessionsPageVm
{
    public int TotalSessions { get; set; }
    public int TotalMinutes { get; set; }
    public IReadOnlyCollection<PortalSessionSummaryVm> TopCompanies { get; set; } = Array.Empty<PortalSessionSummaryVm>();
    public IReadOnlyCollection<PortalSessionEntryVm> Latest { get; set; } = Array.Empty<PortalSessionEntryVm>();
}

public class ExcelImportEntryVm
{
    public Guid Id { get; set; }
    public Guid? CompanyId { get; set; }
    public string? CompanyName { get; set; }
    public string? UserId { get; set; }
    public string? UserEmail { get; set; }
    public string? FileName { get; set; }
    public string? ImportType { get; set; }
    public long FileSizeBytes { get; set; }
    public int TotalRows { get; set; }
    public int InvalidRows { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}

public class ExcelImportSummaryVm
{
    public Guid? CompanyId { get; set; }
    public string? CompanyName { get; set; }
    public int Count { get; set; }
    public int TotalRows { get; set; }
}

public class ExcelImportsPageVm
{
    public int TotalImports { get; set; }
    public int TotalRows { get; set; }
    public IReadOnlyCollection<ExcelImportSummaryVm> TopCompanies { get; set; } = Array.Empty<ExcelImportSummaryVm>();
    public IReadOnlyCollection<ExcelImportEntryVm> Latest { get; set; } = Array.Empty<ExcelImportEntryVm>();
}

public class AiQueryEntryVm
{
    public Guid Id { get; set; }
    public Guid? CompanyId { get; set; }
    public string? CompanyName { get; set; }
    public string? UserId { get; set; }
    public string? UserEmail { get; set; }
    public string? Question { get; set; }
    public bool WasSuccessful { get; set; }
    public string? SqlText { get; set; }
    public string? ErrorMessage { get; set; }
    public int? PromptTokens { get; set; }
    public int? CompletionTokens { get; set; }
    public int? TotalTokens { get; set; }
    public decimal? InputCostSek { get; set; }
    public decimal? OutputCostSek { get; set; }
    public decimal? TotalCostSek { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}

public class AiQuerySummaryVm
{
    public Guid? CompanyId { get; set; }
    public string? CompanyName { get; set; }
    public int Count { get; set; }
    public int Successful { get; set; }
}

public class AiQueriesPageVm
{
    public int TotalQueries { get; set; }
    public int SuccessfulQueries { get; set; }
    public int TotalTokens { get; set; }
    public decimal? TotalCostSek { get; set; }
    public int LatestPage { get; set; } = 1;
    public int LatestPageSize { get; set; } = 10;
    public int LatestTotalCount { get; set; }
    public int LatestTotalPages => LatestPageSize <= 0 ? 1 : Math.Max(1, (int)Math.Ceiling(LatestTotalCount / (double)LatestPageSize));
    public IReadOnlyCollection<AiQuerySummaryVm> TopCompanies { get; set; } = Array.Empty<AiQuerySummaryVm>();
    public IReadOnlyCollection<AiQueryEntryVm> Latest { get; set; } = Array.Empty<AiQueryEntryVm>();
    public AiQuotaAdminVm Quota { get; set; } = new();
    public AiQuotaRevenueOverviewVm RevenueOverview { get; set; } = new();
    public bool RevenueDemoEnabled { get; set; }
    public string ActiveTab { get; set; } = "overview";
    public bool IsOverviewTabActive => string.Equals(ActiveTab, "overview", StringComparison.OrdinalIgnoreCase);
    public bool IsQuotaTabActive => string.Equals(ActiveTab, "quota", StringComparison.OrdinalIgnoreCase);
    public bool IsBillingTabActive => string.Equals(ActiveTab, "billing", StringComparison.OrdinalIgnoreCase);
    public int QuotaSelectedYear { get; set; }
    public int QuotaSelectedMonth { get; set; }
    public IReadOnlyCollection<int> QuotaSelectableYears { get; set; } = Array.Empty<int>();
    public bool QuotaIsHistoricalPeriod { get; set; }
    public AiInvoiceExportVm InvoiceExport { get; set; } = new();
}

public class AiQuotaAdminVm
{
    public bool GlobalEnabled { get; set; }
    public int GlobalFreeTokensPerPeriod { get; set; }
    public int GlobalWarningThresholdPercent { get; set; }
    public decimal SurchargePercent { get; set; }
    public int TotalPaidExtraTokensCurrentPeriod { get; set; }
    public decimal TotalPaidExtraBaseCostSekCurrentPeriod { get; set; }
    public decimal TotalPaidExtraRevenueSekCurrentPeriod { get; set; }
    public decimal TotalPaidExtraBillableSekCurrentPeriod { get; set; }
    public IReadOnlyCollection<AiQuotaCompanyAdminVm> Companies { get; set; } = Array.Empty<AiQuotaCompanyAdminVm>();
}

public class AiQuotaCompanyAdminVm
{
    public Guid CompanyId { get; set; }
    public string CompanyName { get; set; } = "-";
    public bool HasOverride { get; set; }
    public bool? EnabledOverride { get; set; }
    public int? FreeTokensPerPeriodOverride { get; set; }
    public int? WarningThresholdPercentOverride { get; set; }
    public bool EffectiveEnabled { get; set; }
    public int EffectiveFreeTokensPerPeriod { get; set; }
    public int EffectiveWarningThresholdPercent { get; set; }
    public int UsedTokensCurrentPeriod { get; set; }
    public int UsagePercentCurrentPeriod { get; set; }
    public string CurrentPeriodMode { get; set; } = "standard";
    public int PaidUsersCount { get; set; }
    public int BlockedUsersCount { get; set; }
    public int PaidExtraTokensCurrentPeriod { get; set; }
    public decimal PaidExtraBaseCostSekCurrentPeriod { get; set; }
    public decimal PaidExtraRevenueSekCurrentPeriod { get; set; }
    public decimal PaidExtraBillableSekCurrentPeriod { get; set; }
}

public class AiQuotaRevenueOverviewVm
{
    public decimal TotalBillableSekCurrentPeriod { get; set; }
    public decimal ServiceMarginSekCurrentPeriod { get; set; }
    public int PaidModeCompaniesCount { get; set; }
    public int BlockedModeCompaniesCount { get; set; }
    public int TotalPaidExtraTokensCurrentPeriod { get; set; }
    public IReadOnlyCollection<AiQuotaRevenueCompanyBarVm> Companies { get; set; } = Array.Empty<AiQuotaRevenueCompanyBarVm>();
    public IReadOnlyCollection<AiQuotaRevenueMonthlyPointVm> MonthlySeries { get; set; } = Array.Empty<AiQuotaRevenueMonthlyPointVm>();
}

public class AiQuotaRevenueCompanyBarVm
{
    public string CompanyName { get; set; } = "-";
    public decimal BillableSekCurrentPeriod { get; set; }
    public double WidthPercent { get; set; }
}

public class AiQuotaRevenueMonthlyPointVm
{
    public string Label { get; set; } = "-";
    public decimal BillableSek { get; set; }
    public decimal RevenueSek { get; set; }
    public double WidthPercent { get; set; }
}

public class AiQuotaGlobalUpdateVm
{
    public bool Enabled { get; set; }
    public int FreeTokensPerPeriod { get; set; }
    public int WarningThresholdPercent { get; set; }
}

public class AiQuotaCompanyOverrideUpdateVm
{
    public Guid CompanyId { get; set; }
    public bool? EnabledOverride { get; set; }
    public int? FreeTokensPerPeriodOverride { get; set; }
    public int? WarningThresholdPercentOverride { get; set; }
}
