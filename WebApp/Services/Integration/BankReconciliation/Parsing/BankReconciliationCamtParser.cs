using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using WebApp.Models.Integration;

namespace WebApp.Services.Integration.BankReconciliation;

// CAMT parser preserves source hierarchy and exposes a compatible flattened transaction view.
public sealed class BankReconciliationCamtParser : IBankReconciliationCamtParser
{
    private static readonly Regex WhitespaceRegex = new(@"\s+", RegexOptions.Compiled);
    private static readonly Regex ReferenceNormalizerRegex = new(@"[\s\-_/\.]", RegexOptions.Compiled);

    public IReadOnlyList<BankReconciliationParsedTransaction> Parse(string filePath)
        => ParseDocument(filePath).Transactions;

    public BankReconciliationParsedDocument ParseDocument(string filePath)
    {
        var settings = new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null
        };

        using var reader = XmlReader.Create(filePath, settings);
        var source = XDocument.Load(reader, LoadOptions.None);
        var ns = source.Root?.Name.Namespace ?? XNamespace.None;
        var document = new BankReconciliationParsedDocument();
        var fingerprintOccurrences = new Dictionary<string, int>(StringComparer.Ordinal);
        var legacySequence = 0;

        foreach (var statementElement in source.Descendants(ns + "Stmt"))
        {
            var statement = ParseStatement(statementElement, ns);
            document.Statements.Add(statement);

            foreach (var entryDetails in statementElement.Elements(ns + "Ntry"))
            {
                var entry = ParseEntry(entryDetails, ns);
                statement.Entries.Add(entry);

                foreach (var detailsElement in entryDetails.Elements(ns + "NtryDtls"))
                {
                    var batches = detailsElement.Elements(ns + "Btch")
                        .Select(batch => ParseBatch(batch, ns))
                        .ToList();
                    entry.Batches.AddRange(batches);
                    var batch = batches.FirstOrDefault();

                    foreach (var transactionElement in detailsElement.Elements(ns + "TxDtls"))
                    {
                        legacySequence += 1;
                        var transaction = ParseTransaction(
                            transactionElement,
                            ns,
                            statement,
                            entry,
                            batch);
                        transaction.LegacyId = $"TX-{legacySequence:000}";
                        var baseFingerprint = BankReconciliationCamtTransactionIdentity.CreateBaseFingerprint(transaction);
                        var occurrence = fingerprintOccurrences.TryGetValue(baseFingerprint, out var previousOccurrence)
                            ? previousOccurrence + 1
                            : 1;
                        fingerprintOccurrences[baseFingerprint] = occurrence;
                        BankReconciliationCamtTransactionIdentity.Assign(transaction, baseFingerprint, occurrence);
                        entry.Transactions.Add(transaction);
                    }
                }
            }
        }

        return document;
    }

    private static BankReconciliationParsedStatement ParseStatement(XElement statement, XNamespace ns)
    {
        var account = statement.Element(ns + "Acct");
        var financialInstitution = account?.Element(ns + "Svcr")?.Element(ns + "FinInstnId");
        return new BankReconciliationParsedStatement
        {
            StatementId = NormalizeText(statement.Element(ns + "Id")?.Value),
            ElectronicSequenceNumber = NormalizeText(statement.Element(ns + "ElctrncSeqNb")?.Value),
            LegalSequenceNumber = NormalizeText(statement.Element(ns + "LglSeqNb")?.Value),
            CreatedAt = NormalizeText(statement.Element(ns + "CreDtTm")?.Value),
            AccountIban = NormalizeText(account?.Element(ns + "Id")?.Element(ns + "IBAN")?.Value),
            AccountNumber = NormalizeText(account?.Element(ns + "Id")?.Element(ns + "Othr")?.Element(ns + "Id")?.Value),
            AccountCurrency = NormalizeText(account?.Element(ns + "Ccy")?.Value),
            AccountOwner = NormalizeText(account?.Element(ns + "Ownr")?.Element(ns + "Nm")?.Value),
            BankBic = NormalizeText(financialInstitution?.Element(ns + "BIC")?.Value)
                ?? NormalizeText(financialInstitution?.Element(ns + "BICFI")?.Value),
            Balances = statement.Elements(ns + "Bal").Select(balance => ParseBalance(balance, ns)).ToList()
        };
    }

    private static BankReconciliationParsedBalance ParseBalance(XElement balance, XNamespace ns)
    {
        var amount = balance.Element(ns + "Amt");
        return new BankReconciliationParsedBalance
        {
            TypeCode = NormalizeText(balance.Element(ns + "Tp")?.Element(ns + "CdOrPrtry")?.Element(ns + "Cd")?.Value)
                ?? NormalizeText(balance.Element(ns + "Tp")?.Element(ns + "CdOrPrtry")?.Element(ns + "Prtry")?.Value),
            Amount = ParseAmount(amount?.Value),
            Currency = NormalizeText(amount?.Attribute("Ccy")?.Value),
            Direction = NormalizeText(balance.Element(ns + "CdtDbtInd")?.Value),
            Date = NormalizeText(balance.Element(ns + "Dt")?.Element(ns + "Dt")?.Value)
        };
    }

    private static BankReconciliationParsedEntry ParseEntry(XElement entry, XNamespace ns)
    {
        var amount = entry.Element(ns + "Amt");
        var bankCode = entry.Element(ns + "BkTxCd")?.Element(ns + "Domn");
        return new BankReconciliationParsedEntry
        {
            EntryReference = NormalizeText(entry.Element(ns + "NtryRef")?.Value),
            AccountServiceReference = NormalizeText(entry.Element(ns + "AcctSvcrRef")?.Value),
            Status = NormalizeText(entry.Element(ns + "Sts")?.Value),
            Direction = NormalizeText(entry.Element(ns + "CdtDbtInd")?.Value),
            Amount = ParseAmount(amount?.Value),
            Currency = NormalizeText(amount?.Attribute("Ccy")?.Value),
            BookingDate = NormalizeText(entry.Element(ns + "BookgDt")?.Element(ns + "Dt")?.Value),
            ValueDate = NormalizeText(entry.Element(ns + "ValDt")?.Element(ns + "Dt")?.Value),
            DomainCode = NormalizeText(bankCode?.Element(ns + "Cd")?.Value),
            FamilyCode = NormalizeText(bankCode?.Element(ns + "Fmly")?.Element(ns + "Cd")?.Value),
            SubFamilyCode = NormalizeText(bankCode?.Element(ns + "Fmly")?.Element(ns + "SubFmlyCd")?.Value)
        };
    }

    private static BankReconciliationParsedBatch ParseBatch(XElement batch, XNamespace ns)
        => new()
        {
            MessageId = NormalizeText(batch.Element(ns + "MsgId")?.Value),
            PaymentInformationId = NormalizeText(batch.Element(ns + "PmtInfId")?.Value),
            DeclaredTransactionCount = int.TryParse(
                NormalizeText(batch.Elements(ns + "NbOfTxs").FirstOrDefault()?.Value),
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out var count)
                ? count
                : null
        };

    private static BankReconciliationParsedTransaction ParseTransaction(
        XElement detail,
        XNamespace ns,
        BankReconciliationParsedStatement statement,
        BankReconciliationParsedEntry entry,
        BankReconciliationParsedBatch? batch)
    {
        var amountNode = detail.Element(ns + "AmtDtls")?.Element(ns + "TxAmt")?.Element(ns + "Amt");
        var amount = ParseAmount(amountNode?.Value);
        if (string.Equals(entry.Direction, "DBIT", StringComparison.OrdinalIgnoreCase))
            amount = -Math.Abs(amount);

        var references = ParseReferences(detail, ns);
        var refs = detail.Element(ns + "Refs");
        var endToEndId = NormalizeText(refs?.Element(ns + "EndToEndId")?.Value);
        var txId = NormalizeText(refs?.Element(ns + "TxId")?.Value);
        var accountServiceReference = NormalizeText(refs?.Element(ns + "AcctSvcrRef")?.Value);
        var instructionId = NormalizeText(refs?.Element(ns + "InstrId")?.Value);
        var paymentInformationId = NormalizeText(refs?.Element(ns + "PmtInfId")?.Value);
        var remittanceInfo = detail.Element(ns + "RmtInf");
        var remittanceAllocations = ParseRemittanceAllocations(remittanceInfo, ns);
        var remittanceParts = references
            .Where(reference => reference.SourcePath.StartsWith("TxDtls/RmtInf/", StringComparison.Ordinal))
            .Select(reference => reference.RawValue)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var remittance = remittanceParts.Length == 0 ? null : string.Join(" | ", remittanceParts);

        var relatedParties = detail.Element(ns + "RltdPties");
        var ultimateDebtor = relatedParties?.Element(ns + "UltmtDbtr");
        var debtor = relatedParties?.Element(ns + "Dbtr");
        var ultimateCreditor = relatedParties?.Element(ns + "UltmtCdtr");
        var creditor = relatedParties?.Element(ns + "Cdtr");
        var debtorName = NormalizeText(ultimateDebtor?.Element(ns + "Nm")?.Value)
            ?? NormalizeText(debtor?.Element(ns + "Nm")?.Value);
        var creditorName = NormalizeText(ultimateCreditor?.Element(ns + "Nm")?.Value)
            ?? NormalizeText(creditor?.Element(ns + "Nm")?.Value);

        var transactionBankCode = detail.Element(ns + "BkTxCd")?.Element(ns + "Domn");
        var domain = NormalizeText(transactionBankCode?.Element(ns + "Cd")?.Value) ?? entry.DomainCode;
        var family = NormalizeText(transactionBankCode?.Element(ns + "Fmly")?.Element(ns + "Cd")?.Value) ?? entry.FamilyCode;
        var subFamily = NormalizeText(transactionBankCode?.Element(ns + "Fmly")?.Element(ns + "SubFmlyCd")?.Value) ?? entry.SubFamilyCode;
        var scorType = NormalizeText(remittanceInfo?
            .Element(ns + "Strd")?
            .Element(ns + "CdtrRefInf")?
            .Element(ns + "Tp")?
            .Element(ns + "CdOrPrtry")?
            .Element(ns + "Cd")?
            .Value);
        var reference = SelectPrimaryReference(references);
        var classification = BankReconciliationTransactionClassifier.Classify(
            domain,
            family,
            subFamily,
            entry.Direction,
            scorType,
            remittance,
            debtorName);

        return new BankReconciliationParsedTransaction
        {
            StatementId = statement.StatementId,
            StatementAccountIban = statement.AccountIban,
            StatementAccountNumber = statement.AccountNumber,
            StatementAccountOwner = statement.AccountOwner,
            StatementBankBic = statement.BankBic,
            EntryReference = entry.EntryReference,
            EntryAccountServiceReference = entry.AccountServiceReference,
            EntryStatus = entry.Status,
            BatchMessageId = batch?.MessageId,
            BatchPaymentInformationId = batch?.PaymentInformationId,
            InstructionId = instructionId,
            PaymentInformationId = paymentInformationId,
            Date = entry.BookingDate,
            ValueDate = entry.ValueDate,
            Amount = amount,
            Currency = NormalizeText(amountNode?.Attribute("Ccy")?.Value) ?? entry.Currency ?? statement.AccountCurrency ?? string.Empty,
            Reference = reference,
            EndToEndId = endToEndId,
            TxId = txId,
            AcctSvcrRef = accountServiceReference,
            DebtorName = debtorName,
            DebtorOrganizationId = ParsePartyId(ultimateDebtor, ns) ?? ParsePartyId(debtor, ns),
            DebtorAccountId = ParseAccountId(relatedParties?.Element(ns + "DbtrAcct"), ns),
            CreditorName = creditorName,
            CreditorOrganizationId = ParsePartyId(ultimateCreditor, ns) ?? ParsePartyId(creditor, ns),
            CreditorAccountId = ParseAccountId(relatedParties?.Element(ns + "CdtrAcct"), ns),
            CounterpartyName = string.Equals(entry.Direction, "DBIT", StringComparison.OrdinalIgnoreCase) ? creditorName : debtorName,
            Remittance = remittance,
            Direction = entry.Direction,
            Domn = domain,
            Fmly = family,
            SubFmly = subFamily,
            ScorType = scorType,
            Classification = classification,
            Group = classification.LegacyGroup,
            ClassificationRule = classification.LegacyRule,
            ReferenceCandidates = references,
            RemittanceAllocations = remittanceAllocations
        };
    }

    private static List<BankReconciliationReferenceCandidate> ParseReferences(XElement detail, XNamespace ns)
    {
        var references = new List<BankReconciliationReferenceCandidate>();
        var refs = detail.Element(ns + "Refs");
        AddReference(references, "TxDtls/Refs/EndToEndId", refs?.Element(ns + "EndToEndId")?.Value, "end-to-end-id");
        AddReference(references, "TxDtls/Refs/TxId", refs?.Element(ns + "TxId")?.Value, "transaction-id");
        AddReference(references, "TxDtls/Refs/AcctSvcrRef", refs?.Element(ns + "AcctSvcrRef")?.Value, "account-service-reference");
        foreach (var proprietaryRef in refs?.Elements(ns + "Prtry") ?? Enumerable.Empty<XElement>())
            AddReference(references, "TxDtls/Refs/Prtry/Ref", proprietaryRef.Element(ns + "Ref")?.Value, "bank-proprietary-reference");

        var remittance = detail.Element(ns + "RmtInf");
        foreach (var creditorReference in remittance?.Descendants(ns + "CdtrRefInf") ?? Enumerable.Empty<XElement>())
            AddReference(references, "TxDtls/RmtInf/Strd/CdtrRefInf/Ref", creditorReference.Element(ns + "Ref")?.Value, "creditor-reference");
        foreach (var documentInfo in remittance?.Descendants(ns + "RfrdDocInf") ?? Enumerable.Empty<XElement>())
            AddReference(references, "TxDtls/RmtInf/Strd/RfrdDocInf/Nb", documentInfo.Element(ns + "Nb")?.Value, "referred-document-number");
        foreach (var unstructured in remittance?.Elements(ns + "Ustrd") ?? Enumerable.Empty<XElement>())
            AddReference(references, "TxDtls/RmtInf/Ustrd", unstructured.Value, "unstructured-remittance");
        foreach (var additional in remittance?.Descendants(ns + "AddtlRmtInf") ?? Enumerable.Empty<XElement>())
            AddReference(references, "TxDtls/RmtInf/Strd/AddtlRmtInf", additional.Value, "additional-remittance");
        return references;
    }

    private static List<BankReconciliationParsedRemittanceAllocation> ParseRemittanceAllocations(XElement? remittance, XNamespace ns)
    {
        if (remittance is null) return new List<BankReconciliationParsedRemittanceAllocation>();

        return remittance.Elements(ns + "Strd")
            .Select(structured =>
            {
                var documentInfo = structured.Element(ns + "RfrdDocInf");
                var creditorReference = structured.Element(ns + "CdtrRefInf")?.Element(ns + "Ref");
                var referredDocumentAmount = structured.Element(ns + "RfrdDocAmt");
                var remittedAmount = referredDocumentAmount?.Element(ns + "RmtdAmt");
                var creditNoteAmount = referredDocumentAmount?.Element(ns + "CdtNoteAmt");
                return new BankReconciliationParsedRemittanceAllocation
                {
                    DocumentTypeCode = NormalizeText(documentInfo?.Element(ns + "Tp")?.Element(ns + "CdOrPrtry")?.Element(ns + "Cd")?.Value),
                    DocumentNumber = NormalizeText(documentInfo?.Element(ns + "Nb")?.Value),
                    CreditorReference = NormalizeText(creditorReference?.Value),
                    RemittedAmount = TryParseAmount(remittedAmount?.Value),
                    CreditNoteAmount = TryParseAmount(creditNoteAmount?.Value),
                    Currency = NormalizeText((remittedAmount ?? creditNoteAmount)?.Attribute("Ccy")?.Value),
                    AdditionalInformation = JoinText(structured.Elements(ns + "AddtlRmtInf").Select(node => node.Value))
                };
            })
            .Where(allocation => allocation.DocumentNumber is not null
                || allocation.CreditorReference is not null
                || allocation.RemittedAmount.HasValue
                || allocation.CreditNoteAmount.HasValue
                || allocation.AdditionalInformation is not null)
            .ToList();
    }

    private static string? SelectPrimaryReference(IReadOnlyList<BankReconciliationReferenceCandidate> references)
    {
        var priorities = new[]
        {
            "creditor-reference",
            "referred-document-number",
            "unstructured-remittance",
            "end-to-end-id",
            "transaction-id",
            "account-service-reference",
            "bank-proprietary-reference"
        };
        foreach (var priority in priorities)
        {
            var value = references.FirstOrDefault(reference => reference.CandidateType == priority)?.RawValue;
            if (!string.IsNullOrWhiteSpace(value)) return value;
        }
        return null;
    }

    private static string? ParsePartyId(XElement? party, XNamespace ns)
        => NormalizeText(party?.Element(ns + "Id")?.Element(ns + "OrgId")?.Element(ns + "Othr")?.Element(ns + "Id")?.Value)
            ?? NormalizeText(party?.Element(ns + "Id")?.Element(ns + "PrvtId")?.Element(ns + "Othr")?.Element(ns + "Id")?.Value);

    private static string? ParseAccountId(XElement? account, XNamespace ns)
        => NormalizeText(account?.Element(ns + "Id")?.Element(ns + "IBAN")?.Value)
            ?? NormalizeText(account?.Element(ns + "Id")?.Element(ns + "Othr")?.Element(ns + "Id")?.Value);

    private static void AddReference(
        ICollection<BankReconciliationReferenceCandidate> references,
        string sourcePath,
        string? rawValue,
        string candidateType)
    {
        var normalizedText = NormalizeText(rawValue);
        if (string.IsNullOrWhiteSpace(normalizedText)) return;

        references.Add(new BankReconciliationReferenceCandidate
        {
            SourcePath = sourcePath,
            RawValue = normalizedText,
            NormalizedValue = NormalizeReference(normalizedText),
            CandidateType = candidateType
        });
    }

    private static decimal ParseAmount(string? value) => TryParseAmount(value) ?? 0m;

    private static decimal? TryParseAmount(string? value)
        => decimal.TryParse(NormalizeText(value), NumberStyles.Any, CultureInfo.InvariantCulture, out var amount)
            ? amount
            : null;

    private static string? JoinText(IEnumerable<string?> values)
    {
        var parts = values.Select(NormalizeText).Where(value => value is not null).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        return parts.Length == 0 ? null : string.Join(" | ", parts!);
    }

    private static string? NormalizeText(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : WhitespaceRegex.Replace(value, " ").Trim();

    private static string NormalizeReference(string value)
        => ReferenceNormalizerRegex.Replace(value, string.Empty).ToUpperInvariant();
}
