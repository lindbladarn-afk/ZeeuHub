namespace WebApp.Services.Purchase.Demo;

// Persists the per-company purchase demo toggle in the current session.
public interface IPurchaseDemoModeService
{
    bool IsEnabled();
    void SetEnabled(bool enabled);
}
