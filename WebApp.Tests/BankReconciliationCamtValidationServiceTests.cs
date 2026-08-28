using WebApp.Services.Integration.BankReconciliation.Validation;

namespace WebApp.Tests;

// CAMT validation tests protect accounting integrity before uploaded statements become active.
public sealed class BankReconciliationCamtValidationServiceTests : IDisposable
{
    private readonly string _tempDirectory = Path.Combine(Path.GetTempPath(), $"bankrec-validation-{Guid.NewGuid():N}");
    private readonly BankReconciliationCamtValidationService _service = new();

    public BankReconciliationCamtValidationServiceTests()
    {
        Directory.CreateDirectory(_tempDirectory);
    }

    [Fact]
    public void Validate_ConsistentExtendedStatement_ReturnsValidResult()
    {
        var result = _service.Validate(Write("valid.nda", ValidDocument()));

        Assert.True(result.IsValid);
        Assert.Equal(1, result.StatementCount);
        Assert.Equal(1, result.TransactionCount);
        Assert.Empty(result.Issues);
    }

    [Fact]
    public void Validate_BundledAiCamtLabDemo_ReturnsValidResult()
    {
        var webAppRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../WebApp"));
        var filePath = Path.Combine(
            webAppRoot,
            "Data",
            "Integration",
            "BankReconciliation",
            "demo",
            "ai-camt-lab.camt053.xml");

        var result = _service.Validate(filePath);

        Assert.True(result.IsValid, string.Join("; ", result.Issues.Select(issue => issue.Message)));
        Assert.Equal("camt.053.001.02", result.CamtVersion);
        Assert.Equal("ZEEU-AI-CAMT-LAB", result.StatementId);
        Assert.Equal(1, result.StatementCount);
        Assert.Equal(14, result.EntryCount);
        Assert.Equal(14, result.TransactionCount);
        Assert.Equal(14, result.BookedEntryCount);
        Assert.Equal(0, result.BlockedEntryCount);
        Assert.Equal("SEK", result.Currency);
        Assert.Equal("SE35 •••• 0003", result.MaskedAccount);
        Assert.Equal(0m, result.OpeningBalance);
        Assert.Equal(41632.37m, result.ClosingBalance);
        Assert.DoesNotContain(result.Issues, issue => issue.Code == "entry-not-booked");
    }

    [Fact]
    public void Validate_EntryDetailsDoNotSumToEntry_ReturnsBlockingError()
    {
        var document = ValidDocument().Replace(
            "<Amt Ccy=\"SEK\">125.00</Amt></TxAmt>",
            "<Amt Ccy=\"SEK\">124.00</Amt></TxAmt>",
            StringComparison.Ordinal);

        var result = _service.Validate(Write("entry-mismatch.nda", document));

        Assert.False(result.IsValid);
        Assert.Contains(result.Issues, issue => issue.Code == "entry-total-mismatch");
    }

    [Fact]
    public void Validate_CreditNoteIsSubtractedFromRemittedAmounts()
    {
        var document = ValidDocument().Replace(
            "<RmtdAmt Ccy=\"SEK\">125.00</RmtdAmt>",
            "<RmtdAmt Ccy=\"SEK\">150.00</RmtdAmt><CdtNoteAmt Ccy=\"SEK\">25.00</CdtNoteAmt>",
            StringComparison.Ordinal);

        var result = _service.Validate(Write("credit-note.nda", document));

        Assert.True(result.IsValid, string.Join("; ", result.Issues.Select(issue => issue.Message)));
        Assert.DoesNotContain(result.Issues, issue => issue.Code == "remitted-total-mismatch");
    }

    [Fact]
    public void Validate_BalanceEquationDoesNotClose_ReturnsBlockingError()
    {
        var document = ValidDocument().Replace(
            "<Amt Ccy=\"SEK\">225.00</Amt><CdtDbtInd>CRDT</CdtDbtInd>",
            "<Amt Ccy=\"SEK\">226.00</Amt><CdtDbtInd>CRDT</CdtDbtInd>",
            StringComparison.Ordinal);

        var result = _service.Validate(Write("balance-mismatch.nda", document));

        Assert.False(result.IsValid);
        Assert.Contains(result.Issues, issue => issue.Code == "balance-equation-mismatch");
    }

    [Fact]
    public void Validate_UnbookedEntry_ReturnsBlockingError()
    {
        var document = ValidDocument().Replace("<Sts>BOOK</Sts>", "<Sts>PDNG</Sts>", StringComparison.Ordinal);

        var result = _service.Validate(Write("pending.nda", document));

        Assert.False(result.IsValid);
        Assert.Contains(result.Issues, issue => issue.Code == "entry-not-booked");
    }

    [Fact]
    public void Validate_ReusedTransactionIdentifiers_ReturnsWarningsWithoutBlocking()
    {
        var document = ValidDocument()
            .Replace("<NbOfTxs>1</NbOfTxs>", "<NbOfTxs>2</NbOfTxs>", StringComparison.Ordinal)
            .Replace(
                "</TxDtls>",
                """
                </TxDtls>
                <TxDtls>
                  <Refs><TxId>TX-1</TxId><AcctSvcrRef>ASR-1</AcctSvcrRef></Refs>
                  <AmtDtls><TxAmt><Amt Ccy="SEK">0.00</Amt></TxAmt></AmtDtls>
                </TxDtls>
                """,
                StringComparison.Ordinal);

        var result = _service.Validate(Write("duplicate-ids.nda", document));

        Assert.True(result.IsValid);
        Assert.Contains(result.Issues, issue => issue.Code == "duplicate-transaction-id" && issue.Severity == BankReconciliationCamtValidationSeverity.Warning);
        Assert.Contains(result.Issues, issue => issue.Code == "duplicate-service-reference" && issue.Severity == BankReconciliationCamtValidationSeverity.Warning);
    }

    [Fact]
    public void Validate_UnsupportedNamespace_ReturnsBlockingError()
    {
        var document = ValidDocument().Replace("camt.053.001.02", "pain.001.001.03", StringComparison.Ordinal);

        var result = _service.Validate(Write("unsupported.xml", document));

        Assert.False(result.IsValid);
        Assert.Contains(result.Issues, issue => issue.Code == "unsupported-document");
    }

    [Fact]
    public void Validate_DtdInput_ReturnsBlockingError()
    {
        var document = ValidDocument().Replace(
            "<Document xmlns=",
            "<!DOCTYPE Document [<!ENTITY source \"blocked\">]><Document xmlns=",
            StringComparison.Ordinal);

        var result = _service.Validate(Write("dtd.xml", document));

        Assert.False(result.IsValid);
        Assert.Contains(result.Issues, issue => issue.Code == "invalid-xml");
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDirectory))
            Directory.Delete(_tempDirectory, recursive: true);
    }

    private string Write(string fileName, string content)
    {
        var path = Path.Combine(_tempDirectory, fileName);
        File.WriteAllText(path, content);
        return path;
    }

    private static string ValidDocument() => """
        <?xml version="1.0" encoding="utf-8"?>
        <Document xmlns="urn:iso:std:iso:20022:tech:xsd:camt.053.001.02">
          <BkToCstmrStmt>
            <Stmt>
              <Id>STMT-1</Id>
              <Acct><Id><IBAN>SE0000000000000000000000</IBAN></Id><Ccy>SEK</Ccy></Acct>
              <Bal><Tp><CdOrPrtry><Cd>OPBD</Cd></CdOrPrtry></Tp><Amt Ccy="SEK">100.00</Amt><CdtDbtInd>CRDT</CdtDbtInd></Bal>
              <Bal><Tp><CdOrPrtry><Cd>CLBD</Cd></CdOrPrtry></Tp><Amt Ccy="SEK">225.00</Amt><CdtDbtInd>CRDT</CdtDbtInd></Bal>
              <Ntry>
                <NtryRef>ENTRY-1</NtryRef>
                <Amt Ccy="SEK">125.00</Amt>
                <CdtDbtInd>CRDT</CdtDbtInd>
                <Sts>BOOK</Sts>
                <BookgDt><Dt>2026-05-12</Dt></BookgDt>
                <ValDt><Dt>2026-05-12</Dt></ValDt>
                <NtryDtls>
                  <Btch><NbOfTxs>1</NbOfTxs></Btch>
                  <TxDtls>
                    <Refs><TxId>TX-1</TxId><AcctSvcrRef>ASR-1</AcctSvcrRef></Refs>
                    <AmtDtls><TxAmt><Amt Ccy="SEK">125.00</Amt></TxAmt></AmtDtls>
                    <RmtInf><Strd><RfrdDocAmt><RmtdAmt Ccy="SEK">125.00</RmtdAmt></RfrdDocAmt></Strd></RmtInf>
                  </TxDtls>
                </NtryDtls>
              </Ntry>
            </Stmt>
          </BkToCstmrStmt>
        </Document>
        """;
}
