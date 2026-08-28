namespace WebApp.Services.NotifyMe;

// Validates the tenant database context required by NotifyMe reads and writes.
internal static class NotifyMeConnectionContext
{
    public static bool HasConnectionContext(string? connectionString, int? companyCode, out string message)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            message = "Tenantdata från Jeeves är inte tillgänglig just nu. NotifyMe kan öppnas, men live-data kan inte laddas för valt bolag.";
            return false;
        }

        if (!companyCode.HasValue)
        {
            message = "Det finns inget aktivt Jeeves-bolag kopplat till användaren för det här portalbolaget. NotifyMe kan öppnas, men live-data kan inte laddas ännu.";
            return false;
        }

        message = string.Empty;
        return true;
    }
}
