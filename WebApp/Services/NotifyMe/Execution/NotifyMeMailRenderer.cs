using System.Net;
using System.Text;

namespace WebApp.Services.NotifyMe;

public interface INotifyMeMailRenderer
{
    string BuildHtml(PortalNotifyMeState state, IReadOnlyList<IReadOnlyDictionary<string, object?>> rows);
}

// Builds the generic NotifyMe mail body from the query result table.
public sealed class NotifyMeMailRenderer : INotifyMeMailRenderer
{
    public string BuildHtml(PortalNotifyMeState state, IReadOnlyList<IReadOnlyDictionary<string, object?>> rows)
    {
        var builder = new StringBuilder();
        builder.Append("<h2>").Append(WebUtility.HtmlEncode(state.Description)).Append("</h2>");
        builder.Append("<p><strong>Notifiering:</strong> ").Append(state.NotificationId).Append("<br/>");
        builder.Append("<strong>Typ:</strong> ").Append(WebUtility.HtmlEncode(state.TypeCode ?? string.Empty)).Append("<br/>");
        builder.Append("<strong>Prioritet:</strong> ").Append(WebUtility.HtmlEncode(state.PriorityCode ?? string.Empty)).Append("</p>");
        if (!string.IsNullOrWhiteSpace(state.Comment))
            builder.Append("<p>").Append(WebUtility.HtmlEncode(state.Comment)).Append("</p>");

        builder.Append("<table border=\"1\" cellpadding=\"6\" cellspacing=\"0\" style=\"border-collapse:collapse;width:100%;\">");
        var columns = rows[0].Keys.ToArray();
        builder.Append("<thead><tr>");
        foreach (var column in columns)
            builder.Append("<th>").Append(WebUtility.HtmlEncode(column)).Append("</th>");
        builder.Append("</tr></thead><tbody>");

        foreach (var row in rows)
        {
            builder.Append("<tr>");
            foreach (var column in columns)
            {
                var value = row.TryGetValue(column, out var cell) ? cell : null;
                builder.Append("<td>").Append(WebUtility.HtmlEncode(FormatValue(value))).Append("</td>");
            }
            builder.Append("</tr>");
        }

        builder.Append("</tbody></table>");
        return builder.ToString();
    }

    private static string FormatValue(object? value)
    {
        return value switch
        {
            null => string.Empty,
            DateTime dateTime => dateTime.ToString("yyyy-MM-dd HH:mm:ss"),
            IFormattable formattable => formattable.ToString(null, System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty,
            _ => value.ToString() ?? string.Empty
        };
    }
}
