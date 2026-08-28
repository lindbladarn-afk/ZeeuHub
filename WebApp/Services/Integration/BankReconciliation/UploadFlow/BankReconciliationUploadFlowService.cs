using Entities.Application;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Localization;
using WebApp.Services.Integration.BankReconciliation.Imports;
using WebApp.Services.Integration.BankReconciliation.Upload;
using WebApp.ViewModels.Shared;

namespace WebApp.Services.Integration.BankReconciliation.UploadFlow;

// Coordinates upload validation, active file session state, and cleanup for bank reconciliation.
public sealed class BankReconciliationUploadFlowService : IBankReconciliationUploadFlowService
{
    private const string SessionFileKey = "BankRec.UploadedCamtFile";
    private const string SessionFileDisplayNameKey = "BankRec.UploadedCamtDisplayName";
    private const string SessionCompanyIdKey = "BankRec.UploadedCamtCompanyId";
    private static readonly string[] SupportedExtensions = [".xml", ".nda"];

    private readonly IHttpContextAccessor _contextAccessor;
    private readonly IBankReconciliationCamtUploadService _uploadService;
    private readonly IStringLocalizer<SharedResources> _sharedLocalizer;

    public BankReconciliationUploadFlowService(
        IHttpContextAccessor contextAccessor,
        IBankReconciliationCamtUploadService uploadService,
        IStringLocalizer<SharedResources> sharedLocalizer)
    {
        _contextAccessor = contextAccessor;
        _uploadService = uploadService;
        _sharedLocalizer = sharedLocalizer;
    }

    public async Task<BankReconciliationUploadFlowResult> UploadAsync(
        IFormFile? file,
        CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
        {
            return UploadError(_sharedLocalizer["Integration_NoFileSelected"].Value);
        }

        var extension = Path.GetExtension(file.FileName);
        if (!SupportedExtensions.Any(item => string.Equals(item, extension, StringComparison.OrdinalIgnoreCase)))
        {
            return UploadError(_sharedLocalizer["Integration_InvalidFileFormatOnlyXml"].Value);
        }

        var session = Session;
        var companyId = session.Get<UserSession>("UserObject")?.CompanyId ?? Guid.Empty;
        if (companyId == Guid.Empty)
        {
            return UploadError("Ett aktivt bolag krävs för att importera ett kontoutdrag.");
        }

        var result = await _uploadService.PrepareUploadAsync(
            file,
            companyId,
            session.Id,
            session.GetString(SessionFileKey),
            cancellationToken);

        if (!result.Success || string.IsNullOrWhiteSpace(result.StoredFilePath))
        {
            return UploadError(MapFailureMessage(result));
        }

        session.SetString(SessionFileKey, result.StoredFilePath);
        session.SetString(SessionFileDisplayNameKey, Path.GetFileName(file.FileName));
        session.SetString(SessionCompanyIdKey, companyId.ToString("N"));
        return new BankReconciliationUploadFlowResult
        {
            UploadInfo = result.ImportStatus switch
            {
                BankReconciliationImportStatus.Corrected => $"En korrigerad version av kontoutdraget har lästs in: {Path.GetFileName(result.StoredFilePath)}",
                BankReconciliationImportStatus.ExactDuplicate => $"Kontoutdraget var redan registrerat och har öppnats i arbetsytan igen: {Path.GetFileName(result.StoredFilePath)}",
                _ => _sharedLocalizer["Integration_FileUploaded", Path.GetFileName(result.StoredFilePath)].Value
            }
        };
    }

    public BankReconciliationUploadFlowResult ClearUpload()
    {
        var sessionFile = Session.GetString(SessionFileKey);
        if (!string.IsNullOrWhiteSpace(sessionFile) && File.Exists(sessionFile))
        {
            try
            {
                File.Delete(sessionFile);
            }
            catch
            {
                // Session state controls visibility; a failed cleanup should not block starting over.
            }
        }

        Session.Remove(SessionFileKey);
        Session.Remove(SessionFileDisplayNameKey);
        Session.Remove(SessionCompanyIdKey);
        return new BankReconciliationUploadFlowResult
        {
            StatusTone = "success",
            StatusMessage = _sharedLocalizer["BankRec_FileCleared"].Value
        };
    }

    public string? ResolveLatestCamtFile()
    {
        if (!IsBoundToCurrentCompany())
            return null;

        var sessionFile = Session.GetString(SessionFileKey);
        if (string.IsNullOrWhiteSpace(sessionFile))
        {
            return null;
        }

        return File.Exists(sessionFile) ? sessionFile : null;
    }

    public string? ResolveLatestCamtDisplayName()
        => IsBoundToCurrentCompany() ? Session.GetString(SessionFileDisplayNameKey) : null;

    // Keeps the temporary upload scoped to the active company.
    private bool IsBoundToCurrentCompany()
    {
        var companyId = Session.Get<UserSession>("UserObject")?.CompanyId ?? Guid.Empty;
        if (companyId == Guid.Empty)
            return false;

        var storedCompanyId = Session.GetString(SessionCompanyIdKey);
        if (string.IsNullOrWhiteSpace(storedCompanyId))
        {
            // Preserve sessions created before company binding was introduced.
            Session.SetString(SessionCompanyIdKey, companyId.ToString("N"));
            return true;
        }

        return Guid.TryParse(storedCompanyId, out var uploadedCompanyId) && uploadedCompanyId == companyId;
    }

    private ISession Session => _contextAccessor.HttpContext?.Session
        ?? throw new InvalidOperationException("Bank reconciliation upload flow requires an active HTTP session.");

    private string MapFailureMessage(BankReconciliationCamtUploadResult result)
        => result.FailureReason switch
        {
            BankReconciliationCamtUploadFailureReason.NoTransactionsFound => _sharedLocalizer["Integration_CamtFileContainsNoTransactions"].Value,
            BankReconciliationCamtUploadFailureReason.ValidationError => result.FailureDetails ?? "CAMT-filen klarade inte integritetskontrollen.",
            BankReconciliationCamtUploadFailureReason.FileTooLarge => "CAMT-filen är större än tillåten filstorlek.",
            BankReconciliationCamtUploadFailureReason.DuplicateImport => "Kontoutdraget är redan importerat och har inte lästs in igen.",
            BankReconciliationCamtUploadFailureReason.OverlappingImport => $"Kontoutdraget överlappar ett tidigare statement med {result.OverlappingTransactionCount} transaktioner och har stoppats.",
            BankReconciliationCamtUploadFailureReason.MissingCompany => "Ett aktivt bolag krävs för att importera ett kontoutdrag.",
            BankReconciliationCamtUploadFailureReason.ParseError => _sharedLocalizer["Integration_CouldNotReadCamtFile", result.FailureDetails ?? string.Empty].Value,
            _ => _sharedLocalizer["Integration_CouldNotSaveCamtFile"].Value
        };

    private static BankReconciliationUploadFlowResult UploadError(string message)
        => new()
        {
            UploadError = message
        };
}
