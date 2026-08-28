using System;

namespace WebApp.Services.ExcelImport;

public class EditableImportRowLimitExceededException : Exception
{
    public int RowCount { get; }
    public int MaxRows { get; }

    public EditableImportRowLimitExceededException(int rowCount, int maxRows)
        : base($"Filen innehåller fler än {maxRows} rader ({rowCount}).")
    {
        RowCount = rowCount;
        MaxRows = maxRows;
    }
}
