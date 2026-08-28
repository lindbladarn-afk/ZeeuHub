using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using System.Text.Json;
using WebApp.Services.Integration;
using WebApp.Services.Integration.BankReconciliation;
using WebApp.Services.Integration.BankReconciliation.Imports;
using WebApp.Services.Integration.BankReconciliation.Validation;
using System.Xml;

namespace WebApp.Services.Integration.BankReconciliation.Upload;

// Handles atomic camt.053 upload staging and parser validation before a file becomes active.
public sealed class BankReconciliationCamtUploadService : IBankReconciliationCamtUploadService
{
    private readonly IWebHostEnvironment _environment;
    private readonly IBankReconciliationCamtParser _parser;
    private readonly IBankReconciliationCamtValidationService _validationService;
    private readonly BankReconciliationCamtValidationOptions _validationOptions;
    private readonly IBankReconciliationImportRegistry _importRegistry;

    public BankReconciliationCamtUploadService(
        IWebHostEnvironment environment,
        IBankReconciliationCamtParser parser,
        IBankReconciliationCamtValidationService validationService,
        IBankReconciliationImportRegistry importRegistry,
        IOptions<BankReconciliationCamtValidationOptions>? validationOptions = null)
    {
        _environment = environment;
        _parser = parser;
        _validationService = validationService;
        _importRegistry = importRegistry;
        _validationOptions = validationOptions?.Value ?? new BankReconciliationCamtValidationOptions();
    }

    public async Task<BankReconciliationCamtUploadResult> PrepareUploadAsync(
        IFormFile file,
        Guid companyId,
        string sessionId,
        string? previousFilePath,
        CancellationToken cancellationToken = default)
    {
        if (companyId == Guid.Empty)
        {
            return new BankReconciliationCamtUploadResult
            {
                Success = false,
                FailureReason = BankReconciliationCamtUploadFailureReason.MissingCompany
            };
        }

        if (file.Length > Math.Max(1, _validationOptions.MaximumFileSizeBytes))
        {
            return new BankReconciliationCamtUploadResult
            {
                Success = false,
                FailureReason = BankReconciliationCamtUploadFailureReason.FileTooLarge
            };
        }

        var targetDir = Path.Combine(_environment.ContentRootPath, "App_Data", "Integration", "BankReconciliation", "camt053", "session", sessionId);
        Directory.CreateDirectory(targetDir);

        var safeName = SanitizeFileName(Path.GetFileNameWithoutExtension(file.FileName));
        var timestamp = DateTime.UtcNow.ToString("yyyyMMddHHmmssfff");
        var uploadId = Guid.NewGuid().ToString("N")[..8];
        var targetFile = Path.Combine(targetDir, $"{safeName}-{timestamp}-{uploadId}.xml");
        var stagingFile = Path.Combine(targetDir, $"{safeName}-{timestamp}-{uploadId}.uploading");

        try
        {
            await using (var stream = File.Create(stagingFile))
            {
                await file.CopyToAsync(stream, cancellationToken);
                await stream.FlushAsync(cancellationToken);
            }

            var validation = _validationService.Validate(stagingFile);
            if (!validation.IsValid)
            {
                DeleteIfExists(stagingFile);
                return new BankReconciliationCamtUploadResult
                {
                    Success = false,
                    FailureReason = BankReconciliationCamtUploadFailureReason.ValidationError,
                    FailureDetails = validation.Issues
                        .First(issue => issue.Severity == BankReconciliationCamtValidationSeverity.Error)
                        .Message
                };
            }

            WebApp.Models.Integration.BankReconciliationParsedDocument parsedDocument;
            try
            {
                parsedDocument = _parser.ParseDocument(stagingFile);
            }
            catch (Exception ex) when (ex is XmlException || ex is InvalidOperationException)
            {
                DeleteIfExists(stagingFile);
                return new BankReconciliationCamtUploadResult
                {
                    Success = false,
                    FailureReason = BankReconciliationCamtUploadFailureReason.ParseError,
                    FailureDetails = "Filen kunde inte tolkas efter validering."
                };
            }

            var transactions = parsedDocument.Transactions;

            if (transactions.Count == 0)
            {
                DeleteIfExists(stagingFile);
                return new BankReconciliationCamtUploadResult
                {
                    Success = false,
                    FailureReason = BankReconciliationCamtUploadFailureReason.NoTransactionsFound
                };
            }

            File.Move(stagingFile, targetFile, overwrite: true);
            var registration = await _importRegistry.RegisterAsync(
                new BankReconciliationImportRegistrationRequest
                {
                    CompanyId = companyId,
                    Document = parsedDocument
                },
                cancellationToken);
            if (!registration.Accepted && registration.Status != BankReconciliationImportStatus.ExactDuplicate)
            {
                DeleteIfExists(targetFile);
                return new BankReconciliationCamtUploadResult
                {
                    Success = false,
                    FailureReason = BankReconciliationCamtUploadFailureReason.OverlappingImport,
                    ImportStatus = registration.Status,
                    OverlappingTransactionCount = registration.OverlappingTransactionCount
                };
            }

            if (!string.IsNullOrWhiteSpace(previousFilePath)
                && !string.Equals(previousFilePath, targetFile, StringComparison.OrdinalIgnoreCase))
            {
                DeleteIfExists(previousFilePath);
            }

            return new BankReconciliationCamtUploadResult
            {
                Success = true,
                StoredFilePath = targetFile,
                TransactionCount = transactions.Count,
                ImportStatus = registration.Status
            };
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            DeleteIfExists(stagingFile);
            DeleteIfExists(targetFile);
            throw;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException or JsonException)
        {
            DeleteIfExists(stagingFile);
            DeleteIfExists(targetFile);
            return new BankReconciliationCamtUploadResult
            {
                Success = false,
                FailureReason = BankReconciliationCamtUploadFailureReason.SaveError,
                FailureDetails = IntegrationLogSanitizer.Diagnostic(ex.Message)
            };
        }
    }

    private static string SanitizeFileName(string? value)
    {
        var candidate = string.IsNullOrWhiteSpace(value) ? "camt053" : value;
        candidate = string.Concat(candidate.Split(Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries));
        return string.IsNullOrWhiteSpace(candidate) ? "camt053" : candidate;
    }

    private static void DeleteIfExists(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return;
        }

        try
        {
            File.Delete(path);
        }
        catch
        {
            // Cleanup should never block the active import path.
        }
    }
}
