using Microsoft.AspNetCore.Http;
using WebApp.Models.ControlPanel;
using WebApp.Services;

namespace WebApp.Services.Application;

public interface IFeatureAccessService
{
    IReadOnlyList<FeatureAccessSelection> GetSelections(ISession session);
    void SaveSelections(ISession session, IEnumerable<FeatureAccessSelection> selections);
    bool IsEnabled(ISession session, int companyCode, FeatureFlag feature);
}

public class FeatureAccessService : IFeatureAccessService
{
    private const string SessionKey = "FeatureAccessSelections";

    public IReadOnlyList<FeatureAccessSelection> GetSelections(ISession session)
    {
        return session.Get<List<FeatureAccessSelection>>(SessionKey) ?? new List<FeatureAccessSelection>();
    }

    public void SaveSelections(ISession session, IEnumerable<FeatureAccessSelection> selections)
    {
        session.Set(SessionKey, selections.ToList());
    }

    public bool IsEnabled(ISession session, int companyCode, FeatureFlag feature)
    {
        var selections = GetSelections(session);
        var selection = selections.FirstOrDefault(x => x.CompanyCode == companyCode);
        if (selection is null)
            return true; // default allow until explicitly switched off

        return feature switch
        {
            FeatureFlag.Invoices => selection.InvoicesEnabled,
            FeatureFlag.Orders => selection.OrdersEnabled,
            FeatureFlag.Ai => selection.AiEnabled,
            FeatureFlag.ExcelImport => selection.ExcelImportEnabled,
            FeatureFlag.Dashboard => selection.DashboardEnabled,
            _ => true
        };
    }
}
