using System.Reflection;
using System.Text;

namespace WebApp.Services.WebApproval;

// Builds consistent WebApproval error details for logging and technical notifications.
public static class WebApprovalErrorDetailsBuilder
{
    public static string BuildSqlErrorDetails(
        string operation,
        Exception exception,
        string originalStoredProcedure,
        params string[] extraLines)
    {
        var procedure = exception.GetType().GetProperty("Procedure", BindingFlags.Instance | BindingFlags.Public)?.GetValue(exception)?.ToString() ?? "-";
        var lineNumber = exception.GetType().GetProperty("LineNumber", BindingFlags.Instance | BindingFlags.Public)?.GetValue(exception)?.ToString() ?? "-";
        var number = exception.GetType().GetProperty("Number", BindingFlags.Instance | BindingFlags.Public)?.GetValue(exception)?.ToString() ?? "-";

        var sb = new StringBuilder();
        sb.AppendLine($"SQL Error when {operation}: ");
        sb.AppendLine($"Procedure={procedure}");
        sb.AppendLine($"LineNumber={lineNumber}");
        sb.AppendLine($"Message='{exception.Message}");
        sb.AppendLine($"Number={number}");
        sb.AppendLine($"Original StoredProcedure: {originalStoredProcedure}");

        foreach (var line in extraLines)
            sb.AppendLine(line);

        return sb.ToString();
    }

    public static string BuildExceptionDetails(
        string message,
        string originalStoredProcedure,
        params string[] extraLines)
    {
        var sb = new StringBuilder();
        sb.AppendLine(message);
        sb.AppendLine($"Original StoredProcedure: {originalStoredProcedure}");

        foreach (var line in extraLines)
            sb.AppendLine(line);

        return sb.ToString();
    }
}
