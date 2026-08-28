namespace WebApp.Services.NotifyMe;

// Builds the explicit confirmation mail used when a NotifyMe test run returns no rows.
public static class NotifyMeNoHitTestMailComposer
{
    public static (string Subject, string Html) Compose(string? subject)
    {
        var effectiveSubject = string.IsNullOrWhiteSpace(subject)
            ? "[Ingen träff] NotifyMe"
            : $"[Ingen träff] {subject}";

        const string html =
            "<p>Ingen träff i källdatan.</p><p>Det här testmailet skickades för att bekräfta att NotifyMe fungerar, även när SQL-underlaget inte returnerar några rader.</p>";

        return (effectiveSubject, html);
    }
}
