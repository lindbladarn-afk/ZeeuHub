(() => {
  const txScript = document.getElementById('bankrec-transactions-json');
  const invScript = document.getElementById('bankrec-invoices-json');
  const invoiceIdScript = document.getElementById('bankrec-invoice-id');
  if (!txScript || !invScript || !invoiceIdScript) return;

  const transactions = JSON.parse(txScript.textContent || '[]');
  const invoices = JSON.parse(invScript.textContent || '[]');
  const invoiceId = JSON.parse(invoiceIdScript.textContent || '""');

  const titleEl = document.getElementById('bankrec-invoice-detail-title');
  const statusEl = document.getElementById('bankrec-invoice-detail-status');
  const bodyEl = document.getElementById('bankrec-invoice-detail-body');
  if (!titleEl || !statusEl || !bodyEl) return;

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

  const getAllocations = (tx) => Array.isArray(tx.allocations) ? tx.allocations : [];

  const payments = transactions.flatMap((tx) =>
    getAllocations(tx)
      .filter((allocation) => allocation.invoiceId === invoiceId)
      .map((allocation) => ({
        transactionId: tx.id,
        date: tx.date || tx.valueDate || '',
        amount: allocation.matchedAmount || 0,
        transactionAmount: tx.amount || 0,
        currency: allocation.currency || tx.currency || 'SEK',
        reference: tx.reference || '',
        payer: tx.debtorName || '',
        matchRule: allocation.matchRule || tx.matchRule || 'manual',
        matchType: allocation.matchType || tx.matchType || 'manual'
      }))
  );

  const paid = payments.reduce((sum, payment) => sum + (payment.amount || 0), 0);
  const invoice = invoices.find((item) => item.id === invoiceId) || invoices.find((item) => item.invoiceNo === invoiceId);

  if (!invoice) {
    titleEl.textContent = 'Ingen faktura hittades.';
    statusEl.textContent = '—';
    bodyEl.innerHTML = '<div class="text-muted">Fakturan finns inte i den aktuella listan.</div>';
    return;
  }

  const remainingBase = Number(invoice.amount || 0);
  const remaining = Math.max(remainingBase - paid, 0);
  const full = remaining === 0 && paid > 0;
  const partial = paid > 0 && remaining > 0;
  const status = full ? 'Matchad' : partial ? 'Delbetald' : 'Omatchad';

  titleEl.textContent = `Faktura ${invoice.invoiceNo || invoice.id} · ${invoice.customerName || ''}`;
  statusEl.textContent = status;
  statusEl.className = `badge rounded-pill ${full ? 'bg-success' : partial ? 'bg-warning text-dark' : 'bg-secondary'}`;

  const demoBadge = invoice.isDemo
    ? '<span class="badge rounded-pill bg-info text-dark ms-2">Demo</span>'
    : '';

  bodyEl.innerHTML = `
    <div class="row g-3">
      <div class="col-md-4"><div class="text-muted small">Belopp</div><div class="h6">${formatAmount(invoice.amount)} ${escapeHtml(invoice.currency || '')}${demoBadge}</div></div>
      <div class="col-md-4"><div class="text-muted small">Matchat</div><div class="h6">${formatAmount(paid)} ${escapeHtml(invoice.currency || '')}</div></div>
      <div class="col-md-4"><div class="text-muted small">Kvar</div><div class="h6">${formatAmount(remaining)} ${escapeHtml(invoice.currency || '')}</div></div>
    </div>
    <div class="mt-3">
      <div class="text-muted small mb-2">Allokeringar</div>
      ${payments.length === 0 ? '<div class="text-muted">Ingen matchning ännu.</div>' : `
        <div class="table-responsive">
          <table class="table table-sm table-borderless align-middle mb-0 bankrec-table">
            <thead>
              <tr>
                <th>Transaktion</th>
                <th>Datum</th>
                <th>Allokerat</th>
                <th>Transaktionsbelopp</th>
                <th>Referens</th>
                <th>Betalare</th>
                <th>Regel</th>
              </tr>
            </thead>
            <tbody>
              ${payments.map((payment) => `
                <tr>
                  <td>${escapeHtml(payment.transactionId)}</td>
                  <td>${escapeHtml(payment.date)}</td>
                  <td>${formatAmount(payment.amount)} ${escapeHtml(payment.currency)}</td>
                  <td>${formatAmount(payment.transactionAmount)} ${escapeHtml(payment.currency)}</td>
                  <td>${escapeHtml(payment.reference || '-')}</td>
                  <td>${escapeHtml(payment.payer || '-')}</td>
                  <td>${escapeHtml(payment.matchRule || payment.matchType || 'manual')}</td>
                </tr>
              `).join('')}
            </tbody>
          </table>
        </div>
      `}
    </div>
  `;
})();
