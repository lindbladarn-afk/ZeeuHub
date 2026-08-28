using Entities.Application;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using WebApp.Models.Integration;
using WebApp.Services.Integration.BankReconciliation.CodingRules;
using WebApp.Services.Integration;

namespace WebApp.Services.Integration.BankReconciliation.Commands;

// Validates and persists bank reconciliation coding rule changes.
public sealed class BankReconciliationCodingRuleCommandService : IBankReconciliationCodingRuleCommandService
{
    private readonly IBankReconciliationCodingRuleService _codingRuleService;
    private readonly IHttpContextAccessor _contextAccessor;
    private readonly ILogger<BankReconciliationCodingRuleCommandService> _logger;

    public BankReconciliationCodingRuleCommandService(
        IBankReconciliationCodingRuleService codingRuleService,
        IHttpContextAccessor contextAccessor,
        ILogger<BankReconciliationCodingRuleCommandService> logger)
    {
        _codingRuleService = codingRuleService;
        _contextAccessor = contextAccessor;
        _logger = logger;
    }

    public async Task<BankReconciliationCodingRuleCommandResult> SaveAsync(
        UserSession? user,
        BankReconciliationCodingRuleSaveRequest? request,
        CancellationToken cancellationToken)
    {
        if (user?.CompanyId is not Guid companyId || companyId == Guid.Empty)
        {
            return Failure("Saknar aktivt bolag.");
        }

        if (request is null || string.IsNullOrWhiteSpace(request.BankAccountKey))
        {
            return Failure("Saknar bankkonto för reglerna.");
        }

        try
        {
            var result = await _codingRuleService.SaveAsync(
                companyId,
                request.BankAccountKey,
                user,
                request.Rows ?? new List<BankReconciliationCodingRuleRow>(),
                request.BankAccountLabel,
                request.ExpectedVersion,
                cancellationToken);

            return new BankReconciliationCodingRuleCommandResult
            {
                Success = true,
                Version = result.Version,
                Rows = result.Rows,
                BankAccountKey = result.BankAccountKey,
                BankAccountLabel = result.BankAccountLabel
            };
        }
        catch (BankReconciliationCodingRuleConflictException ex)
        {
            return new BankReconciliationCodingRuleCommandResult
            {
                Success = false,
                Conflict = true,
                ErrorMessage = BankReconciliationErrorHandling.LogAndBuildUserMessage(
                    _logger,
                    _contextAccessor.HttpContext,
                    "BankReconciliationCodingRules conflict",
                    "Konteringsreglerna kunde inte sparas på grund av en versionskonflikt.",
                    ex),
                CurrentVersion = ex.CurrentVersion
            };
        }
    }

    private static BankReconciliationCodingRuleCommandResult Failure(string errorMessage)
        => new()
        {
            Success = false,
            ErrorMessage = errorMessage
        };
}
