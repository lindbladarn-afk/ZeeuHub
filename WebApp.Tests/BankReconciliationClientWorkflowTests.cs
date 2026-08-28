namespace WebApp.Tests;

// Client workflow tests protect navigation behavior around demo scenario redirects.
public sealed class BankReconciliationClientWorkflowTests
{
    [Fact]
    public void DemoScenarioSelection_PreservesScrollWithoutSkippingWorkflowSteps()
    {
        var webAppRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../WebApp"));
        var script = File.ReadAllText(Path.Combine(webAppRoot, "wwwroot", "js", "bank-reconciliation-demo.js"));

        Assert.Contains("bankrec-pending-demo-scroll-position", script, StringComparison.Ordinal);
        Assert.Contains("String(Math.max(0, window.scrollY))", script, StringComparison.Ordinal);
        Assert.Contains("await restoreDemoScrollPosition(pendingScrollPosition);", script, StringComparison.Ordinal);
        Assert.DoesNotContain("bankrec-pending-demo-workflow", script, StringComparison.Ordinal);
        Assert.DoesNotContain("runPendingDemoWorkflow", script, StringComparison.Ordinal);
    }

    [Fact]
    public void Workflow_UsesFiveClearStepsWithDedicatedPartialPayments()
    {
        var webAppRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../WebApp"));
        var view = File.ReadAllText(Path.Combine(
            webAppRoot,
            "Views",
            "Integration",
            "BankReconciliation",
            "BankReconciliation.cshtml"));
        var script = File.ReadAllText(Path.Combine(webAppRoot, "wwwroot", "js", "bank-reconciliation-demo.js"));

        Assert.Contains("id=\"bankrec-mode-overview\"", view, StringComparison.Ordinal);
        Assert.Contains("id=\"bankrec-mode-classification\"", view, StringComparison.Ordinal);
        Assert.Contains("id=\"bankrec-mode-reconciliation\"", view, StringComparison.Ordinal);
        Assert.Contains("id=\"bankrec-mode-partial\"", view, StringComparison.Ordinal);
        Assert.Contains("id=\"bankrec-mode-complete\"", view, StringComparison.Ordinal);
        Assert.Contains("BankRec_ProcessPartialTitle", view, StringComparison.Ordinal);
        Assert.Contains("BankRec_ProcessPartialDescription", view, StringComparison.Ordinal);
        Assert.Contains("id=\"bankrec-match-btn\"", view, StringComparison.Ordinal);
        Assert.Contains("BankRec_RunSafeAutoMatch", view, StringComparison.Ordinal);
        Assert.Contains("@foreach (var scenario in demoScenarios)", view, StringComparison.Ordinal);
        Assert.Contains("name=\"scenarioKey\" value=\"@scenario.Key\"", view, StringComparison.Ordinal);
        Assert.Contains("BankRec_DemoScenarioButtonPrefix", view, StringComparison.Ordinal);
        Assert.DoesNotContain("data-demo-workflow", view, StringComparison.Ordinal);
        Assert.Contains("workspaceModePartialBtn?.addEventListener", script, StringComparison.Ordinal);
        Assert.Contains("await window.BankRecPaymentBundles?.reload?.();", script, StringComparison.Ordinal);
        Assert.Contains("workpanel?.scrollIntoView({ behavior: 'smooth', block: 'start' });", script, StringComparison.Ordinal);
        Assert.DoesNotContain("demoSummaryMatchedCard?.addEventListener", script, StringComparison.Ordinal);
    }

    [Fact]
    public void ReviewWorkflow_WarnsAboutAccountAndProtectsUnsavedCodingChanges()
    {
        var webAppRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../WebApp"));
        var view = File.ReadAllText(Path.Combine(
            webAppRoot,
            "Views",
            "Integration",
            "BankReconciliation",
            "BankReconciliation.cshtml"));
        var script = File.ReadAllText(Path.Combine(webAppRoot, "wwwroot", "js", "bank-reconciliation-demo.js"));

        Assert.Contains("BankRec_AccountNotVerifiedMessage", view, StringComparison.Ordinal);
        Assert.Contains("id=\"bankrec-coding-dirty\"", view, StringComparison.Ordinal);
        Assert.Contains("hasUnsavedCodingChanges", script, StringComparison.Ordinal);
        Assert.Contains("window.confirm(codingUnsavedConfirmText)", script, StringComparison.Ordinal);
        Assert.Contains("beforeunload", script, StringComparison.Ordinal);
    }

    [Fact]
    public void PaymentBundleConfirmation_RefreshesWorkspaceWithoutReloadingPage()
    {
        var webAppRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../WebApp"));
        var workspaceScript = File.ReadAllText(Path.Combine(webAppRoot, "wwwroot", "js", "bank-reconciliation-demo.js"));
        var bundleScript = File.ReadAllText(Path.Combine(webAppRoot, "wwwroot", "js", "bank-reconciliation-payment-bundles.js"));

        Assert.Contains("refreshAfterPaymentBundleConfirmation", workspaceScript, StringComparison.Ordinal);
        Assert.Contains("await refreshWorkspaceAfterConfirmation();", bundleScript, StringComparison.Ordinal);
        Assert.Contains("await loadSuggestions();", bundleScript, StringComparison.Ordinal);
        Assert.DoesNotContain("window.location.reload()", bundleScript, StringComparison.Ordinal);
    }

    [Fact]
    public void PartialPaymentWorkflow_PrioritizesSuggestionsAndSupportsManualGroups()
    {
        var webAppRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../WebApp"));
        var view = File.ReadAllText(Path.Combine(
            webAppRoot,
            "Views",
            "Integration",
            "BankReconciliation",
            "BankReconciliation.cshtml"));
        var workspaceScript = File.ReadAllText(Path.Combine(webAppRoot, "wwwroot", "js", "bank-reconciliation-demo.js"));
        var bundleScript = File.ReadAllText(Path.Combine(webAppRoot, "wwwroot", "js", "bank-reconciliation-payment-bundles.js"));
        var stylesheet = File.ReadAllText(Path.Combine(webAppRoot, "wwwroot", "css", "bankrec.css"));

        Assert.Contains("class=\"bankrec-standard-match-workspace\"", view, StringComparison.Ordinal);
        Assert.Contains("id=\"bankrec-manual-bundle-toggle\"", view, StringComparison.Ordinal);
        Assert.Contains("bankrec-manual-bundle-toggle d-none", view, StringComparison.Ordinal);
        Assert.Contains("bankrec-manual-bundle-toggle", stylesheet, StringComparison.Ordinal);
        Assert.Contains("id=\"bankrec-manual-bundle-builder\"", view, StringComparison.Ordinal);
        Assert.Contains("id=\"bankrec-manual-bundle-recommendation\"", view, StringComparison.Ordinal);
        Assert.Contains("id=\"bankrec-manual-bundle-apply-suggestion\"", view, StringComparison.Ordinal);
        Assert.Contains("id=\"bankrec-confirm-manual-payment-bundle-endpoint\"", view, StringComparison.Ordinal);
        Assert.Contains("id=\"bankrec-adjust-payment-bundle-label\"", view, StringComparison.Ordinal);
        Assert.Contains("dataset.partialManualCount", workspaceScript, StringComparison.Ordinal);
        Assert.Contains("[data-partial-manual-count=\"0\"] .bankrec-standard-match-workspace", stylesheet, StringComparison.Ordinal);
        Assert.Contains("transactionIds", bundleScript, StringComparison.Ordinal);
        Assert.Contains("expectedVersion: currentVersion", bundleScript, StringComparison.Ordinal);
        Assert.Contains("getSelectedInvoiceSuggestion", bundleScript, StringComparison.Ordinal);
        Assert.Contains("applyManualSuggestion", bundleScript, StringComparison.Ordinal);
        Assert.Contains("!isSuggestedInvoice(invoice) || invoice.invoiceId === manualOverrideInvoiceId", bundleScript, StringComparison.Ordinal);
        Assert.Contains("openManualAdjustment", bundleScript, StringComparison.Ordinal);
        Assert.Contains("bankrec-payment-bundle__adjust", bundleScript, StringComparison.Ordinal);
        Assert.Contains("adjustIcon.className = 'fa fa-edit'", bundleScript, StringComparison.Ordinal);
        Assert.Contains("hasManualGroupCandidates", bundleScript, StringComparison.Ordinal);
        Assert.Contains("manualToggle.classList.toggle('d-none', !hasCandidates)", bundleScript, StringComparison.Ordinal);
    }

    [Fact]
    public void CompletionWorkflow_UsesServerCloseAndRequiresReasonToReopen()
    {
        var webAppRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../WebApp"));
        var view = File.ReadAllText(Path.Combine(
            webAppRoot,
            "Views",
            "Integration",
            "BankReconciliation",
            "BankReconciliation.cshtml"));
        var script = File.ReadAllText(Path.Combine(
            webAppRoot,
            "wwwroot",
            "js",
            "bank-reconciliation-demo.js"));

        Assert.Contains("id=\"bankrec-close-btn\"", view, StringComparison.Ordinal);
        Assert.Contains("id=\"bankrec-reopen-reason\"", view, StringComparison.Ordinal);
        Assert.Contains("BankReconciliationClose", view, StringComparison.Ordinal);
        Assert.Contains("BankReconciliationReopen", view, StringComparison.Ordinal);
        Assert.Contains("expectedVersion: currentStateVersion", script, StringComparison.Ordinal);
        Assert.Contains("isReconciliationClosed", script, StringComparison.Ordinal);
        Assert.Contains("hasUnsavedCodingChanges", script, StringComparison.Ordinal);
    }

    [Fact]
    public void AutoMatchAndCompletionStatus_ExplainResultsAndOpenActionableQueue()
    {
        var webAppRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../WebApp"));
        var view = File.ReadAllText(Path.Combine(
            webAppRoot,
            "Views",
            "Integration",
            "BankReconciliation",
            "BankReconciliation.cshtml"));
        var script = File.ReadAllText(Path.Combine(
            webAppRoot,
            "wwwroot",
            "js",
            "bank-reconciliation-demo.js"));

        Assert.Contains("id=\"bankrec-auto-match-feedback\"", view, StringComparison.Ordinal);
        Assert.Contains("id=\"bankrec-complete-card-matched\"", view, StringComparison.Ordinal);
        Assert.Contains("id=\"bankrec-complete-card-review\"", view, StringComparison.Ordinal);
        Assert.Contains("id=\"bankrec-complete-card-unmatched\"", view, StringComparison.Ordinal);
        Assert.Contains("id=\"bankrec-complete-items\"", view, StringComparison.Ordinal);
        Assert.Contains("matchedTransactionIdsBefore", script, StringComparison.Ordinal);
        Assert.Contains("autoMatchSuccessTemplate", script, StringComparison.Ordinal);
        Assert.Contains("completeMatchedCard?.addEventListener", script, StringComparison.Ordinal);
        Assert.Contains("await showCompletionStatus('unmatched');", script, StringComparison.Ordinal);
        Assert.Contains("await openCompletionItem(transactionId, status);", script, StringComparison.Ordinal);
        Assert.Contains("const nextMode = status === 'matched' ? 'overview' : 'manual-review';", script, StringComparison.Ordinal);
        Assert.Contains("inv: status === 'matched' ? 'matched' : 'all'", script, StringComparison.Ordinal);
        Assert.Contains("renderManualInvoiceChoices(targetTxId);", script, StringComparison.Ordinal);
        Assert.Contains("data-manual-invoice-id", script, StringComparison.Ordinal);
        Assert.Contains("enabled: false", script, StringComparison.Ordinal);
        Assert.DoesNotContain("openSummaryView", script, StringComparison.Ordinal);
    }
}
