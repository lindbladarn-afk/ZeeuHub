namespace WebApp.Services.ExcelImport;

// Resolves the shared connection string used by Excel import repositories.
public interface IExcelImportConnectionResolver
{
    string ResolveConnectionString();
}
