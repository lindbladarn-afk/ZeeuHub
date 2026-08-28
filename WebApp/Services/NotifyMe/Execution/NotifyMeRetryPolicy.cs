using Microsoft.Data.SqlClient;

namespace WebApp.Services.NotifyMe;

public interface INotifyMeRetryPolicy
{
    int MaxAttempts { get; }
    bool IsRetryable(Exception exception);
    DateTime CalculateRetryAt(DateTime failedAtUtc, int retryAttempt);
}

// Central retry rules for scheduled NotifyMe executions.
public sealed class NotifyMeRetryPolicy : INotifyMeRetryPolicy
{
    private static readonly TimeSpan[] ScheduledRetryDelays =
    {
        TimeSpan.FromMinutes(2),
        TimeSpan.FromMinutes(4),
        TimeSpan.FromMinutes(6)
    };

    public int MaxAttempts => ScheduledRetryDelays.Length;

    public bool IsRetryable(Exception exception)
    {
        if (exception is InvalidOperationException invalidOperation)
        {
            return !invalidOperation.Message.Contains("saknar mottagare", StringComparison.OrdinalIgnoreCase)
                && !invalidOperation.Message.Contains("saknar SQL-underlag", StringComparison.OrdinalIgnoreCase)
                && !invalidOperation.Message.Contains("Dynamiska mottagare stöds inte", StringComparison.OrdinalIgnoreCase)
                && !invalidOperation.Message.Contains("hittades inte", StringComparison.OrdinalIgnoreCase);
        }

        if (exception is SqlException sqlException)
        {
            return sqlException.Number switch
            {
                102 or 156 or 195 or 207 or 208 or 2812 or 4121 => false,
                _ => true
            };
        }

        return true;
    }

    public DateTime CalculateRetryAt(DateTime failedAtUtc, int retryAttempt)
    {
        if (retryAttempt < 1 || retryAttempt > ScheduledRetryDelays.Length)
            throw new ArgumentOutOfRangeException(nameof(retryAttempt), retryAttempt, "Retry attempt is outside configured NotifyMe retry delays.");

        return failedAtUtc.Add(ScheduledRetryDelays[retryAttempt - 1]);
    }
}
