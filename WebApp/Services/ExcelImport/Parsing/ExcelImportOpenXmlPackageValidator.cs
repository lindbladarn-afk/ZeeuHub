using System.IO.Compression;

namespace WebApp.Services.ExcelImport;

// Rejects unsafe OpenXML packages before a workbook reader expands their contents.
public static class ExcelImportOpenXmlPackageValidator
{
    public static bool TryValidate(Stream stream, out string error)
    {
        ArgumentNullException.ThrowIfNull(stream);

        if (!stream.CanSeek)
        {
            error = "Excel-filen kunde inte säkerhetskontrolleras eftersom filströmmen inte går att söka i.";
            return false;
        }

        try
        {
            stream.Position = 0;
            using var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: true);
            if (archive.Entries.Count > ExcelImportResourceLimits.MaxOpenXmlEntries)
            {
                error = "Excel-filen innehåller för många interna delar och kan inte behandlas säkert.";
                return false;
            }

            long totalUncompressedBytes = 0;
            foreach (var entry in archive.Entries)
            {
                if (entry.Length > ExcelImportResourceLimits.MaxOpenXmlUncompressedBytes
                    || totalUncompressedBytes > ExcelImportResourceLimits.MaxOpenXmlUncompressedBytes - entry.Length)
                {
                    error = "Excel-filen expanderar till för mycket data och kan inte behandlas säkert.";
                    return false;
                }

                totalUncompressedBytes += entry.Length;
            }

            error = string.Empty;
            return true;
        }
        catch (InvalidDataException)
        {
            error = "Filen kunde inte läsas. Kontrollera att den är en giltig Excel-fil.";
            return false;
        }
        finally
        {
            stream.Position = 0;
        }
    }
}
