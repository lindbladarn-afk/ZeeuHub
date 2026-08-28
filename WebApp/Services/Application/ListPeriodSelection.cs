using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace WebApp.Services.Application
{
    /// <summary>
    /// Shared period state for server-rendered list pages such as orders and invoices.
    /// Keeps year selection, explicit date range, and fallback-to-latest-data-year consistent.
    /// </summary>
    public sealed class ListPeriodSelection
    {
        public DateTime? FromDate { get; private set; }
        public DateTime? ToDate { get; private set; }
        public int? SelectedYear { get; private set; }
        public IReadOnlyList<int> AvailableYears { get; private set; } = Array.Empty<int>();
        public bool UsesDefaultPeriod { get; private set; }

        public static ListPeriodSelection Create(
            DateTime? fromDate,
            DateTime? toDate,
            int? selectedYear,
            IReadOnlyList<int>? availableYears = null,
            bool? usesDefaultPeriodOverride = null,
            int yearsBack = 4)
        {
            var normalizedFromDate = fromDate;
            var normalizedToDate = toDate;
            var usesDefaultPeriod = false;

            if (selectedYear.HasValue)
            {
                normalizedFromDate = new DateTime(selectedYear.Value, 1, 1);
                normalizedToDate = new DateTime(selectedYear.Value, 12, 31);
            }
            else if (!normalizedFromDate.HasValue && !normalizedToDate.HasValue)
            {
                var today = DateTime.Today;
                normalizedFromDate = new DateTime(today.Year, 1, 1);
                normalizedToDate = today;
                usesDefaultPeriod = true;
            }

            return new ListPeriodSelection
            {
                FromDate = normalizedFromDate,
                ToDate = normalizedToDate,
                SelectedYear = selectedYear,
                AvailableYears = availableYears ?? BuildAvailableYears(yearsBack),
                UsesDefaultPeriod = usesDefaultPeriodOverride ?? usesDefaultPeriod
            };
        }

        /// <summary>
        /// Resolves the requested period and optionally falls back to the latest full year with data.
        /// </summary>
        public static async Task<ListPeriodSelection> ResolveWithLatestDataFallbackAsync(
            DateTime? fromDate,
            DateTime? toDate,
            int? selectedYear,
            IReadOnlyList<int>? availableYears,
            bool usesDefaultPeriod,
            Func<Task<DateTime?>> latestDataProvider)
        {
            var period = Create(fromDate, toDate, selectedYear, availableYears, usesDefaultPeriod);
            if (!usesDefaultPeriod)
            {
                return period;
            }

            period.ApplyLatestDataYearFallback(await latestDataProvider());
            return period;
        }

        // If the default "current year" window is empty, show the latest full year with data instead.
        public void ApplyLatestDataYearFallback(DateTime? latestDataDate)
        {
            if (!UsesDefaultPeriod || !latestDataDate.HasValue || latestDataDate.Value.Year >= DateTime.Today.Year)
            {
                return;
            }

            var latestDataYear = latestDataDate.Value.Year;
            FromDate = new DateTime(latestDataYear, 1, 1);
            ToDate = new DateTime(latestDataYear, 12, 31);
            SelectedYear = latestDataYear;
            UsesDefaultPeriod = false;
            AvailableYears = MergeAvailableYears(AvailableYears, latestDataYear);
        }

        private static IReadOnlyList<int> BuildAvailableYears(int yearsBack)
        {
            var currentYear = DateTime.Today.Year;
            return Enumerable.Range(currentYear - yearsBack, yearsBack + 1)
                .Reverse()
                .ToArray();
        }

        private static IReadOnlyList<int> MergeAvailableYears(IReadOnlyList<int> availableYears, int year)
        {
            return availableYears
                .Append(year)
                .Distinct()
                .OrderByDescending(x => x)
                .ToArray();
        }
    }
}
