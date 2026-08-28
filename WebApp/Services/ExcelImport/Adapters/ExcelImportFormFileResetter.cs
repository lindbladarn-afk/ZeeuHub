using Microsoft.AspNetCore.Http;

namespace WebApp.Services.ExcelImport;

// Rewinds uploaded files before a fallback import reuses the same form file instance.
public static class ExcelImportFormFileResetter
{
    public static void RewindIfPossible(IFormFile file)
    {
        try
        {
            var stream = file.OpenReadStream();
            if (stream.CanSeek)
                stream.Position = 0;
        }
        catch
        {
            // Some form file streams cannot be reopened or rewound; the fallback import will surface the real error.
        }
    }
}
