using Entities.Application;
using Microsoft.Extensions.Localization;
using System.Security.Cryptography;
using System.Text;
using WebApp.Models.Integration;
using WebApp.Services.Integration;
using WebApp.Services.Integration.BankReconciliation.CodingRules;

namespace WebApp.Services.Integration.BankReconciliation.Workspace;

// Resolves the active bank reconciliation workspace from demo state or an uploaded CAMT file.
public sealed class BankReconciliationWorkspaceService : IBankReconciliationWorkspaceService
{
    private readonly IBankReconciliationCamtParser _camtParser;
    private readonly IBankReconciliationCodingRuleService _codingRuleService;
    private readonly IBankReconciliationDemoDataService _demoDataService;
    private readonly IBankReconciliationService _bankReconciliationService;
    private readonly IStringLocalizer<SharedResources> _sharedLocalizer;

    public BankReconciliationWorkspaceService(
        IBankReconciliationCamtParser camtParser,
        IBankReconciliationCodingRuleService codingRuleService,
        IBankReconciliationDemoDataService demoDataService,
        IBankReconciliationService bankReconciliationService,
        IStringLocalizer<SharedResources> sharedLocalizer)
    {
        _camtParser = camtParser;
        _codingRuleService = codingRuleService;
        _demoDataService = demoDataService;
        _bankReconciliationService = bankReconciliationService;
        _sharedLocalizer = sharedLocalizer;
    }

    public async Task<BankReconciliationSourceContext> ResolveSourceAsync(
        UserSession? user,
        string? sessionFile,
        bool isDemoMode,
        string demoScenarioKey,
        CancellationToken cancellationToken = default)
    {
        if (user?.CompanyId is not Guid companyId || companyId == Guid.Empty)
        {
            return new BankReconciliationSourceContext();
        }

        if (isDemoMode)
        {
            return await ResolveDemoSourceAsync(companyId, demoScenarioKey, sessionFile, cancellationToken);
        }

        if (string.IsNullOrWhiteSpace(sessionFile))
        {
            return new BankReconciliationSourceContext();
        }

        return await ResolveUploadedSourceAsync(companyId, sessionFile, cancellationToken);
    }

    public async Task<BankReconciliationCodingRuleSet> LoadCodingRulesAsync(
        UserSession? user,
        BankReconciliationSourceContext source,
        CancellationToken cancellationToken = default)
    {
        if (user?.CompanyId is not Guid companyId || companyId == Guid.Empty)
        {
            return new BankReconciliationCodingRuleSet();
        }

        return await _codingRuleService.LoadAsync(companyId, source.BankAccountKey ?? "default", cancellationToken);
    }

    public async Task ResetDemoScenarioAsync(
        Guid companyId,
        string scenarioKey,
        UserSession? user,
        CancellationToken cancellationToken = default)
    {
        var normalizedScenario = NormalizeDemoScenarioKey(scenarioKey);
        var scenario = await _demoDataService.LoadScenarioAsync(normalizedScenario, cancellationToken);
        await _bankReconciliationService.ReplaceMatchesAsync(
            companyId,
            BuildDemoStateKey(normalizedScenario),
            user,
            scenario.SeedMatches,
            auditActionType: "replace-matches",
            note: $"Demo scenario reset: {normalizedScenario}",
            cancellationToken: cancellationToken);
    }

    private async Task<BankReconciliationSourceContext> ResolveDemoSourceAsync(
        Guid companyId,
        string demoScenarioKey,
        string? sessionFile,
        CancellationToken cancellationToken)
    {
        var scenario = await _demoDataService.LoadScenarioAsync(demoScenarioKey, cancellationToken);
        var normalizedScenario = NormalizeDemoScenarioKey(scenario.Key);
        var baseSourceLabel = $"{_sharedLocalizer["BankRec_DemoSourceLabel"].Value} · {scenario.Title}";
        var useUploadedCamt = string.Equals(normalizedScenario, "ai-camt-lab", StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(sessionFile);

        if (!useUploadedCamt)
        {
            var builtInDemoStateKey = BuildDemoStateKey(normalizedScenario);
            var demoTransactions = scenario.Data.Transactions.Select(MapDemoTransaction).ToList();
            await ApplyPersistedMatchesAsync(companyId, demoTransactions, builtInDemoStateKey, cancellationToken);
            var demoSource = new BankReconciliationSourceContext
            {
                IsDemoMode = true,
                HasSource = demoTransactions.Count > 0,
                StateKey = builtInDemoStateKey,
                DemoScenarioKey = normalizedScenario,
                SourceLabel = baseSourceLabel,
                Transactions = demoTransactions
            };
            ApplyBankAccountMetadata(demoSource);
            return demoSource;
        }

        var demoStateKey = BuildDemoCamtStateKey(normalizedScenario, sessionFile!);
        List<BankReconciliationParsedTransaction> transactions;
        try
        {
            transactions = await ParseSourceTransactionsAsync(companyId, demoStateKey, sessionFile!, cancellationToken);
        }
        catch (Exception ex) when (ex is System.Xml.XmlException || ex is InvalidOperationException)
        {
            return new BankReconciliationSourceContext
            {
                IsDemoMode = true,
                HasSource = false,
                StateKey = BuildDemoStateKey(normalizedScenario),
                DemoScenarioKey = normalizedScenario,
                SourceLabel = baseSourceLabel,
                ErrorMessage = _sharedLocalizer["Integration_CouldNotReadCamtFile", IntegrationLogSanitizer.Diagnostic(ex.Message)].Value
            };
        }

        if (transactions.Count == 0)
        {
            return new BankReconciliationSourceContext
            {
                IsDemoMode = true,
                HasSource = false,
                StateKey = BuildDemoStateKey(normalizedScenario),
                DemoScenarioKey = normalizedScenario,
                SourceLabel = baseSourceLabel,
                ErrorMessage = _sharedLocalizer["Integration_CamtFileContainsNoTransactions"].Value
            };
        }

        var info = new FileInfo(sessionFile!);
        var source = new BankReconciliationSourceContext
        {
            IsDemoMode = true,
            HasSource = true,
            StateKey = demoStateKey,
            DemoScenarioKey = normalizedScenario,
            SourceLabel = $"{baseSourceLabel} · {info.Name}",
            SourceUpdatedAt = info.LastWriteTime,
            Transactions = transactions
        };
        ApplyBankAccountMetadata(source);
        return source;
    }

    private async Task<BankReconciliationSourceContext> ResolveUploadedSourceAsync(
        Guid companyId,
        string sessionFile,
        CancellationToken cancellationToken)
    {
        try
        {
            var transactions = _camtParser.Parse(sessionFile).ToList();
            if (transactions.Count == 0)
            {
                return new BankReconciliationSourceContext
                {
                    ErrorMessage = _sharedLocalizer["Integration_CamtFileContainsNoTransactions"].Value
                };
            }

            var legacyStateKey = BuildLegacyUploadedStateKey(sessionFile);
            var stateKey = BuildUploadedStateKey(transactions, sessionFile);
            await MigrateUploadedStateAsync(companyId, legacyStateKey, stateKey, cancellationToken);
            await ApplyPersistedMatchesAsync(companyId, transactions, stateKey, cancellationToken);

            var info = new FileInfo(sessionFile);
            var source = new BankReconciliationSourceContext
            {
                HasSource = true,
                StateKey = stateKey,
                SourceLabel = info.Name,
                SourceUpdatedAt = info.LastWriteTime,
                Transactions = transactions
            };
            ApplyBankAccountMetadata(source);
            return source;
        }
        catch (Exception ex) when (ex is System.Xml.XmlException || ex is InvalidOperationException)
        {
            return new BankReconciliationSourceContext
            {
                ErrorMessage = _sharedLocalizer["Integration_CouldNotReadCamtFile", IntegrationLogSanitizer.Diagnostic(ex.Message)].Value
            };
        }
    }

    private async Task MigrateUploadedStateAsync(
        Guid companyId,
        string legacyStateKey,
        string stableStateKey,
        CancellationToken cancellationToken)
    {
        if (string.Equals(legacyStateKey, stableStateKey, StringComparison.Ordinal))
            return;

        var stableState = await _bankReconciliationService.LoadStateAsync(companyId, stableStateKey, cancellationToken);
        if (stableState.Version > 0 || stableState.Matches.Count > 0)
            return;

        var legacyState = await _bankReconciliationService.LoadStateAsync(companyId, legacyStateKey, cancellationToken);
        if (legacyState.Matches.Count == 0)
            return;

        try
        {
            await _bankReconciliationService.ReplaceMatchesAsync(
                companyId,
                stableStateKey,
                user: null,
                legacyState.Matches,
                auditActionType: "migrate-uploaded-source-state",
                expectedVersion: stableState.Version,
                note: "Migrerade bankavstämningens state från filnamn till stabil statement-identitet.",
                cancellationToken: cancellationToken);
        }
        catch (BankReconciliationStateConflictException)
        {
            // Another request completed the same idempotent migration.
        }
    }

    private async Task<List<BankReconciliationParsedTransaction>> ParseSourceTransactionsAsync(
        Guid companyId,
        string stateKey,
        string sessionFile,
        CancellationToken cancellationToken)
    {
        var transactions = _camtParser.Parse(sessionFile).ToList();
        if (transactions.Count > 0)
        {
            await ApplyPersistedMatchesAsync(companyId, transactions, stateKey, cancellationToken);
        }

        return transactions;
    }

    private async Task ApplyPersistedMatchesAsync(
        Guid companyId,
        List<BankReconciliationParsedTransaction> transactions,
        string stateKey,
        CancellationToken cancellationToken)
    {
        var state = await _bankReconciliationService.LoadStateAsync(companyId, stateKey, cancellationToken);
        state = await MigrateLegacyTransactionIdsAsync(companyId, transactions, stateKey, state, cancellationToken);
        foreach (var tx in transactions)
        {
            var matches = state.Matches
                .Where(x => string.Equals(x.TransactionId, tx.Id, StringComparison.OrdinalIgnoreCase))
                .OrderBy(x => x.CreatedAtUtc)
                .ToList();

            tx.Allocations = matches
                .Select(match => new BankReconciliationParsedAllocation
                {
                    AllocationId = match.AllocationId,
                    InvoiceId = match.InvoiceId,
                    MatchType = match.MatchType,
                    MatchRule = match.MatchRule,
                    MatchedAmount = match.MatchedAmount,
                    Currency = match.Currency
                })
                .ToList();

            var primary = matches.FirstOrDefault();
            if (primary is not null)
            {
                tx.MatchedInvoiceId = primary.InvoiceId;
                tx.MatchType = primary.MatchType;
                tx.MatchRule = primary.MatchRule;
                tx.MatchedAmount = matches.Sum(x => x.MatchedAmount);
            }
        }
    }

    private async Task<BankReconciliationPersistedState> MigrateLegacyTransactionIdsAsync(
        Guid companyId,
        IReadOnlyList<BankReconciliationParsedTransaction> transactions,
        string stateKey,
        BankReconciliationPersistedState state,
        CancellationToken cancellationToken)
    {
        var aliases = transactions
            .Where(transaction => !string.IsNullOrWhiteSpace(transaction.LegacyId))
            .ToDictionary(transaction => transaction.LegacyId!, transaction => transaction.Id, StringComparer.OrdinalIgnoreCase);
        var changed = false;
        foreach (var match in state.Matches)
        {
            if (!aliases.TryGetValue(match.TransactionId, out var stableId))
                continue;

            match.TransactionId = stableId;
            changed = true;
        }

        if (!changed)
            return state;

        try
        {
            return await _bankReconciliationService.ReplaceMatchesAsync(
                companyId,
                stateKey,
                user: null,
                state.Matches,
                auditActionType: "migrate-transaction-identities",
                expectedVersion: state.Version,
                note: "Migrerade äldre ordningsbaserade transaktions-id:n till stabila CAMT-identiteter.",
                cancellationToken: cancellationToken);
        }
        catch (BankReconciliationStateConflictException)
        {
            return await _bankReconciliationService.LoadStateAsync(companyId, stateKey, cancellationToken);
        }
    }

    private static void ApplyBankAccountMetadata(BankReconciliationSourceContext source)
    {
        var firstTransaction = source.Transactions.FirstOrDefault(x =>
            !string.IsNullOrWhiteSpace(x.StatementAccountIban)
            || !string.IsNullOrWhiteSpace(x.StatementAccountNumber));
        if (firstTransaction is null)
        {
            return;
        }

        source.BankAccountIban = firstTransaction.StatementAccountIban;
        source.BankAccountNumber = firstTransaction.StatementAccountNumber;
        source.BankAccountOwner = firstTransaction.StatementAccountOwner;
        source.BankAccountBic = firstTransaction.StatementBankBic;
        source.BankAccountKey = BuildBankAccountKey(firstTransaction.StatementAccountIban, firstTransaction.StatementAccountNumber);
        source.BankAccountLabel = BuildBankAccountLabel(
            firstTransaction.StatementAccountOwner,
            firstTransaction.StatementAccountIban,
            firstTransaction.StatementAccountNumber,
            firstTransaction.StatementBankBic);
    }

    private static BankReconciliationParsedTransaction MapDemoTransaction(BankReconciliationDemoTransaction transaction)
    {
        var classification = BankReconciliationTransactionClassifier.Classify(
            "PMNT",
            "RCDT",
            "DMCT",
            transaction.Amount < 0m ? "DBIT" : "CRDT",
            string.IsNullOrWhiteSpace(transaction.Reference) ? null : "SCOR",
            transaction.Remittance,
            transaction.DebtorName);

        return new BankReconciliationParsedTransaction
        {
            Id = transaction.Id,
            Date = transaction.Date,
            ValueDate = transaction.Date,
            EntryStatus = "BOOK",
            Amount = transaction.Amount,
            Currency = transaction.Currency,
            Reference = transaction.Reference,
            EndToEndId = transaction.EndToEndId,
            DebtorName = transaction.DebtorName,
            Remittance = transaction.Remittance,
            Direction = transaction.Amount < 0m ? "DBIT" : "CRDT",
            Domn = "PMNT",
            Fmly = "RCDT",
            SubFmly = "DMCT",
            ScorType = string.IsNullOrWhiteSpace(transaction.Reference) ? null : "SCOR",
            Classification = classification,
            Group = classification.LegacyGroup,
            ClassificationRule = classification.LegacyRule,
            ReferenceCandidates = BuildDemoReferenceCandidates(transaction)
        };
    }

    private static List<BankReconciliationReferenceCandidate> BuildDemoReferenceCandidates(
        BankReconciliationDemoTransaction transaction)
    {
        var candidates = new List<BankReconciliationReferenceCandidate>();
        AddDemoReference(candidates, "Demo/Reference", transaction.Reference, "creditor-reference");
        AddDemoReference(candidates, "Demo/Remittance", transaction.Remittance, "unstructured-remittance");
        AddDemoReference(candidates, "Demo/EndToEndId", transaction.EndToEndId, "end-to-end-id");
        return candidates;
    }

    private static void AddDemoReference(
        ICollection<BankReconciliationReferenceCandidate> candidates,
        string sourcePath,
        string? value,
        string candidateType)
    {
        if (string.IsNullOrWhiteSpace(value))
            return;

        candidates.Add(new BankReconciliationReferenceCandidate
        {
            SourcePath = sourcePath,
            RawValue = value.Trim(),
            NormalizedValue = new string(value.Where(char.IsLetterOrDigit).Select(char.ToUpperInvariant).ToArray()),
            CandidateType = candidateType
        });
    }

    private static string BuildBankAccountKey(string? iban, string? accountNumber)
    {
        var candidate = !string.IsNullOrWhiteSpace(iban)
            ? iban
            : accountNumber;
        return string.IsNullOrWhiteSpace(candidate) ? "default" : candidate.Trim().ToUpperInvariant();
    }

    private static string BuildBankAccountLabel(string? owner, string? iban, string? accountNumber, string? bic)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(owner)) parts.Add(owner.Trim());
        if (!string.IsNullOrWhiteSpace(iban)) parts.Add(iban.Trim());
        else if (!string.IsNullOrWhiteSpace(accountNumber)) parts.Add(accountNumber.Trim());
        if (!string.IsNullOrWhiteSpace(bic)) parts.Add(bic.Trim());
        return parts.Count == 0 ? "Okänt bankkonto" : string.Join(" · ", parts);
    }

    private static string BuildUploadedStateKey(
        IReadOnlyList<BankReconciliationParsedTransaction> transactions,
        string sessionFile)
    {
        var first = transactions.FirstOrDefault();
        if (first is null || string.IsNullOrWhiteSpace(first.StatementId))
            return BuildLegacyUploadedStateKey(sessionFile);

        var canonical = string.Join('\u001f', new[]
        {
            first.StatementAccountIban,
            first.StatementAccountNumber,
            first.StatementId
        }.Select(value => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim().ToUpperInvariant()));
        var fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
        return $"bankrec-uploaded-statement-v2:{fingerprint}";
    }

    private static string BuildLegacyUploadedStateKey(string sessionFile) => sessionFile;

    private static string BuildDemoStateKey(string scenarioKey)
        => $"bankrec-demo-state-v1:{NormalizeDemoScenarioKey(scenarioKey)}";

    private static string BuildDemoCamtStateKey(string scenarioKey, string sessionFile)
        => $"{BuildDemoStateKey(scenarioKey)}:{sessionFile}";

    private static string NormalizeDemoScenarioKey(string? scenarioKey)
        => string.IsNullOrWhiteSpace(scenarioKey) ? "ai-camt-lab" : scenarioKey.Trim().ToLowerInvariant();
}
