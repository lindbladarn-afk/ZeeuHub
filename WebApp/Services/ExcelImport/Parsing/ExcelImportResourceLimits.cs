namespace WebApp.Services.ExcelImport;

// Defines defensive resource limits shared by Excel Import upload and workbook parsers.
public static class ExcelImportResourceLimits
{
    public const long MaxUploadBytes = 50L * 1024L * 1024L;
    public const int MaxStandardRows = 10_000;
    public const int MaxSupplierPriceRows = 100_000;
    public const int MaxColumns = 256;
    public const int MaxCellLength = 10_000;
    public const int MaxOpenXmlEntries = 2_000;
    public const long MaxOpenXmlUncompressedBytes = 200L * 1024L * 1024L;

    public static string TooManyRowsMessage(int maxRows)
        => $"Filen innehåller fler än {maxRows} rader. Dela upp importen i mindre filer.";

    public static string TooManyColumnsMessage(int maxColumns)
        => $"Filen innehåller fler än {maxColumns} kolumner. Ta bort oanvända kolumner och försök igen.";

    public static string CellTooLongMessage(int rowNo, int maxCellLength)
        => $"Rad {rowNo}: en cell är för lång. Max {maxCellLength} tecken per cell.";

    public static string FileTooLargeMessage(long maxBytes)
        => $"Filen är för stor. Maximal filstorlek är {maxBytes / (1024L * 1024L)} MB.";
}
