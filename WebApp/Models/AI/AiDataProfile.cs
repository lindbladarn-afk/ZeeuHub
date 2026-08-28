// Defines the approved schema profile used when Intelligence plans SQL for a company.
namespace WebApp.Models.AI;

public static class AiDataProfile
{
    public const string JeevesDirect = "JeevesDirect";
    public const string DataWarehouse = "DataWarehouse";

    public static bool IsSupported(string? value) =>
        string.Equals(value, JeevesDirect, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(value, DataWarehouse, StringComparison.OrdinalIgnoreCase);

    public static string Normalize(string? value) =>
        string.Equals(value, DataWarehouse, StringComparison.OrdinalIgnoreCase)
            ? DataWarehouse
            : JeevesDirect;
}
