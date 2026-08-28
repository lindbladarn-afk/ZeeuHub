using Entities.ZeeuDashboard;

namespace WebApp.Helpers
{

    /// <summary>
    /// Returns a list or ProductionDashboardVM that has a calculated remaining time
    /// It takes into account that it can be more than one Employee that works on the same operation
    /// </summary>
    public static class ZeeuDashboardHelper
    {
        public static IEnumerable<ProductionDashboardVM> CalculateRemainingOperationTime(IEnumerable<ProductionDashboardVM> model)
        {
            foreach (var item in model)
            {
                if (item.WorkOrder == null || !item.OperationStarted.HasValue)
                    continue;
                var moreThenOneRunning = model.Select(x => x).Where(x => x.WorkOrder == item.WorkOrder && x.OperationNumber == item.OperationNumber).ToList();
                double? total = 0D;
                TimeZoneInfo tz = TimeZoneInfo.FindSystemTimeZoneById("W. Europe Standard Time");
                DateTime localDatetime = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz);
                
                if (moreThenOneRunning.Count > 1)
                {

                    TimeSpan totalTimeSpan = TimeSpan.Zero;
                    foreach(var subItem in moreThenOneRunning)
                    {
                        if (!subItem.OperationStarted.HasValue)
                            continue;

                        var tempTime = localDatetime - subItem.OperationStarted.Value;
                        totalTimeSpan += tempTime;
                    }
                    total = totalTimeSpan.TotalHours;
                }
                else
                {
                    var tempTime = localDatetime - item.OperationStarted.Value;
                    total = tempTime.TotalHours;
                }

                item.RemainingTime = item.CalculatedOperationTime.GetValueOrDefault() - (item.ReportedOperationTime.GetValueOrDefault() + total.GetValueOrDefault());
            }
            return model;
        }
    }
}
