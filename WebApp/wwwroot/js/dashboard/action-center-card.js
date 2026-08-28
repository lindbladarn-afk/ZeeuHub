// Handles expansion and interaction inside the Action Center dashboard card.
window.ZeeUDashboardActionCenterCard = (function () {
    const updateBadge = (count) => {
        document.querySelectorAll('.action-center-badge').forEach((badge) => {
            badge.textContent = `${count} Insikter`;
        });

        const headerBadge = document.getElementById('actionCenterBadge');
        const headerBadgeCount = document.getElementById('actionCenterBadgeCount');
        if (!headerBadge || !headerBadgeCount) {
            return;
        }

        headerBadgeCount.textContent = String(count);
        headerBadge.classList.toggle('d-none', count <= 0);
    };

    const getToastHost = () => {
        let host = document.querySelector('.ac-toast');
        if (!host) {
            host = document.createElement('div');
            host.className = 'ac-toast';
            document.body.appendChild(host);
        }

        return host;
    };

    const showToast = (message, undoCallback) => {
        const host = getToastHost();
        host.innerHTML = `
            <div class="card bg-dark border border-secondary shadow-sm p-3 text-white">
                <div class="d-flex justify-content-between align-items-center mb-2">
                    <span class="fw-bold">Action Center</span>
                    <button class="btn btn-sm btn-outline-light rounded-pill px-2 py-0" type="button" data-ac-toast-close="true">X</button>
                </div>
                <div class="mb-2">${message}</div>
                ${undoCallback ? '<button class="btn btn-sm btn-warning rounded-pill px-3 py-1" type="button" data-ac-toast-undo="true">Ångra</button>' : ''}
            </div>`;

        host.querySelector('[data-ac-toast-close="true"]')?.addEventListener('click', () => {
            host.innerHTML = '';
        });

        if (undoCallback) {
            host.querySelector('[data-ac-toast-undo="true"]')?.addEventListener('click', async () => {
                await undoCallback();
                host.innerHTML = '';
                window.location.reload();
            });
        }

        window.setTimeout(() => {
            if (host) {
                host.innerHTML = '';
            }
        }, 5000);
    };

    const postStatus = async (root, payload) => {
        const token = root.querySelector('.ac-antiforgery')?.value || '';
        const updateUrl = root.dataset.updateUrl || '/ActionCenter/UpdateStatus';

        try {
            const response = await fetch(updateUrl, {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    'RequestVerificationToken': token
                },
                credentials: 'same-origin',
                body: JSON.stringify(payload)
            });

            if (!response.ok) {
                showToast(`Serverfel (${response.status})`);
                return false;
            }

            const data = await response.json().catch(() => ({}));
            if (data?.success === false) {
                showToast(data.message || 'Okänt fel');
                return false;
            }

            return true;
        } catch {
            showToast('Kunde inte nå servern');
            return false;
        }
    };

    const refreshSummary = async (root) => {
        const summaryUrl = root.dataset.summaryUrl || '/ActionCenter/Summary';

        try {
            const response = await fetch(summaryUrl, {
                method: 'GET',
                credentials: 'same-origin'
            });

            if (!response.ok) {
                return;
            }

            const data = await response.json();
            updateBadge(data.count ?? 0);
        } catch {
            // Ignore summary refresh failures in the dashboard shell.
        }
    };

    const buildPayload = (card, status, comment) => ({
        insightId: card.getAttribute('data-insight-id'),
        status,
        title: card.getAttribute('data-title'),
        description: card.getAttribute('data-description'),
        category: card.getAttribute('data-category'),
        priority: card.getAttribute('data-priority'),
        detectedAt: card.getAttribute('data-detected'),
        comment
    });

    const escapeSelectorValue = (value) => {
        if (window.CSS?.escape) {
            return window.CSS.escape(value);
        }

        return value.replace(/["\\]/g, '\\$&');
    };

    const findInsightCard = (root, element) => {
        const directCard = element.closest('.list-group-item');
        if (directCard && root.contains(directCard)) {
            return directCard;
        }

        const insightId = element.getAttribute('data-insight-id') || element.closest('[data-ac-detail-panel]')?.getAttribute('data-ac-detail-panel');
        if (!insightId) {
            return null;
        }

        return root.querySelector(`.action-item[data-insight-id="${escapeSelectorValue(insightId)}"]`);
    };

    const selectDetail = (root, card) => {
        if (!root.classList.contains('ac-card--workspace') || !card) {
            return;
        }

        const insightId = card.getAttribute('data-insight-id');
        root.querySelectorAll('.action-item.is-selected').forEach((item) => item.classList.remove('is-selected'));
        card.classList.add('is-selected');

        root.querySelector('.ac-detail-placeholder')?.classList.add('d-none');
        root.querySelectorAll('.ac-detail-content').forEach((panel) => {
            panel.classList.toggle('d-none', panel.getAttribute('data-ac-detail-panel') !== insightId);
        });
    };

    const selectFirstVisibleDetail = (root) => {
        const firstVisible = Array.from(root.querySelectorAll('.action-item'))
            .find((card) => !card.classList.contains('d-none') && !card.classList.contains('ac-soft-hidden'));

        if (firstVisible) {
            selectDetail(root, firstVisible);
            return;
        }

        root.querySelectorAll('.action-item.is-selected').forEach((item) => item.classList.remove('is-selected'));
        root.querySelector('.ac-detail-placeholder')?.classList.remove('d-none');
        root.querySelectorAll('.ac-detail-content').forEach((panel) => panel.classList.add('d-none'));
    };

    const applyFilter = (root, filter) => {
        root.querySelectorAll('.ac-filter').forEach((button) => {
            const active = button.getAttribute('data-ac-filter') === filter;
            button.classList.toggle('active', active);
            button.classList.toggle('btn-portal-outline', !active);
        });

        root.querySelectorAll('.action-item').forEach((card) => {
            const terms = (card.getAttribute('data-ac-filter-terms') || '').split(/\s+/);
            card.classList.toggle('d-none', filter !== 'all' && !terms.includes(filter));
        });

        selectFirstVisibleDetail(root);
    };

    const handleStatusAction = async (root, button, status) => {
        const card = findInsightCard(root, button);
        if (!card) {
            showToast('Hittade inte kortet för denna åtgärd.');
            return;
        }

        const insightId = card.getAttribute('data-insight-id') || button.getAttribute('data-insight-id');
        if (!insightId) {
            showToast('Ogiltigt kort (saknar id).');
            return;
        }

        const actionScope = button.closest('.ac-detail-content') || card;
        const commentBox = actionScope.querySelector('.ac-comment-box');
        const commentInput = actionScope.querySelector('.ac-comment-input');
        if (status === 'Completed' && commentBox && commentBox.classList.contains('d-none')) {
            commentBox.classList.remove('d-none');
            commentInput?.focus();
            return;
        }

        const payload = buildPayload(card, status, status === 'Completed' ? (commentInput?.value || '') : undefined);
        const ok = await postStatus(root, payload);
        if (!ok) {
            return;
        }

        if (status === 'Completed') {
            card.classList.add('ac-soft-hidden');
            root.querySelector(`[data-ac-detail-panel="${escapeSelectorValue(insightId)}"]`)?.classList.add('ac-soft-hidden');
            commentBox?.classList.add('d-none');
            if (commentInput) {
                commentInput.value = '';
            }

            showToast('Insikt markerad som klar.', async () => {
                await postStatus(root, { ...payload, status: 'Active', comment: undefined });
            });
        } else {
            card.classList.remove('ac-soft-hidden');
            root.querySelector(`[data-ac-detail-panel="${escapeSelectorValue(insightId)}"]`)?.classList.remove('ac-soft-hidden');
            showToast('Insikt återöppnad.');
        }

        await refreshSummary(root);
    };

    const initCardRoot = (root) => {
        if (root.dataset.acInitialized === 'true') {
            return;
        }
        root.dataset.acInitialized = 'true';

        root.addEventListener('click', (event) => {
            const target = event.target;

            const toggle = target.closest('.ac-toggle');
            if (toggle && root.contains(toggle)) {
                const card = toggle.closest('.action-item');
                if (root.classList.contains('ac-card--workspace')) {
                    selectDetail(root, card);
                    return;
                }

                const details = toggle.parentElement?.querySelector('.ac-details');
                const icon = toggle.querySelector('.fa-chevron-down');
                details?.classList.toggle('d-none');
                icon?.classList.toggle('fa-rotate-180');
                return;
            }

            const completeButton = target.closest('.ac-mark-done');
            if (completeButton && root.contains(completeButton)) {
                event.preventDefault();
                handleStatusAction(root, completeButton, 'Completed');
                return;
            }

            const reopenButton = target.closest('.ac-reopen');
            if (reopenButton && root.contains(reopenButton)) {
                event.preventDefault();
                handleStatusAction(root, reopenButton, 'Active');
                return;
            }

            const sendButton = target.closest('.ac-send-comment');
            if (sendButton && root.contains(sendButton)) {
                event.preventDefault();
                handleStatusAction(root, sendButton, 'Completed');
                return;
            }

            const filterButton = target.closest('.ac-filter');
            if (filterButton && root.contains(filterButton)) {
                event.preventDefault();
                applyFilter(root, filterButton.getAttribute('data-ac-filter') || 'all');
            }
        });

        root.addEventListener('keydown', (event) => {
            const textarea = event.target.closest('.ac-comment-input');
            if (!textarea || !root.contains(textarea)) {
                return;
            }

            if (event.key === 'Enter' && !event.shiftKey) {
                event.preventDefault();
                const button = textarea.closest('.list-group-item')?.querySelector('.ac-send-comment');
                if (button) {
                    handleStatusAction(root, button, 'Completed');
                }
            }
        });

        root.addEventListener('input', (event) => {
            const textarea = event.target.closest('.ac-comment-input');
            if (!textarea || !root.contains(textarea)) {
                return;
            }

            textarea.style.height = 'auto';
            textarea.style.height = `${textarea.scrollHeight}px`;
        });

        refreshSummary(root);
        selectFirstVisibleDetail(root);
    };

    const initHistoryRoot = (root) => {
        if (root.dataset.acHistoryInitialized === 'true') {
            return;
        }
        root.dataset.acHistoryInitialized = 'true';

        root.addEventListener('click', (event) => {
            const toggle = event.target.closest('.ac-history-toggle');
            if (toggle && root.contains(toggle)) {
                const details = toggle.parentElement?.querySelector('.ac-history-details');
                const icon = toggle.querySelector('.fa-chevron-down');
                details?.classList.toggle('d-none');
                icon?.classList.toggle('fa-rotate-180');
                return;
            }

            const reopenButton = event.target.closest('.ac-reopen');
            if (!reopenButton || !root.contains(reopenButton)) {
                return;
            }

            event.preventDefault();
            handleStatusAction(root, reopenButton, 'Active');
        });
    };

    const init = () => {
        document.querySelectorAll('[data-dashboard-action-center="true"]').forEach(initCardRoot);
        document.querySelectorAll('[data-dashboard-action-center-history="true"]').forEach(initHistoryRoot);
    };

    return { init };
})();

document.addEventListener('DOMContentLoaded', () => {
    window.ZeeUDashboardActionCenterCard?.init?.();
});
