// Coordinates dashboard card loading, safe AI output rendering, and client-side refresh behavior.
window.ZeeUDashboard = (function () {
    const getAntiForgery = () => document.querySelector('#__af input[name="__RequestVerificationToken"]')?.value;
    const escapeHtml = value => {
        const element = document.createElement('span');
        element.textContent = String(value ?? '');
        return element.innerHTML;
    };

    const formatAiResponse = (text) => {
        if (!text) return "";
        return String(text)
            .split('\n')
            .map(line => line.trim())
            .filter(line => line.length > 0)
            .map(line => `<p>${escapeHtml(line)}</p>`)
            .join('');
    };

    const formatSek = (value) => {
        if (value === null || value === undefined || Number.isNaN(Number(value))) return null;
        return new Intl.NumberFormat('sv-SE', {
            style: 'currency',
            currency: 'SEK',
            minimumFractionDigits: 4,
            maximumFractionDigits: 4
        }).format(Number(value));
    };

    const initAiSearch = () => {
        const btnAsk = document.getElementById('btnAiAsk');
        const inputAsk = document.getElementById('aiDashboardQuery');
        const aiContainer = document.getElementById('aiResponseContainer');
        const aiText = document.getElementById('aiResponseText');
        const aiHeaderTitle = document.getElementById('aiHeaderTitle');

        const askAi = async () => {
            const q = (inputAsk?.value || '').trim();
            if (!q) return;

            aiContainer.style.display = 'block';
            aiHeaderTitle.innerHTML = '<span class="zeeu-z-badge">Z</span> ZeeU Intelligence tänker...';
            aiText.innerHTML = `<div class="p-3">Tänker...</div>`;

            if (btnAsk) btnAsk.disabled = true;

            try {
                const response = await fetch('/AI/query', {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/json', 'RequestVerificationToken': getAntiForgery() },
                    credentials: 'same-origin',
                    body: JSON.stringify({ question: q, dataSourceKey: 'Default', source: 'dashboard' })
                });

                if (response.redirected && (response.url || '').includes('/Identity/Account/Login')) {
                    throw new Error('Du verkar vara utloggad. Ladda om sidan och logga in igen.');
                }

                if (!response.ok) {
                    if (response.status === 401 || response.status === 403) {
                        throw new Error('Du saknar behörighet för ZeeU Intelligence.');
                    }
                    throw new Error(`ZeeU Intelligence svarade med status ${response.status}.`);
                }

                const contentType = (response.headers.get('content-type') || '').toLowerCase();
                if (!contentType.includes('application/json')) {
                    const txt = await response.text();
                    if ((txt || '').includes('/Identity/Account/Login')) {
                        throw new Error('Du verkar vara utloggad. Ladda om sidan och logga in igen.');
                    }
                    throw new Error('ZeeU Intelligence returnerade ett ogiltigt svar.');
                }

                const data = await response.json();
                aiHeaderTitle.innerHTML = '<span class="zeeu-z-badge">Z</span> ZeeU Intelligence Insikt';
                const answer = data.answer || data.Answer || "Hittade inget svar.";
                const cost = formatSek(data.totalCostSek);
                const totalTokens = data.totalTokens ?? data.TotalTokens ?? null;
                const metaParts = [];
                if (totalTokens !== null && totalTokens !== undefined) metaParts.push(`Tokens: ${escapeHtml(totalTokens)}`);
                if (cost) metaParts.push(`Kostnad: ${cost}`);
                const metaHtml = metaParts.length > 0
                    ? `<div class="small text-muted mt-2">${metaParts.join(' | ')}</div>`
                    : '';

                aiText.innerHTML = `<div class="ai-answer-content">${formatAiResponse(answer)}${metaHtml}</div>`;
            } catch (e) {
                const msg = (e && e.message) ? e.message : 'Kunde inte ansluta till ZeeU Intelligence.';
                const error = document.createElement('p');
                error.className = 'text-danger';
                error.textContent = msg;
                aiText.replaceChildren(error);
            } finally { if (btnAsk) btnAsk.disabled = false; }
        };

        btnAsk?.addEventListener('click', (e) => { e.preventDefault(); askAi(); });
        inputAsk?.addEventListener('keydown', (e) => { if (e.key === 'Enter') { e.preventDefault(); askAi(); } });
    };

    const renderCardLoadError = (shell) => {
        shell.dataset.loaded = 'error';
        shell.setAttribute('aria-busy', 'false');
        shell.innerHTML = `
            <div class="card kpi-card h-100 dashboard-card-state dashboard-card-state--error" role="alert">
                <div class="dashboard-card-state__body">
                    <span class="dashboard-card-state__icon" aria-hidden="true">
                        <i class="fa fa-triangle-exclamation"></i>
                    </span>
                    <div class="dashboard-card-state__copy">
                        <div class="dashboard-card-state__eyebrow">Dashboardblock</div>
                        <h3 class="dashboard-card-state__title">Blocket kunde inte laddas</h3>
                        <p class="dashboard-card-state__message">Försök igen för att hämta den senaste informationen.</p>
                    </div>
                    <button type="button" class="btn btn-portal btn-portal-outline btn-sm dashboard-card-state__retry" data-dashboard-card-retry>
                        <i class="fa fa-rotate-right me-1" aria-hidden="true"></i>
                        Försök igen
                    </button>
                </div>
            </div>`;
    };

    const formatCardTimestamps = (root = document) => {
        const timestamps = [];
        if (root instanceof Element && root.matches('[data-dashboard-updated-at]')) {
            timestamps.push(root);
        }
        timestamps.push(...root.querySelectorAll('[data-dashboard-updated-at]'));

        timestamps.forEach((element) => {
            const value = element.getAttribute('datetime');
            if (!value) return;
            const updatedAt = new Date(value);
            if (Number.isNaN(updatedAt.getTime())) return;
            element.textContent = new Intl.DateTimeFormat('sv-SE', {
                hour: '2-digit',
                minute: '2-digit'
            }).format(updatedAt);
            element.title = updatedAt.toLocaleString('sv-SE');
        });
    };

    const loadDashboardCard = (shell, url) => {
        if (!url || shell.dataset.loaded === 'loading') return;
        shell.dataset.loaded = 'loading';
        shell.setAttribute('aria-busy', 'true');
        const retryButton = shell.querySelector('[data-dashboard-card-retry]');
        if (retryButton) {
            retryButton.disabled = true;
            retryButton.innerHTML = '<i class="fa fa-spinner fa-spin me-1" aria-hidden="true"></i>Laddar…';
        }

        fetch(url, {
                method: 'GET',
                credentials: 'same-origin',
                headers: {
                    'X-Requested-With': 'XMLHttpRequest'
                }
            })
                .then(async (response) => {
                    if (!response.ok) {
                        throw new Error(`Lazy card failed with ${response.status}`);
                    }

                    const html = await response.text();
                    const template = document.createElement('template');
                    template.innerHTML = html.trim();
                    const nextNode = template.content.firstElementChild;
                    if (!nextNode) {
                        throw new Error('Lazy card returned empty markup');
                    }

                    shell.replaceWith(nextNode);
                    formatCardTimestamps(nextNode);
                    window.ZeeUDashboardActionCenterCard?.init?.();
                })
                .catch(() => {
                    renderCardLoadError(shell);
                });
    };

    const initLazyCards = () => {
        const cards = document.querySelectorAll('[data-dashboard-lazy-card="true"]');
        cards.forEach((shell) => {
            loadDashboardCard(shell, shell.dataset.url);
        });
    };

    let cardStateEventsBound = false;
    const initCardStates = () => {
        formatCardTimestamps();
        if (cardStateEventsBound) return;
        cardStateEventsBound = true;

        document.addEventListener('click', (event) => {
            const retryButton = event.target.closest('[data-dashboard-card-retry]');
            if (!retryButton) return;

            const shell = retryButton.closest('[data-dashboard-card-content]');
            const url = shell?.dataset.dashboardCardRefreshUrl;
            if (!shell || !url) return;

            event.preventDefault();
            shell.dataset.loaded = 'idle';
            loadDashboardCard(shell, url);
        });
    };

    const refreshCards = () => {
        window.ZeeUDashboardRevenueCard?.init?.();
        initCardStates();
        initLazyCards();
        window.ZeeUDashboardActionCenterCard?.init?.();
        window.ZeeUDashboardIntegrationStatus?.init?.();
    };

    const init = () => {
        initAiSearch();
        window.ZeeUDashboardLayout?.init?.();
        refreshCards();
    };

    return { init, refreshCards };
})();

document.addEventListener('DOMContentLoaded', () => {
    window.ZeeUDashboard.init();
});
