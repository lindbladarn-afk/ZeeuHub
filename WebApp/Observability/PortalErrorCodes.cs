namespace WebApp.Observability;

// Provides stable, searchable error codes for operational failures.
public static class PortalErrorCodes
{
    public const string UnhandledException = "UNHANDLED_EXCEPTION";
    public const string BackgroundJobFailed = "BACKGROUND_JOB_FAILED";
    public const string CustomerSyncFailed = "CUSTOMER_SYNC_FAILED";
    public const string ExcelImportValidationFailed = "EXCEL_IMPORT_VALIDATION_FAILED";
    public const string ExcelImportProcessingFailed = "EXCEL_IMPORT_PROCESSING_FAILED";
    public const string TransAutoPriceImportFailed = "TRANS_AUTO_PRICE_IMPORT_FAILED";
    public const string TransAutoPriceWorkbookReadFailed = "TRANS_AUTO_PRICE_WORKBOOK_READ_FAILED";
    public const string TransAutoPriceStagingFailed = "TRANS_AUTO_PRICE_STAGING_FAILED";
    public const string PressKogyoPriceImportFailed = "PRESS_KOGYO_PRICE_IMPORT_FAILED";
    public const string PressKogyoPriceWorkbookReadFailed = "PRESS_KOGYO_PRICE_WORKBOOK_READ_FAILED";
    public const string PressKogyoPriceStagingFailed = "PRESS_KOGYO_PRICE_STAGING_FAILED";
    public const string FlowEngineExecutionFailed = "FLOWENGINE_EXECUTION_FAILED";
    public const string DocumentSigningSyncFailed = "DOCUMENT_SIGNING_SYNC_FAILED";
    public const string RuntimeEventPublishFailed = "RUNTIME_EVENT_PUBLISH_FAILED";
    public const string JeevesConnectionFailed = "JEEVES_CONNECTION_FAILED";
    public const string JeevesQueryFailed = "JEEVES_QUERY_FAILED";
    public const string ExternalApiTimeout = "EXTERNAL_API_TIMEOUT";
    public const string DatabaseOperationFailed = "DATABASE_OPERATION_FAILED";
}
