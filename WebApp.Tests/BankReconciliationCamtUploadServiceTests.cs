using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Options;
using WebApp.Services.Integration.BankReconciliation;
using WebApp.Services.Integration.BankReconciliation.Imports;
using WebApp.Services.Integration.BankReconciliation.Upload;
using WebApp.Services.Integration.BankReconciliation.Validation;

namespace WebApp.Tests;

// Upload tests keep camt.053 staging atomic before the session points at a new file.
public sealed class BankReconciliationCamtUploadServiceTests : IDisposable
{
    private static readonly Guid CompanyId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private readonly string _contentRoot;

    public BankReconciliationCamtUploadServiceTests()
    {
        _contentRoot = Path.Combine(Path.GetTempPath(), "bankrec-upload-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_contentRoot);
    }

    [Fact]
    public async Task PrepareUploadAsync_StoresValidatedFileAndReturnsTransactionCount()
    {
        var service = CreateService();
        var file = CreateFormFile("""
            <?xml version="1.0" encoding="utf-8"?>
            <Document xmlns="urn:iso:std:iso:20022:tech:xsd:camt.053.001.02">
              <BkToCstmrStmt>
                <Stmt>
                  <Id>STMT-1</Id>
                  <Acct><Id><IBAN>SE0000000000000000000000</IBAN></Id><Ccy>SEK</Ccy></Acct>
                  <Bal><Tp><CdOrPrtry><Cd>OPBD</Cd></CdOrPrtry></Tp><Amt Ccy="SEK">100.00</Amt><CdtDbtInd>CRDT</CdtDbtInd></Bal>
                  <Bal><Tp><CdOrPrtry><Cd>CLBD</Cd></CdOrPrtry></Tp><Amt Ccy="SEK">225.00</Amt><CdtDbtInd>CRDT</CdtDbtInd></Bal>
                  <Ntry>
                    <Sts>BOOK</Sts>
                    <Amt Ccy="SEK">125.00</Amt>
                    <CdtDbtInd>CRDT</CdtDbtInd>
                    <BookgDt><Dt>2026-05-06</Dt></BookgDt>
                    <ValDt><Dt>2026-05-06</Dt></ValDt>
                    <BkTxCd><Domn><Cd>PMNT</Cd><Fmly><Cd>RCDT</Cd><SubFmlyCd>DMCT</SubFmlyCd></Fmly></Domn></BkTxCd>
                    <NtryDtls>
                      <TxDtls>
                        <Refs><TxId>TX-1</TxId></Refs>
                        <AmtDtls><TxAmt><Amt Ccy="SEK">125.00</Amt></TxAmt></AmtDtls>
                      </TxDtls>
                    </NtryDtls>
                  </Ntry>
                </Stmt>
              </BkToCstmrStmt>
            </Document>
            """, "statement.nda");

        var result = await service.PrepareUploadAsync(file, CompanyId, "session-1", previousFilePath: null);

        Assert.True(result.Success);
        Assert.Equal(1, result.TransactionCount);
        Assert.False(string.IsNullOrWhiteSpace(result.StoredFilePath));
        Assert.True(File.Exists(result.StoredFilePath));
        Assert.DoesNotContain(".uploading", result.StoredFilePath, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task PrepareUploadAsync_NoTransactions_DoesNotReplacePreviousFile()
    {
        var service = CreateService();
        var previousFile = Path.Combine(_contentRoot, "keep-me.xml");
        File.WriteAllText(previousFile, "keep");

        var file = CreateFormFile("""
            <?xml version="1.0" encoding="utf-8"?>
            <Document xmlns="urn:iso:std:iso:20022:tech:xsd:camt.053.001.02">
              <BkToCstmrStmt>
                <Stmt />
              </BkToCstmrStmt>
            </Document>
            """, "empty.xml");

        var result = await service.PrepareUploadAsync(file, CompanyId, "session-1", previousFile);

        Assert.False(result.Success);
        Assert.Equal(BankReconciliationCamtUploadFailureReason.ValidationError, result.FailureReason);
        Assert.True(File.Exists(previousFile));
        Assert.Equal("keep", File.ReadAllText(previousFile));
    }

    [Fact]
    public async Task PrepareUploadAsync_FileExceedsConfiguredLimit_DoesNotCreateStagingFile()
    {
        var service = CreateService(maximumFileSizeBytes: 8);
        var file = CreateFormFile("more than eight bytes", "large.nda");

        var result = await service.PrepareUploadAsync(file, CompanyId, "session-1", previousFilePath: null);

        Assert.False(result.Success);
        Assert.Equal(BankReconciliationCamtUploadFailureReason.FileTooLarge, result.FailureReason);
        Assert.Empty(Directory.EnumerateFiles(_contentRoot, "*", SearchOption.AllDirectories));
    }

    [Fact]
    public async Task PrepareUploadAsync_IdenticalImport_ReopensFileWithoutRegisteringNewImport()
    {
        var service = CreateService(
            importRegistry: new BankReconciliationImportRegistry(
                new TestApplicationDbContextFactory()));
        var first = await service.PrepareUploadAsync(
            CreateFormFile(ValidUploadDocument(), "statement.nda"),
            CompanyId,
            "session-1",
            previousFilePath: null);

        var duplicate = await service.PrepareUploadAsync(
            CreateFormFile(ValidUploadDocument(), "statement.nda"),
            CompanyId,
            "session-1",
            first.StoredFilePath);

        Assert.True(first.Success);
        Assert.True(duplicate.Success);
        Assert.Equal(BankReconciliationImportStatus.ExactDuplicate, duplicate.ImportStatus);
        Assert.False(string.IsNullOrWhiteSpace(duplicate.StoredFilePath));
        Assert.True(File.Exists(duplicate.StoredFilePath));
        Assert.False(File.Exists(first.StoredFilePath));
        Assert.Empty(Directory.EnumerateFiles(_contentRoot, "*.uploading", SearchOption.AllDirectories));
    }

    [Fact]
    public async Task PrepareUploadAsync_SaveFailure_SanitizesFailureDetails()
    {
        var service = CreateService(importRegistry: new ThrowingImportRegistry());
        var file = CreateFormFile(ValidUploadDocument(), "statement.nda");

        var result = await service.PrepareUploadAsync(file, CompanyId, "session-1", previousFilePath: null);

        Assert.False(result.Success);
        Assert.Equal(BankReconciliationCamtUploadFailureReason.SaveError, result.FailureReason);
        Assert.DoesNotContain("authorization=secret-value", result.FailureDetails, StringComparison.OrdinalIgnoreCase);
    }

    public void Dispose()
    {
        if (Directory.Exists(_contentRoot))
        {
            Directory.Delete(_contentRoot, recursive: true);
        }
    }

    private BankReconciliationCamtUploadService CreateService(
        long maximumFileSizeBytes = 10 * 1024 * 1024,
        IBankReconciliationImportRegistry? importRegistry = null)
    {
        var environment = new TestHostEnvironment
        {
            ContentRootPath = _contentRoot,
            ContentRootFileProvider = new PhysicalFileProvider(_contentRoot)
        };
        return new BankReconciliationCamtUploadService(
            environment,
            new BankReconciliationCamtParser(),
            new BankReconciliationCamtValidationService(),
            importRegistry ?? new AcceptingImportRegistry(),
            Options.Create(new BankReconciliationCamtValidationOptions
            {
                MaximumFileSizeBytes = maximumFileSizeBytes
            }));
    }

    private sealed class AcceptingImportRegistry : IBankReconciliationImportRegistry
    {
        public Task<BankReconciliationImportRegistrationResult> RegisterAsync(
            BankReconciliationImportRegistrationRequest request,
            CancellationToken cancellationToken = default)
            => Task.FromResult(new BankReconciliationImportRegistrationResult
            {
                Status = BankReconciliationImportStatus.New,
                TransactionCount = request.Document.Transactions.Count
            });
    }

    private sealed class ThrowingImportRegistry : IBankReconciliationImportRegistry
    {
        public Task<BankReconciliationImportRegistrationResult> RegisterAsync(
            BankReconciliationImportRegistrationRequest request,
            CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("authorization=secret-value");
    }

    private static IFormFile CreateFormFile(string content, string fileName)
    {
        var bytes = Encoding.UTF8.GetBytes(content);
        var stream = new MemoryStream(bytes);
        return new FormFile(stream, 0, bytes.Length, "file", fileName)
        {
            Headers = new HeaderDictionary(),
            ContentType = "application/xml"
        };
    }

    private static string ValidUploadDocument() => """
        <?xml version="1.0" encoding="utf-8"?>
        <Document xmlns="urn:iso:std:iso:20022:tech:xsd:camt.053.001.02">
          <BkToCstmrStmt><Stmt>
            <Id>STMT-DUPLICATE</Id>
            <Acct><Id><IBAN>SE0000000000000000000000</IBAN></Id><Ccy>SEK</Ccy></Acct>
            <Bal><Tp><CdOrPrtry><Cd>OPBD</Cd></CdOrPrtry></Tp><Amt Ccy="SEK">100.00</Amt><CdtDbtInd>CRDT</CdtDbtInd></Bal>
            <Bal><Tp><CdOrPrtry><Cd>CLBD</Cd></CdOrPrtry></Tp><Amt Ccy="SEK">225.00</Amt><CdtDbtInd>CRDT</CdtDbtInd></Bal>
            <Ntry><NtryRef>ENTRY-1</NtryRef><Amt Ccy="SEK">125.00</Amt><CdtDbtInd>CRDT</CdtDbtInd><Sts>BOOK</Sts>
              <BookgDt><Dt>2026-05-12</Dt></BookgDt><ValDt><Dt>2026-05-12</Dt></ValDt>
              <NtryDtls><TxDtls><Refs><TxId>TX-1</TxId><AcctSvcrRef>ASR-1</AcctSvcrRef></Refs><AmtDtls><TxAmt><Amt Ccy="SEK">125.00</Amt></TxAmt></AmtDtls></TxDtls></NtryDtls>
            </Ntry>
          </Stmt></BkToCstmrStmt>
        </Document>
        """;
}
