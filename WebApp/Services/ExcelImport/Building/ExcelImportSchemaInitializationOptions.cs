namespace WebApp.Services.ExcelImport;

// Controls whether the administrative schema initializer may execute DDL in production.
public sealed class ExcelImportSchemaInitializationOptions
{
    public const string SectionName = "ExcelImport:SchemaInitialization";

    public bool AllowRuntimeInitializationInProduction { get; set; }
}
