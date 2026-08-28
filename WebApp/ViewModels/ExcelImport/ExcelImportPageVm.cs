using System.Collections.Generic;
using WebApp.Models.Application;
using WebApp.Services.ExcelImport;

namespace WebApp.ViewModels.ExcelImport
{
    // Carries Excel Import page state for uploads, validation, edit sessions, and recent runtime status.
    public class ExcelImportPageVm
    {
        public string? ImportMessage { get; set; }
        public string? ImportMessageType { get; set; }
        public string? ImportAlertClass { get; set; }
        public string? ImportDetails { get; set; }
        public List<string> ImportErrors { get; set; } = new();
        public string? ImportType { get; set; }
        public Guid? EditSessionId { get; set; }
        public string? VoucherPostingDate { get; set; }
        public string? VoucherReversalDate { get; set; }
        public string? CancelEditUrl { get; set; }
        public bool CanEditSession { get; set; }
        public ExcelImportTypeDefinition ImportTypeDefinition { get; set; } = ExcelImportTypeDefinitions.Get(null);
        public bool IsFatalValidationError { get; set; }
        public string? ValidationHint { get; set; }

        public IReadOnlyList<SidebarRuntimeStatusItemViewModel> RuntimeStatusItems { get; set; } = Array.Empty<SidebarRuntimeStatusItemViewModel>();

        public bool ShowValidation { get; set; }
        public string FileName { get; set; } = string.Empty;
        public string FileSizeKb { get; set; } = string.Empty;
        public int TotalRows { get; set; }
        public int ValidRows { get; set; }
        public int InvalidRows { get; set; }
        public int RowPage { get; set; } = 1;
        public int RowPageSize { get; set; } = WebApp.Services.ExcelImport.ExcelImportRowPaging.DefaultPageSize;
        public int RowTotalCount { get; set; }
        public int RowFilteredCount { get; set; }
        public int RowTotalPages { get; set; } = 1;
        public bool ShowOnlyInvalidRows { get; set; }
        public bool IsServerPagedRows { get; set; }
        public List<int> InvalidRowNos { get; set; } = new();
        public List<string> RowHeaders { get; set; } = new();
        public List<ExcelImportRowResult> RowResults { get; set; } = new();
        public List<ExcelImportRowResult> VisibleRowResults { get; set; } = new();
    }
}
