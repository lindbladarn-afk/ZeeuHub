using System.Globalization;
using System.Xml;
using WebApp.Services.Integration.BankReconciliation;

namespace WebApp.Tests;

// Parser tests keep CAMT ingestion deterministic before the matching engine evaluates candidates.
public sealed class BankReconciliationCamtParserTests : IDisposable
{
    private readonly string _tempDirectory = Path.Combine(Path.GetTempPath(), $"bankrec-camt-{Guid.NewGuid():N}");
    private readonly BankReconciliationCamtParser _parser = new();

    public BankReconciliationCamtParserTests()
    {
        Directory.CreateDirectory(_tempDirectory);
    }

    [Fact]
    public void Parse_CamtReferences_ExtractsCandidatesFromStructuredAndUnstructuredFields()
    {
        var filePath = WriteTempFile("references.xml", """
            <?xml version="1.0" encoding="utf-8"?>
            <Document xmlns="urn:iso:std:iso:20022:tech:xsd:camt.053.001.02">
              <BkToCstmrStmt>
                <Stmt>
                  <Ntry>
                    <Amt Ccy="SEK">100.00</Amt>
                    <CdtDbtInd>CRDT</CdtDbtInd>
                    <BookgDt><Dt>2026-04-24</Dt></BookgDt>
                    <ValDt><Dt>2026-04-24</Dt></ValDt>
                    <BkTxCd><Domn><Cd>PMNT</Cd><Fmly><Cd>RCDT</Cd><SubFmlyCd>ESCT</SubFmlyCd></Fmly></Domn></BkTxCd>
                    <NtryDtls>
                      <TxDtls>
                        <Refs>
                          <EndToEndId>NOTPROVIDED</EndToEndId>
                          <TxId>TX-ABC</TxId>
                          <AcctSvcrRef>ASR-123</AcctSvcrRef>
                          <Prtry><Ref>BANK-777</Ref></Prtry>
                        </Refs>
                        <AmtDtls><TxAmt><Amt Ccy="SEK">100.00</Amt></TxAmt></AmtDtls>
                        <RmtInf>
                          <Ustrd>CINV 462166596</Ustrd>
                          <Strd>
                            <CdtrRefInf>
                              <Tp><CdOrPrtry><Cd>SCOR</Cd></CdOrPrtry></Tp>
                              <Ref>OCR-123</Ref>
                            </CdtrRefInf>
                            <RfrdDocInf><Nb>INV-456</Nb></RfrdDocInf>
                            <AddtlRmtInf>Extra reference 789</AddtlRmtInf>
                          </Strd>
                        </RmtInf>
                        <RltdPties><Dbtr><Nm>Example Customer AB</Nm></Dbtr></RltdPties>
                      </TxDtls>
                    </NtryDtls>
                  </Ntry>
                </Stmt>
              </BkToCstmrStmt>
            </Document>
            """);

        var transactions = _parser.Parse(filePath);

        var transaction = Assert.Single(transactions);
        Assert.Equal(100m, transaction.Amount);
        Assert.Equal("Kundinbetalningar", transaction.Group);
        Assert.Equal("Bankinbetalningar", transaction.Classification.TypeLabel);
        Assert.Equal("bankinbetalningar", transaction.Classification.TypeKey);
        Assert.Equal("OCR-123", transaction.Reference);
        Assert.Equal("Example Customer AB", transaction.DebtorName);
        Assert.Contains(transaction.ReferenceCandidates, candidate => candidate.SourcePath == "TxDtls/RmtInf/Strd/CdtrRefInf/Ref" && candidate.RawValue == "OCR-123");
        Assert.Contains(transaction.ReferenceCandidates, candidate => candidate.SourcePath == "TxDtls/RmtInf/Strd/RfrdDocInf/Nb" && candidate.RawValue == "INV-456");
        Assert.Contains(transaction.ReferenceCandidates, candidate => candidate.SourcePath == "TxDtls/RmtInf/Ustrd" && candidate.RawValue == "CINV 462166596");
        Assert.Contains(transaction.ReferenceCandidates, candidate => candidate.SourcePath == "TxDtls/RmtInf/Strd/AddtlRmtInf" && candidate.RawValue == "Extra reference 789");
        Assert.Contains(transaction.ReferenceCandidates, candidate => candidate.SourcePath == "TxDtls/Refs/Prtry/Ref" && candidate.RawValue == "BANK-777");
    }

    [Fact]
    public void Parse_DtdInput_RejectsFile()
    {
        var filePath = WriteTempFile("dtd.xml", """
            <?xml version="1.0" encoding="utf-8"?>
            <!DOCTYPE Document [
              <!ENTITY test "blocked">
            ]>
            <Document xmlns="urn:iso:std:iso:20022:tech:xsd:camt.053.001.02">
              <BkToCstmrStmt />
            </Document>
            """);

        Assert.Throws<XmlException>(() => _parser.Parse(filePath));
    }

    [Fact]
    public void Parse_AiCamtLabFixture_ReadsExpectedCustomerPayments()
    {
        var webAppRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../WebApp"));
        var filePath = Path.Combine(webAppRoot, "Data", "Integration", "BankReconciliation", "demo", "ai-camt-lab.camt053.xml");

        var transactions = _parser.Parse(filePath);

        Assert.Equal(14, transactions.Count);
        Assert.Contains(transactions, x => x.Amount == 11396.00m && x.Reference == "873550016" && x.DebtorName == "Pelles Butik AB");
        Assert.All(transactions, x => Assert.Equal("Kundinbetalningar", x.Group));
        Assert.All(transactions, x => Assert.Equal("Bankinbetalningar", x.Classification.TypeLabel));
        Assert.Contains(transactions, x => x.Remittance?.Contains("NO-MATCH-001", StringComparison.OrdinalIgnoreCase) == true);
        Assert.Equal(10000m, transactions.Where(x => x.Reference == "992000110").Sum(x => x.Amount));
        Assert.Equal(7250m, transactions.Where(x => x.Reference == "992000129").Sum(x => x.Amount));
        Assert.Equal(4999.50m, transactions.Where(x => x.Reference == "992000137").Sum(x => x.Amount));
    }

    [Fact]
    public void ParseDocument_ExtendedFields_PreservesStatementEntryBatchAndRemittanceAllocation()
    {
        var filePath = WriteTempFile("extended.nda", BuildHierarchicalDocument(TransactionDetail("TX-1", "OCR-1", 125m)));

        var document = _parser.ParseDocument(filePath);

        var statement = Assert.Single(document.Statements);
        Assert.Equal("STMT-1", statement.StatementId);
        Assert.Equal("7", statement.ElectronicSequenceNumber);
        Assert.Equal("SEK", statement.AccountCurrency);
        Assert.Equal(2, statement.Balances.Count);

        var entry = Assert.Single(statement.Entries);
        Assert.Equal("ENTRY-1", entry.EntryReference);
        Assert.Equal("BOOK", entry.Status);
        Assert.Equal(125m, entry.Amount);

        var batch = Assert.Single(entry.Batches);
        Assert.Equal("BATCH-1", batch.MessageId);
        Assert.Equal("PAYMENT-1", batch.PaymentInformationId);
        Assert.Equal(1, batch.DeclaredTransactionCount);

        var transaction = Assert.Single(entry.Transactions);
        Assert.StartsWith("TX-", transaction.Id, StringComparison.Ordinal);
        Assert.Equal("TX-001", transaction.LegacyId);
        Assert.Equal(27, transaction.Id.Length);
        Assert.Equal(64, transaction.SourceFingerprint.Length);
        Assert.Equal("ENTRY-1", transaction.EntryReference);
        Assert.Equal("BOOK", transaction.EntryStatus);
        Assert.Equal("BATCH-1", transaction.BatchMessageId);
        Assert.Equal("INSTRUCTION-TX-1", transaction.InstructionId);
        Assert.Equal("Customer AB", transaction.CounterpartyName);
        Assert.Equal("5560000000", transaction.DebtorOrganizationId);
        Assert.Equal("BG-100", transaction.DebtorAccountId);

        var allocation = Assert.Single(transaction.RemittanceAllocations);
        Assert.Equal("CINV", allocation.DocumentTypeCode);
        Assert.Equal("INV-TX-1", allocation.DocumentNumber);
        Assert.Equal("OCR-1", allocation.CreditorReference);
        Assert.Equal(125m, allocation.RemittedAmount);
        Assert.Equal("SEK", allocation.Currency);
    }

    [Fact]
    public void Parse_TransactionOrderChanges_KeepsStableHubIdentifiers()
    {
        var firstPath = WriteTempFile(
            "first.xml",
            BuildHierarchicalDocument(
                TransactionDetail("TX-A", "OCR-A", 50m),
                TransactionDetail("TX-B", "OCR-B", 75m)));
        var secondPath = WriteTempFile(
            "second.xml",
            BuildHierarchicalDocument(
                TransactionDetail("TX-B", "OCR-B", 75m),
                TransactionDetail("TX-A", "OCR-A", 50m)));

        var first = _parser.Parse(firstPath).ToDictionary(transaction => transaction.TxId!, transaction => transaction.Id);
        var second = _parser.Parse(secondPath).ToDictionary(transaction => transaction.TxId!, transaction => transaction.Id);

        Assert.Equal(first["TX-A"], second["TX-A"]);
        Assert.Equal(first["TX-B"], second["TX-B"]);
        Assert.NotEqual(first["TX-A"], first["TX-B"]);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, recursive: true);
        }
    }

    private string WriteTempFile(string fileName, string content)
    {
        var path = Path.Combine(_tempDirectory, fileName);
        File.WriteAllText(path, content);
        return path;
    }

    private static string BuildHierarchicalDocument(params string[] transactionDetails) => $$"""
        <?xml version="1.0" encoding="utf-8"?>
        <Document xmlns="urn:iso:std:iso:20022:tech:xsd:camt.053.001.02">
          <BkToCstmrStmt>
            <Stmt>
              <Id>STMT-1</Id>
              <ElctrncSeqNb>7</ElctrncSeqNb>
              <Acct><Id><IBAN>SE0000000000000000000000</IBAN></Id><Ccy>SEK</Ccy><Ownr><Nm>Example AB</Nm></Ownr></Acct>
              <Bal><Tp><CdOrPrtry><Cd>OPBD</Cd></CdOrPrtry></Tp><Amt Ccy="SEK">100.00</Amt><CdtDbtInd>CRDT</CdtDbtInd><Dt><Dt>2026-05-11</Dt></Dt></Bal>
              <Bal><Tp><CdOrPrtry><Cd>CLBD</Cd></CdOrPrtry></Tp><Amt Ccy="SEK">225.00</Amt><CdtDbtInd>CRDT</CdtDbtInd><Dt><Dt>2026-05-12</Dt></Dt></Bal>
              <Ntry>
                <NtryRef>ENTRY-1</NtryRef><AcctSvcrRef>ENTRY-ASR-1</AcctSvcrRef>
                <Amt Ccy="SEK">125.00</Amt><CdtDbtInd>CRDT</CdtDbtInd><Sts>BOOK</Sts>
                <BookgDt><Dt>2026-05-12</Dt></BookgDt><ValDt><Dt>2026-05-12</Dt></ValDt>
                <BkTxCd><Domn><Cd>PMNT</Cd><Fmly><Cd>RCDT</Cd><SubFmlyCd>DMCT</SubFmlyCd></Fmly></Domn></BkTxCd>
                <NtryDtls>
                  <Btch><MsgId>BATCH-1</MsgId><PmtInfId>PAYMENT-1</PmtInfId><NbOfTxs>{{transactionDetails.Length}}</NbOfTxs></Btch>
                  {{string.Join(Environment.NewLine, transactionDetails)}}
                </NtryDtls>
              </Ntry>
            </Stmt>
          </BkToCstmrStmt>
        </Document>
        """;

    private static string TransactionDetail(string transactionId, string creditorReference, decimal amount) => $$"""
        <TxDtls>
          <Refs><InstrId>INSTRUCTION-{{transactionId}}</InstrId><EndToEndId>END-{{transactionId}}</EndToEndId><TxId>{{transactionId}}</TxId><AcctSvcrRef>ASR-{{transactionId}}</AcctSvcrRef></Refs>
          <AmtDtls><TxAmt><Amt Ccy="SEK">{{amount.ToString(CultureInfo.InvariantCulture)}}</Amt></TxAmt></AmtDtls>
          <RltdPties>
            <Dbtr><Nm>Customer AB</Nm><Id><OrgId><Othr><Id>5560000000</Id></Othr></OrgId></Id></Dbtr>
            <DbtrAcct><Id><Othr><Id>BG-100</Id></Othr></Id></DbtrAcct>
          </RltdPties>
          <RmtInf><Strd>
            <RfrdDocInf><Tp><CdOrPrtry><Cd>CINV</Cd></CdOrPrtry></Tp><Nb>INV-{{transactionId}}</Nb></RfrdDocInf>
            <RfrdDocAmt><RmtdAmt Ccy="SEK">{{amount.ToString(CultureInfo.InvariantCulture)}}</RmtdAmt></RfrdDocAmt>
            <CdtrRefInf><Tp><CdOrPrtry><Cd>SCOR</Cd></CdOrPrtry></Tp><Ref>{{creditorReference}}</Ref></CdtrRefInf>
          </Strd></RmtInf>
        </TxDtls>
        """;
}
