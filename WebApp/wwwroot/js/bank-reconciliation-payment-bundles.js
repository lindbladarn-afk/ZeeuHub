// Renders and confirms server-verified many-to-one payment bundle suggestions.
(() => {
  const container = document.getElementById('bankrec-payment-bundles');
  const queryScript = document.getElementById('bankrec-payment-bundles-endpoint');
  const confirmScript = document.getElementById('bankrec-confirm-payment-bundle-endpoint');
  const confirmManualScript = document.getElementById('bankrec-confirm-manual-payment-bundle-endpoint');
  const adjustManualLabelScript = document.getElementById('bankrec-adjust-payment-bundle-label');
  const detailModalElement = document.getElementById('bankrec-bundle-detail-modal');
  const detailModalTitle = document.getElementById('bankrec-bundle-detail-title');
  const detailModalBody = document.getElementById('bankrec-bundle-detail-body');
  const manualToggle = document.getElementById('bankrec-manual-bundle-toggle');
  const manualBuilder = document.getElementById('bankrec-manual-bundle-builder');
  const manualClose = document.getElementById('bankrec-manual-bundle-close');
  const manualInvoice = document.getElementById('bankrec-manual-bundle-invoice');
  const manualRecommendation = document.getElementById('bankrec-manual-bundle-recommendation');
  const manualRecommendationTitle = document.getElementById('bankrec-manual-bundle-recommendation-title');
  const manualRecommendationDetail = document.getElementById('bankrec-manual-bundle-recommendation-detail');
  const manualApplySuggestion = document.getElementById('bankrec-manual-bundle-apply-suggestion');
  const manualTransactions = document.getElementById('bankrec-manual-bundle-transactions');
  const manualSummary = document.getElementById('bankrec-manual-bundle-summary');
  const manualFeedback = document.getElementById('bankrec-manual-bundle-feedback');
  const manualConfirm = document.getElementById('bankrec-manual-bundle-confirm');
  if (!container || !queryScript || !confirmScript) return;

  const queryEndpoint = JSON.parse(queryScript.textContent || '""');
  const confirmEndpoint = JSON.parse(confirmScript.textContent || '""');
  const confirmManualEndpoint = JSON.parse(confirmManualScript?.textContent || '""');
  const adjustManualLabel = JSON.parse(adjustManualLabelScript?.textContent || '"Justera manuellt"');
  const antiForgeryToken = document.querySelector('#__af input[name="__RequestVerificationToken"]')?.value || '';
  const currencyFormatter = new Intl.NumberFormat('sv-SE', {
    style: 'currency',
    currency: 'SEK',
    minimumFractionDigits: 2
  });
  const dateFormatter = new Intl.DateTimeFormat('sv-SE');
  let currentVersion = 0;
  let currentSuggestions = [];
  let availableTransactions = [];
  let availableInvoices = [];
  let manualOverrideInvoiceId = null;
  const selectedManualTransactionIds = new Set();

  const formatDate = (value) => {
    if (!value) return 'Saknas';
    const date = new Date(`${value}T00:00:00`);
    return Number.isNaN(date.getTime()) ? value : dateFormatter.format(date);
  };

  const createDetailItem = (label, value) => {
    const item = document.createElement('div');
    item.className = 'bankrec-bundle-detail-item';
    const term = document.createElement('span');
    term.textContent = label;
    const description = document.createElement('strong');
    description.textContent = value || 'Saknas';
    item.append(term, description);
    return item;
  };

  const createCheck = (passed, label, detail) => {
    const item = document.createElement('li');
    item.className = passed ? 'is-passed' : 'is-warning';
    const icon = document.createElement('i');
    icon.className = passed ? 'fa fa-check-circle' : 'fa fa-triangle-exclamation';
    icon.setAttribute('aria-hidden', 'true');
    const copy = document.createElement('div');
    const title = document.createElement('strong');
    title.textContent = label;
    const description = document.createElement('span');
    description.textContent = detail;
    copy.append(title, description);
    item.append(icon, copy);
    return item;
  };

  const showBundleDetails = (suggestion) => {
    if (!detailModalElement || !detailModalTitle || !detailModalBody) return;

    detailModalTitle.textContent = `Faktura ${suggestion.invoiceNo} · matchningsunderlag`;
    detailModalBody.replaceChildren();

    const exactReferenceCount = (suggestion.allocations || [])
      .filter((allocation) => allocation.exactReferenceMatched).length;
    const allReferencesMatch = exactReferenceCount === suggestion.allocations.length;
    const amountDifference = Number(suggestion.amountDifference) || 0;

    const checks = document.createElement('ul');
    checks.className = 'bankrec-bundle-checks';
    checks.append(
      createCheck(
        allReferencesMatch,
        'Fakturareferens',
        `${exactReferenceCount} av ${suggestion.allocations.length} betalningar har exakt OCR-/referensträff.`),
      createCheck(
        amountDifference === 0,
        amountDifference === 0 ? 'Belopp summerar exakt' : 'Belopp inom tolerans',
        amountDifference === 0
          ? `${currencyFormatter.format(Number(suggestion.totalMatchedAmount) || 0)} motsvarar fakturans restbelopp.`
          : `Differensen är ${currencyFormatter.format(amountDifference)} och kräver särskild kontroll.`),
      createCheck(
        (suggestion.allocations || []).every((allocation) => (allocation.currency || 'SEK') === suggestion.currency),
        'Valuta',
        `Samtliga betalningar och fakturan är i ${suggestion.currency || 'SEK'}.`)
    );

    const invoiceSection = document.createElement('section');
    invoiceSection.className = 'bankrec-bundle-detail-section';
    const invoiceHeading = document.createElement('h6');
    invoiceHeading.textContent = 'Faktura';
    const invoiceGrid = document.createElement('div');
    invoiceGrid.className = 'bankrec-bundle-detail-grid';
    invoiceGrid.append(
      createDetailItem('Fakturanummer', suggestion.invoiceNo),
      createDetailItem('OCR', suggestion.invoiceOcr),
      createDetailItem('Kund', suggestion.customerName),
      createDetailItem('Förfallodatum', formatDate(suggestion.invoiceDueDate)),
      createDetailItem('Restbelopp', currencyFormatter.format(Number(suggestion.invoiceRemainingAmount) || 0)),
      createDetailItem('Regelstöd', `${suggestion.confidenceScore}%`)
    );
    invoiceSection.append(invoiceHeading, invoiceGrid);

    const paymentSection = document.createElement('section');
    paymentSection.className = 'bankrec-bundle-detail-section';
    const paymentHeading = document.createElement('h6');
    paymentHeading.textContent = 'Betalningar i gruppen';
    const tableWrapper = document.createElement('div');
    tableWrapper.className = 'table-responsive';
    const table = document.createElement('table');
    table.className = 'bankrec-bundle-detail-table';
    const thead = document.createElement('thead');
    const headerRow = document.createElement('tr');
    ['Transaktion', 'Datum', 'Betalare', 'Referens', 'Meddelande', 'Belopp', 'Stöd'].forEach((label) => {
      const cell = document.createElement('th');
      cell.scope = 'col';
      cell.textContent = label;
      headerRow.appendChild(cell);
    });
    thead.appendChild(headerRow);
    const tbody = document.createElement('tbody');
    (suggestion.allocations || []).forEach((allocation) => {
      const row = document.createElement('tr');
      [
        allocation.transactionId,
        formatDate(allocation.date),
        allocation.debtorName || 'Saknas',
        allocation.reference || 'Saknas',
        allocation.remittance || 'Saknas',
        currencyFormatter.format(Number(allocation.matchedAmount) || 0),
        `${allocation.evidenceScore}%`
      ].forEach((value) => {
        const cell = document.createElement('td');
        cell.textContent = value;
        row.appendChild(cell);
      });
      tbody.appendChild(row);
    });
    table.append(thead, tbody);
    tableWrapper.appendChild(table);

    const total = document.createElement('div');
    total.className = 'bankrec-bundle-detail-total';
    const equation = suggestion.allocations
      .map((allocation) => currencyFormatter.format(Number(allocation.matchedAmount) || 0))
      .join(' + ');
    total.textContent = `${equation} = ${currencyFormatter.format(Number(suggestion.totalMatchedAmount) || 0)}`;
    paymentSection.append(paymentHeading, tableWrapper, total);

    const confirmation = document.createElement('p');
    confirmation.className = 'bankrec-bundle-confirmation-note';
    confirmation.textContent = 'Förslaget bokas inte automatiskt. Kontrollera betalare, referenser och belopp innan gruppen bekräftas.';

    detailModalBody.append(checks, invoiceSection, paymentSection, confirmation);
    const Modal = window.bootstrap?.Modal;
    if (Modal) {
      Modal.getOrCreateInstance(detailModalElement).show();
    }
  };

  const setMessage = (message, tone = 'muted') => {
    container.replaceChildren();
    const element = document.createElement('p');
    element.className = tone === 'error' ? 'bankrec-payment-bundle-message is-error' : 'bankrec-payment-bundle-message';
    element.textContent = message;
    container.appendChild(element);
  };

  const prependMessage = (message, tone = 'muted') => {
    const element = document.createElement('p');
    const toneClass = tone === 'success' ? ' is-success' : tone === 'error' ? ' is-error' : '';
    element.className = `bankrec-payment-bundle-message${toneClass}`;
    element.textContent = message;
    container.prepend(element);
  };

  const refreshWorkspaceAfterConfirmation = async () => {
    const workspaceRefreshed = await window.BankRecWorkspace?.refreshAfterPaymentBundleConfirmation?.();
    const suggestionsLoaded = await loadSuggestions();
    return workspaceRefreshed !== false && suggestionsLoaded;
  };

  const createAllocationRow = (allocation) => {
    const row = document.createElement('li');
    const transaction = document.createElement('span');
    transaction.textContent = allocation.transactionId;
    const amount = document.createElement('strong');
    amount.textContent = currencyFormatter.format(Number(allocation.matchedAmount) || 0);
    row.append(transaction, amount);
    return row;
  };

  const confirmBundle = async (suggestion, version, button) => {
    button.disabled = true;
    button.querySelector('span').textContent = 'Bekräftar...';

    try {
      const response = await fetch(confirmEndpoint, {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
          'RequestVerificationToken': antiForgeryToken,
          'X-Requested-With': 'XMLHttpRequest'
        },
        body: JSON.stringify({ bundleId: suggestion.bundleId, expectedVersion: version })
      });
      const payload = await response.json();
      if (!response.ok || !payload.success) {
        if (response.status === 409 || payload.conflict) {
          setMessage('Underlaget har ändrats. Uppdaterar betalningsgrupperna för en ny säkerhetskontroll.', 'error');
          await refreshWorkspaceAfterConfirmation();
          prependMessage('Underlaget ändrades innan bekräftelsen. Granska de uppdaterade förslagen.', 'error');
          return;
        }
        throw new Error(payload.errorMessage || 'Betalningsgruppen kunde inte bekräftas.');
      }

      const refreshed = await refreshWorkspaceAfterConfirmation();
      if (refreshed) {
        prependMessage('Betalningsgruppen är bekräftad. Du kan fortsätta med nästa grupp.', 'success');
      } else {
        setMessage('Betalningsgruppen är bekräftad, men vyn kunde inte uppdateras. Ladda om sidan innan du fortsätter.', 'error');
      }
    } catch (error) {
      setMessage(error instanceof Error ? error.message : 'Betalningsgruppen kunde inte bekräftas.', 'error');
    }
  };

  const createSuggestion = (suggestion, version) => {
    const article = document.createElement('article');
    article.className = 'bankrec-payment-bundle';

    const header = document.createElement('div');
    header.className = 'bankrec-payment-bundle__header';
    const title = document.createElement('strong');
    title.textContent = `Faktura ${suggestion.invoiceNo}`;
    const headerActions = document.createElement('div');
    headerActions.className = 'bankrec-payment-bundle__header-actions';
    const infoButton = document.createElement('button');
    infoButton.type = 'button';
    infoButton.className = 'bankrec-payment-bundle__info';
    infoButton.setAttribute('aria-label', `Visa matchningsunderlag för faktura ${suggestion.invoiceNo}`);
    const infoIcon = document.createElement('i');
    infoIcon.className = 'fa fa-circle-info';
    infoIcon.setAttribute('aria-hidden', 'true');
    const infoLabel = document.createElement('span');
    infoLabel.textContent = 'Se detaljer';
    infoButton.append(infoIcon, infoLabel);
    infoButton.addEventListener('click', () => showBundleDetails(suggestion));
    const confidence = document.createElement('div');
    confidence.className = 'bankrec-payment-bundle__confidence';
    confidence.setAttribute('aria-label', `${suggestion.confidenceScore} procent regelstöd`);
    const confidenceMeter = document.createElement('span');
    confidenceMeter.className = 'bankrec-payment-bundle__confidence-meter';
    confidenceMeter.style.setProperty('--bankrec-confidence', `${Math.max(0, Math.min(100, Number(suggestion.confidenceScore) || 0)) * 3.6}deg`);
    confidenceMeter.textContent = `${suggestion.confidenceScore}%`;
    const confidenceLabel = document.createElement('span');
    confidenceLabel.className = 'bankrec-payment-bundle__confidence-label';
    confidenceLabel.textContent = 'Regelstöd';
    confidence.append(confidenceMeter, confidenceLabel);
    headerActions.append(confidence, infoButton);
    header.append(title, headerActions);

    const customer = document.createElement('div');
    customer.className = 'bankrec-payment-bundle__customer';
    customer.textContent = suggestion.customerName || 'Kundnamn saknas';

    const evidence = document.createElement('div');
    evidence.className = 'bankrec-payment-bundle__evidence';
    const exactReferenceCount = (suggestion.allocations || [])
      .filter((allocation) => allocation.exactReferenceMatched).length;
    evidence.textContent = `${exactReferenceCount}/${suggestion.allocations.length} exakta referenser · ${Number(suggestion.amountDifference) === 0 ? 'exakt summa' : `differens ${currencyFormatter.format(Number(suggestion.amountDifference) || 0)}`}`;

    const allocations = document.createElement('ul');
    allocations.className = 'bankrec-payment-bundle__allocations';
    (suggestion.allocations || []).forEach((allocation) => allocations.appendChild(createAllocationRow(allocation)));

    const summary = document.createElement('div');
    summary.className = 'bankrec-payment-bundle__summary';
    const totalLabel = document.createElement('span');
    totalLabel.textContent = `${suggestion.allocations.length} betalningar`;
    const total = document.createElement('strong');
    total.textContent = currencyFormatter.format(Number(suggestion.totalMatchedAmount) || 0);
    summary.append(totalLabel, total);

    const actions = document.createElement('div');
    actions.className = 'bankrec-payment-bundle__actions';
    const adjustAction = document.createElement('button');
    adjustAction.type = 'button';
    adjustAction.className = 'btn btn-portal btn-portal-outline bankrec-payment-bundle__adjust';
    const adjustIcon = document.createElement('i');
    adjustIcon.className = 'fa fa-edit';
    adjustIcon.setAttribute('aria-hidden', 'true');
    const adjustLabel = document.createElement('span');
    adjustLabel.textContent = adjustManualLabel;
    adjustAction.append(adjustIcon, adjustLabel);
    adjustAction.addEventListener('click', () => openManualAdjustment(suggestion));

    const action = document.createElement('button');
    action.type = 'button';
    action.className = 'btn btn-portal bankrec-payment-bundle__action';
    const icon = document.createElement('i');
    icon.className = 'fa fa-check';
    icon.setAttribute('aria-hidden', 'true');
    const label = document.createElement('span');
    label.textContent = 'Bekräfta grupp';
    action.append(icon, label);
    action.addEventListener('click', () => confirmBundle(suggestion, version, action));
    actions.append(adjustAction, action);

    article.append(header, customer, evidence, allocations, summary, actions);
    return article;
  };

  const renderSuggestions = (suggestions, version) => {
    if (!Array.isArray(suggestions) || suggestions.length === 0) {
      setMessage('Inga säkra betalningsgrupper hittades i det aktuella underlaget.');
      return;
    }

    container.replaceChildren(...suggestions.map((suggestion) => createSuggestion(suggestion, version)));
  };

  const setManualFeedback = (message, tone = 'muted') => {
    if (!manualFeedback) return;
    manualFeedback.textContent = message;
    manualFeedback.classList.toggle('is-error', tone === 'error');
    manualFeedback.classList.toggle('is-success', tone === 'success');
  };

  const getSelectedManualInvoice = () =>
    availableInvoices.find((invoice) => invoice.invoiceId === manualInvoice?.value) || null;

  const getSelectedManualTransactions = () =>
    availableTransactions.filter((transaction) => selectedManualTransactionIds.has(transaction.transactionId));

  const getSelectedInvoiceSuggestion = () => {
    const invoice = getSelectedManualInvoice();
    if (!invoice) return null;
    return currentSuggestions.find((suggestion) =>
      suggestion.invoiceId === invoice.invoiceId || suggestion.invoiceNo === invoice.invoiceNo) || null;
  };

  const isSuggestedInvoice = (invoice) =>
    currentSuggestions.some((suggestion) =>
      suggestion.invoiceId === invoice.invoiceId || suggestion.invoiceNo === invoice.invoiceNo);

  const getVisibleManualTransactions = () => {
    const overrideSuggestion = manualOverrideInvoiceId
      ? currentSuggestions.find((suggestion) => suggestion.invoiceId === manualOverrideInvoiceId)
      : null;
    const allowedOverrideIds = new Set(
      (overrideSuggestion?.allocations || []).map((allocation) => allocation.transactionId));
    const reservedIds = new Set(currentSuggestions
      .flatMap((suggestion) => suggestion.allocations || [])
      .map((allocation) => allocation.transactionId));

    return availableTransactions.filter((transaction) =>
      !reservedIds.has(transaction.transactionId) || allowedOverrideIds.has(transaction.transactionId));
  };

  const hasManualGroupCandidates = () => {
    const selectableInvoices = availableInvoices.filter((invoice) => !isSuggestedInvoice(invoice));
    const unreservedTransactions = getVisibleManualTransactions()
      .filter((transaction) => Number(transaction.remainingAmount) > 0);

    return selectableInvoices.some((invoice) => {
      const invoiceRemainingAmount = Number(invoice.remainingAmount) || 0;
      if (invoiceRemainingAmount <= 0) return false;

      const matchingAmounts = unreservedTransactions
        .filter((transaction) =>
          (transaction.currency || 'SEK') === (invoice.currency || 'SEK'))
        .map((transaction) => Number(transaction.remainingAmount) || 0)
        .filter((amount) => amount > 0 && amount <= invoiceRemainingAmount)
        .sort((left, right) => left - right);

      return matchingAmounts.length >= 2 &&
        matchingAmounts[0] + matchingAmounts[1] <= invoiceRemainingAmount;
    });
  };

  const updateManualToggleAvailability = () => {
    if (!manualToggle) return;

    const hasCandidates = hasManualGroupCandidates();
    manualToggle.classList.toggle('d-none', !hasCandidates);
    if (!hasCandidates && !manualOverrideInvoiceId) {
      setManualBuilderOpen(false);
    }
  };

  const createManualSummaryItem = (label, value) => {
    const item = document.createElement('div');
    const term = document.createElement('span');
    term.textContent = label;
    const description = document.createElement('strong');
    description.textContent = value;
    item.append(term, description);
    return item;
  };

  const updateManualSummary = () => {
    if (!manualSummary || !manualConfirm) return;

    const invoice = getSelectedManualInvoice();
    const selected = getSelectedManualTransactions();
    const total = selected.reduce((sum, transaction) => sum + (Number(transaction.remainingAmount) || 0), 0);
    const remaining = Number(invoice?.remainingAmount) || 0;
    const difference = invoice ? remaining - total : 0;
    const hasCurrencyMismatch = Boolean(invoice) && selected.some((transaction) =>
      (transaction.currency || 'SEK') !== (invoice.currency || 'SEK'));
    const isValid = Boolean(invoice) &&
      selected.length >= 2 &&
      total > 0 &&
      total <= remaining &&
      !hasCurrencyMismatch;

    manualSummary.replaceChildren(
      createManualSummaryItem('Valda betalningar', String(selected.length)),
      createManualSummaryItem('Vald summa', currencyFormatter.format(total)),
      createManualSummaryItem('Kvar efter grupp', invoice ? currencyFormatter.format(Math.max(difference, 0)) : '—')
    );
    manualConfirm.disabled = !isValid;

    if (!invoice) {
      setManualFeedback('Välj först vilken faktura betalningarna ska matchas mot.');
    } else if (selected.length < 2) {
      setManualFeedback('Välj minst två betalningar.');
    } else if (hasCurrencyMismatch) {
      setManualFeedback('Alla betalningar måste ha samma valuta som fakturan.', 'error');
    } else if (total > remaining) {
      setManualFeedback(`Gruppen överstiger fakturans restbelopp med ${currencyFormatter.format(total - remaining)}.`, 'error');
    } else if (difference === 0) {
      setManualFeedback('Betalningarna täcker fakturans restbelopp exakt.', 'success');
    } else {
      setManualFeedback(`${currencyFormatter.format(difference)} återstår på fakturan efter gruppen.`);
    }
  };

  const renderManualTransactions = () => {
    if (!manualTransactions) return;
    manualTransactions.replaceChildren();
    const visibleTransactions = getVisibleManualTransactions();

    if (visibleTransactions.length === 0) {
      const empty = document.createElement('p');
      empty.className = 'bankrec-payment-bundle-message';
      empty.textContent = 'Det finns inga övriga betalningar att gruppera. Hantera regelmotorns förslag ovan.';
      manualTransactions.appendChild(empty);
      updateManualSummary();
      return;
    }

    const suggestion = getSelectedInvoiceSuggestion();
    const suggestedTransactionIds = new Set(
      (suggestion?.allocations || []).map((allocation) => allocation.transactionId));
    const sortedTransactions = [...visibleTransactions].sort((left, right) => {
      const leftSuggested = suggestedTransactionIds.has(left.transactionId);
      const rightSuggested = suggestedTransactionIds.has(right.transactionId);
      if (leftSuggested !== rightSuggested) return leftSuggested ? -1 : 1;
      return visibleTransactions.indexOf(left) - visibleTransactions.indexOf(right);
    });

    sortedTransactions.forEach((transaction, index) => {
      const isSuggested = suggestedTransactionIds.has(transaction.transactionId);
      const row = document.createElement('label');
      row.className = 'bankrec-manual-bundle-transaction';
      row.classList.toggle('is-suggested', isSuggested);
      const checkbox = document.createElement('input');
      checkbox.type = 'checkbox';
      checkbox.value = transaction.transactionId;
      checkbox.id = `bankrec-manual-bundle-tx-${index}`;
      checkbox.checked = selectedManualTransactionIds.has(transaction.transactionId);
      checkbox.addEventListener('change', () => {
        if (checkbox.checked) {
          selectedManualTransactionIds.add(transaction.transactionId);
        } else {
          selectedManualTransactionIds.delete(transaction.transactionId);
        }
        row.classList.toggle('is-selected', checkbox.checked);
        updateManualSummary();
      });
      const copy = document.createElement('span');
      copy.className = 'bankrec-manual-bundle-transaction__copy';
      const title = document.createElement('strong');
      title.textContent = transaction.transactionId;
      if (isSuggested) {
        const badge = document.createElement('span');
        badge.className = 'bankrec-manual-bundle-transaction__badge';
        badge.textContent = `Föreslagen · ${suggestion.confidenceScore}%`;
        copy.append(title, badge);
      } else {
        copy.appendChild(title);
      }
      const meta = document.createElement('span');
      meta.textContent = [
        transaction.debtorName || 'Betalare saknas',
        formatDate(transaction.date),
        transaction.reference || 'Referens saknas'
      ].join(' · ');
      copy.appendChild(meta);
      const amount = document.createElement('strong');
      amount.className = 'bankrec-manual-bundle-transaction__amount';
      amount.textContent = currencyFormatter.format(Number(transaction.remainingAmount) || 0);
      row.classList.toggle('is-selected', checkbox.checked);
      row.append(checkbox, copy, amount);
      manualTransactions.appendChild(row);
    });

    updateManualSummary();
  };

  const renderManualRecommendation = () => {
    if (!manualRecommendation || !manualRecommendationTitle || !manualRecommendationDetail || !manualApplySuggestion) return;

    const invoice = getSelectedManualInvoice();
    const suggestion = getSelectedInvoiceSuggestion();
    manualRecommendation.classList.toggle('d-none', !invoice);
    manualRecommendation.classList.toggle('is-empty', Boolean(invoice) && !suggestion);
    manualApplySuggestion.classList.toggle('d-none', !suggestion);

    if (!invoice) {
      manualRecommendationTitle.textContent = '';
      manualRecommendationDetail.textContent = '';
      return;
    }

    if (!suggestion) {
      manualRecommendationTitle.textContent = 'Ingen säker grupp hittades för fakturan.';
      manualRecommendationDetail.textContent = 'Du kan fortfarande välja betalningarna manuellt i listan.';
      return;
    }

    const allocationCount = suggestion.allocations?.length || 0;
    const exactReferenceCount = (suggestion.allocations || [])
      .filter((allocation) => allocation.exactReferenceMatched).length;
    manualRecommendationTitle.textContent =
      `Systemet föreslår ${allocationCount} betalningar med ${suggestion.confidenceScore}% regelstöd.`;
    manualRecommendationDetail.textContent =
      `${exactReferenceCount}/${allocationCount} referenser stämmer · summa ${currencyFormatter.format(Number(suggestion.totalMatchedAmount) || 0)}.`;
  };

  const handleManualInvoiceChange = () => {
    const selectedInvoiceId = manualInvoice?.value || '';
    if (manualOverrideInvoiceId && selectedInvoiceId !== manualOverrideInvoiceId) {
      manualOverrideInvoiceId = null;
      renderManualInvoiceOptions(selectedInvoiceId);
    }
    selectedManualTransactionIds.clear();
    renderManualRecommendation();
    renderManualTransactions();
  };

  const applyManualSuggestion = () => {
    const suggestion = getSelectedInvoiceSuggestion();
    if (!suggestion) return;

    selectedManualTransactionIds.clear();
    (suggestion.allocations || []).forEach((allocation) => {
      selectedManualTransactionIds.add(allocation.transactionId);
    });
    renderManualTransactions();
  };

  const renderManualInvoiceOptions = (selectedInvoiceId = '') => {
    if (!manualInvoice) return;

    const selectableInvoices = availableInvoices.filter((invoice) =>
      !isSuggestedInvoice(invoice) || invoice.invoiceId === manualOverrideInvoiceId);
    manualInvoice.replaceChildren();
    const placeholder = document.createElement('option');
    placeholder.value = '';
    placeholder.textContent = selectableInvoices.length > 0
      ? 'Välj faktura'
      : 'Alla fakturor har redan ett säkert förslag ovan';
    manualInvoice.appendChild(placeholder);

    selectableInvoices.forEach((invoice) => {
      const option = document.createElement('option');
      option.value = invoice.invoiceId;
      option.textContent = `Faktura ${invoice.invoiceNo} · ${invoice.customerName || 'Kund saknas'} · rest ${currencyFormatter.format(Number(invoice.remainingAmount) || 0)}`;
      manualInvoice.appendChild(option);
    });

    manualInvoice.disabled = selectableInvoices.length === 0;
    manualInvoice.value = selectableInvoices.some((invoice) => invoice.invoiceId === selectedInvoiceId)
      ? selectedInvoiceId
      : '';
  };

  const openManualAdjustment = (suggestion) => {
    manualOverrideInvoiceId = suggestion.invoiceId;
    selectedManualTransactionIds.clear();
    (suggestion.allocations || []).forEach((allocation) => {
      selectedManualTransactionIds.add(allocation.transactionId);
    });
    renderManualInvoiceOptions(suggestion.invoiceId);
    renderManualRecommendation();
    renderManualTransactions();
    setManualBuilderOpen(true);
    manualBuilder?.scrollIntoView({ behavior: 'smooth', block: 'start' });
  };

  const renderManualBuilderData = (payload) => {
    currentSuggestions = Array.isArray(payload.suggestions) ? payload.suggestions : [];
    availableTransactions = Array.isArray(payload.availableTransactions) ? payload.availableTransactions : [];
    availableInvoices = Array.isArray(payload.availableInvoices) ? payload.availableInvoices : [];
    manualOverrideInvoiceId = null;
    selectedManualTransactionIds.clear();

    renderManualInvoiceOptions();
    renderManualRecommendation();
    renderManualTransactions();
    updateManualToggleAvailability();
  };

  const setManualBuilderOpen = (open) => {
    if (!manualBuilder || !manualToggle) return;
    manualBuilder.classList.toggle('d-none', !open);
    manualToggle.setAttribute('aria-expanded', String(open));
    if (open) {
      manualInvoice?.focus();
    }
  };

  const confirmManualBundle = async () => {
    if (!manualConfirm || !confirmManualEndpoint) return;
    const invoice = getSelectedManualInvoice();
    const transactionIds = [...selectedManualTransactionIds];
    if (!invoice || transactionIds.length < 2) return;

    manualConfirm.disabled = true;
    setManualFeedback('Kontrollerar och sparar betalningsgruppen...');
    try {
      const response = await fetch(confirmManualEndpoint, {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
          'RequestVerificationToken': antiForgeryToken,
          'X-Requested-With': 'XMLHttpRequest'
        },
        body: JSON.stringify({
          invoiceId: invoice.invoiceId,
          transactionIds,
          expectedVersion: currentVersion
        })
      });
      const payload = await response.json();
      if (!response.ok || !payload.success) {
        if (response.status === 409 || payload.conflict) {
          await refreshWorkspaceAfterConfirmation();
          throw new Error('Underlaget ändrades. Välj betalningarna igen i den uppdaterade listan.');
        }
        throw new Error(payload.errorMessage || 'Den manuella betalningsgruppen kunde inte sparas.');
      }

      setManualBuilderOpen(false);
      const refreshed = await refreshWorkspaceAfterConfirmation();
      if (refreshed) {
        prependMessage('Den manuella betalningsgruppen är bekräftad.', 'success');
      } else {
        setMessage('Gruppen sparades, men vyn kunde inte uppdateras. Ladda om sidan innan du fortsätter.', 'error');
      }
    } catch (error) {
      updateManualSummary();
      setManualFeedback(error instanceof Error ? error.message : 'Den manuella betalningsgruppen kunde inte sparas.', 'error');
    }
  };

  const loadSuggestions = async () => {
    if (!queryEndpoint || !confirmEndpoint) {
      setMessage('Betalningsgrupper är inte tillgängliga.', 'error');
      return;
    }

    try {
      const response = await fetch(queryEndpoint, { headers: { 'X-Requested-With': 'XMLHttpRequest' } });
      const payload = await response.json();
      if (!response.ok || !payload.success) {
        throw new Error(payload.errorMessage || 'Betalningsgrupper kunde inte laddas.');
      }

      currentVersion = Number(payload.version) || 0;
      renderSuggestions(payload.suggestions, currentVersion);
      renderManualBuilderData(payload);
      return true;
    } catch (error) {
      setMessage(error instanceof Error ? error.message : 'Betalningsgrupper kunde inte laddas.', 'error');
      return false;
    }
  };

  window.BankRecPaymentBundles = {
    render: renderSuggestions,
    reload: loadSuggestions
  };

  manualToggle?.addEventListener('click', () => {
    const shouldOpen = manualBuilder?.classList.contains('d-none') ?? true;
    if (shouldOpen) {
      manualOverrideInvoiceId = null;
      selectedManualTransactionIds.clear();
      renderManualInvoiceOptions();
      renderManualRecommendation();
      renderManualTransactions();
    }
    setManualBuilderOpen(shouldOpen);
  });
  manualClose?.addEventListener('click', () => setManualBuilderOpen(false));
  manualInvoice?.addEventListener('change', handleManualInvoiceChange);
  manualApplySuggestion?.addEventListener('click', applyManualSuggestion);
  manualConfirm?.addEventListener('click', confirmManualBundle);

  loadSuggestions();
})();
