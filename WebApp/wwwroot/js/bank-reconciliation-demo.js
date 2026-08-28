// Coordinates the bank reconciliation workflow, filters, coding rules and matching interactions.
(() => {
  const txScript = document.getElementById('bankrec-transactions-json');
  const invScript = document.getElementById('bankrec-invoices-json');
  const isDemoModeScript = document.getElementById('bankrec-is-demo-mode');
  const demoScenarioKeyScript = document.getElementById('bankrec-demo-scenario-key');
  const transactionsEndpointScript = document.getElementById('bankrec-transactions-endpoint');
  const transactionPageSizeScript = document.getElementById('bankrec-transactions-page-size');
  const invoiceEndpointScript = document.getElementById('bankrec-invoices-endpoint');
  const invoicePageSizeScript = document.getElementById('bankrec-invoices-page-size');
  const noInvoicesMessageScript = document.getElementById('bankrec-no-invoices-message');
  const invoicesErrorMessageScript = document.getElementById('bankrec-invoices-error-message');
  const saveMatchesEndpointScript = document.getElementById('bankrec-save-matches-endpoint');
  const autoMatchEndpointScript = document.getElementById('bankrec-auto-match-endpoint');
  const resetMatchesEndpointScript = document.getElementById('bankrec-reset-matches-endpoint');
  const manualMatchEndpointScript = document.getElementById('bankrec-manual-match-endpoint');
  const reverseMatchEndpointScript = document.getElementById('bankrec-reverse-match-endpoint');
  const stateEndpointScript = document.getElementById('bankrec-state-endpoint');
  const closeEndpointScript = document.getElementById('bankrec-close-endpoint');
  const reopenEndpointScript = document.getElementById('bankrec-reopen-endpoint');
  const codingRulesScript = document.getElementById('bankrec-coding-rules-json');
  const codingRulesVersionScript = document.getElementById('bankrec-coding-rules-version');
  const codingBankAccountKeyScript = document.getElementById('bankrec-bank-account-key');
  const codingBankAccountLabelScript = document.getElementById('bankrec-bank-account-label');
  const codingSaveEndpointScript = document.getElementById('bankrec-coding-save-endpoint');
  const recommendationsEndpointScript = document.getElementById('bankrec-recommendations-endpoint');
  const aiSuggestionsEndpointScript = document.getElementById('bankrec-ai-suggestions-endpoint');
  if (!txScript || !invScript) return;

  let transactions = JSON.parse(txScript.textContent || '[]');
  let invoices = JSON.parse(invScript.textContent || '[]');
  let initialTransactions = JSON.parse(JSON.stringify(transactions));
  const isDemoMode = isDemoModeScript ? JSON.parse(isDemoModeScript.textContent || 'false') : false;
  const demoScenarioKey = demoScenarioKeyScript ? JSON.parse(demoScenarioKeyScript.textContent || '"overview"') : 'overview';
  const transactionsEndpoint = transactionsEndpointScript ? JSON.parse(transactionsEndpointScript.textContent || '""') : '';
  const transactionPageSize = transactionPageSizeScript ? Number(JSON.parse(transactionPageSizeScript.textContent || '20')) || 20 : 20;
  const invoicesEndpoint = invoiceEndpointScript ? JSON.parse(invoiceEndpointScript.textContent || '""') : '';
  const saveMatchesEndpoint = saveMatchesEndpointScript ? JSON.parse(saveMatchesEndpointScript.textContent || '""') : '';
  const autoMatchEndpoint = autoMatchEndpointScript ? JSON.parse(autoMatchEndpointScript.textContent || '""') : '';
  const resetMatchesEndpoint = resetMatchesEndpointScript ? JSON.parse(resetMatchesEndpointScript.textContent || '""') : '';
  const manualMatchEndpoint = manualMatchEndpointScript ? JSON.parse(manualMatchEndpointScript.textContent || '""') : '';
  const reverseMatchEndpoint = reverseMatchEndpointScript ? JSON.parse(reverseMatchEndpointScript.textContent || '""') : '';
  const stateEndpoint = stateEndpointScript ? JSON.parse(stateEndpointScript.textContent || '""') : '';
  const closeEndpoint = closeEndpointScript ? JSON.parse(closeEndpointScript.textContent || '""') : '';
  const reopenEndpoint = reopenEndpointScript ? JSON.parse(reopenEndpointScript.textContent || '""') : '';
  const codingRulesVersion = codingRulesVersionScript ? Number(JSON.parse(codingRulesVersionScript.textContent || '0')) || 0 : 0;
  const codingBankAccountKey = codingBankAccountKeyScript ? JSON.parse(codingBankAccountKeyScript.textContent || '""') : '';
  const codingBankAccountLabel = codingBankAccountLabelScript ? JSON.parse(codingBankAccountLabelScript.textContent || '""') : '';
  const codingSaveEndpoint = codingSaveEndpointScript ? JSON.parse(codingSaveEndpointScript.textContent || '""') : '';
  const initialCodingRules = codingRulesScript ? JSON.parse(codingRulesScript.textContent || '[]') : [];
  const recommendationsEndpoint = recommendationsEndpointScript ? JSON.parse(recommendationsEndpointScript.textContent || '""') : '';
  const aiSuggestionsEndpoint = aiSuggestionsEndpointScript ? JSON.parse(aiSuggestionsEndpointScript.textContent || '""') : '';
  const invoicePageSize = invoicePageSizeScript ? Number(JSON.parse(invoicePageSizeScript.textContent || '20')) || 20 : 20;
  const noInvoicesMessage = noInvoicesMessageScript ? JSON.parse(noInvoicesMessageScript.textContent || '""') : 'Inga fakturor att visa.';
  const invoicesErrorMessage = invoicesErrorMessageScript ? JSON.parse(invoicesErrorMessageScript.textContent || '""') : 'Fakturor kunde inte laddas.';
  const confidenceEmptyMessage = JSON.parse(document.getElementById('bankrec-confidence-empty')?.textContent || '"Välj transaktion och faktura för bedömning."');
  const recommendationEmptyMessage = JSON.parse(document.getElementById('bankrec-recommendation-empty')?.textContent || '"Välj en transaktion för att se rekommendationer."');
  const recommendationLoadingMessage = JSON.parse(document.getElementById('bankrec-recommendation-loading')?.textContent || '"Laddar rekommendationer..."');
  const noRecommendationsMessage = JSON.parse(document.getElementById('bankrec-no-recommendations')?.textContent || '"Inga trygga rekommendationer hittades."');
  const recommendationTitleSingularRaw = JSON.parse(document.getElementById('bankrec-recommendation-title-singular')?.textContent || '"Rekommenderad faktura"');
  const recommendationTitlePluralRaw = JSON.parse(document.getElementById('bankrec-recommendation-title-plural')?.textContent || '"Rekommenderade fakturor"');
  const step2RecommendationSingularRaw = JSON.parse(document.getElementById('bankrec-step2-singular')?.textContent || '"Rekommenderad faktura"');
  const step2RecommendationPluralRaw = JSON.parse(document.getElementById('bankrec-step2-plural')?.textContent || '"Rekommenderade fakturor"');
  const activityEmptyMessage = JSON.parse(document.getElementById('bankrec-activity-empty')?.textContent || '"Inga ändringar ännu."');
  const confidenceHighLabel = JSON.parse(document.getElementById('bankrec-confidence-high')?.textContent || '"Hög trygghet"');
  const confidenceMediumLabel = JSON.parse(document.getElementById('bankrec-confidence-medium')?.textContent || '"Medel trygghet"');
  const confidenceLowLabel = JSON.parse(document.getElementById('bankrec-confidence-low')?.textContent || '"Låg trygghet"');
  const manualConfirmationLabel = JSON.parse(document.getElementById('bankrec-manual-confirmation-label')?.textContent || '"Kräver manuell bekräftelse"');
  const manualConfirmationDefaultReason = JSON.parse(document.getElementById('bankrec-manual-confirmation-default-reason')?.textContent || '"Den här matchningen ska granskas manuellt innan den bekräftas."');
  const actionManualMatchLabel = JSON.parse(document.getElementById('bankrec-action-manual-match')?.textContent || '"Manuell matchning"');
  const actionAutoMatchLabel = JSON.parse(document.getElementById('bankrec-action-auto-match')?.textContent || '"Auto-matchning"');
  const actionResetLabel = JSON.parse(document.getElementById('bankrec-action-reset')?.textContent || '"Återställning"');
  const actionReverseLabel = JSON.parse(document.getElementById('bankrec-action-reverse')?.textContent || '"Ångrad matchning"');
  const currentAllocationsEmptyMessage = JSON.parse(document.getElementById('bankrec-current-allocations-empty')?.textContent || '"Inga allokeringar ännu."');
  const removeAllocationLabel = JSON.parse(document.getElementById('bankrec-remove-allocation-label')?.textContent || '"Ta bort allokering"');
  const allocationPrefix = JSON.parse(document.getElementById('bankrec-current-allocations-prefix')?.textContent || '"Allokering"');
  const matchAmountDefaultNote = JSON.parse(document.getElementById('bankrec-match-amount-note-default')?.textContent || '"Välj transaktion och faktura för att ange matchbelopp."');
  const matchAmountSelectedNote = JSON.parse(document.getElementById('bankrec-match-amount-note-selected')?.textContent || '"Standardvärdet följer minsta möjliga trygga matchbelopp."');
  const matchAmountLimitTransactionNote = JSON.parse(document.getElementById('bankrec-match-amount-note-limit-transaction')?.textContent || '"Matchbeloppet begränsas av transaktionsbeloppet."');
  const matchAmountLimitInvoiceNote = JSON.parse(document.getElementById('bankrec-match-amount-note-limit-invoice')?.textContent || '"Matchbeloppet begränsas av fakturans kvarvarande belopp."');
  const matchAmountLimitBothNote = JSON.parse(document.getElementById('bankrec-match-amount-note-limit-both')?.textContent || '"Matchbeloppet begränsas av både transaktionen och fakturan."');
  const autoResultsEmptyMessage = JSON.parse(document.getElementById('bankrec-auto-results-empty')?.textContent || '"Inga auto-matchningar ännu."');
  const autoMatchSuccessTemplate = JSON.parse(document.getElementById('bankrec-auto-match-success')?.textContent || '"Säkra auto-matchningar: {0}. Kvar att hantera manuellt: {1}."');
  const autoMatchNoChangeTemplate = JSON.parse(document.getElementById('bankrec-auto-match-no-change')?.textContent || '"Inga nya säkra auto-matchningar hittades. Kvar att hantera manuellt: {0}."');
  const manualReviewEmptyMessage = JSON.parse(document.getElementById('bankrec-manual-review-empty')?.textContent || '"Inget kräver manuell granskning just nu."');
  const noSafeRecommendationMessage = JSON.parse(document.getElementById('bankrec-no-safe-recommendation')?.textContent || '"Ingen trygg rekommendation hittades."');
  const aiSelectTransactionMessage = JSON.parse(document.getElementById('bankrec-ai-select-transaction')?.textContent || '"Välj en kundinbetalning för att kontrollera AI-status."');
  const aiLoadingMessage = JSON.parse(document.getElementById('bankrec-ai-loading')?.textContent || '"Kontrollerar AI-status..."');
  const aiDisabledMessage = JSON.parse(document.getElementById('bankrec-ai-disabled')?.textContent || '"AI-förslag är avstängt."');
  const aiProviderMissingMessage = JSON.parse(document.getElementById('bankrec-ai-provider-missing')?.textContent || '"AI-provider saknas."');
  const aiNoSuggestionsMessage = JSON.parse(document.getElementById('bankrec-ai-no-suggestions')?.textContent || '"Inga verifierade AI-förslag finns."');
  const aiPromptVersionLabel = JSON.parse(document.getElementById('bankrec-ai-prompt-version-label')?.textContent || '"Promptversion"');
  const aiInputHashLabel = JSON.parse(document.getElementById('bankrec-ai-input-hash-label')?.textContent || '"Input-hash"');
  const aiNoExternalDataMessage = JSON.parse(document.getElementById('bankrec-ai-no-external-data')?.textContent || '"Ingen bankdata har skickats externt."');
  const aiLimitedExternalDataMessage = JSON.parse(document.getElementById('bankrec-ai-limited-external-data')?.textContent || '"Endast minimerat transaktionsunderlag skickades till godkänd AI-provider."');
  const aiSkippedStrongMatchMessage = JSON.parse(document.getElementById('bankrec-ai-skipped-strong-match')?.textContent || '"AI kördes inte eftersom regelmotorn redan har en stark träff."');
  const viewTransactionLabel = JSON.parse(document.getElementById('bankrec-view-transaction-label')?.textContent || '"Visa transaktion"');
  const selectedTransactionLabel = JSON.parse(document.getElementById('bankrec-selected-transaction-label')?.textContent || '"Vald transaktion"');
  const conflictTitleText = JSON.parse(document.getElementById('bankrec-conflict-title-text')?.textContent || '"Underlaget har ändrats"');
  const conflictMessageText = JSON.parse(document.getElementById('bankrec-conflict-message-text')?.textContent || '"Någon annan hann ändra bankavstämningen. Ladda om underlaget och försök igen."');
  const conflictMessageWithVersionText = JSON.parse(document.getElementById('bankrec-conflict-message-current-version')?.textContent || '"Någon annan hann ändra bankavstämningen. Ladda om underlaget och fortsätt från version {0}."');
  const workspaceModeOverviewLabel = JSON.parse(document.getElementById('bankrec-mode-overview-label')?.textContent || '"Översikt"');
  const workspaceModeClassificationLabel = JSON.parse(document.getElementById('bankrec-mode-classification-label')?.textContent || '"Klassificering"');
  const workspaceModeReconciliationLabel = JSON.parse(document.getElementById('bankrec-mode-reconciliation-label')?.textContent || '"Avstämning"');
  const workspaceModeAutoLabel = JSON.parse(document.getElementById('bankrec-mode-auto-label')?.textContent || '"Auto-match"');
  const workspaceModePartialLabel = JSON.parse(document.getElementById('bankrec-mode-partial-label')?.textContent || '"Delbetalningar"');
  const workspaceModeOverviewDescription = JSON.parse(document.getElementById('bankrec-mode-overview-description')?.textContent || '"Nuvarande flöde med transaktioner, fakturor och matchning."');
  const workspaceModeClassificationDescription = JSON.parse(document.getElementById('bankrec-mode-classification-description')?.textContent || '"Översikt över bankhändelsernas typer och volymer."');
  const workspaceModeReconciliationDescription = JSON.parse(document.getElementById('bankrec-mode-reconciliation-description')?.textContent || '"Fokuserat läge för manuell granskning och matchning."');
  const workspaceModeAutoDescription = JSON.parse(document.getElementById('bankrec-mode-auto-description')?.textContent || '"Säkra träffar bokas automatiskt och betalningsgrupper visas för bekräftelse."');
  const workspaceModePartialDescription = JSON.parse(document.getElementById('bankrec-mode-partial-description')?.textContent || '"Granska flera betalningar som tillsammans matchar en faktura."');
  const classificationAllTypesLabel = JSON.parse(document.getElementById('bankrec-classification-all-types')?.textContent || '"Alla typer"');
  const noTransactionsForSelectedTypeMessage = JSON.parse(document.getElementById('bankrec-no-transactions-for-selected-type')?.textContent || '"Inga banktransaktioner för vald typ."');
  const suggestedAccountLabel = JSON.parse(document.getElementById('bankrec-suggested-account-label')?.textContent || '"Föreslaget konto"');
  const suggestedCostCenterLabel = JSON.parse(document.getElementById('bankrec-suggested-cost-center-label')?.textContent || '"Kostnadsställe"');
  const codingPanelEyebrow = JSON.parse(document.getElementById('bankrec-coding-panel-eyebrow')?.textContent || '"Kontering"');
  const codingPanelTitle = JSON.parse(document.getElementById('bankrec-coding-panel-title')?.textContent || '"Konteringsförslag"');
  const codingPanelHint = JSON.parse(document.getElementById('bankrec-coding-panel-hint')?.textContent || '"Justera kontoförslag per typ innan vi sparar regler."');
  const codingLegendSuggested = JSON.parse(document.getElementById('bankrec-coding-legend-suggested')?.textContent || '"Föreslaget"');
  const codingLegendEditable = JSON.parse(document.getElementById('bankrec-coding-legend-editable')?.textContent || '"Redigerbart"');
  const codingAccountLabel = JSON.parse(document.getElementById('bankrec-coding-account-label')?.textContent || '"Konto"');
  const codingCostCenterLabel = JSON.parse(document.getElementById('bankrec-coding-cost-center-label')?.textContent || '"Kostnadsställe"');
  const codingResetLabel = JSON.parse(document.getElementById('bankrec-coding-reset-label')?.textContent || '"Återställ"');
  const codingSaveLabel = JSON.parse(document.getElementById('bankrec-coding-save-label')?.textContent || '"Spara regler"');
  const codingSaveSuccessLabel = JSON.parse(document.getElementById('bankrec-coding-save-success-label')?.textContent || '"Konteringsregler sparade."');
  const codingSaveFailureLabel = JSON.parse(document.getElementById('bankrec-coding-save-failure-label')?.textContent || '"Konteringsreglerna kunde inte sparas."');
  const codingUnsavedConfirmText = JSON.parse(document.getElementById('bankrec-coding-unsaved-confirm')?.textContent || '"Du har osparade konteringsändringar. Vill du lämna granskningen utan att spara?"');
  const completeReadyTitle = JSON.parse(document.getElementById('bankrec-complete-ready-title')?.textContent || '"Avstämningen är genomgången"');
  const completeReadyMessage = JSON.parse(document.getElementById('bankrec-complete-ready-message')?.textContent || '"Inga poster kräver fortsatt manuell granskning."');
  const completePendingTitle = JSON.parse(document.getElementById('bankrec-complete-pending-title')?.textContent || '"Avstämningen behöver mer arbete"');
  const completePendingMessage = JSON.parse(document.getElementById('bankrec-complete-pending-message')?.textContent || '"Fortsätt med posterna som återstår."');
  const completeItemsLoadingMessage = JSON.parse(document.getElementById('bankrec-complete-items-loading')?.textContent || '"Laddar poster..."');
  const completeItemsEmptyMessage = JSON.parse(document.getElementById('bankrec-complete-items-empty')?.textContent || '"Det finns inga poster i den här statusen."');
  const completeReviewReason = JSON.parse(document.getElementById('bankrec-complete-review-reason')?.textContent || '"Posten behöver kontrolleras."');
  const completeUnmatchedReason = JSON.parse(document.getElementById('bankrec-complete-unmatched-reason')?.textContent || '"Ingen trygg fakturakandidat hittades."');
  const completeMatchedReason = JSON.parse(document.getElementById('bankrec-complete-matched-reason')?.textContent || '"Matchningen är klar och kan kontrolleras."');
  const completeHandleAction = JSON.parse(document.getElementById('bankrec-complete-handle-action')?.textContent || '"Hantera i Matcha"');
  const completeViewAction = JSON.parse(document.getElementById('bankrec-complete-view-action')?.textContent || '"Visa matchning"');
  const completeLoadErrorMessage = JSON.parse(document.getElementById('bankrec-complete-load-error')?.textContent || '"Posterna kunde inte laddas. Försök igen."');
  const manualInvoiceTitle = JSON.parse(document.getElementById('bankrec-manual-invoice-title')?.textContent || '"Välj faktura manuellt"');
  const manualInvoiceDescription = JSON.parse(document.getElementById('bankrec-manual-invoice-description')?.textContent || '"Välj en öppen faktura om du vet vilken betalningen avser."');
  const manualInvoiceChoiceLabel = JSON.parse(document.getElementById('bankrec-manual-invoice-choice')?.textContent || '"Manuellt val"');
  const closedTitle = JSON.parse(document.getElementById('bankrec-closed-title')?.textContent || '"Avstämningen är slutförd"');
  const closedMessage = JSON.parse(document.getElementById('bankrec-closed-message')?.textContent || '"Avstämningen är låst för ändringar."');
  const closeReadyStatus = JSON.parse(document.getElementById('bankrec-close-ready-status')?.textContent || '"Alla poster är klara. Avstämningen kan slutföras."');
  const closePendingStatus = JSON.parse(document.getElementById('bankrec-close-pending-status')?.textContent || '"Hantera posterna som återstår innan avstämningen slutförs."');
  const closedStatusTemplate = JSON.parse(document.getElementById('bankrec-closed-status')?.textContent || '"Slutförd {0} av {1}."');
  const saveCodingBeforeClose = JSON.parse(document.getElementById('bankrec-save-coding-before-close')?.textContent || '"Spara konteringsändringarna innan avstämningen slutförs."');

  const txTable = document.getElementById('bankrec-tx-table');
  const txPagination = document.getElementById('bankrec-tx-pagination');
  const txPrevBtn = document.getElementById('bankrec-tx-prev');
  const txNextBtn = document.getElementById('bankrec-tx-next');
  const txPageInfo = document.getElementById('bankrec-tx-page-info');
  const invTable = document.getElementById('bankrec-inv-table');
  const invLoading = document.getElementById('bankrec-inv-loading');
  const invPagination = document.getElementById('bankrec-inv-pagination');
  const invPrevBtn = document.getElementById('bankrec-inv-prev');
  const invNextBtn = document.getElementById('bankrec-inv-next');
  const invPageInfo = document.getElementById('bankrec-inv-page-info');
  const matchBtn = document.getElementById('bankrec-match-btn');
  const autoMatchFeedbackEl = document.getElementById('bankrec-auto-match-feedback');
  const resetBtn = document.getElementById('bankrec-reset-btn');
  const manualBtn = document.getElementById('bankrec-manual-btn');
  const undoBtn = document.getElementById('bankrec-undo-btn');
  const clearSelectionBtn = document.getElementById('bankrec-clear-selection-btn');
  const txCount = document.getElementById('bankrec-tx-count');
  const invCount = document.getElementById('bankrec-inv-count');
  const txFilterSelect = document.getElementById('bankrec-tx-filter');
  const txGroupFilterSelect = document.getElementById('bankrec-tx-group-filter');
  const invFilterSelect = document.getElementById('bankrec-inv-filter');
  const selectionHint = document.getElementById('bankrec-selection-hint');
  const selectedTxSummary = document.getElementById('bankrec-selected-tx-summary');
  const selectedInvSummary = document.getElementById('bankrec-selected-inv-summary');
  const differenceEl = document.getElementById('bankrec-match-difference');
  const confidenceSummary = document.getElementById('bankrec-confidence-summary');
  const matchAmountInput = document.getElementById('bankrec-match-amount-input');
  const matchAmountCurrency = document.getElementById('bankrec-match-amount-currency');
  const matchAmountNote = document.getElementById('bankrec-match-amount-note');
  const currentAllocationsEl = document.getElementById('bankrec-current-allocations');
  const recommendationsEl = document.getElementById('bankrec-recommendations');
  const recommendationsTitleEl = document.getElementById('bankrec-recommendations-title');
  const aiSuggestionsEl = document.getElementById('bankrec-ai-suggestions');
  const recentActivityEl = document.getElementById('bankrec-recent-activity');
  const autoResultsEl = document.getElementById('bankrec-auto-results');
  const manualReviewQueueEl = document.getElementById('bankrec-manual-review-queue');
  const manualRemainingCountEl = document.getElementById('bankrec-manual-remaining-count');
  const conflictBannerEl = document.getElementById('bankrec-conflict-banner');
  const conflictTitleEl = document.getElementById('bankrec-conflict-title');
  const conflictMessageEl = document.getElementById('bankrec-conflict-message');
  const conflictReloadBtn = document.getElementById('bankrec-conflict-reload-btn');
  const pageRoot = document.getElementById('bankrec-page-root');
  const workspaceModeOverviewBtn = document.getElementById('bankrec-mode-overview');
  const workspaceModeClassificationBtn = document.getElementById('bankrec-mode-classification');
  const workspaceModeReconciliationBtn = document.getElementById('bankrec-mode-reconciliation');
  const workspaceModePartialBtn = document.getElementById('bankrec-mode-partial');
  const workspaceModeCompleteBtn = document.getElementById('bankrec-mode-complete');
  const workspaceModeTitleEl = document.getElementById('bankrec-mode-title');
  const workspaceModeDescriptionEl = document.getElementById('bankrec-mode-description');
  const workpanelTitleEl = document.getElementById('bankrec-workpanel-title');
  const workpanelDescriptionEl = document.getElementById('bankrec-workpanel-description');
  const classificationPanelEl = document.getElementById('bankrec-classification-panel');
  const classificationFilterEl = document.getElementById('bankrec-classification-filter');
  const classificationSummaryEl = document.getElementById('bankrec-classification-summary');
  const codingPanelEl = document.getElementById('bankrec-coding-panel');
  const codingSummaryEl = document.getElementById('bankrec-coding-summary');
  const codingBankAccountEl = document.getElementById('bankrec-coding-bank-account');
  const codingSaveBtn = document.getElementById('bankrec-coding-save-btn');
  const codingDirtyEl = document.getElementById('bankrec-coding-dirty');
  const completePanelEl = document.getElementById('bankrec-complete-panel');
  const completeStateEl = document.getElementById('bankrec-complete-state');
  const completeTitleEl = document.getElementById('bankrec-complete-title');
  const completeMessageEl = document.getElementById('bankrec-complete-message');
  const completeMatchedEl = document.getElementById('bankrec-complete-matched');
  const completeReviewEl = document.getElementById('bankrec-complete-review');
  const completeUnmatchedEl = document.getElementById('bankrec-complete-unmatched');
  const completeMatchedCard = document.getElementById('bankrec-complete-card-matched');
  const completeReviewCard = document.getElementById('bankrec-complete-card-review');
  const completeUnmatchedCard = document.getElementById('bankrec-complete-card-unmatched');
  const completeItemsEl = document.getElementById('bankrec-complete-items');
  const lifecycleStatusEl = document.getElementById('bankrec-lifecycle-status');
  const lifecycleErrorEl = document.getElementById('bankrec-lifecycle-error');
  const closeBtn = document.getElementById('bankrec-close-btn');
  const reopenControlsEl = document.getElementById('bankrec-reopen-controls');
  const reopenReasonInput = document.getElementById('bankrec-reopen-reason');
  const reopenBtn = document.getElementById('bankrec-reopen-btn');

  const totalCreditEl = document.getElementById('bankrec-total-credit');
  const totalDebitEl = document.getElementById('bankrec-total-debit');
  const totalMatchedEl = document.getElementById('bankrec-total-matched');
  const totalUnmatchedEl = document.getElementById('bankrec-total-unmatched');
  const demoSummaryMatchedEl = document.getElementById('bankrec-demo-summary-matched');
  const demoSummaryReviewEl = document.getElementById('bankrec-demo-summary-review');
  const demoSummaryUnmatchedEl = document.getElementById('bankrec-demo-summary-unmatched');

  const invoiceDetailTitle = document.getElementById('bankrec-invoice-detail-title');
  const invoiceDetailStatus = document.getElementById('bankrec-invoice-detail-status');
  const invoiceDetailBody = document.getElementById('bankrec-invoice-detail-body');

  let selectedTxId = null;
  let selectedInvId = null;
  let txFilter = 'all';
  let invFilter = 'all';
  let txGroupFilter = 'all';
  let transactionPage = 1;
  let transactionTotalPages = 1;
  let transactionTotalCount = 0;
  let transactionTotals = { credit: 0, debit: 0, matched: 0, unmatched: 0 };
  let transactionGroupCounts = { all: 0, Kundinbetalningar: 0, Leverantorsutbetalningar: 0, Ovrigt: 0 };
  let transactionClassificationSummary = [];
  let manualReviewQueueItems = [];
  let autoResultItems = [];
  let transactionCache = new Map();
  let isTransactionsLoading = false;
  let transactionsLoadError = '';
  let summaryCounts = { matched: 0, review: 0, unmatched: 0 };
  let hasTransactionSummaryLoaded = false;
  let invoicePage = 1;
  let invoiceTotalPages = 1;
  let invoiceTotalCount = 0;
  let isInvoicesLoading = false;
  let invoiceLoadingTimer = null;
  let invoicesLoadError = '';
  let latestRecommendationsToken = 0;
  let latestAiSuggestionsToken = 0;
  let recommendedInvoiceLookup = new Map();
  let recommendationCache = new Map();
  let currentStateVersion = 0;
  let isReconciliationClosed = false;
  let reconciliationClosedAtUtc = null;
  let reconciliationClosedByName = '';
  let workspaceMode = 'overview';
  let classificationTypeFilter = 'all';
  let codingRuleBaseline = new Map(
    Array.isArray(initialCodingRules)
      ? initialCodingRules
        .filter((row) => row?.typeKey || row?.TypeKey)
        .map((row) => [String(row.typeKey || row.TypeKey).toLowerCase(), {
          account: row.account || row.Account || '',
          costCenter: row.costCenter || row.CostCenter || '',
          sourceBankAccountKey: row.sourceBankAccountKey || row.SourceBankAccountKey || '',
          isInherited: Boolean(row.isInherited || row.IsInherited)
        }])
      : []
  );
  let codingOverrides = new Map();
  let codingRuleSetVersion = codingRulesVersion;
  let hasUnsavedCodingChanges = false;
  let summaryView = 'all';
  let completionItems = [];
  let latestCompletionItemsToken = 0;
  let persistedMatches = [];
  let initialStatePromise = null;
  const pendingDemoScrollPositionStorageKey = 'bankrec-pending-demo-scroll-position';
  const manualWorkspaceTitle = workpanelTitleEl?.textContent || 'Manuell matchning';
  const manualWorkspaceDescription = workpanelDescriptionEl?.textContent || 'Välj transaktion och faktura för att matcha manuellt.';

  const formatText = (template, ...args) =>
    String(template || '').replace(/\{(\d+)\}/g, (_, index) => args[Number(index)] ?? '');

  const resolveLocalizedLabel = (value, fallback) => {
    if (!value) return fallback;
    return String(value).startsWith('BankRec_') ? fallback : value;
  };

  const recommendationTitleSingular = resolveLocalizedLabel(recommendationTitleSingularRaw, 'Rekommenderad faktura');
  const recommendationTitlePlural = resolveLocalizedLabel(recommendationTitlePluralRaw, 'Rekommenderade fakturor');
  const step2RecommendationSingular = resolveLocalizedLabel(step2RecommendationSingularRaw, 'Rekommenderad faktura');
  const step2RecommendationPlural = resolveLocalizedLabel(step2RecommendationPluralRaw, 'Rekommenderade fakturor');

  const formatDateTime = (value) => {
    if (!value) return '—';
    const date = new Date(value);
    if (Number.isNaN(date.getTime())) return escapeHtml(value);
    return new Intl.DateTimeFormat('sv-SE', {
      year: 'numeric',
      month: '2-digit',
      day: '2-digit',
      hour: '2-digit',
      minute: '2-digit'
    }).format(date);
  };

  const formatAmount = (value) =>
    new Intl.NumberFormat('sv-SE', { minimumFractionDigits: 2, maximumFractionDigits: 2 }).format(value || 0);

  const escapeHtml = (value) => {
    if (value === null || value === undefined) return '';
    return String(value)
      .replace(/&/g, '&amp;')
      .replace(/</g, '&lt;')
      .replace(/>/g, '&gt;')
      .replace(/"/g, '&quot;')
      .replace(/'/g, '&#39;');
  };

  const stringEquals = (left, right) => String(left || '').toLowerCase() === String(right || '').toLowerCase();

  const getAntiForgery = () => document.querySelector('#__af input[name="__RequestVerificationToken"]')?.value;

  const setConflictState = (isActive, message = '', currentVersion = null) => {
    if (!conflictBannerEl || !conflictMessageEl || !conflictTitleEl) return;
    conflictBannerEl.classList.toggle('d-none', !isActive);
    conflictTitleEl.textContent = conflictTitleText;
    if (isActive) {
      conflictMessageEl.textContent = message
        || (Number.isFinite(currentVersion)
          ? formatText(conflictMessageWithVersionText, currentVersion)
          : conflictMessageText);
    } else {
      conflictMessageEl.textContent = conflictMessageText;
    }
  };

  const createRequestError = (body, status) => {
    const error = new Error(body?.errorMessage || `HTTP ${status}`);
    error.status = status;
    const parsedVersion = Number(body?.currentVersion);
    if (Number.isFinite(parsedVersion)) {
      error.currentVersion = parsedVersion;
    }
    return error;
  };

  const handleBankRecError = (error, fallbackMessage) => {
    if (error && error.status === 409) {
      const conflictVersion = Number(error.currentVersion);
      if (Number.isFinite(conflictVersion)) {
        currentStateVersion = conflictVersion;
      }
      setConflictState(true, error.message, Number.isFinite(conflictVersion) ? conflictVersion : null);
      return error.message;
    }

    setConflictState(false);
    return error instanceof Error ? error.message : fallbackMessage;
  };

  const postJson = async (url, payload) => {
    const token = getAntiForgery();
    const expectedVersionHeader = payload && Number.isFinite(payload.expectedVersion)
      ? { 'X-BankRec-State-Version': String(payload.expectedVersion) }
      : (autoMatchEndpoint && url === autoMatchEndpoint ? { 'X-BankRec-State-Version': String(currentStateVersion) } : {});
    const response = await fetch(url, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        ...expectedVersionHeader,
        ...(token ? { 'RequestVerificationToken': token } : {})
      },
      body: JSON.stringify(payload)
    });

    const body = await response.json();
    if (!response.ok || body.success === false) {
      throw createRequestError(body, response.status);
    }

    return body;
  };

  const ensureInitialStateLoaded = async () => {
    if (!initialStatePromise) return false;
    try {
      await initialStatePromise;
      return true;
    } catch {
      return false;
    }
  };

  const getTxGroup = (tx) => tx.group || 'Ovrigt';
  const getTxGroupLabel = (tx) => {
    const group = getTxGroup(tx);
    if (group === 'Kundinbetalningar') return 'Kundinbetalning';
    if (group === 'Leverantorsutbetalningar') return 'Leverantörsutbetalning';
    return 'Övrigt';
  };
  const isCredit = (tx) => (tx.direction || '').toUpperCase() === 'CRDT' || !tx.direction;
  const isCustomerReceipt = (tx) => getTxGroup(tx) === 'Kundinbetalningar';
  const getPersistedAllocationsForTransaction = (transactionId) => {
    if (!transactionId) return [];
    return persistedMatches
      .filter((allocation) => allocation?.transactionId === transactionId)
      .map((allocation) => ({
        allocationId: allocation.allocationId || null,
        invoiceId: allocation.invoiceId,
        matchType: allocation.matchType || 'manual',
        matchRule: allocation.matchRule || 'manual',
        matchedAmount: Number(allocation.matchedAmount ?? 0) || 0,
        currency: allocation.currency || 'SEK'
      }));
  };
  const getTxAllocations = (tx) => {
    if (!tx) return [];
    if (Array.isArray(tx.allocations) && tx.allocations.length > 0) {
      return tx.allocations
        .filter((allocation) => allocation?.invoiceId)
        .map((allocation) => ({
          allocationId: allocation.allocationId || null,
          invoiceId: allocation.invoiceId,
          matchType: allocation.matchType || tx.matchType || 'manual',
          matchRule: allocation.matchRule || tx.matchRule || 'manual',
          matchedAmount: Number(allocation.matchedAmount ?? 0) || 0,
          currency: allocation.currency || tx.currency || 'SEK'
        }));
    }

    if (tx.matchedInvoiceId) {
      return [{
        allocationId: null,
        invoiceId: tx.matchedInvoiceId,
        matchType: tx.matchType || 'manual',
        matchRule: tx.matchRule || 'manual',
        matchedAmount: Number(tx.matchedAmount ?? tx.amount ?? 0) || 0,
        currency: tx.currency || 'SEK'
      }];
    }

    return getPersistedAllocationsForTransaction(tx.id);
  };
  const getMatchedAmount = (tx) => getTxAllocations(tx).reduce((sum, allocation) => sum + (allocation.matchedAmount || 0), 0);
  const getInvoicePayments = (invId) => {
    const transactionIds = new Set(
      persistedMatches
        .filter((allocation) => allocation?.invoiceId === invId)
        .map((allocation) => allocation.transactionId)
    );
    return [...transactionCache.values()].filter((tx) => transactionIds.has(tx.id));
  };
  const getInvoicePaid = (invId) => persistedMatches
    .filter((allocation) => allocation?.invoiceId === invId)
    .reduce((sum, allocation) => sum + (Number(allocation.matchedAmount ?? 0) || 0), 0);
  const getInvoiceRemaining = (inv) => Math.max((inv.amount || 0) - getInvoicePaid(inv.id), 0);
  const getTxAllocationAmountForInvoice = (tx, invoiceId) => getTxAllocations(tx)
    .filter((allocation) => allocation.invoiceId === invoiceId)
    .reduce((sum, allocation) => sum + (allocation.matchedAmount || 0), 0);
  const getTransactionRemaining = (tx) => Math.max((tx?.amount || 0) - getMatchedAmount(tx), 0);
  const getEditableTransactionRemaining = (tx, invoiceId) => Math.max(getTransactionRemaining(tx) + getTxAllocationAmountForInvoice(tx, invoiceId), 0);
  const getEditableInvoiceRemaining = (inv, tx) => Math.max(getInvoiceRemaining(inv) + getTxAllocationAmountForInvoice(tx, inv.id), 0);
  const getSelectedMatchAmount = () => {
    if (!matchAmountInput) return 0;
    const parsed = Number.parseFloat(matchAmountInput.value || '0');
    return Number.isFinite(parsed) ? parsed : 0;
  };
  const getRecommendedMatchAmount = (tx, inv) => {
    if (!tx || !inv) return 0;
    return Math.max(Math.min(getEditableTransactionRemaining(tx, inv.id), getEditableInvoiceRemaining(inv, tx)), 0);
  };
  const getAllKnownInvoices = () => {
    const seen = new Map();
    invoices.forEach((inv) => {
      if (inv?.id) seen.set(inv.id, inv);
    });
    recommendedInvoiceLookup.forEach((inv, id) => {
      if (inv?.id) seen.set(id, inv);
    });
    return Array.from(seen.values());
  };

  const getInvoiceById = (invoiceId) => getAllKnownInvoices().find((inv) => inv.id === invoiceId) || null;

  const getConfidenceCopy = (level) => {
    if (level === 'Hög') return confidenceHighLabel;
    if (level === 'Medel') return confidenceMediumLabel;
    return confidenceLowLabel;
  };

  const formatConfidenceScore = (confidence) => {
    const score = Math.max(0, Math.min(100, Math.round(Number(confidence?.score ?? 0) || 0)));
    return `${score}%`;
  };

  const requiresManualConfirmation = (detail) => {
    if (!detail) return true;
    return detail.confidence.score < 80
      || detail.signals.amountTolerance
      || detail.signals.refPartial
      || (!detail.signals.refExact && (detail.signals.nameMatch || detail.signals.dateMatch));
  };

  const normalizeRecommendationItem = (item) => {
    if (!item?.invoice) return null;

    const detail = item.detail || null;
    const confidence = item.confidence || detail?.confidence || { level: 'Låg', score: 0 };
    const ruleLabel = item.ruleLabel || detail?.ruleLabel || 'Manuell';
    const ruleHelp = item.ruleHelp || detail?.ruleHelp || 'Matchningen kräver manuell kontroll.';
    const manualRequired = typeof item.requiresManualConfirmation === 'boolean'
      ? item.requiresManualConfirmation
      : requiresManualConfirmation(detail);
    const manualReason = item.manualConfirmationReason
      || (manualRequired ? manualConfirmationDefaultReason : null);

    return {
      invoice: item.invoice,
      confidence,
      ruleLabel,
      ruleHelp,
      requiresManualConfirmation: manualRequired,
      manualConfirmationReason: manualReason,
      evidence: item.evidence || detail?.evidence || null,
      detail
    };
  };

  const getLocalRecommendations = (tx, maxResults = 3) => {
    if (!tx || !window.BankRecMatching?.describeMatch || !isCredit(tx) || !isCustomerReceipt(tx)) return [];
    return getAllKnownInvoices()
      .map((inv) => {
        const availableAmount = getInvoiceRemaining(inv) + getTxAllocationAmountForInvoice(tx, inv.id);
        if (availableAmount <= 0) return null;
        const detail = window.BankRecMatching.describeMatch(tx, inv);
        return normalizeRecommendationItem({
          invoice: inv,
          detail,
          requiresManualConfirmation: requiresManualConfirmation(detail)
        });
      })
      .filter(Boolean)
      .filter((item) => item.confidence.score >= 35)
      .sort((a, b) => b.confidence.score - a.confidence.score)
      .slice(0, maxResults);
  };

  const getActionCopy = (actionType) => {
    if (actionType === 'manual-match') return actionManualMatchLabel;
    if (actionType === 'replace-matches') return actionAutoMatchLabel;
    if (actionType === 'reverse-match') return actionReverseLabel;
    return actionResetLabel;
  };

  const renderManualConfirmationNotice = (reason) =>
    `<div class="bankrec-manual-confirmation"><strong>${escapeHtml(manualConfirmationLabel)}</strong><div class="mt-1">${escapeHtml(reason || manualConfirmationDefaultReason)}</div></div>`;

  const renderRecommendationEvidence = (evidence) => {
    if (!evidence) return '';

    const reference = Array.isArray(evidence.referenceMatches) && evidence.referenceMatches.length > 0
      ? evidence.referenceMatches[0]
      : null;
    const parts = [];

    if (reference) {
      parts.push(`Referens ${reference.matchType === 'exact' ? 'exakt' : 'delträff'}: ${reference.transactionValue} ↔ ${reference.invoiceValue}`);
    }

    if (typeof evidence.amountDifference === 'number') {
      parts.push(`Beloppsdiff: ${formatAmount(evidence.amountDifference)} ${evidence.currency || 'SEK'}`);
    }

    if (Array.isArray(evidence.matchedNameTokens) && evidence.matchedNameTokens.length > 0) {
      parts.push(`Namnträff: ${evidence.matchedNameTokens.slice(0, 3).join(', ')}`);
    }

    if (typeof evidence.dateDifferenceDays === 'number') {
      parts.push(`Datumdiff: ${evidence.dateDifferenceDays} dagar`);
    }

    if (parts.length === 0) return '';

    return `
      <div class="bankrec-recommendation-evidence">
        ${parts.map((part) => `<span>${escapeHtml(part)}</span>`).join('')}
      </div>
    `;
  };

  const renderAiSuggestions = (state, targetTxId = selectedTxId) => {
    if (!aiSuggestionsEl) return;
    if (targetTxId && selectedTxId && !stringEquals(targetTxId, selectedTxId)) return;

    if (state === null) {
      aiSuggestionsEl.textContent = aiSelectTransactionMessage;
      return;
    }

    if (state === 'loading') {
      aiSuggestionsEl.innerHTML = `<div class="bankrec-inline-loading"><span class="bankrec-spinner"></span><span>${escapeHtml(aiLoadingMessage)}</span></div>`;
      return;
    }

    if (!state || typeof state !== 'object') {
      aiSuggestionsEl.textContent = aiNoSuggestionsMessage;
      return;
    }

    if (state.status === 'skipped-strong-rule-match') {
      aiSuggestionsEl.innerHTML = `
        <div class="bankrec-ai-status is-enabled">
          <div class="bankrec-ai-status__top">
            <strong>${escapeHtml(state.message || aiSkippedStrongMatchMessage)}</strong>
            <span class="bankrec-ai-status__badge">rules</span>
          </div>
          <div class="bankrec-ai-status__meta">${escapeHtml(aiNoExternalDataMessage)}</div>
        </div>
      `;
      return;
    }

    const status = state.status || (state.enabled ? 'enabled' : 'disabled');
    const message = state.message
      || (status === 'provider-not-configured' ? aiProviderMissingMessage : aiDisabledMessage);
    const suggestions = Array.isArray(state.suggestions) ? state.suggestions : [];
    const suggestionItems = suggestions.map((suggestion) => {
      const invoice = getInvoiceById(suggestion.invoiceId) || getAllKnownInvoices().find((item) => stringEquals(item.invoiceNo, suggestion.invoiceId)) || null;
      const invoiceLabel = invoice?.invoiceNo || suggestion.invoiceId || '-';
      const customerLabel = invoice?.customerName || '-';
      const confidence = typeof suggestion.confidenceScore === 'number' ? `${suggestion.confidenceScore}%` : '-';
      const matchedAmount = typeof suggestion.matchedAmount === 'number'
        ? `${formatAmount(suggestion.matchedAmount)} ${suggestion.currency || 'SEK'}`
        : '-';
      return `
        <div class="bankrec-ai-suggestion">
          <div class="bankrec-ai-suggestion__top">
            <strong>${escapeHtml(invoiceLabel)}</strong>
            <span>${escapeHtml(confidence)}</span>
          </div>
          <div class="bankrec-ai-suggestion__customer">${escapeHtml(customerLabel)}</div>
          <div class="bankrec-ai-suggestion__meta">
            <span>${escapeHtml(matchedAmount)}</span>
            <span>${escapeHtml(suggestion.reasonCode || 'verified')}</span>
          </div>
          ${suggestion.explanation ? `<p>${escapeHtml(suggestion.explanation)}</p>` : ''}
        </div>
      `;
    }).join('');

    aiSuggestionsEl.innerHTML = `
      <div class="bankrec-ai-status${state.enabled ? ' is-enabled' : ' is-disabled'}">
        <div class="bankrec-ai-status__top">
          <strong>${escapeHtml(status === 'provider-not-configured' ? aiProviderMissingMessage : message)}</strong>
          <span class="bankrec-ai-status__badge">${escapeHtml(status)}</span>
        </div>
        <div class="bankrec-ai-status__meta">${escapeHtml(state.enabled ? aiLimitedExternalDataMessage : aiNoExternalDataMessage)}</div>
        <div class="bankrec-ai-status__grid">
          <div>
            <span>${escapeHtml(aiPromptVersionLabel)}</span>
            <strong>${escapeHtml(state.promptVersion || '-')}</strong>
          </div>
          <div>
            <span>${escapeHtml(aiInputHashLabel)}</span>
            <strong title="${escapeHtml(state.inputHash || '')}">${escapeHtml((state.inputHash || '-').slice(0, 16))}</strong>
          </div>
        </div>
        ${suggestions.length === 0 ? `<div class="bankrec-ai-status__empty">${escapeHtml(aiNoSuggestionsMessage)}</div>` : `<div class="bankrec-ai-suggestion-list">${suggestionItems}</div>`}
      </div>
    `;
  };

  const hasStrongDeterministicRecommendation = (recommendations) => {
    const strongCandidates = (Array.isArray(recommendations) ? recommendations : [])
      .filter((item) => {
        const ruleKey = String(item?.ruleKey || '');
        return item?.requiresManualConfirmation === false
          && (Number(item?.confidence?.score ?? 0) || 0) >= 90
          && ruleKey.includes('ref-exact')
          && ruleKey.includes('amount-exact');
      });

    return strongCandidates.length === 1;
  };

  const syncAiForRecommendations = (tx, recommendations, targetTxId = tx?.id || selectedTxId) => {
    if (!tx || !targetTxId || !isCredit(tx) || !isCustomerReceipt(tx)) {
      renderAiSuggestions(null);
      return;
    }

    if (!Array.isArray(recommendations) || recommendations.length === 0) {
      latestAiSuggestionsToken += 1;
      renderAiSuggestions({
        status: 'no-rule-candidates',
        enabled: false,
        message: noRecommendationsMessage,
        suggestions: []
      }, targetTxId);
      return;
    }

    if (hasStrongDeterministicRecommendation(recommendations)) {
      latestAiSuggestionsToken += 1;
      renderAiSuggestions({
        status: 'skipped-strong-rule-match',
        enabled: true,
        message: aiSkippedStrongMatchMessage,
        suggestions: []
      }, targetTxId);
      return;
    }

    renderAiSuggestions('loading', targetTxId);
    buildAiSuggestions(tx);
  };

  const selectTransaction = (transactionId) => {
    selectedTxId = transactionId;
    selectedInvId = null;
    recommendedInvoiceLookup = new Map();
    renderInvoiceDetail(null);
    renderTable();
    const tx = getTransactionById(transactionId);
    const localRecommendations = tx ? (recommendationCache.get(transactionId) || getLocalRecommendations(tx, 3)) : [];
    if (Array.isArray(localRecommendations) && localRecommendations.length > 0) {
      renderRecommendations(localRecommendations, transactionId);
      syncAiForRecommendations(tx, localRecommendations, transactionId);
      buildRecommendations(tx, false);
    } else {
      renderRecommendations('loading', transactionId);
      renderAiSuggestions(null, transactionId);
      buildRecommendations(tx, true);
    }
  };

  const renderAutoResults = () => {
    if (!autoResultsEl) return;

    const autoMatchedTransactions = autoResultItems;

    if (autoMatchedTransactions.length === 0) {
      autoResultsEl.textContent = autoResultsEmptyMessage;
      return;
    }

    autoResultsEl.innerHTML = `
      <div class="bankrec-insight-list">
        ${autoMatchedTransactions.map((tx) => {
          const allocations = getTxAllocations(tx);
          const invoiceLabels = allocations
            .map((allocation) => getInvoiceById(allocation.invoiceId)?.invoiceNo || allocation.invoiceId)
            .join(', ');
          const selectedClass = selectedTxId === tx.id ? ' is-selected' : '';

          return `
            <button type="button" class="bankrec-insight-item${selectedClass}" data-insight-transaction="${escapeHtml(tx.id)}">
              <div class="bankrec-insight-top">
                <strong>${escapeHtml(tx.id)}</strong>
                <span class="bankrec-confidence-chip high">${escapeHtml(getConfidenceCopy('Hög'))}</span>
              </div>
              <div class="bankrec-insight-body">${escapeHtml(tx.debtorName || '-')} · ${formatAmount(tx.amount)} ${escapeHtml(tx.currency || '')}</div>
              <div class="bankrec-insight-meta">${escapeHtml(invoiceLabels)} · ${formatAmount(getMatchedAmount(tx))} ${escapeHtml(tx.currency || '')}</div>
              <div class="bankrec-insight-footer">${selectedTxId === tx.id ? escapeHtml(selectedTransactionLabel) : escapeHtml(viewTransactionLabel)}</div>
            </button>
          `;
        }).join('')}
      </div>
    `;

    autoResultsEl.querySelectorAll('[data-insight-transaction]').forEach((button) => {
      button.addEventListener('click', () => {
        const transactionId = button.getAttribute('data-insight-transaction');
        if (!transactionId) return;
        selectTransaction(transactionId);
      });
    });
  };

  const renderManualReviewQueue = (items = transactions) => {
    if (!manualReviewQueueEl) return;

    const queue = items
      .map((tx) => {
        const recommendations = recommendationCache.get(tx.id) || getLocalRecommendations(tx, 3);
        const topRecommendation = recommendations[0] || null;
        return { tx, topRecommendation };
      });
    const remainingCount = queue.filter((item) => !isTxMatched(item.tx)).length;
    if (manualRemainingCountEl) {
      const valueEl = manualRemainingCountEl.querySelector('strong');
      if (valueEl) valueEl.textContent = String(remainingCount);
    }

    if (queue.length === 0) {
      manualReviewQueueEl.textContent = 'Inga banktransaktioner att visa.';
      return;
    }

    manualReviewQueueEl.innerHTML = `
      <div class="bankrec-insight-list">
        ${queue.map((item) => {
          const selectedClass = selectedTxId === item.tx.id ? ' is-selected' : '';
          const confidenceLevel = item.topRecommendation?.confidence?.level || 'Låg';
          const recommendationLabel = item.topRecommendation?.invoice?.invoiceNo || item.topRecommendation?.invoice?.id || 'Ingen rekommendation';
          const reason = item.topRecommendation
            ? `${recommendationLabel} · ${item.topRecommendation.ruleLabel}`
            : noSafeRecommendationMessage;
          const matched = getTxAllocations(item.tx).length > 0;
          const partial = matched && getMatchedAmount(item.tx) > 0 && getMatchedAmount(item.tx) < (item.tx.amount || 0);
          const statusText = matched ? (partial ? 'Delmatchad' : 'Matchad') : 'Ej matchad';

          return `
            <button type="button" class="bankrec-insight-item${selectedClass}" data-insight-transaction="${escapeHtml(item.tx.id)}">
              <div class="bankrec-insight-top">
                <strong>${escapeHtml(item.tx.id)}</strong>
                <span class="bankrec-confidence-chip ${confidenceLevel === 'Hög' ? 'high' : confidenceLevel === 'Medel' ? 'medium' : 'low'}">${escapeHtml(getConfidenceCopy(confidenceLevel))}</span>
              </div>
              <div class="bankrec-insight-body">${escapeHtml(item.tx.debtorName || '-')} · ${formatAmount(item.tx.amount)} ${escapeHtml(item.tx.currency || '')}</div>
              <div class="bankrec-insight-meta">${escapeHtml(item.tx.date || '')} · ${escapeHtml(item.tx.reference || '-')} · ${escapeHtml(statusText)}</div>
              <div class="bankrec-insight-meta">${escapeHtml(reason)}</div>
              <div class="bankrec-insight-footer">${selectedTxId === item.tx.id ? escapeHtml(selectedTransactionLabel) : escapeHtml(viewTransactionLabel)}</div>
            </button>
          `;
        }).join('')}
      </div>
    `;

    manualReviewQueueEl.querySelectorAll('[data-insight-transaction]').forEach((button) => {
      button.addEventListener('click', () => {
        const transactionId = button.getAttribute('data-insight-transaction');
        if (!transactionId) return;
        selectTransaction(transactionId);
      });
    });
  };

  const setInvoicesLoading = (loading) => {
    isInvoicesLoading = loading;

    if (invoiceLoadingTimer) {
      window.clearTimeout(invoiceLoadingTimer);
      invoiceLoadingTimer = null;
    }

    if (loading) {
      // Visa bara loadern om hämtningen tar märkbart lång tid.
      invoiceLoadingTimer = window.setTimeout(() => {
        if (!isInvoicesLoading) return;
        invLoading?.classList.remove('d-none');
        invTable?.classList.add('bankrec-table-loading');
      }, 250);
      return;
    }

    invLoading?.classList.add('d-none');
    invTable?.classList.remove('bankrec-table-loading');
  };

  const setTransactionsLoading = (loading) => {
    isTransactionsLoading = loading;
    txTable?.classList.toggle('bankrec-table-loading', loading);
  };

  const updateTransactionPagination = () => {
    const shouldShow = !isTransactionsLoading && !transactionsLoadError && transactionTotalPages > 1;
    txPagination?.classList.toggle('d-none', !shouldShow);
    if (txPageInfo) txPageInfo.textContent = `${transactionPage} / ${Math.max(transactionTotalPages, 1)}`;
    if (txPrevBtn) txPrevBtn.disabled = isTransactionsLoading || transactionPage <= 1;
    if (txNextBtn) txNextBtn.disabled = isTransactionsLoading || transactionPage >= transactionTotalPages;
  };

  const updateInvoicePagination = () => {
    const shouldShow = !isInvoicesLoading && !invoicesLoadError && invoiceTotalPages > 1;
    invPagination?.classList.toggle('d-none', !shouldShow);
    if (invPageInfo) invPageInfo.textContent = `${invoicePage} / ${Math.max(invoiceTotalPages, 1)}`;
    if (invPrevBtn) invPrevBtn.disabled = isInvoicesLoading || invoicePage <= 1;
    if (invNextBtn) invNextBtn.disabled = isInvoicesLoading || invoicePage >= invoiceTotalPages;
  };

  const renderInvoiceDetail = (inv) => {
    if (!invoiceDetailTitle || !invoiceDetailBody || !invoiceDetailStatus) return;
    if (!inv) {
      invoiceDetailTitle.textContent = 'Ingen faktura vald.';
      invoiceDetailBody.innerHTML = '';
      invoiceDetailStatus.textContent = '—';
      return;
    }

    const paid = getInvoicePaid(inv.id);
    const remaining = getInvoiceRemaining(inv);
    const full = remaining === 0 && paid > 0;
    const partial = paid > 0 && remaining > 0;
    const status = full ? 'Matchad' : partial ? 'Delbetald' : 'Omatchad';

    invoiceDetailTitle.textContent = `Faktura ${inv.invoiceNo || inv.id} · ${inv.customerName || ''}`;
    invoiceDetailStatus.textContent = status;
    invoiceDetailStatus.className = `badge rounded-pill ${full ? 'bg-success' : partial ? 'bg-warning text-dark' : 'bg-secondary'}`;
    invoiceDetailBody.innerHTML = `
      <div class="row g-3">
        <div class="col-md-4"><div class="text-muted small">Belopp</div><div class="h6">${formatAmount(inv.amount)} ${escapeHtml(inv.currency || '')}</div></div>
        <div class="col-md-4"><div class="text-muted small">Betalt</div><div class="h6">${formatAmount(paid)} ${escapeHtml(inv.currency || '')}</div></div>
        <div class="col-md-4"><div class="text-muted small">Kvar</div><div class="h6">${formatAmount(remaining)} ${escapeHtml(inv.currency || '')}</div></div>
      </div>
    `;
  };

  const updateMatchAmountUi = (tx, inv) => {
    if (!matchAmountInput || !matchAmountCurrency || !matchAmountNote) return;

    if (!tx || !inv) {
      matchAmountInput.value = '';
      matchAmountInput.disabled = true;
      matchAmountInput.max = '';
      matchAmountCurrency.textContent = inv?.currency || tx?.currency || 'SEK';
      matchAmountNote.textContent = matchAmountDefaultNote;
      return;
    }

    const transactionRemaining = getEditableTransactionRemaining(tx, inv.id);
    const invoiceRemaining = getEditableInvoiceRemaining(inv, tx);
    const safeMax = Math.max(Math.min(transactionRemaining, invoiceRemaining), 0);
    const parsed = Number.parseFloat(matchAmountInput.value || '');
    const preferred = Number.isFinite(parsed) && parsed > 0 ? parsed : getRecommendedMatchAmount(tx, inv);
    const clamped = Math.max(Math.min(preferred, safeMax), 0);

    matchAmountInput.disabled = false;
    matchAmountInput.max = String(safeMax);
    matchAmountInput.value = clamped > 0 ? clamped.toFixed(2) : '';
    matchAmountCurrency.textContent = inv.currency || tx.currency || 'SEK';

    if (transactionRemaining <= 0 || invoiceRemaining <= 0) {
      matchAmountNote.textContent = matchAmountDefaultNote;
    } else if (transactionRemaining < invoiceRemaining) {
      matchAmountNote.textContent = matchAmountLimitTransactionNote;
    } else if (invoiceRemaining < transactionRemaining) {
      matchAmountNote.textContent = matchAmountLimitInvoiceNote;
    } else if (transactionRemaining !== invoiceRemaining) {
      matchAmountNote.textContent = matchAmountLimitBothNote;
    } else {
      matchAmountNote.textContent = matchAmountSelectedNote;
    }
  };

  const renderRecentActivity = (items) => {
    if (!recentActivityEl) return;
    if (!Array.isArray(items) || items.length === 0) {
      recentActivityEl.textContent = activityEmptyMessage;
      return;
    }

    recentActivityEl.innerHTML = items.map((item) => {
      const parts = [
        `<span class="bankrec-activity-type">${escapeHtml(getActionCopy(item.actionType))}</span>`,
        item.transactionId ? `<span>${escapeHtml(item.transactionId)}</span>` : '',
        item.invoiceId ? `<span>${escapeHtml(item.invoiceId)}</span>` : '',
        item.userName ? `<span>${escapeHtml(item.userName)}</span>` : ''
      ].filter(Boolean).join(' · ');

      const note = item.note ? `<div class="bankrec-activity-note">${escapeHtml(item.note)}</div>` : '';
      return `
        <div class="bankrec-activity-item">
          <div class="bankrec-activity-meta">${parts}</div>
          <div class="bankrec-activity-time">${escapeHtml(formatDateTime(item.createdAtUtc))}</div>
          ${note}
        </div>
      `;
    }).join('');
  };

  const renderCurrentAllocations = () => {
    if (!currentAllocationsEl) return;
    const tx = transactions.find((item) => item.id === selectedTxId) || null;
    const allocations = tx ? getTxAllocations(tx) : [];
    if (!tx || allocations.length === 0) {
      currentAllocationsEl.textContent = currentAllocationsEmptyMessage;
      return;
    }

    currentAllocationsEl.innerHTML = allocations.map((allocation, index) => {
      const invoice = getInvoiceById(allocation.invoiceId);
      const invoiceLabel = invoice?.invoiceNo || allocation.invoiceId;
      const customer = invoice?.customerName || '-';
      return `
        <div class="bankrec-allocation-item">
          <div class="bankrec-allocation-top">
            <strong>${escapeHtml(`${allocationPrefix} ${index + 1}`)}</strong>
            <span class="bankrec-confidence-chip ${allocation.matchType === 'manual' ? 'medium' : 'high'}">${escapeHtml(allocation.matchType === 'manual' ? 'Manuell' : 'Auto')}</span>
          </div>
          <div>${escapeHtml(invoiceLabel)} · ${escapeHtml(customer)}</div>
          <div class="bankrec-allocation-meta">${formatAmount(allocation.matchedAmount)} ${escapeHtml(allocation.currency || tx.currency || 'SEK')} · ${escapeHtml(allocation.matchRule || '-')}</div>
          <div class="bankrec-allocation-actions">
            <button type="button" class="btn btn-sm bankrec-btn bankrec-btn-ghost" data-remove-allocation-id="${escapeHtml(allocation.allocationId || '')}" data-remove-invoice-id="${escapeHtml(allocation.invoiceId)}" ${isReconciliationClosed ? 'disabled' : ''}>${escapeHtml(removeAllocationLabel)}</button>
          </div>
        </div>
      `;
    }).join('');

    currentAllocationsEl.querySelectorAll('[data-remove-allocation-id]').forEach((button) => {
      button.addEventListener('click', async () => {
        if (!tx) return;
        try {
          await postJson(reverseMatchEndpoint, {
            transactionId: tx.id,
            allocationId: button.getAttribute('data-remove-allocation-id') || null,
            invoiceId: button.getAttribute('data-remove-invoice-id') || null,
            expectedVersion: currentStateVersion,
            reason: 'Enskild allokering ångrad i bankavstämningens arbetspanel.'
          });
          await loadRecentActivity();
          await loadTransactions(transactionPage);
          await loadInvoices(invoicePage);
          await loadRecentActivity();
          renderTable();
        } catch (error) {
          invoicesLoadError = error instanceof Error ? error.message : 'Ångra allokering misslyckades.';
          renderTable();
        }
      });
    });
  };

  const loadRecentActivity = async () => {
    if (!stateEndpoint) return false;
    try {
      const response = await fetch(stateEndpoint, { headers: { 'X-Requested-With': 'XMLHttpRequest' } });
      const payload = await response.json();
      if (!response.ok || payload.success === false) {
        throw createRequestError(payload, response.status);
      }

      currentStateVersion = Number(payload.version ?? currentStateVersion) || 0;
      persistedMatches = Array.isArray(payload.matches) ? payload.matches : [];
      isReconciliationClosed = Boolean(payload.isClosed);
      reconciliationClosedAtUtc = payload.closedAtUtc || null;
      reconciliationClosedByName = payload.closedByName || '';
      setConflictState(false);
      if (recentActivityEl) {
        renderRecentActivity(payload.recentActivity || []);
      }
      renderTable();
      syncLifecycleState();
      return true;
    } catch {
      persistedMatches = [];
      if (recentActivityEl) {
        renderRecentActivity([]);
      }
      return false;
    }
  };

  const renderManualInvoiceChoices = (targetTxId) => {
    if (!recommendationsEl) return;
    const availableInvoices = invoices.filter((invoice) => getInvoiceRemaining(invoice) > 0);
    recommendedInvoiceLookup = new Map(
      availableInvoices
        .filter((invoice) => invoice?.id)
        .map((invoice) => [invoice.id, invoice])
    );
    if (!availableInvoices.some((invoice) => stringEquals(invoice.id, selectedInvId))) {
      selectedInvId = null;
      renderInvoiceDetail(null);
    }
    if (recommendationsTitleEl) recommendationsTitleEl.textContent = manualInvoiceTitle;

    const invoiceItems = availableInvoices.length === 0
      ? `<div class="bankrec-manual-invoice-empty">${escapeHtml(noInvoicesMessage)}</div>`
      : availableInvoices.map((invoice) => `
          <button type="button"
                  class="bankrec-recommendation-item bankrec-manual-invoice-item${stringEquals(selectedInvId, invoice.id) ? ' is-selected' : ''}"
                  data-manual-invoice-id="${escapeHtml(invoice.id)}">
            <div class="bankrec-recommendation-top">
              <strong>${escapeHtml(invoice.invoiceNo || invoice.id)}</strong>
              <span class="bankrec-manual-invoice-badge">${escapeHtml(manualInvoiceChoiceLabel)}</span>
            </div>
            <div class="bankrec-recommendation-body">${escapeHtml(invoice.customerName || '-')} · ${formatAmount(getInvoiceRemaining(invoice))} ${escapeHtml(invoice.currency || '')}</div>
            <div class="bankrec-recommendation-meta">OCR ${escapeHtml(invoice.ocr || '-')} · ${escapeHtml(invoice.dueDate || '-')}</div>
          </button>
        `).join('');

    recommendationsEl.innerHTML = `
      <div class="bankrec-manual-invoice-intro">${escapeHtml(manualInvoiceDescription)}</div>
      <div class="bankrec-manual-invoice-list">${invoiceItems}</div>
      <div class="bankrec-manual-invoice-pagination">
        <button type="button" class="btn btn-portal btn-portal-outline btn-sm" data-manual-invoice-page="${invoicePage - 1}" ${invoicePage <= 1 ? 'disabled' : ''}>${escapeHtml(invPrevBtn?.textContent?.trim() || 'Föregående')}</button>
        <span>${invoicePage} / ${Math.max(invoiceTotalPages, 1)}</span>
        <button type="button" class="btn btn-portal btn-portal-outline btn-sm" data-manual-invoice-page="${invoicePage + 1}" ${invoicePage >= invoiceTotalPages ? 'disabled' : ''}>${escapeHtml(invNextBtn?.textContent?.trim() || 'Nästa')}</button>
      </div>
    `;

    recommendationsEl.querySelectorAll('[data-manual-invoice-id]').forEach((button) => {
      button.addEventListener('click', () => {
        const invoiceId = button.getAttribute('data-manual-invoice-id');
        const invoice = getInvoiceById(invoiceId);
        if (!invoice) return;
        selectedInvId = invoice.id;
        renderInvoiceDetail(invoice);
        renderTable();
        renderManualInvoiceChoices(targetTxId);
      });
    });

    recommendationsEl.querySelectorAll('[data-manual-invoice-page]').forEach((button) => {
      button.addEventListener('click', async () => {
        const nextPage = Number(button.getAttribute('data-manual-invoice-page'));
        if (!Number.isInteger(nextPage) || nextPage < 1 || nextPage > invoiceTotalPages) return;
        recommendationsEl.innerHTML = `<div class="bankrec-inline-loading"><span class="bankrec-spinner"></span><span>${escapeHtml(completeItemsLoadingMessage)}</span></div>`;
        try {
          const payload = await fetchInvoicePage(nextPage, invoicePageSize);
          if (!stringEquals(selectedTxId, targetTxId)) return;
          invoices = Array.isArray(payload.items) ? payload.items : [];
          invoicePage = Number(payload.page) || nextPage;
          invoiceTotalPages = Math.max(Number(payload.totalPages) || 1, 1);
          invoiceTotalCount = Number(payload.totalCount) || 0;
          renderTable();
          renderManualInvoiceChoices(targetTxId);
        } catch {
          if (!stringEquals(selectedTxId, targetTxId)) return;
          recommendationsEl.textContent = invoicesErrorMessage;
        }
      });
    });

    renderWorkPanel();
  };

  const renderRecommendations = (state, targetTxId = selectedTxId) => {
    if (!recommendationsEl) return;
    if (targetTxId && selectedTxId && !stringEquals(targetTxId, selectedTxId)) return;

    if (state === null) {
      if (recommendationsTitleEl) recommendationsTitleEl.textContent = recommendationTitlePlural;
      recommendedInvoiceLookup = new Map();
      if (targetTxId) {
        recommendationCache.delete(targetTxId);
      }
      selectedInvId = null;
      renderInvoiceDetail(null);
      recommendationsEl.textContent = recommendationEmptyMessage;
      renderWorkPanel();
      return;
    }
    if (state === 'loading') {
      if (recommendationsTitleEl) recommendationsTitleEl.textContent = recommendationTitlePlural;
      recommendedInvoiceLookup = new Map();
      if (targetTxId) {
        recommendationCache.delete(targetTxId);
      }
      selectedInvId = null;
      renderInvoiceDetail(null);
      recommendationsEl.innerHTML = `<div class="bankrec-inline-loading"><span class="bankrec-spinner"></span><span>${escapeHtml(recommendationLoadingMessage)}</span></div>`;
      renderWorkPanel();
      return;
    }

    if (!Array.isArray(state) || state.length === 0) {
      if (targetTxId) {
        recommendationCache.set(targetTxId, []);
      }
      selectedInvId = null;
      renderInvoiceDetail(null);
      renderManualInvoiceChoices(targetTxId);
      return;
    }

    const orderedState = state
      .slice()
      .map((item) => normalizeRecommendationItem(item))
      .filter(Boolean)
      .sort((a, b) => (Number(b?.confidence?.score ?? 0) || 0) - (Number(a?.confidence?.score ?? 0) || 0));

    if (targetTxId) {
      recommendationCache.set(targetTxId, orderedState);
    }

    recommendedInvoiceLookup = new Map(
      orderedState
        .filter((item) => item?.invoice?.id)
        .map((item) => [item.invoice.id, item.invoice])
    );

    if (!orderedState.some((item) => item?.invoice?.id === selectedInvId)) {
      selectedInvId = orderedState[0]?.invoice?.id || null;
    }

    const showSingularRecommendation = orderedState.length === 1 && !orderedState[0]?.requiresManualConfirmation;
    if (recommendationsTitleEl) {
      recommendationsTitleEl.textContent = showSingularRecommendation
        ? recommendationTitleSingular
        : recommendationTitlePlural;
    }

    const selectedInvoice = getInvoiceById(selectedInvId);
    renderInvoiceDetail(selectedInvoice);

    recommendationsEl.innerHTML = orderedState.map((item, index) => `
      <button type="button" class="bankrec-recommendation-item${selectedInvId === item.invoice.id ? ' is-selected' : ''}" data-recommended-invoice-id="${escapeHtml(item.invoice.id)}">
        <div class="bankrec-recommendation-top">
          <strong>${escapeHtml(`${index + 1}. ${item.invoice.invoiceNo || item.invoice.id}`)}</strong>
          <span class="bankrec-confidence-chip ${item.confidence.level === 'Hög' ? 'high' : item.confidence.level === 'Medel' ? 'medium' : 'low'}">${escapeHtml(getConfidenceCopy(item.confidence.level))}</span>
        </div>
        <div class="bankrec-recommendation-body">${escapeHtml(item.invoice.customerName || '-')} · ${formatAmount(item.invoice.amount)} ${escapeHtml(item.invoice.currency || '')}</div>
        <div class="bankrec-recommendation-meta">${escapeHtml(item.ruleLabel)} · ${escapeHtml(item.ruleHelp)}</div>
        ${renderRecommendationEvidence(item.evidence)}
        ${item.requiresManualConfirmation ? renderManualConfirmationNotice(item.manualConfirmationReason) : ''}
      </button>
    `).join('');

    recommendationsEl.querySelectorAll('[data-recommended-invoice-id]').forEach((button) => {
      button.addEventListener('click', () => {
        const invoiceId = button.getAttribute('data-recommended-invoice-id');
        const invoice = getInvoiceById(invoiceId);
        if (!invoice) return;
        selectedInvId = invoice.id;
        renderInvoiceDetail(invoice);
        renderTable();
      });
    });

    renderWorkPanel();
  };

  const buildRecommendations = async (tx, showLoading = true) => {
    if (!tx || !isCredit(tx) || !isCustomerReceipt(tx)) {
      renderRecommendations(null);
      return;
    }

    const targetTxId = tx.id;
    const requestToken = ++latestRecommendationsToken;
    if (showLoading) {
      renderRecommendations('loading', targetTxId);
    }

    try {
      const url = new URL(recommendationsEndpoint, window.location.origin);
      url.searchParams.set('transactionId', targetTxId);
      const response = await fetch(url.toString(), { headers: { 'X-Requested-With': 'XMLHttpRequest' } });
      const payload = await response.json();
      if (requestToken !== latestRecommendationsToken || !stringEquals(selectedTxId, targetTxId)) return;
      if (!response.ok || payload.success === false) {
        throw createRequestError(payload, response.status);
      }

      const candidates = Array.isArray(payload.items) ? payload.items : [];
      renderRecommendations(candidates, targetTxId);
      syncAiForRecommendations(tx, candidates, targetTxId);
    } catch {
      if (requestToken !== latestRecommendationsToken || !stringEquals(selectedTxId, targetTxId)) return;
      renderRecommendations([], targetTxId);
      syncAiForRecommendations(tx, [], targetTxId);
    }
  };

  const buildAiSuggestions = async (tx) => {
    const targetTxId = tx?.id || null;
    if (!targetTxId || !isCredit(tx) || !isCustomerReceipt(tx)) {
      renderAiSuggestions(null);
      return;
    }

    if (!aiSuggestionsEndpoint) {
      renderAiSuggestions({ status: 'disabled', enabled: false, message: aiDisabledMessage }, targetTxId);
      return;
    }

    const token = ++latestAiSuggestionsToken;
    try {
      const url = new URL(aiSuggestionsEndpoint, window.location.origin);
      url.searchParams.set('transactionId', targetTxId);
      const response = await fetch(url.toString(), { headers: { 'X-Requested-With': 'XMLHttpRequest' } });
      const payload = await response.json();
      if (!response.ok || payload.success === false) {
        throw createRequestError(payload, response.status);
      }

      if (token !== latestAiSuggestionsToken) return;
      renderAiSuggestions(payload.result || null, targetTxId);
    } catch (error) {
      if (token !== latestAiSuggestionsToken) return;
      renderAiSuggestions({
        status: 'disabled',
        enabled: false,
        message: error instanceof Error ? error.message : aiDisabledMessage
      }, targetTxId);
    }
  };

  const renderWorkPanel = () => {
    const tx = getTransactionById(selectedTxId);
    const inv = getInvoiceById(selectedInvId);

    if (selectedTxSummary) {
      const allocations = tx ? getTxAllocations(tx) : [];
      const coding = tx ? resolveCodingForClassification(getClassification(tx)) : null;
      selectedTxSummary.innerHTML = tx
        ? `<strong>${escapeHtml(tx.id)}</strong> · ${escapeHtml(tx.date || '')} · ${formatAmount(tx.amount)} ${escapeHtml(tx.currency || '')}${coding && coding.hasValue ? `<div class="small text-muted mt-1">${escapeHtml(codingAccountLabel)} ${escapeHtml(coding.account || '—')} · ${escapeHtml(codingCostCenterLabel)} ${escapeHtml(coding.costCenter || '—')} · ${escapeHtml(coding.sourceLabel)}${coding.isInherited ? ' · ärvd' : ''}</div>` : ''}`
        : 'Välj en transaktion.';
      selectedTxSummary.title = tx
        ? `${tx.reference || '-'} · ${allocations.length > 0 ? `allokerat ${formatAmount(getMatchedAmount(tx))} ${tx.currency || ''} · kvar ${formatAmount(getTransactionRemaining(tx))}` : 'ej allokerad'}`
        : '';
    }

    if (selectedInvSummary) {
      if (inv) {
        selectedInvSummary.innerHTML = `<strong>${escapeHtml(inv.invoiceNo || inv.id)}</strong> · ${formatAmount(inv.amount)} ${escapeHtml(inv.currency || '')}`;
        selectedInvSummary.title = `${inv.customerName || '-'} · ${inv.invoiceNo || inv.id}`;
      } else if (tx) {
        selectedInvSummary.innerHTML = 'Välj en föreslagen faktura.';
        selectedInvSummary.title = '';
      } else {
        selectedInvSummary.innerHTML = 'Välj faktura efter transaktion.';
        selectedInvSummary.title = '';
      }
    }

    updateMatchAmountUi(tx, inv);

    if (differenceEl) {
      if (tx && inv) {
        const matchAmount = getSelectedMatchAmount();
        const transactionRemainingAfter = Math.max(getEditableTransactionRemaining(tx, inv.id) - matchAmount, 0);
        const invoiceRemainingAfter = Math.max(getEditableInvoiceRemaining(inv, tx) - matchAmount, 0);
        differenceEl.textContent = `Match ${formatAmount(matchAmount)} ${inv.currency || ''} · kvar tx ${formatAmount(transactionRemainingAfter)} · kvar fak ${formatAmount(invoiceRemainingAfter)}`;
      } else {
        differenceEl.textContent = '—';
      }
    }

    if (confidenceSummary) {
      if (tx && inv && window.BankRecMatching?.describeMatch) {
        const detail = window.BankRecMatching.describeMatch(tx, inv);
        const requiresManual = requiresManualConfirmation(detail);
        confidenceSummary.innerHTML = `
          <div class="bankrec-confidence-summary-row">
            <span class="bankrec-confidence-chip ${detail.confidence.level === 'Hög' ? 'high' : detail.confidence.level === 'Medel' ? 'medium' : 'low'}">${escapeHtml(getConfidenceCopy(detail.confidence.level))}</span>
            <span class="bankrec-confidence-score">${escapeHtml(formatConfidenceScore(detail.confidence))}</span>
          </div>
          <div class="bankrec-confidence-summary-text">${escapeHtml(detail.ruleLabel)}${requiresManual ? ` · ${escapeHtml(manualConfirmationLabel)}` : ''}</div>
        `;
      } else if (tx && getTxAllocations(tx).length > 0 && window.BankRecMatching?.describeMatch) {
        const matchedInvoice = getInvoiceById(getTxAllocations(tx)[0]?.invoiceId);
        if (matchedInvoice) {
          const detail = window.BankRecMatching.describeMatch(tx, matchedInvoice);
          const requiresManual = requiresManualConfirmation(detail);
          confidenceSummary.innerHTML = `
            <div class="bankrec-confidence-summary-row">
              <span class="bankrec-confidence-chip ${detail.confidence.level === 'Hög' ? 'high' : detail.confidence.level === 'Medel' ? 'medium' : 'low'}">${escapeHtml(getConfidenceCopy(detail.confidence.level))}</span>
              <span class="bankrec-confidence-score">${escapeHtml(formatConfidenceScore(detail.confidence))}</span>
            </div>
            <div class="bankrec-confidence-summary-text">${escapeHtml(detail.ruleLabel)}${requiresManual ? ` · ${escapeHtml(manualConfirmationLabel)}` : ''}</div>
          `;
        } else {
          confidenceSummary.textContent = confidenceEmptyMessage;
        }
      } else if (tx) {
        const cachedRecommendations = recommendationCache.get(tx.id);
        const topRecommendation = Array.isArray(cachedRecommendations) && cachedRecommendations.length > 0
          ? cachedRecommendations[0]
          : getLocalRecommendations(tx, 1)[0];

        if (topRecommendation) {
          confidenceSummary.innerHTML = `
            <div class="bankrec-confidence-summary-row">
              <span class="bankrec-confidence-chip ${topRecommendation.confidence.level === 'Hög' ? 'high' : topRecommendation.confidence.level === 'Medel' ? 'medium' : 'low'}">${escapeHtml(getConfidenceCopy(topRecommendation.confidence.level))}</span>
              <span class="bankrec-confidence-score">${escapeHtml(formatConfidenceScore(topRecommendation.confidence))}</span>
            </div>
            <div class="bankrec-confidence-summary-text">${escapeHtml(topRecommendation.ruleLabel)}${topRecommendation.requiresManualConfirmation ? ` · ${escapeHtml(manualConfirmationLabel)}` : ''}</div>
          `;
        } else {
          confidenceSummary.innerHTML = `
            <div class="bankrec-confidence-summary-row">
              <span class="bankrec-confidence-chip low">${escapeHtml(getConfidenceCopy('Låg'))}</span>
              <span class="bankrec-confidence-score">0%</span>
            </div>
            <div class="bankrec-confidence-summary-text">${escapeHtml(noSafeRecommendationMessage)}</div>
          `;
        }
      } else {
        confidenceSummary.textContent = confidenceEmptyMessage;
      }
    }

    if (manualBtn) manualBtn.disabled = isReconciliationClosed || !(tx && inv && getSelectedMatchAmount() > 0);
    if (undoBtn) undoBtn.disabled = isReconciliationClosed || !(tx && getTxAllocations(tx).length > 0);
  };

  const updateTotals = () => {
    const creditTotal = Number(transactionTotals.credit || 0);
    const debitTotal = Number(transactionTotals.debit || 0);
    const matchedTotal = Number(transactionTotals.matched || 0);
    const unmatchedTotal = Number(transactionTotals.unmatched || 0);
    if (totalCreditEl) totalCreditEl.textContent = `${formatAmount(creditTotal)} SEK`;
    if (totalDebitEl) totalDebitEl.textContent = `${formatAmount(debitTotal)} SEK`;
    if (totalMatchedEl) totalMatchedEl.textContent = `${formatAmount(matchedTotal)} SEK`;
    if (totalUnmatchedEl) totalUnmatchedEl.textContent = `${formatAmount(unmatchedTotal)} SEK`;
  };

  const syncLifecycleState = () => {
    const isReady = hasTransactionSummaryLoaded
      && Number(summaryCounts.review || 0) === 0
      && Number(summaryCounts.unmatched || 0) === 0;
    pageRoot?.classList.toggle('is-reconciliation-closed', isReconciliationClosed);
    pageRoot?.setAttribute('data-reconciliation-closed', String(isReconciliationClosed));
    if (closeBtn) {
      closeBtn.classList.toggle('d-none', isReconciliationClosed);
      closeBtn.disabled = isReconciliationClosed || !isReady || !closeEndpoint;
    }
    reopenControlsEl?.classList.toggle('d-none', !isReconciliationClosed);
    if (lifecycleStatusEl) {
      const closedAt = reconciliationClosedAtUtc ? formatDateTime(reconciliationClosedAtUtc) : '—';
      lifecycleStatusEl.textContent = isReconciliationClosed
        ? formatText(closedStatusTemplate, closedAt, reconciliationClosedByName || '—')
        : isReady ? closeReadyStatus : closePendingStatus;
    }
    [matchBtn, resetBtn, manualBtn, undoBtn].forEach((button) => {
      if (button && isReconciliationClosed) button.disabled = true;
    });
  };

  const updateStatusSummary = () => {
    const matched = Number(summaryCounts.matched || 0);
    const review = Number(summaryCounts.review || 0);
    const unmatched = Number(summaryCounts.unmatched || 0);
    if (demoSummaryMatchedEl) demoSummaryMatchedEl.textContent = String(matched);
    if (demoSummaryReviewEl) demoSummaryReviewEl.textContent = String(review);
    if (demoSummaryUnmatchedEl) demoSummaryUnmatchedEl.textContent = String(unmatched);
    if (completeMatchedEl) completeMatchedEl.textContent = String(matched);
    if (completeReviewEl) completeReviewEl.textContent = String(review);
    if (completeUnmatchedEl) completeUnmatchedEl.textContent = String(unmatched);

    const isReady = hasTransactionSummaryLoaded && review === 0 && unmatched === 0;
    completeStateEl?.classList.toggle('is-ready', isReady || isReconciliationClosed);
    if (completeTitleEl) {
      completeTitleEl.textContent = isReconciliationClosed
        ? closedTitle
        : isReady ? completeReadyTitle : completePendingTitle;
    }
    if (completeMessageEl) {
      completeMessageEl.textContent = isReconciliationClosed
        ? closedMessage
        : isReady ? completeReadyMessage : completePendingMessage;
    }
    syncLifecycleState();
  };

  const getClassification = (tx) => tx?.classification || null;

  const getClassificationLabel = (classification) => {
    if (!classification) return 'DEF';
    return classification.typeLabel || classification.TypeLabel || classification.typeKey || classification.TypeKey || 'DEF';
  };

  const getClassificationKey = (classification) => {
    if (!classification) return 'def';
    return classification.typeKey || classification.TypeKey || 'def';
  };

  const getTransactionById = (transactionId) => {
    if (!transactionId) return null;
    return transactions.find((item) => item.id === transactionId)
      || transactionCache.get(transactionId)
      || null;
  };

  const resolveCodingForClassification = (classification) => {
    const key = getClassificationKey(classification);
    const baseline = codingRuleBaseline.get(String(key || '').toLowerCase()) || {};
    const override = getCodingOverride(key);
    const account = override.account || baseline.account || classification?.suggestedAccount || classification?.SuggestedAccount || '';
    const costCenter = override.costCenter || baseline.costCenter || classification?.suggestedCostCenter || classification?.SuggestedCostCenter || '';
    const sourceKey = override.sourceBankAccountKey || baseline.sourceBankAccountKey || codingBankAccountKey || 'DEFAULT';

    return {
      key,
      account,
      costCenter,
      sourceKey,
      sourceLabel: resolveCodingSourceLabel(sourceKey),
      isInherited: Boolean(override.isInherited || baseline.isInherited),
      hasValue: Boolean(account || costCenter)
    };
  };

  const resolveCodingSourceLabel = (sourceKey) => {
    if (stringEquals(sourceKey, codingBankAccountKey)) {
      return codingBankAccountLabel || codingBankAccountKey || 'Aktuellt konto';
    }

    if (stringEquals(sourceKey, 'DEFAULT')) {
      return 'Bolagsstandard';
    }

    return sourceKey || 'Bolagsstandard';
  };

  const getClassificationAggregate = () => {
    return Array.isArray(transactionClassificationSummary) ? transactionClassificationSummary : [];
  };

  const renderClassificationFilters = (items) => {
    if (!classificationFilterEl) return;

    const options = [
      {
        key: 'all',
        label: classificationAllTypesLabel,
        count: transactionGroupCounts.all || 0,
        active: classificationTypeFilter === 'all'
      },
      ...items.map((item) => ({
        key: item.key,
        label: item.label,
        count: item.count,
        active: classificationTypeFilter === item.key
      }))
    ];

    classificationFilterEl.innerHTML = options.map((option) => `
      <button type="button" class="bankrec-classification-filter__chip ${option.active ? 'is-active' : ''}" data-classification-filter="${escapeHtml(option.key)}">
        <span>${escapeHtml(option.label)}</span>
        <strong>${option.count}</strong>
      </button>
    `).join('');

    classificationFilterEl.querySelectorAll('[data-classification-filter]').forEach((button) => {
      button.addEventListener('click', async () => {
        const nextFilter = button.getAttribute('data-classification-filter') || 'all';
        classificationTypeFilter = nextFilter;
        await Promise.all([
          loadTransactions(1),
          loadInvoices(1)
        ]);
      });
    });
  };

  const renderClassificationSummary = () => {
    if (!classificationSummaryEl) return;

    const items = getClassificationAggregate();
    if (classificationTypeFilter !== 'all' && !items.some((item) => item.key === classificationTypeFilter)) {
      classificationTypeFilter = 'all';
    }
    renderClassificationFilters(items);

    if (items.length === 0) {
      classificationSummaryEl.innerHTML = `
        <div class="bankrec-classification-empty">
          Inga transaktioner att klassificera just nu.
        </div>
      `;
      return;
    }

    classificationSummaryEl.innerHTML = items.map((item) => `
      <div class="bankrec-classification-card ${item.isDefault ? 'is-default' : ''} ${classificationTypeFilter === item.key ? 'is-selected' : ''}">
        <button type="button" class="bankrec-classification-card__button" data-classification-filter="${escapeHtml(item.key)}" aria-label="${escapeHtml(item.label)}">
          <div class="bankrec-classification-card__top">
            <div>
              <div class="bankrec-classification-card__label">${escapeHtml(item.label)}</div>
              <div class="bankrec-classification-card__meta">${escapeHtml(item.ruleLabel)}${item.isDefault ? ' · DEF' : ''}</div>
            </div>
            ${item.isDefault ? '<span class="badge rounded-pill bankrec-classification-card__badge">DEF</span>' : ''}
          </div>
          <div class="bankrec-classification-card__amount">${formatAmount(item.amount)} SEK</div>
          <div class="bankrec-classification-card__suggestions">
            <span>${escapeHtml(suggestedAccountLabel)}: <strong>${escapeHtml(item.suggestedAccount || '—')}</strong></span>
            <span>${escapeHtml(suggestedCostCenterLabel)}: <strong>${escapeHtml(item.suggestedCostCenter || '—')}</strong></span>
          </div>
          <div class="bankrec-classification-card__footer">${item.count} transaktioner</div>
        </button>
      </div>
    `).join('');

    classificationSummaryEl.querySelectorAll('[data-classification-filter]').forEach((button) => {
      button.addEventListener('click', async () => {
        const nextFilter = button.getAttribute('data-classification-filter') || 'all';
        classificationTypeFilter = nextFilter;
        await Promise.all([
          loadTransactions(1),
          loadInvoices(1)
        ]);
      });
    });
  };

  const getCodingRows = () => {
    const items = getClassificationAggregate().map((item) => {
      const baseline = codingRuleBaseline.get(String(item.key || '').toLowerCase()) || {};
      const override = getCodingOverride(item.key);
      return {
        ...item,
        displayLabel: item.isDefault ? 'DEF' : item.label,
        displayRuleLabel: item.isDefault ? 'Standard' : item.ruleLabel,
        account: override.account || baseline.account || item.suggestedAccount || '',
        costCenter: override.costCenter || baseline.costCenter || item.suggestedCostCenter || '',
        hasOverride: Boolean(override.account || override.costCenter),
        sourceBankAccountKey: override.sourceBankAccountKey || baseline.sourceBankAccountKey || codingBankAccountKey || 'default',
        isInherited: Boolean(override.isInherited || baseline.isInherited)
      };
    });

    if (!items.some((item) => item.isDefault)) {
      const totalAmount = items.reduce((sum, item) => sum + Number(item.amount || 0), 0);
      items.unshift({
        key: 'def',
        label: 'DEF',
        displayLabel: 'DEF',
        count: transactionGroupCounts.all || items.reduce((sum, item) => sum + Number(item.count || 0), 0),
        amount: totalAmount,
        defaultCount: transactionGroupCounts.all || items.reduce((sum, item) => sum + Number(item.defaultCount || 0), 0),
        ruleLabel: 'Standard',
        displayRuleLabel: 'Standard',
        suggestedAccount: '',
        suggestedCostCenter: '',
        account: '',
        costCenter: '',
        hasOverride: false,
        sourceBankAccountKey: codingBankAccountKey || 'default',
        isInherited: false,
        isDefault: true
      });
    }

    return items.sort((left, right) => {
      if (left.isDefault !== right.isDefault) return left.isDefault ? -1 : 1;
      if (right.count !== left.count) return right.count - left.count;
      return left.displayLabel.localeCompare(right.displayLabel, 'sv-SE');
    });
  };

  const getCodingOverride = (key) => codingOverrides.get(String(key || '').toLowerCase()) || {};

  const syncCodingDirtyState = () => {
    codingDirtyEl?.classList.toggle('d-none', !hasUnsavedCodingChanges);
  };

  const setCodingOverride = (key, field, value) => {
    const normalizedKey = String(key || '').toLowerCase();
    const current = getCodingOverride(normalizedKey);
    const baseline = codingRuleBaseline.get(normalizedKey) || {};
    const normalized = String(value || '').trim();
    const next = {
      account: current.account ?? baseline.account ?? '',
      costCenter: current.costCenter ?? baseline.costCenter ?? '',
      [field]: normalized,
      sourceBankAccountKey: codingBankAccountKey || 'default',
      isInherited: false
    };

    const matchesBaseline = stringEquals(next.account, baseline.account)
      && stringEquals(next.costCenter, baseline.costCenter);
    if (matchesBaseline || (!next.account && !next.costCenter)) {
      codingOverrides.delete(normalizedKey);
      return;
    }

    codingOverrides.set(normalizedKey, next);
  };

  const renderCodingSummary = () => {
    if (!codingSummaryEl) return;
    if (codingBankAccountEl) {
      codingBankAccountEl.textContent = codingBankAccountLabel
        ? `${codingBankAccountLabel} · ${codingBankAccountKey || 'default'}`
        : `${codingBankAccountKey || 'default'}`;
    }

    const items = getCodingRows();
    if (items.length === 0) {
      codingSummaryEl.innerHTML = `
        <div class="bankrec-coding-empty">
          Inga transaktioner att kontera just nu.
        </div>
      `;
      return;
    }

    codingSummaryEl.innerHTML = items.map((item) => {
      const account = item.account ?? '';
      const costCenter = item.costCenter ?? '';
      const hasOverride = Boolean(item.hasOverride);
      const sourceLabel = stringEquals(item.sourceBankAccountKey, codingBankAccountKey)
        ? codingBankAccountLabel || codingBankAccountKey
        : 'Bolagsstandard';

      return `
        <div class="bankrec-coding-card ${item.isDefault ? 'is-default' : ''} ${hasOverride ? 'is-overridden' : ''}">
          <div class="bankrec-coding-card__top">
            <div>
              <div class="bankrec-coding-card__label">${escapeHtml(item.displayLabel)}</div>
              <div class="bankrec-coding-card__meta">${escapeHtml(item.displayRuleLabel)}${item.isDefault ? ' · DEF' : ''}</div>
            </div>
            ${item.isDefault ? '<span class="badge rounded-pill bankrec-coding-card__badge">DEF</span>' : ''}
          </div>
          <div class="bankrec-coding-card__amount">${formatAmount(item.amount)} SEK</div>
          <div class="bankrec-coding-card__stats">${item.count} transaktioner</div>
          <div class="bankrec-coding-card__source">${escapeHtml(sourceLabel)}${item.isInherited ? ' · ärvd' : ''}</div>
          <div class="bankrec-coding-card__fields">
            <label class="bankrec-coding-field">
              <span>${escapeHtml(codingAccountLabel)}</span>
              <input type="text" class="form-control form-control-sm bankrec-coding-input" data-coding-field="account" data-coding-key="${escapeHtml(item.key)}" value="${escapeHtml(account)}" placeholder="${escapeHtml(item.suggestedAccount || '—')}" />
            </label>
            <label class="bankrec-coding-field">
              <span>${escapeHtml(codingCostCenterLabel)}</span>
              <input type="text" class="form-control form-control-sm bankrec-coding-input" data-coding-field="costCenter" data-coding-key="${escapeHtml(item.key)}" value="${escapeHtml(costCenter)}" placeholder="${escapeHtml(item.suggestedCostCenter || '—')}" />
            </label>
          </div>
          <div class="bankrec-coding-card__footer">
            <button type="button" class="btn btn-portal btn-portal-outline btn-sm bankrec-coding-reset" data-coding-reset="${escapeHtml(item.key)}">${escapeHtml(codingResetLabel)}</button>
            <div class="bankrec-coding-card__hint">${hasOverride ? 'Lokalt ändrat i vyn.' : 'Förslaget följer classifiern.'}</div>
          </div>
        </div>
      `;
    }).join('');

    codingSummaryEl.querySelectorAll('[data-coding-field]').forEach((input) => {
      input.addEventListener('input', () => {
        const key = input.getAttribute('data-coding-key') || 'def';
        const field = input.getAttribute('data-coding-field');
        if (field !== 'account' && field !== 'costCenter') return;
        setCodingOverride(key, field, input.value);
        hasUnsavedCodingChanges = codingOverrides.size > 0;
        syncCodingDirtyState();
      });
    });

    codingSummaryEl.querySelectorAll('[data-coding-reset]').forEach((button) => {
      button.addEventListener('click', () => {
        const key = String(button.getAttribute('data-coding-reset') || 'def').toLowerCase();
        codingOverrides.delete(key);
        hasUnsavedCodingChanges = codingOverrides.size > 0;
        syncCodingDirtyState();
        renderCodingSummary();
      });
    });

    if (codingSaveBtn) {
      codingSaveBtn.disabled = !codingSaveEndpoint || !codingBankAccountKey;
      codingSaveBtn.textContent = codingSaveLabel;
    }
  };

  const saveCodingRules = async () => {
    if (!codingSaveEndpoint || !codingBankAccountKey) return;
    if (codingSaveBtn) {
      codingSaveBtn.disabled = true;
      codingSaveBtn.textContent = '...';
    }
    let savedSuccessfully = false;

    try {
      const rows = getCodingRows().map((item, index) => ({
        rowId: item.rowId || null,
        typeKey: item.key,
        typeLabel: item.displayLabel,
        ruleLabel: item.displayRuleLabel,
        sourceBankAccountKey: item.sourceBankAccountKey || codingBankAccountKey || 'default',
        suggestedAccount: item.suggestedAccount || null,
        suggestedCostCenter: item.suggestedCostCenter || null,
        account: item.account || null,
        costCenter: item.costCenter || null,
        isDefault: Boolean(item.isDefault),
        isInherited: Boolean(item.isInherited),
        sortOrder: index,
        enabled: true
      }));

      const result = await postJson(codingSaveEndpoint, {
        bankAccountKey: codingBankAccountKey,
        bankAccountLabel: codingBankAccountLabel,
        expectedVersion: codingRuleSetVersion,
        rows
      });

      codingRuleSetVersion = Number(result.version ?? codingRuleSetVersion) || codingRuleSetVersion;
      codingRuleBaseline = new Map(
        Array.isArray(result.rows)
          ? result.rows
            .filter((row) => row?.typeKey || row?.TypeKey)
            .map((row) => [String(row.typeKey || row.TypeKey).toLowerCase(), {
              account: row.account || row.Account || '',
              costCenter: row.costCenter || row.CostCenter || '',
              sourceBankAccountKey: row.sourceBankAccountKey || row.SourceBankAccountKey || '',
              isInherited: Boolean(row.isInherited || row.IsInherited)
            }])
          : []
      );
      codingOverrides = new Map();
      hasUnsavedCodingChanges = false;
      syncCodingDirtyState();
      renderCodingSummary();
      if (codingSaveBtn) {
        savedSuccessfully = true;
        codingSaveBtn.textContent = codingSaveSuccessLabel;
        window.setTimeout(() => {
          if (codingSaveBtn) {
            codingSaveBtn.textContent = codingSaveLabel;
          }
        }, 1200);
      }
    } catch (error) {
      handleBankRecError(error, codingSaveFailureLabel);
      renderCodingSummary();
    } finally {
      if (codingSaveBtn) {
        if (!savedSuccessfully) {
          codingSaveBtn.textContent = codingSaveLabel;
        }
        codingSaveBtn.disabled = !codingSaveEndpoint || !codingBankAccountKey;
      }
    }
  };

  const syncSummaryCards = () => {
    completeMatchedCard?.classList.toggle('is-active', summaryView === 'matched');
    completeReviewCard?.classList.toggle('is-active', summaryView === 'review');
    completeUnmatchedCard?.classList.toggle('is-active', summaryView === 'unmatched');
  };

  const isTxMatched = (tx) => getTxAllocations(tx).length > 0;
  const isInvMatched = (inv) => getInvoicePaid(inv.id) > 0;
  const isInvPartial = (inv) => {
    const paid = getInvoicePaid(inv.id);
    const remaining = getInvoiceRemaining(inv);
    return paid > 0 && remaining > 0;
  };

  const applyFilter = (items, filter, matcher, partialMatcher) => {
    if (filter === 'matched') return items.filter(matcher);
    if (filter === 'partial') return partialMatcher ? items.filter(partialMatcher) : items;
    if (filter === 'unmatched') return items.filter((item) => !matcher(item));
    return items;
  };

  const isTxPartial = (tx) => {
    const matchedAmount = getMatchedAmount(tx);
    return matchedAmount > 0 && matchedAmount < (tx?.amount || 0);
  };

  const getManualReviewItems = () => manualReviewQueueItems
    .filter((tx) => isCredit(tx) && isCustomerReceipt(tx) && !isTxMatched(tx))
    .map((tx) => {
      const recommendations = recommendationCache.get(tx.id) || getLocalRecommendations(tx, 3);
      const topRecommendation = recommendations[0] || null;
      return { tx, topRecommendation };
    })
    .filter((item) => !item.topRecommendation || item.topRecommendation.requiresManualConfirmation);

  const getPartialTransactionItems = () => transactions.filter((tx) => isTxPartial(tx));

  const syncScopeFilterButtons = (scope, value) => {
    if (scope === 'tx' && txFilterSelect) {
      txFilterSelect.value = value;
    }
    if (scope === 'inv' && invFilterSelect) {
      invFilterSelect.value = value;
    }
  };

  const syncGroupFilterButtons = (value) => {
    if (txGroupFilterSelect) {
      txGroupFilterSelect.value = value;
    }
  };

  const getWorkspaceModeCopy = () => {
    if (workspaceMode === 'classification') {
      return { title: workspaceModeClassificationLabel, description: workspaceModeClassificationDescription };
    }
    if (workspaceMode === 'complete') {
      return {
        title: workspaceModeCompleteBtn?.querySelector('.bankrec-process-step__title')?.textContent || completePendingTitle,
        description: workspaceModeCompleteBtn?.querySelector('.bankrec-process-step__meta')?.textContent || completePendingMessage
      };
    }
    if (workspaceMode === 'manual-review') {
      return { title: workspaceModeReconciliationLabel, description: workspaceModeReconciliationDescription };
    }
    if (workspaceMode === 'auto-match') {
      return { title: workspaceModeAutoLabel, description: workspaceModeAutoDescription };
    }
    if (workspaceMode === 'partial-payments') {
      return { title: workspaceModePartialLabel, description: workspaceModePartialDescription };
    }
    return { title: workspaceModeOverviewLabel, description: workspaceModeOverviewDescription };
  };

  const syncWorkspaceButtons = () => {
    workspaceModeOverviewBtn?.classList.toggle('is-active', workspaceMode === 'overview');
    workspaceModeClassificationBtn?.classList.toggle('is-active', workspaceMode === 'classification');
    const isMatchingMode = ['manual-review', 'auto-match'].includes(workspaceMode);
    workspaceModeReconciliationBtn?.classList.toggle('is-active', isMatchingMode);
    workspaceModePartialBtn?.classList.toggle('is-active', workspaceMode === 'partial-payments');
    workspaceModeCompleteBtn?.classList.toggle('is-active', workspaceMode === 'complete');
    [workspaceModeOverviewBtn, workspaceModeClassificationBtn, workspaceModeReconciliationBtn, workspaceModePartialBtn, workspaceModeCompleteBtn]
      .forEach((button) => button?.removeAttribute('aria-current'));
    const activeProcessButton = workspaceMode === 'classification'
      ? workspaceModeClassificationBtn
      : workspaceMode === 'complete'
        ? workspaceModeCompleteBtn
        : workspaceMode === 'partial-payments'
          ? workspaceModePartialBtn
          : isMatchingMode
            ? workspaceModeReconciliationBtn
            : workspaceModeOverviewBtn;
    activeProcessButton?.setAttribute('aria-current', 'step');
    matchBtn?.classList.toggle('is-active', workspaceMode === 'auto-match');
    pageRoot?.setAttribute('data-workspace-mode', workspaceMode);
    if (pageRoot) {
      pageRoot.dataset.partialManualCount = String(getPartialTransactionItems().length);
    }
    const modeCopy = getWorkspaceModeCopy();
    if (workspaceModeTitleEl) {
      workspaceModeTitleEl.textContent = modeCopy.title;
    }
    if (workspaceModeDescriptionEl) {
      workspaceModeDescriptionEl.textContent = modeCopy.description;
    }
    if (workpanelTitleEl) {
      workpanelTitleEl.textContent = workspaceMode === 'partial-payments'
        ? workspaceModePartialLabel
        : manualWorkspaceTitle;
    }
    if (workpanelDescriptionEl) {
      workpanelDescriptionEl.textContent = workspaceMode === 'partial-payments'
        ? workspaceModePartialDescription
        : manualWorkspaceDescription;
    }
    const isReviewMode = workspaceMode === 'classification';
    classificationPanelEl?.classList.toggle('d-none', !isReviewMode);
    codingPanelEl?.classList.toggle('d-none', !isReviewMode);
    completePanelEl?.classList.toggle('d-none', workspaceMode !== 'complete');
    if (isReviewMode) {
      renderCodingSummary();
    }

    syncSummaryCards();
  };

  const activateWorkspaceMode = (nextMode) => {
    const leavesCodingReview = workspaceMode === 'classification' && nextMode !== 'classification';
    if (leavesCodingReview && hasUnsavedCodingChanges && !window.confirm(codingUnsavedConfirmText)) {
      return false;
    }

    workspaceMode = nextMode;
    syncWorkspaceButtons();
    return true;
  };

  const runAutoMatchWorkflow = async () => {
    await ensureInitialStateLoaded();
    if (!autoMatchEndpoint) return;
    matchBtn && (matchBtn.disabled = true);
    autoMatchFeedbackEl?.classList.add('d-none');
    setInvoicesLoading(true);
    try {
      workspaceMode = 'auto-match';
      syncWorkspaceButtons();
      const matchedTransactionIdsBefore = new Set(
        persistedMatches
          .map((match) => match?.transactionId)
          .filter(Boolean)
      );
      const result = await postJson(autoMatchEndpoint, {});
      currentStateVersion = Number(result.version ?? currentStateVersion) || currentStateVersion;
      setConflictState(false);
      applyServerMatches(result.matches);
      const matchedTransactionIdsAfter = new Set(
        persistedMatches
          .map((match) => match?.transactionId)
          .filter(Boolean)
      );
      const newAutoMatches = [...matchedTransactionIdsAfter]
        .filter((transactionId) => !matchedTransactionIdsBefore.has(transactionId))
        .length;
      window.BankRecPaymentBundles?.render(result.paymentBundleSuggestions, currentStateVersion);
      clearSelection();
      await applyWorkspaceFilters({
        tx: 'all',
        inv: 'all',
        group: 'all'
      });
      await loadRecentActivity();
      if (autoMatchFeedbackEl) {
        const remainingManual = Number(summaryCounts.review || 0) + Number(summaryCounts.unmatched || 0);
        autoMatchFeedbackEl.textContent = newAutoMatches > 0
          ? formatText(autoMatchSuccessTemplate, newAutoMatches, remainingManual)
          : formatText(autoMatchNoChangeTemplate, remainingManual);
        autoMatchFeedbackEl.classList.remove('d-none');
      }
    } catch (error) {
      invoicesLoadError = handleBankRecError(error, 'Matchning misslyckades.');
      renderTable();
    } finally {
      setInvoicesLoading(false);
      matchBtn && (matchBtn.disabled = isReconciliationClosed);
    }
  };

  const applyWorkspaceFilters = async ({ tx = txFilter, inv = invFilter, group = txGroupFilter }) => {
    txFilter = tx;
    invFilter = inv;
    txGroupFilter = group;
    syncScopeFilterButtons('tx', txFilter);
    syncScopeFilterButtons('inv', invFilter);
    syncGroupFilterButtons(txGroupFilter);
    await loadTransactions(1);
    await loadInvoices(1);
  };

  const getCompletionStatusLabel = (mode) => {
    const card = mode === 'matched'
      ? completeMatchedCard
      : mode === 'review'
        ? completeReviewCard
        : completeUnmatchedCard;
    return card?.querySelector('span')?.textContent?.trim() || mode;
  };

  const getCompletionReason = (mode) => {
    if (mode === 'review') return completeReviewReason;
    if (mode === 'unmatched') return completeUnmatchedReason;
    return completeMatchedReason;
  };

  const renderCompletionItems = (state = completionItems) => {
    if (!completeItemsEl) return;

    if (state === 'loading') {
      completeItemsEl.innerHTML = `<div class="bankrec-complete-items__message"><span class="bankrec-spinner"></span><span>${escapeHtml(completeItemsLoadingMessage)}</span></div>`;
      return;
    }

    if (state === 'error') {
      completeItemsEl.innerHTML = `<div class="bankrec-complete-items__message is-error">${escapeHtml(completeLoadErrorMessage)}</div>`;
      return;
    }

    const items = Array.isArray(state) ? state : [];
    if (items.length === 0) {
      completeItemsEl.innerHTML = `<div class="bankrec-complete-items__message">${escapeHtml(completeItemsEmptyMessage)}</div>`;
      return;
    }

    const statusLabel = getCompletionStatusLabel(summaryView);
    const actionLabel = summaryView === 'matched' ? completeViewAction : completeHandleAction;
    completeItemsEl.innerHTML = `
      <div class="bankrec-complete-items__list">
        ${items.map((tx) => `
          <button type="button"
                  class="bankrec-complete-item"
                  data-completion-transaction="${escapeHtml(tx.id)}"
                  data-completion-status="${escapeHtml(summaryView)}">
            <span class="bankrec-complete-item__main">
              <span class="bankrec-complete-item__top">
                <strong>${escapeHtml(tx.id)}</strong>
                <span class="bankrec-complete-item__status is-${escapeHtml(summaryView)}">${escapeHtml(statusLabel)}</span>
              </span>
              <span class="bankrec-complete-item__body">${escapeHtml(tx.debtorName || '-')} · ${formatAmount(tx.amount)} ${escapeHtml(tx.currency || '')}</span>
              <span class="bankrec-complete-item__meta">${escapeHtml(tx.date || '')} · ${escapeHtml(tx.reference || '-')}</span>
              <span class="bankrec-complete-item__reason">${escapeHtml(getCompletionReason(summaryView))}</span>
            </span>
            <span class="bankrec-complete-item__action">${escapeHtml(actionLabel)} <i class="fa fa-arrow-right" aria-hidden="true"></i></span>
          </button>
        `).join('')}
      </div>
    `;

    completeItemsEl.querySelectorAll('[data-completion-transaction]').forEach((button) => {
      button.addEventListener('click', async () => {
        const transactionId = button.getAttribute('data-completion-transaction');
        const status = button.getAttribute('data-completion-status');
        if (!transactionId || !status) return;
        await openCompletionItem(transactionId, status);
      });
    });
  };

  const loadAllTransactionsForStatus = async (status) => {
    const items = [];
    let page = 1;
    let totalPages = 1;
    do {
      const payload = await fetchTransactionPage(page, 100, {
        filter: status,
        groupFilter: 'all',
        classificationFilter: 'all'
      });
      items.push(...(Array.isArray(payload.items) ? payload.items : []));
      totalPages = Math.max(Number(payload.totalPages) || 1, 1);
      page += 1;
    } while (page <= totalPages);
    return items;
  };

  const showCompletionStatus = async (mode) => {
    summaryView = mode;
    syncSummaryCards();
    renderCompletionItems('loading');
    const requestToken = ++latestCompletionItemsToken;
    try {
      const items = await loadAllTransactionsForStatus(mode);
      if (requestToken !== latestCompletionItemsToken || workspaceMode !== 'complete' || summaryView !== mode) return;
      completionItems = items;
      renderCompletionItems();
    } catch (error) {
      if (requestToken !== latestCompletionItemsToken || workspaceMode !== 'complete') return;
      completionItems = [];
      renderCompletionItems('error');
    }
  };

  const getDefaultCompletionStatus = () => {
    if (Number(summaryCounts.review || 0) > 0) return 'review';
    if (Number(summaryCounts.unmatched || 0) > 0) return 'unmatched';
    return 'matched';
  };

  const openCompleteWorkspace = async () => {
    if (!activateWorkspaceMode('complete')) return;
    await showCompletionStatus(getDefaultCompletionStatus());
  };

  const openCompletionItem = async (transactionId, status) => {
    const selectedCompletionItem = completionItems.find((item) => stringEquals(item.id, transactionId)) || null;
    const nextMode = status === 'matched' ? 'overview' : 'manual-review';
    if (!activateWorkspaceMode(nextMode)) return;

    await applyWorkspaceFilters({
      tx: status,
      inv: status === 'matched' ? 'matched' : 'all',
      group: 'all'
    });
    if (selectedCompletionItem?.id) {
      transactionCache.set(selectedCompletionItem.id, selectedCompletionItem);
    }
    selectTransaction(transactionId);

    const target = status === 'matched'
      ? document.querySelector('.bankrec-browse-sections')
      : document.querySelector('.bankrec-workpanel');
    target?.scrollIntoView({ behavior: 'smooth', block: 'start' });
  };

  const focusManualReviewWorkspace = async () => {
    workspaceMode = 'manual-review';
    syncWorkspaceButtons();
    await applyWorkspaceFilters({
      tx: 'all',
      inv: 'all',
      group: 'all'
    });

    const firstReviewItem = getManualReviewItems()[0] || null;
    if (firstReviewItem?.tx?.id) {
      selectTransaction(firstReviewItem.tx.id);
      return;
    }

    const firstTransaction = transactions[0] || null;
    if (firstTransaction?.id) {
      selectTransaction(firstTransaction.id);
      return;
    }

    clearSelection();
  };

  const focusPartialPaymentsWorkspace = async () => {
    workspaceMode = 'partial-payments';
    syncWorkspaceButtons();
    await applyWorkspaceFilters({
      tx: 'partial',
      inv: 'unmatched',
      group: 'Kundinbetalningar'
    });
    await window.BankRecPaymentBundles?.reload?.();

    const firstPartialTx = getPartialTransactionItems()[0] || null;
    if (firstPartialTx?.id) {
      selectTransaction(firstPartialTx.id);
    } else {
      clearSelection();
    }

    const workpanel = document.querySelector('.bankrec-workpanel');
    workpanel?.scrollIntoView({ behavior: 'smooth', block: 'start' });
  };

  const updateGroupFilterCounts = () => {
    txGroupFilterSelect?.querySelectorAll('option').forEach((option) => {
      const groupKey = option.value;
      const baseLabel = groupKey === 'Leverantorsutbetalningar'
        ? 'Leverantörsutb.'
        : (option.dataset.baseLabel || option.textContent || '');
      const count = transactionGroupCounts[groupKey] ?? 0;
      option.textContent = `${baseLabel} (${count})`;
    });
  };

  const renderInvoiceEmptyState = (message) => {
    const invBody = invTable.querySelector('tbody');
    invBody.innerHTML = `<tr class="bankrec-empty-row"><td colspan="7" class="text-center text-muted">${escapeHtml(message)}</td></tr>`;
  };

  const renderTransactionEmptyState = (message) => {
    const txBody = txTable.querySelector('tbody');
    txBody.innerHTML = `<tr class="bankrec-empty-row"><td colspan="6" class="text-center text-muted">${escapeHtml(message)}</td></tr>`;
  };

  const appendFillerRows = (tbody, columnCount, count) => {
    if (!tbody || count <= 0) return;
    for (let index = 0; index < count; index += 1) {
      const tr = document.createElement('tr');
      tr.className = 'bankrec-filler-row';
      tr.innerHTML = `<td colspan="${columnCount}" aria-hidden="true"></td>`;
      tbody.appendChild(tr);
    }
  };

  const fetchTransactionPage = async (page = 1, pageSize = transactionPageSize, filters = {}) => {
    const url = new URL(transactionsEndpoint, window.location.origin);
    url.searchParams.set('page', String(page));
    url.searchParams.set('pageSize', String(pageSize));
    url.searchParams.set('filter', filters.filter || txFilter);
    url.searchParams.set('groupFilter', filters.groupFilter || txGroupFilter);
    url.searchParams.set(
      'classificationFilter',
      filters.classificationFilter || (workspaceMode === 'classification' ? classificationTypeFilter : 'all')
    );
    const response = await fetch(url.toString(), { headers: { 'X-Requested-With': 'XMLHttpRequest' } });
    const payload = await response.json();
    if (!response.ok || payload.errorMessage) {
      throw createRequestError(payload, response.status);
    }
    return payload;
  };

  const loadTransactions = async (page = 1) => {
    if (!transactionsEndpoint) return;
    setTransactionsLoading(true);
    transactionsLoadError = '';
    try {
      const payload = await fetchTransactionPage(page, transactionPageSize);
      transactions = Array.isArray(payload.items) ? payload.items : [];
      initialTransactions = JSON.parse(JSON.stringify(transactions));
      manualReviewQueueItems = Array.isArray(payload.manualReviewItems) ? payload.manualReviewItems : [];
      autoResultItems = Array.isArray(payload.autoResultItems) ? payload.autoResultItems : [];
      transactionCache = new Map(
        [...transactions, ...manualReviewQueueItems, ...autoResultItems]
          .filter((tx) => tx?.id)
          .map((tx) => [tx.id, tx])
      );
      summaryCounts = {
        matched: Number(payload.summary?.matched ?? 0) || 0,
        review: Number(payload.summary?.review ?? 0) || 0,
        unmatched: Number(payload.summary?.unmatched ?? 0) || 0
      };
      hasTransactionSummaryLoaded = true;
      transactionTotals = {
        credit: Number(payload.totals?.credit ?? 0) || 0,
        debit: Number(payload.totals?.debit ?? 0) || 0,
        matched: Number(payload.totals?.matched ?? 0) || 0,
        unmatched: Number(payload.totals?.unmatched ?? 0) || 0
      };
      transactionGroupCounts = {
        all: Number(payload.groupCounts?.all ?? 0) || 0,
        Kundinbetalningar: Number(payload.groupCounts?.kundinbetalningar ?? payload.groupCounts?.Kundinbetalningar ?? 0) || 0,
        Leverantorsutbetalningar: Number(payload.groupCounts?.leverantorsutbetalningar ?? payload.groupCounts?.Leverantorsutbetalningar ?? 0) || 0,
        Ovrigt: Number(payload.groupCounts?.ovrigt ?? payload.groupCounts?.Ovrigt ?? 0) || 0
      };
      transactionClassificationSummary = Array.isArray(payload.classificationSummary) ? payload.classificationSummary : [];
      transactionPage = Number(payload.page) || 1;
      transactionTotalPages = Math.max(Number(payload.totalPages) || 1, 1);
      transactionTotalCount = Number(payload.totalCount) || 0;
      if (selectedTxId && !getTransactionById(selectedTxId)) {
        selectedTxId = null;
        latestRecommendationsToken += 1;
        latestAiSuggestionsToken += 1;
        renderRecommendations(null);
        renderAiSuggestions(null);
      }
    } catch (error) {
      transactions = [];
      initialTransactions = [];
      summaryCounts = { matched: 0, review: 0, unmatched: 0 };
      hasTransactionSummaryLoaded = false;
      transactionTotals = { credit: 0, debit: 0, matched: 0, unmatched: 0 };
      transactionGroupCounts = { all: 0, Kundinbetalningar: 0, Leverantorsutbetalningar: 0, Ovrigt: 0 };
      transactionClassificationSummary = [];
      manualReviewQueueItems = [];
      autoResultItems = [];
      transactionCache = new Map();
      transactionPage = 1;
      transactionTotalPages = 1;
      transactionTotalCount = 0;
      transactionsLoadError = handleBankRecError(error, 'Banktransaktioner kunde inte laddas.');
    } finally {
      setTransactionsLoading(false);
      renderTable();
    }
  };

  const fetchInvoicePage = async (page = 1, pageSize = invoicePageSize) => {
    const url = new URL(invoicesEndpoint, window.location.origin);
    url.searchParams.set('page', String(page));
    url.searchParams.set('pageSize', String(pageSize));
    url.searchParams.set('classificationFilter', classificationTypeFilter);
    url.searchParams.set('groupFilter', txGroupFilter);
    const response = await fetch(url.toString(), { headers: { 'X-Requested-With': 'XMLHttpRequest' } });
    const payload = await response.json();
    if (!response.ok || payload.errorMessage) {
      throw createRequestError(payload, response.status);
    }
    return payload;
  };

  const loadInvoices = async (page = 1) => {
    setInvoicesLoading(true);
    invoicesLoadError = '';
    try {
      const payload = await fetchInvoicePage(page, invoicePageSize);
      invoices = Array.isArray(payload.items) ? payload.items : [];
      invoicePage = Number(payload.page) || 1;
      invoiceTotalPages = Math.max(Number(payload.totalPages) || 1, 1);
      invoiceTotalCount = Number(payload.totalCount) || 0;
      if (selectedInvId && !invoices.some((inv) => inv.id === selectedInvId)) {
        selectedInvId = null;
        renderInvoiceDetail(null);
      }
    } catch (error) {
      invoices = [];
      invoicePage = 1;
      invoiceTotalPages = 1;
      invoiceTotalCount = 0;
      invoicesLoadError = error instanceof Error ? `${invoicesErrorMessage} ${error.message}` : invoicesErrorMessage;
    } finally {
      setInvoicesLoading(false);
      renderTable();
    }
  };

  const persistCurrentMatches = async () => {
    if (!saveMatchesEndpoint) return;
    const matches = transactions
      .flatMap((tx) => getTxAllocations(tx).map((allocation) => ({
        transactionId: tx.id,
        invoiceId: allocation.invoiceId,
        matchType: allocation.matchType || tx.matchType || 'auto',
        matchRule: allocation.matchRule || tx.matchRule || 'auto',
        matchedAmount: allocation.matchedAmount || 0
      })));
    const result = await postJson(saveMatchesEndpoint, { expectedVersion: currentStateVersion, matches });
    currentStateVersion = Number(result.version ?? currentStateVersion) || currentStateVersion;
  };

  const renderTable = () => {
    if (pageRoot) {
      pageRoot.dataset.partialManualCount = String(getPartialTransactionItems().length);
    }
    const txBody = txTable.querySelector('tbody');
    const invBody = invTable.querySelector('tbody');
    txBody.innerHTML = '';
    invBody.innerHTML = '';
    updateGroupFilterCounts();

    const filteredTransactions = applyFilter(
      transactions.filter((tx) => txGroupFilter === 'all' || getTxGroup(tx) === txGroupFilter),
      txFilter,
      isTxMatched
    );
    const displayedTransactions = filteredTransactions;

    const filteredInvoices = applyFilter(invoices, invFilter, isInvMatched, isInvPartial);

    displayedTransactions.forEach((tx) => {
      const tr = document.createElement('tr');
      tr.className = 'bankrec-row';
      if (selectedTxId === tx.id) tr.classList.add('is-selected');
      if (selectedInvId && getTxAllocations(tx).some((allocation) => allocation.invoiceId === selectedInvId)) tr.classList.add('is-related');

      const matched = getTxAllocations(tx).length > 0;
      const partial = matched && getMatchedAmount(tx) > 0 && getMatchedAmount(tx) < (tx.amount || 0);
      const status = matched ? (partial ? 'Delmatchad' : (tx.matchType === 'manual' ? 'Manuell' : 'Matchad')) : 'Omatchad';
      const statusClass = matched ? (partial ? 'manual' : (tx.matchType === 'manual' ? 'manual' : 'matched')) : 'unmatched';

      tr.innerHTML = `
        <td><span class="badge rounded-pill status-pill ${statusClass}">${status}</span></td>
        <td>${escapeHtml(getTxGroupLabel(tx))}</td>
        <td>${escapeHtml(tx.date || '')}</td>
        <td>${formatAmount(tx.amount)} ${escapeHtml(tx.currency || '')}</td>
        <td>${escapeHtml(tx.reference || '-')}</td>
        <td>${escapeHtml(tx.debtorName || '-')}</td>
      `;
      tr.addEventListener('click', () => {
        selectTransaction(tx.id);
      });
      txBody.appendChild(tr);
    });

    if (invoicesLoadError) {
      renderInvoiceEmptyState(invoicesLoadError);
    } else if (!isInvoicesLoading && filteredInvoices.length === 0) {
      renderInvoiceEmptyState(noInvoicesMessage);
    }

    filteredInvoices.forEach((inv) => {
      const tr = document.createElement('tr');
      tr.className = 'bankrec-row';
      if (selectedInvId === inv.id) tr.classList.add('is-selected');
      if (selectedTxId && getInvoicePayments(inv.id).some((tx) => tx.id === selectedTxId)) tr.classList.add('is-related');

      const paid = getInvoicePaid(inv.id);
      const remaining = getInvoiceRemaining(inv);
      const full = remaining === 0 && paid > 0;
      const partial = paid > 0 && remaining > 0;
      const status = full ? 'Matchad' : partial ? 'Delbetald' : 'Omatchad';
      const statusClass = full ? 'matched' : partial ? 'manual' : 'unmatched';

      tr.innerHTML = `
        <td><span class="badge rounded-pill status-pill ${statusClass}">${status}</span></td>
        <td>${escapeHtml(inv.invoiceNo || inv.id)}</td>
        <td>${escapeHtml(inv.ocr || '-')}</td>
        <td>${formatAmount(inv.amount)} ${escapeHtml(inv.currency || '')}</td>
        <td>${formatAmount(paid)} ${escapeHtml(inv.currency || '')}</td>
        <td>${formatAmount(remaining)} ${escapeHtml(inv.currency || '')}</td>
        <td>${escapeHtml(inv.customerName || '-')}</td>
      `;
      tr.addEventListener('click', () => {
        selectedInvId = inv.id;
        renderInvoiceDetail(inv);
        renderTable();
      });
      invBody.appendChild(tr);
    });

    if (transactionsLoadError) {
      renderTransactionEmptyState(transactionsLoadError);
    } else if (!isTransactionsLoading && displayedTransactions.length === 0) {
      renderTransactionEmptyState(workspaceMode === 'classification' && classificationTypeFilter !== 'all'
        ? noTransactionsForSelectedTypeMessage
        : 'Inga banktransaktioner att visa på den här sidan.');
    }

    const renderedTransactionRows = txBody.querySelectorAll('tr').length;
    const renderedInvoiceRows = invBody.querySelectorAll('tr').length;
    const alignedRowCount = Math.max(renderedTransactionRows, renderedInvoiceRows, 6);
    appendFillerRows(txBody, 6, alignedRowCount - renderedTransactionRows);
    appendFillerRows(invBody, 7, alignedRowCount - renderedInvoiceRows);

    txCount.textContent = txTotalCount(transactionTotalCount, displayedTransactions.length);
    invCount.textContent = invFilter === 'all' && invoiceTotalCount > 0 ? invoiceTotalCount : filteredInvoices.length;
    updateTransactionPagination();
    updateTotals();
    updateStatusSummary();
    renderClassificationSummary();
    updateInvoicePagination();
    renderWorkPanel();
    renderCurrentAllocations();
    renderAutoResults();
    renderManualReviewQueue(workspaceMode === 'manual-review' ? manualReviewQueueItems : displayedTransactions);
    renderCodingSummary();
  };

  const clearSelection = () => {
    selectedTxId = null;
    selectedInvId = null;
    latestRecommendationsToken += 1;
    latestAiSuggestionsToken += 1;
    renderRecommendations(null);
    renderAiSuggestions(null);
    renderInvoiceDetail(null);
    renderTable();
  };

  const resetMatchesLocally = () => {
    transactions = JSON.parse(JSON.stringify(initialTransactions));
    clearSelection();
  };

  const txTotalCount = (serverCount, fallbackCount) => serverCount > 0 ? serverCount : fallbackCount;

  const applyServerMatches = (matches) => {
    persistedMatches = Array.isArray(matches) ? matches : [];
    const byTransaction = new Map();
    (Array.isArray(matches) ? matches : []).forEach((match) => {
      if (!match?.transactionId || !match?.invoiceId) return;
      const list = byTransaction.get(match.transactionId) || [];
      list.push({
        allocationId: match.allocationId || null,
        invoiceId: match.invoiceId,
        matchType: match.matchType || 'auto',
        matchRule: match.matchRule || 'auto',
        matchedAmount: Number(match.matchedAmount ?? 0) || 0,
        currency: match.currency || 'SEK'
      });
      byTransaction.set(match.transactionId, list);
    });

    transactions.forEach((tx) => {
      const allocations = byTransaction.get(tx.id) || [];
      tx.allocations = allocations;
      tx.matchedInvoiceId = allocations[0]?.invoiceId || null;
      tx.matchType = allocations[0]?.matchType || null;
      tx.matchRule = allocations[0]?.matchRule || null;
      tx.matchedAmount = allocations.length > 0 ? getMatchedAmount({ allocations }) : null;
    });
  };

  workspaceModeOverviewBtn?.addEventListener('click', () => {
    activateWorkspaceMode('overview');
  });

  workspaceModeClassificationBtn?.addEventListener('click', () => {
    activateWorkspaceMode('classification');
  });

  workspaceModeReconciliationBtn?.addEventListener('click', async () => {
    if (!activateWorkspaceMode('manual-review')) return;
    await focusManualReviewWorkspace();
  });

  workspaceModePartialBtn?.addEventListener('click', async () => {
    if (!activateWorkspaceMode('partial-payments')) return;
    await focusPartialPaymentsWorkspace();
  });

  workspaceModeCompleteBtn?.addEventListener('click', async () => {
    await openCompleteWorkspace();
  });

  window.addEventListener('beforeunload', (event) => {
    if (!hasUnsavedCodingChanges) return;
    event.preventDefault();
    event.returnValue = '';
  });

  matchBtn?.addEventListener('click', runAutoMatchWorkflow);

  codingSaveBtn?.addEventListener('click', async () => {
    await saveCodingRules();
  });

  closeBtn?.addEventListener('click', async () => {
    if (hasUnsavedCodingChanges) {
      if (lifecycleErrorEl) {
        lifecycleErrorEl.textContent = saveCodingBeforeClose;
        lifecycleErrorEl.classList.remove('d-none');
      }
      return;
    }

    closeBtn.disabled = true;
    lifecycleErrorEl?.classList.add('d-none');
    try {
      await ensureInitialStateLoaded();
      const result = await postJson(closeEndpoint, {
        expectedVersion: currentStateVersion
      });
      currentStateVersion = Number(result.version ?? currentStateVersion) || currentStateVersion;
      isReconciliationClosed = Boolean(result.isClosed);
      reconciliationClosedAtUtc = result.closedAtUtc || null;
      reconciliationClosedByName = result.closedByName || '';
      await loadRecentActivity();
      updateStatusSummary();
    } catch (error) {
      if (lifecycleErrorEl) {
        lifecycleErrorEl.textContent = handleBankRecError(
          error,
          'Avstämningen kunde inte slutföras.');
        lifecycleErrorEl.classList.remove('d-none');
      }
      syncLifecycleState();
    }
  });

  reopenBtn?.addEventListener('click', async () => {
    const reason = String(reopenReasonInput?.value || '').trim();
    lifecycleErrorEl?.classList.add('d-none');
    reopenBtn.disabled = true;
    try {
      await ensureInitialStateLoaded();
      const result = await postJson(reopenEndpoint, {
        expectedVersion: currentStateVersion,
        reason
      });
      currentStateVersion = Number(result.version ?? currentStateVersion) || currentStateVersion;
      isReconciliationClosed = Boolean(result.isClosed);
      reconciliationClosedAtUtc = result.closedAtUtc || null;
      reconciliationClosedByName = result.closedByName || '';
      if (reopenReasonInput) reopenReasonInput.value = '';
      await loadRecentActivity();
      updateStatusSummary();
    } catch (error) {
      if (lifecycleErrorEl) {
        lifecycleErrorEl.textContent = handleBankRecError(
          error,
          'Avstämningen kunde inte återöppnas.');
        lifecycleErrorEl.classList.remove('d-none');
      }
      syncLifecycleState();
    } finally {
      reopenBtn.disabled = false;
    }
  });

  resetBtn?.addEventListener('click', async () => {
    try {
      await ensureInitialStateLoaded();
      resetMatchesLocally();
      const result = await postJson(resetMatchesEndpoint || saveMatchesEndpoint, { expectedVersion: currentStateVersion, matches: [] });
      currentStateVersion = Number(result.version ?? currentStateVersion) || currentStateVersion;
      setConflictState(false);
      await loadTransactions(transactionPage);
      await loadInvoices(invoicePage);
      await loadRecentActivity();
    } catch (error) {
      invoicesLoadError = handleBankRecError(error, 'Återställning misslyckades.');
      renderTable();
    }
  });

  manualBtn?.addEventListener('click', async () => {
    if (!selectedTxId || !selectedInvId) return;
    try {
      await ensureInitialStateLoaded();
      const matchedTransactionId = selectedTxId;
      const result = await postJson(manualMatchEndpoint, {
        transactionId: selectedTxId,
        invoiceId: selectedInvId,
        matchedAmount: getSelectedMatchAmount(),
        expectedVersion: currentStateVersion
      });
      currentStateVersion = Number(result.version ?? currentStateVersion) || currentStateVersion;
      setConflictState(false);
      selectedTxId = null;
      selectedInvId = null;
      recommendedInvoiceLookup = new Map();
      recommendationCache.delete(matchedTransactionId);
      workspaceMode = 'manual-review';
      syncWorkspaceButtons();
      await applyWorkspaceFilters({
        tx: 'all',
        inv: 'all',
        group: 'Kundinbetalningar'
      });

      const nextReviewItem = getManualReviewItems()[0] || null;
      if (nextReviewItem?.tx?.id) {
        selectTransaction(nextReviewItem.tx.id);
      } else {
        clearSelection();
        await openCompleteWorkspace();
      }

      await loadRecentActivity();
    } catch (error) {
      invoicesLoadError = handleBankRecError(error, 'Manuell matchning misslyckades.');
      renderTable();
    }
  });

  undoBtn?.addEventListener('click', async () => {
    const tx = transactions.find((item) => item.id === selectedTxId);
    if (!tx || getTxAllocations(tx).length === 0) return;
    try {
      await ensureInitialStateLoaded();
      await postJson(reverseMatchEndpoint, {
        transactionId: tx.id,
        expectedVersion: currentStateVersion,
        reason: 'Ångrad i bankavstämningens arbetspanel.'
      });
      setConflictState(false);
      await loadRecentActivity();
      clearSelection();
      await loadTransactions(transactionPage);
      await loadInvoices(invoicePage);
      await loadRecentActivity();
    } catch (error) {
      invoicesLoadError = handleBankRecError(error, 'Ångra matchning misslyckades.');
      renderTable();
    }
  });

  conflictReloadBtn?.addEventListener('click', () => {
    window.location.reload();
  });

  clearSelectionBtn?.addEventListener('click', () => {
    clearSelection();
  });

  completeMatchedCard?.addEventListener('click', async () => {
    await showCompletionStatus('matched');
  });

  completeReviewCard?.addEventListener('click', async () => {
    await showCompletionStatus('review');
  });

  completeUnmatchedCard?.addEventListener('click', async () => {
    await showCompletionStatus('unmatched');
  });

  txFilterSelect?.addEventListener('change', async () => {
    summaryView = 'all';
    txFilter = txFilterSelect.value;
    syncScopeFilterButtons('tx', txFilter);
    syncSummaryCards();
    await loadTransactions(1);
  });

  invFilterSelect?.addEventListener('change', () => {
    summaryView = 'all';
    invFilter = invFilterSelect.value;
    syncScopeFilterButtons('inv', invFilter);
    syncSummaryCards();
    renderTable();
  });

  txGroupFilterSelect?.addEventListener('change', async () => {
    summaryView = 'all';
    txGroupFilter = txGroupFilterSelect.value;
    syncGroupFilterButtons(txGroupFilter);
    syncSummaryCards();
    await loadTransactions(1);
  });

  txPrevBtn?.addEventListener('click', () => {
    if (transactionPage > 1 && !isTransactionsLoading) loadTransactions(transactionPage - 1);
  });

  txNextBtn?.addEventListener('click', () => {
    if (transactionPage < transactionTotalPages && !isTransactionsLoading) loadTransactions(transactionPage + 1);
  });

  invPrevBtn?.addEventListener('click', () => {
    if (invoicePage > 1 && !isInvoicesLoading) loadInvoices(invoicePage - 1);
  });

  invNextBtn?.addEventListener('click', () => {
    if (invoicePage < invoiceTotalPages && !isInvoicesLoading) loadInvoices(invoicePage + 1);
  });

  matchAmountInput?.addEventListener('input', () => {
    renderWorkPanel();
  });

  document.querySelectorAll('.bankrec-demo-scenario-form').forEach((form) => {
    form.addEventListener('submit', () => {
      window.sessionStorage.setItem(pendingDemoScrollPositionStorageKey, String(Math.max(0, window.scrollY)));
    });
  });

  const readPendingDemoScrollPosition = () => {
    const storedValue = window.sessionStorage.getItem(pendingDemoScrollPositionStorageKey);
    if (storedValue === null) return null;
    const position = Number(storedValue);
    return Number.isFinite(position) && position >= 0 ? position : null;
  };

  const restoreDemoScrollPosition = async (position) => {
    if (position === null) return;
    await new Promise((resolve) => window.requestAnimationFrame(() => window.requestAnimationFrame(resolve)));
    window.scrollTo({ top: position, left: 0, behavior: 'auto' });
  };

  const refreshAfterPaymentBundleConfirmation = async () => {
    const stateLoaded = await loadRecentActivity();
    await loadTransactions(transactionPage);
    await loadInvoices(invoicePage);
    syncSummaryCards();
    return stateLoaded;
  };

  window.BankRecWorkspace = {
    refreshAfterPaymentBundleConfirmation
  };

  const initializeBankRec = async () => {
    const pendingScrollPosition = readPendingDemoScrollPosition();
    if (pendingScrollPosition !== null) {
      window.scrollTo({ top: pendingScrollPosition, left: 0, behavior: 'auto' });
    }
    renderInvoiceDetail(null);
    syncWorkspaceButtons();
    renderTable();
    renderRecommendations(null);
    initialStatePromise = loadRecentActivity();
    await initialStatePromise;
    await loadTransactions(1);
    await loadInvoices(1);
    await restoreDemoScrollPosition(pendingScrollPosition);
    window.sessionStorage.removeItem(pendingDemoScrollPositionStorageKey);
  };

  initializeBankRec();
})();
