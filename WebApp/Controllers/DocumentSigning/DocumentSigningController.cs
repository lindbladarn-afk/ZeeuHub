using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebApp.Services.DocumentSigning;

namespace WebApp.Controllers;

[AllowAnonymous]
public class DocumentSigningController : Controller
{
    private readonly IDocumentSigningService _documentSigningService;

    public DocumentSigningController(IDocumentSigningService documentSigningService)
    {
        _documentSigningService = documentSigningService;
    }

    [HttpGet("DocumentSigning/Result/{publicToken}")]
    public async Task<IActionResult> Result(Guid publicToken, CancellationToken cancellationToken)
    {
        var model = await _documentSigningService.GetPublicResultAsync(publicToken, cancellationToken);
        if (model == null)
            return NotFound();

        return View(model);
    }
}
