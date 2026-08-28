// Coordinates the Intelligence chat, results, feedback, quota, and progress UI.
window.ZeeUAI = (() => {
    let cfg = {};
    let strings = {};
    let chat, form, input, sendBtn, cancelBtn, errorTemplate;
    let suggestionButtons, suggestionWrap, modeToggle, modeButtons, assistedPanel, manualPanel, modeDescription;
    let manualForm, manualSql, manualRun, manualStatus;
    let statusDot, statusText;
    let resultsRow, resultsBody, resultsMeta, colMenuBody, tableFilterInput;
    let vizPlaceholder, chartCanvas, chartTypeSelect, chartSummary;
    let queryClient, chartView;
    let quotaWidget, quotaTrigger, quotaMini, quotaPop, quotaPopText, quotaPopBar, quotaInline, quotaInlineText;
    let quotaInlineActions, quotaAllowBtn, quotaBlockBtn;
    let quotaPaidPill;
    let quotaPinnedOpen = false;

    const setStatus = (state, text) => {
        if (!statusDot || !statusText) return;
        statusDot.classList.remove('busy', 'err');
        if (state === 'busy') statusDot.classList.add('busy');
        if (state === 'err') statusDot.classList.add('err');
        statusText.textContent = text || '';
    };

    const setInlineStatusTone = (element, tone) => {
        if (!element) return;
        element.classList.remove(
            'module-inline-status--muted',
            'module-inline-status--info',
            'module-inline-status--success',
            'module-inline-status--warning',
            'module-inline-status--danger');
        element.classList.add(`module-inline-status--${tone}`);
    };

    const autoGrow = (el) => {
        if (!el) return;
        el.style.height = 'auto';
        el.style.height = Math.min(el.scrollHeight, 140) + 'px';
    };

    const callBackend = (question, onProgress) => queryClient.query(
        question,
        {
            source: cfg.source || 'intelligence',
            dataSourceKey: cfg.selectedDataSourceKey || null
        },
        onProgress);

    const runManualQuery = (sql) => queryClient.manualQuery(sql);

    const setQuotaDecision = (choice) => queryClient.setQuotaDecision(choice);

    const getQuotaStatus = () => queryClient.getQuotaStatus();

    const refreshQuotaUi = async () => {
        try {
            const quota = await getQuotaStatus();
            if (quota?.success) updateQuotaUi(quota);
        } catch {
            // Keep current UI state if status endpoint is temporarily unavailable.
        }
    };

    const normalizeQuotaState = (src) => {
        if (!src) return null;
        const status = src.quotaStatus ?? src.status ?? null;
        const usedTokens = src.quotaUsedTokens ?? src.usedTokens ?? null;
        const freeTokens = src.quotaFreeTokens ?? src.freeTokens ?? null;
        const usagePercent = src.quotaUsagePercent ?? src.usagePercent ?? null;
        const message = src.quotaMessage ?? src.message ?? '';
        const periodTotalCostSek = src.quotaPeriodTotalCostSek ?? src.periodTotalCostSek ?? null;
        const paidExtraTokens = src.quotaPaidExtraTokens ?? src.paidExtraTokens ?? null;
        const paidExtraCostSek = src.quotaPaidExtraCostSek ?? src.paidExtraCostSek ?? null;
        const needsDecision = src.quotaNeedsDecision ?? src.needsDecision ?? false;
        const paidMode = src.quotaPaidMode ?? src.paidMode ?? false;

        if (!status && usedTokens === null && freeTokens === null && usagePercent === null) return null;
        return { status, usedTokens, freeTokens, usagePercent, message, periodTotalCostSek, paidExtraTokens, paidExtraCostSek, needsDecision, paidMode };
    };

    const updateQuotaUi = (src) => {
        const q = normalizeQuotaState(src);
        if (!q) return;

        const pct = Math.max(0, Math.min(100, Number(q.usagePercent ?? 0)));
        const used = Number(q.usedTokens ?? 0);
        const free = Math.max(1, Number(q.freeTokens ?? 1));
        const remainingPct = Math.max(0, 100 - pct);

        if (quotaWidget && quotaTrigger && quotaMini && quotaPopText && quotaPopBar) {
            quotaWidget.classList.remove('d-none');
            quotaMini.textContent = q.status === 'paid' ? '∞' : `${pct}%`;
            const paidExtraCostText = formatSek(q.paidExtraCostSek);
            const extraTokens = Math.max(0, Number(q.paidExtraTokens ?? 0));
            if (q.status === 'paid') {
                quotaPopText.innerHTML = `Extra tokens: ${extraTokens}<br>Kostnad extra: ${paidExtraCostText || '-'}`;
            } else {
                quotaPopText.textContent = `${used}/${free} tokens • kvar ${remainingPct}%`;
            }
            quotaPop?.classList.toggle('is-paid', q.status === 'paid');
            quotaPopBar.style.width = `${pct}%`;
            quotaPopBar.classList.remove('warn', 'danger');
            quotaTrigger.classList.remove('warn', 'danger', 'paid');
            let ringColor = '#22c55e';
            if (q.status === 'paid') {
                quotaTrigger.classList.add('paid');
                ringColor = '#38bdf8';
            } else if (pct >= 100 || q.status === 'blocked') {
                quotaPopBar.classList.add('danger');
                quotaTrigger.classList.add('danger');
                ringColor = '#ef4444';
            } else if (pct >= 75 || q.status === 'warning') {
                quotaPopBar.classList.add('warn');
                quotaTrigger.classList.add('warn');
                ringColor = '#f59e0b';
            }
            quotaTrigger.style.background = `conic-gradient(${ringColor} ${pct}%, rgba(148,163,184,0.26) 0)`;
        }

        if (quotaInline && quotaInlineText) {
            const showInline =
                q.status === 'warning' ||
                q.status === 'needsdecision' ||
                q.status === 'needs_decision' ||
                q.status === 'blocked';
            quotaInline.classList.toggle('d-none', !showInline);
            quotaInline.classList.remove('warn', 'danger');
            if (q.status === 'blocked' || pct >= 100) quotaInline.classList.add('danger');
            else quotaInline.classList.add('warn');
            quotaInlineText.textContent = q.message || `Du har använt ${pct}% av din fria AI-kvot (${used}/${free} tokens).`;
        }

        if (quotaInlineActions) {
            const needsDecision = q.status === 'needsdecision' || q.status === 'needs_decision' || q.status === 'blocked';
            quotaInlineActions.classList.toggle('d-none', !needsDecision);
        }

        if (quotaPaidPill) {
            quotaPaidPill.classList.toggle('d-none', q.status !== 'paid');
        }
    };

    const appendBubble = (role, text) => {
        const template = chat.querySelector(`.chat-bubble.${role}`);
        if (!template) return null;
        const bubble = template.cloneNode(true);
        bubble.classList.remove('d-none');

        const textEl = bubble.querySelector('.bubble-text');
        if (textEl) {
            if (role === 'ai') renderShortText(textEl, text || '');
            else textEl.textContent = text || '';
        }

        chat.appendChild(bubble);
        chat.scrollTop = chat.scrollHeight;
        return bubble;
    };

    const appendErrorBubble = (error, originalQuestion, suggestions = []) => {
        const safeError = typeof error === 'string'
            ? { title: 'Frågan kunde inte slutföras', message: error, canRetry: true, tone: 'danger' }
            : (error || {});
        const bubble = errorTemplate.cloneNode(true);
        bubble.id = '';
        bubble.classList.remove('d-none');
        bubble.classList.toggle('is-warning', safeError.tone === 'warning');
        bubble.classList.toggle('is-info', safeError.tone === 'info');
        const titleEl = bubble.querySelector('.ai-error-title');
        const textEl = bubble.querySelector('.bubble-text');
        const actions = bubble.querySelector('.ai-error-actions');
        if (titleEl) titleEl.textContent = safeError.title || 'Frågan kunde inte slutföras';
        if (textEl) textEl.textContent = safeError.message || strings.errorBubble || 'Ett oväntat fel uppstod.';
        if (actions && safeError.canRetry && originalQuestion) {
            const retry = document.createElement('button');
            retry.type = 'button';
            retry.className = 'btn btn-portal btn-sm';
            retry.innerHTML = `<i class="fa fa-rotate-right me-1" aria-hidden="true"></i>${strings.retry || 'Försök igen'}`;
            retry.addEventListener('click', () => sendMessage(originalQuestion));
            actions.appendChild(retry);
        }
        if (actions) {
            suggestions.slice(0, 3).forEach(suggestion => {
                const button = document.createElement('button');
                button.type = 'button';
                button.className = 'btn btn-portal btn-portal-outline btn-sm';
                button.textContent = String(suggestion);
                button.addEventListener('click', () => sendMessage(String(suggestion)));
                actions.appendChild(button);
            });
        }
        chat.appendChild(bubble);
        chat.scrollTop = chat.scrollHeight;
        return bubble;
    };

    const appendLoading = () => {
        const t = document.getElementById('ai-loading-template');
        if (!t) return null;
        const bubble = t.cloneNode(true);
        bubble.id = '';
        bubble.classList.remove('d-none');
        chat.appendChild(bubble);
        chat.scrollTop = chat.scrollHeight;
        return bubble;
    };

    const setUiBusy = (busy) => {
        if (sendBtn) sendBtn.disabled = busy;
        sendBtn?.classList.toggle('d-none', busy);
        cancelBtn?.classList.toggle('d-none', !busy);
        if (input) input.disabled = busy;
        suggestionButtons?.forEach(b => b.disabled = busy);
        if (tableFilterInput) tableFilterInput.disabled = busy;
    };

    const escapeText = (v) => {
        if (v === null || v === undefined) return 'NULL';
        return String(v);
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

    const renderShortText = (container, fullText) => {
        const t = (fullText || '').trim();
        container.textContent = t;
    };

    const updateProgressBubble = (loadingBubble, progress) => {
        if (!loadingBubble || !progress) return;
        const message = loadingBubble.querySelector('.ai-progress__message');
        const percent = loadingBubble.querySelector('.ai-progress__percent');
        const bar = loadingBubble.querySelector('.ai-progress__bar');
        const events = loadingBubble.querySelector('.ai-progress__events');
        const boundedPercent = Math.max(0, Math.min(100, Number(progress.percent || 0)));

        if (message) message.textContent = progress.message || strings.busyText || 'Arbetar...';
        if (percent) percent.textContent = `${boundedPercent}%`;
        if (bar) bar.style.width = `${boundedPercent}%`;

        if (events && progress.step) {
            const previous = events.querySelector(`[data-progress-step="${CSS.escape(progress.step)}"]`);
            const item = previous || document.createElement('li');
            item.dataset.progressStep = progress.step;
            item.textContent = progress.message || progress.step;
            if (!previous) events.appendChild(item);
            Array.from(events.children).forEach(eventItem => {
                eventItem.classList.toggle('is-current', eventItem === item);
                if (eventItem !== item) eventItem.classList.add('is-complete');
            });
        }

        setStatus('busy', progress.message || strings.busyText || 'Arbetar...');
        chat.scrollTop = chat.scrollHeight;
    };

    const filterTable = (searchTerm) => {
        const rows = document.getElementById('results-tab-table')?.querySelectorAll('tbody tr') || [];
        const search = (searchTerm || '').toLowerCase().trim();

        rows.forEach(row => {
            let matches = false;
            row.querySelectorAll('td').forEach(cell => {
                if (cell.textContent.toLowerCase().includes(search)) {
                    matches = true;
                }
            });

            if (matches || search === '') row.classList.remove('filter-hidden');
            else row.classList.add('filter-hidden');
        });
    };

    const renderVisualization = (columns, rows, preferredVisualization = null) => {
        chartView?.render(columns, rows, preferredVisualization);
    };

    const renderTableInResultsPanel = (columns, rows, truncated, preferredVisualization = null) => {
        if (!resultsBody || !resultsMeta) return;
        resultsBody.innerHTML = '';
        if (tableFilterInput) {
            tableFilterInput.value = '';
            tableFilterInput.disabled = false;
        }

        if (!columns || columns.length === 0) {
            resultsBody.innerHTML = `
                <div class="ai-empty-state text-muted small text-center">
                    <i class="fa fa-info-circle fa-2x mb-2 d-block"></i>
                    ${strings.noColumns || 'Resultatet innehåller inga kolumner.'}
                </div>`;
            resultsMeta.textContent = strings.noTableMeta || 'Ingen tabell.';
            colMenuBody.innerHTML = `<div class="text-muted">${strings.noColumnsMenu || 'Ingen kolumn att visa.'}</div>`;
            return;
        }

        const wrapper = document.createElement('div');
        wrapper.className = 'table-responsive';

        const table = document.createElement('table');
        table.className = 'table table-sm mb-0';

        const thead = document.createElement('thead');
        const hr = document.createElement('tr');
        columns.forEach((c, idx) => {
            const th = document.createElement('th');
            th.textContent = escapeText(c);
            th.dataset.colIndex = String(idx);
            hr.appendChild(th);
        });
        thead.appendChild(hr);
        table.appendChild(thead);

        const tbody = document.createElement('tbody');
        (rows || []).forEach(r => {
            const tr = document.createElement('tr');
            for (let i = 0; i < columns.length; i++) {
                const td = document.createElement('td');
                td.textContent = escapeText(r?.[i]);
                td.dataset.colIndex = String(i);
                tr.appendChild(td);
            }
            tbody.appendChild(tr);
        });
        table.appendChild(tbody);

        wrapper.appendChild(table);
        resultsBody.appendChild(wrapper);

        if (truncated) {
            const note = document.createElement('div');
            note.className = 'text-muted small p-3 border-top';
            note.textContent = strings.truncatedNote || 'Visar trunkerat resultat (maxrader).';
            resultsBody.appendChild(note);
        }

        const rowCount = rows ? rows.length : 0;
        const now = new Date().toLocaleTimeString();
        resultsMeta.innerHTML = `${strings.rowsLabel || 'Rader'}: ${rowCount}${truncated ? ' (trunkerad)' : ''} | ${strings.updatedLabel || 'Senast uppdaterad'}: ${now}`;

        buildColumnMenu(columns);
        renderVisualization(columns, rows, preferredVisualization);

        resultsRow?.scrollIntoView({ behavior: 'smooth', block: 'start' });
    };

    const buildColumnMenu = (columns) => {
        if (!colMenuBody) return;
        colMenuBody.innerHTML = '';
        columns.forEach((name, idx) => {
            const label = document.createElement('label');
            const checkbox = document.createElement('input');
            checkbox.type = 'checkbox';
            checkbox.checked = true;
            checkbox.dataset.colToggle = String(idx);
            label.appendChild(checkbox);
            label.appendChild(document.createTextNode(` ${escapeText(name)}`));
            colMenuBody.appendChild(label);
        });

        colMenuBody.querySelectorAll('input[type="checkbox"][data-col-toggle]').forEach(cb => {
            cb.addEventListener('change', () => {
                const colIdx = cb.getAttribute('data-col-toggle');
                const show = cb.checked;
                document.getElementById('results-tab-table').querySelectorAll(`[data-col-index="${colIdx}"]`).forEach(cell => {
                    cell.style.display = show ? '' : 'none';
                });
            });
        });
    };

    const enableFeedback = (bubble, responseId) => {
        const feedback = bubble?.querySelector('.bubble-feedback');
        if (!feedback || !responseId) return;

        feedback.classList.remove('d-none');
        const buttons = feedback.querySelectorAll('[data-ai-feedback]');
        const negativeButton = feedback.querySelector('[data-ai-feedback="not_helpful"]');
        const detail = feedback.querySelector('.bubble-feedback__detail');
        const comment = detail?.querySelector('input');
        const submitNegative = detail?.querySelector('[data-ai-feedback-submit]');
        const confirmation = feedback.querySelector('.bubble-feedback__confirmation');

        const submit = async (rating, feedbackComment = null) => {
            buttons.forEach(button => { button.disabled = true; });
            if (submitNegative) submitNegative.disabled = true;
            if (confirmation) confirmation.textContent = '';

            try {
                const result = await queryClient.submitFeedback(responseId, rating, feedbackComment);
                if (!result?.success) throw new Error(result?.message || strings.feedbackError);
                detail?.classList.add('d-none');
                buttons.forEach(button => button.classList.add('d-none'));
                if (confirmation) confirmation.textContent = result.message || strings.feedbackThanks || 'Tack för feedbacken.';
            } catch {
                buttons.forEach(button => { button.disabled = false; });
                if (submitNegative) submitNegative.disabled = false;
                if (confirmation) confirmation.textContent = strings.feedbackError || 'Feedbacken kunde inte sparas.';
            }
        };

        feedback.querySelector('[data-ai-feedback="helpful"]')?.addEventListener('click', () => {
            submit('helpful');
        });
        negativeButton?.addEventListener('click', () => {
            detail?.classList.remove('d-none');
            comment?.focus();
        });
        submitNegative?.addEventListener('click', () => {
            submit('not_helpful', comment?.value?.trim() || null);
        });
        comment?.addEventListener('keydown', event => {
            if (event.key !== 'Enter') return;
            event.preventDefault();
            submitNegative?.click();
        });
    };

    const renderBubbleMetaAndNext = (bubble, resp, originalQuestion) => {
        if (!bubble) return;
        const badges = bubble.querySelector('.bubble-badges');
        const sqlWrap = bubble.querySelector('.bubble-sqlwrap');
        const sqlPre = bubble.querySelector('.bubble-sql');
        const nextWrap = bubble.querySelector('.bubble-next');

        if (badges) badges.innerHTML = '';
        if (nextWrap) nextWrap.innerHTML = '';

        const rowCount = resp?.rowCount ?? (resp?.rows ? resp.rows.length : null);
        const truncated = !!resp?.truncated;

        if (badges) {
            if (rowCount !== null && rowCount !== undefined) {
                const b = document.createElement('span');
                b.className = 'ai-badge';
                b.textContent = `${strings.rowsLabel || 'Rader'}: ${rowCount}${truncated ? ' (trunkerad)' : ''}`;
                badges.appendChild(b);
            }

            if (resp?.warning) {
                const b = document.createElement('span');
                b.className = 'ai-badge warn';
                b.textContent = `⚠ ${resp.warning}`;
                badges.appendChild(b);
            }

            if (resp?.columns?.length) {
                const b = document.createElement('span');
                b.className = 'ai-badge';
                b.textContent = `${strings.columnsLabel || 'Kolumner'}: ${resp.columns.length}`;
                badges.appendChild(b);
            }

            if (resp?.evidence?.verificationStatus) {
                const verified = resp.evidence.verificationStatus === 'verified';
                const b = document.createElement('span');
                b.className = verified ? 'ai-badge verified' : 'ai-badge warn';
                b.textContent = verified ? '✓ Verifierat mot resultatet' : '⚠ Kontrollera textsammanfattningen';
                badges.appendChild(b);
            }

            if (resp?.evidence?.metricLabel) {
                const b = document.createElement('span');
                b.className = 'ai-badge';
                b.textContent = `Mått: ${resp.evidence.metricLabel}`;
                badges.appendChild(b);
            }

            if (resp?.evidence?.period) {
                const b = document.createElement('span');
                b.className = 'ai-badge';
                b.textContent = `Period: ${resp.evidence.period}`;
                badges.appendChild(b);
            }

            if (resp?.evidence?.dataSource) {
                const b = document.createElement('span');
                b.className = 'ai-badge';
                b.textContent = `Källa: ${resp.evidence.dataSource}`;
                badges.appendChild(b);
            }

            if (resp?.totalTokens !== null && resp?.totalTokens !== undefined) {
                const b = document.createElement('span');
                b.className = 'ai-badge';
                b.textContent = `Tokens: ${resp.totalTokens}`;
                badges.appendChild(b);
            }

            const totalCostText = formatSek(resp?.totalCostSek);
            if (totalCostText) {
                const b = document.createElement('span');
                b.className = 'ai-badge';
                b.textContent = `Kostnad: ${totalCostText}`;
                badges.appendChild(b);
            }

            if (resp?.quotaMessage && (resp?.quotaStatus === 'warning' || resp?.quotaStatus === 'paid')) {
                const b = document.createElement('span');
                b.className = resp?.quotaStatus === 'warning' ? 'ai-badge warn' : 'ai-badge';
                b.textContent = resp.quotaMessage;
                badges.appendChild(b);
            }
        }

        if (resp?.sql) {
            sqlWrap?.classList.remove('d-none');
            if (sqlPre) sqlPre.textContent = resp.sql;
            const answerText = String(resp?.answer || '').toLowerCase();
            const looksLikeError =
                answerText.includes('stoppades/failed') ||
                answerText.includes('sql-körningen') ||
                answerText.includes('kunde inte') ||
                answerText.includes('fel');
            if (sqlWrap && looksLikeError) sqlWrap.open = true;
        } else {
            sqlWrap?.classList.add('d-none');
            if (sqlPre) sqlPre.textContent = '';
            if (sqlWrap) sqlWrap.open = false;
        }

        if (nextWrap) {
            const mkBtn = (label, handler, iconClass, isPrimary = false) => {
                const btn = document.createElement('button');
                btn.type = 'button';
                btn.className = `btn btn-sm ${isPrimary ? 'btn-portal' : 'btn-portal btn-portal-outline'}`;
                btn.innerHTML = `<i class="${iconClass} me-1"></i> ${label}`;
                btn.addEventListener('click', handler);
                return btn;
            };

            if (truncated) {
                nextWrap.appendChild(mkBtn(strings.showMoreRows || 'Visa fler rader', () => {
                    input.value = originalQuestion ? `${originalQuestion} (visa fler rader)` : 'Visa fler rader';
                    input.focus();
                    autoGrow(input);
                }, 'fa fa-arrow-down', true));
            }

            if (resp?.columns?.length) {
                 nextWrap.appendChild(mkBtn(strings.gotoTable || 'Gå till tabell', () => {
                    resultsRow?.scrollIntoView({ behavior: 'smooth', block: 'start' });
                 }, 'fa fa-table'));
            }

            (resp?.suggestions || []).slice(0, 3).forEach(suggestion => {
                const button = document.createElement('button');
                button.type = 'button';
                button.className = 'btn btn-sm btn-portal btn-portal-outline ai-followup-button';
                const icon = document.createElement('i');
                icon.className = 'fa fa-arrow-turn-up me-1';
                icon.setAttribute('aria-hidden', 'true');
                button.appendChild(icon);
                button.appendChild(document.createTextNode(String(suggestion)));
                button.addEventListener('click', () => sendMessage(String(suggestion)));
                nextWrap.appendChild(button);
            });
        }

        if (resp?.success !== false) enableFeedback(bubble, resp?.responseId);
    };

    const renderEmptyTableMessage = (answerPresent) => {
        if (!resultsBody || !resultsMeta) return;
        const now = new Date().toLocaleTimeString();
        resultsBody.innerHTML = `<div class="ai-empty-state text-muted small text-center">
            <i class="fa fa-database fa-2x mb-2 d-block"></i>
            ${answerPresent ? (strings.noTableFromAnswer || 'Frågan gav ingen tabell.') : (strings.noDataReturned || 'Ingen data returnerades.')}
        </div>`;
        resultsMeta.innerHTML = `${strings.noTableMeta || 'Ingen tabell.'} | ${strings.updatedLabel || 'Senast uppdaterad'}: ${now}`;

        chartView?.clear(strings.vizDefault || 'Visualiseringar visas här när data finns.');
    };

    const sendMessage = async (message) => {
        const text = (message || '').trim();
        if (!text) return;

        appendBubble('user', text);
        if (input) {
            input.value = '';
            autoGrow(input);
        }

        if (suggestionWrap) suggestionWrap.style.display = 'none';

        const loadingBubble = appendLoading();

        setUiBusy(true);
        setStatus('busy', strings.busyText || 'Arbetar...');

        try {
            const resp = await callBackend(text, progress => updateProgressBubble(loadingBubble, progress));
            updateQuotaUi(resp);
            await refreshQuotaUi();

            loadingBubble?.remove();

            if (resp?.success === false) {
                if (resp?.error?.code === 'clarification_required') {
                    const clarificationBubble = appendBubble('ai', resp.error.message || resp.answer || '');
                    renderBubbleMetaAndNext(clarificationBubble, resp, text);
                    setStatus('ready', strings.readyText || 'Redo');
                    return;
                }

                appendErrorBubble(
                    resp?.error || {
                        title: 'Frågan kunde inte slutföras',
                        message: resp?.answer || strings.errorBubble,
                        canRetry: true,
                        tone: 'danger'
                    },
                    text,
                    resp?.suggestions || []);
                setStatus('err', resp?.error?.title || strings.errorStatus || 'Fel!');
                return;
            }

            const aiBubble = appendBubble('ai', resp?.answer || '');
            renderBubbleMetaAndNext(aiBubble, resp, text);

            if (resp?.columns && resp.columns.length && resp?.rows) {
                renderTableInResultsPanel(
                    resp.columns,
                    resp.rows,
                    resp.truncated,
                    resp?.plan?.resultContract?.preferredVisualization);
            } else {
                renderEmptyTableMessage(!!resp?.answer);
            }

            document.getElementById('tab-table')?.click();
            setStatus('ready', strings.readyText || 'Redo');

        } catch (e) {
            loadingBubble?.remove();
            if (e?.code === 'cancelled') {
                setStatus('ready', strings.readyText || 'Redo');
                return;
            }
            appendErrorBubble({
                title: e?.code === 'timeout' ? 'Analysen tog för lång tid' : 'Anslutningen misslyckades',
                message: e?.message || strings.errorBubble || 'Ett oväntat fel uppstod.',
                canRetry: e?.canRetry !== false,
                tone: e?.code === 'offline' ? 'warning' : 'danger'
            }, text);
            setStatus('err', strings.errorStatus || 'Fel!');
        } finally {
            setUiBusy(false);
            if (input && input.value.trim() === '' && suggestionWrap) suggestionWrap.style.display = 'flex';
        }
    };

    const attachEvents = () => {
        input?.addEventListener('input', () => {
            autoGrow(input);
            if (suggestionWrap) suggestionWrap.style.display = input.value.trim() ? 'none' : 'flex';
        });

        input?.addEventListener('keydown', (e) => {
            if (e.key === 'Enter' && !e.shiftKey) {
                e.preventDefault();
                form?.dispatchEvent(new Event('submit'));
            }
        });
        if (input) autoGrow(input);

        form?.addEventListener('submit', (e) => {
            e.preventDefault();
            sendMessage(input?.value);
        });

        suggestionButtons?.forEach(btn => {
            btn.addEventListener('click', () => sendMessage(btn.dataset.text));
        });

        const setMode = (mode) => {
            if (mode === 'manual') {
                assistedPanel?.classList.add('d-none');
                manualPanel?.classList.remove('d-none');
                if (tableFilterInput) tableFilterInput.disabled = false;
                if (modeDescription) modeDescription.textContent = strings.manualMode || 'Skriv egna SELECT-frågor.';
            } else {
                assistedPanel?.classList.remove('d-none');
                manualPanel?.classList.add('d-none');
                const hasData = resultsBody?.querySelector('table') !== null;
                if (tableFilterInput) tableFilterInput.disabled = !hasData;
                if (modeDescription) modeDescription.textContent = strings.assistedMode || 'Ställ naturliga språkfrågor.';
            }

            modeButtons?.forEach(btn => {
                btn.classList.remove('btn-outline-secondary', 'active-mode');
                if (btn.dataset.aiMode === mode) btn.classList.add('active-mode');
                else btn.classList.add('btn-outline-secondary');
            });
            localStorage.setItem('zeeuintelligence-mode', mode);
        };

        const storedMode = (!modeToggle || !manualPanel)
            ? 'assisted'
            : (localStorage.getItem('zeeuintelligence-mode') || modeToggle?.dataset.selectedMode || 'assisted');
        setMode(storedMode);

        modeButtons?.forEach(btn => btn.addEventListener('click', () => setMode(btn.dataset.aiMode)));

        quotaAllowBtn?.addEventListener('click', async () => {
            try {
                const decision = await setQuotaDecision('allow_paid');
                if (!decision?.success) {
                    appendErrorBubble(decision?.message || 'Kunde inte spara quota-val.');
                    return;
                }
                updateQuotaUi(decision);
                await refreshQuotaUi();
            } catch (e) {
                appendErrorBubble((e && e.message) ? e.message : 'Kunde inte spara quota-val.');
            }
        });

        quotaBlockBtn?.addEventListener('click', async () => {
            try {
                const decision = await setQuotaDecision('block_until_reset');
                if (!decision?.success) {
                    appendErrorBubble(decision?.message || 'Kunde inte spara quota-val.');
                    return;
                }
                updateQuotaUi(decision);
                await refreshQuotaUi();
            } catch (e) {
                appendErrorBubble((e && e.message) ? e.message : 'Kunde inte spara quota-val.');
            }
        });

        quotaTrigger?.addEventListener('click', (e) => {
            e.preventDefault();
            e.stopPropagation();
            quotaPinnedOpen = !quotaPinnedOpen;
            quotaPop?.classList.toggle('d-none', !quotaPinnedOpen);
        });

        document.addEventListener('click', (e) => {
            if (!quotaPop || !quotaTrigger || !quotaWidget) return;
            if (quotaPop.classList.contains('d-none')) return;
            if (quotaWidget.contains(e.target)) return;
            quotaPinnedOpen = false;
            quotaPop.classList.add('d-none');
        });

        if (window.matchMedia && window.matchMedia('(hover: hover)').matches) {
            quotaWidget?.addEventListener('mouseenter', () => {
                if (!quotaPinnedOpen) quotaPop?.classList.remove('d-none');
            });
            quotaWidget?.addEventListener('mouseleave', () => {
                if (!quotaPinnedOpen) quotaPop?.classList.add('d-none');
            });
        }

        manualForm?.addEventListener('submit', async (e) => {
            e.preventDefault();
            const sql = manualSql?.value?.trim();
            if (!sql) return;

            manualRun.disabled = true;
            setInlineStatusTone(manualStatus, 'info');
            manualStatus.textContent = strings.runningSql || 'Kör fråga...';

            try {
                const resp = await runManualQuery(sql);
                if (resp?.success === false) {
                    const manualError = new Error(resp?.error || strings.sqlError || 'Fel vid körning av SQL-frågan.');
                    manualError.code = resp?.errorCode || 'manual_query_failed';
                    throw manualError;
                }
                if (resp?.columns && resp.columns.length && resp?.rows) {
                    renderTableInResultsPanel(resp.columns, resp.rows, resp.truncated);
                    setInlineStatusTone(manualStatus, 'success');
                    manualStatus.textContent = strings.done || 'Klar.';
                    document.getElementById('tab-table')?.click();
                } else {
                    renderEmptyTableMessage(false);
                    setInlineStatusTone(manualStatus, 'warning');
                    manualStatus.textContent = strings.doneNoData || 'Klar (ingen data).';
                    document.getElementById('tab-table')?.click();
                }
            } catch (e) {
                setInlineStatusTone(manualStatus, 'danger');
                manualStatus.textContent = e?.message || strings.sqlError || 'Fel vid körning av SQL-frågan.';
                if (resultsBody) {
                    const now = new Date().toLocaleTimeString();
                    resultsBody.innerHTML = '';
                    const errorState = document.createElement('div');
                    errorState.className = 'ai-empty-state text-danger small text-center';
                    const icon = document.createElement('i');
                    icon.className = 'fa fa-triangle-exclamation fa-2x mb-2 d-block';
                    icon.setAttribute('aria-hidden', 'true');
                    const message = document.createElement('span');
                    message.textContent = `${strings.errorPrefix || 'Ett fel uppstod:'} ${e?.message || strings.sqlError}`;
                    errorState.append(icon, message);
                    resultsBody.appendChild(errorState);
                    resultsMeta.innerHTML = `${strings.errorStatus || 'Fel.'} | ${strings.updatedLabel || 'Senast uppdaterad'}: ${now}`;
                }
                document.getElementById('tab-table')?.click();
            } finally {
                manualRun.disabled = false;
            }
        });

        if (tableFilterInput) {
            tableFilterInput.addEventListener('input', (e) => filterTable(e.target.value));
            if (storedMode === 'assisted') tableFilterInput.disabled = true;
        }

        document.addEventListener('click', (e) => {
            const colMenu = document.querySelector('.ai-colmenu');
            if (!colMenu) return;
            if (colMenu.hasAttribute('open') && !colMenu.contains(e.target)) {
                colMenu.removeAttribute('open');
            }
        });
        colMenuBody?.addEventListener('click', (e) => e.stopPropagation());
    };

    const cacheDom = () => {
        chat = document.getElementById('ai-chat');
        form = document.getElementById('ai-form');
        input = document.getElementById('ai-input');
        sendBtn = document.getElementById('ai-send');
        cancelBtn = document.getElementById('ai-cancel');
        errorTemplate = document.getElementById('ai-error-template');
        suggestionButtons = document.querySelectorAll('.ai-suggestion-chip');
        suggestionWrap = document.querySelector('.ai-suggestion-wrap');
        modeToggle = document.querySelector('.ai-mode-toggle');
        modeButtons = document.querySelectorAll('.ai-mode-btn');
        assistedPanel = document.getElementById('ai-assisted-panel');
        manualPanel = document.getElementById('ai-manual-panel');
        modeDescription = document.getElementById('mode-description');
        manualForm = document.getElementById('manual-form');
        manualSql = document.getElementById('manual-sql');
        manualRun = document.getElementById('manual-run');
        manualStatus = document.getElementById('manual-status');
        statusDot = document.querySelector('#ai-status .ai-dot');
        statusText = document.getElementById('ai-status-text');
        resultsRow = document.getElementById('ai-results-row');
        resultsBody = document.getElementById('ai-results-body');
        resultsMeta = document.getElementById('ai-results-meta');
        colMenuBody = document.getElementById('ai-colmenu-body');
        tableFilterInput = document.getElementById('ai-table-filter');
        vizPlaceholder = document.getElementById('viz-placeholder');
        chartCanvas = document.getElementById('ai-chart');
        chartTypeSelect = document.getElementById('ai-chart-type');
        chartSummary = document.getElementById('ai-chart-summary');
        quotaWidget = document.getElementById('ai-quota-widget');
        quotaTrigger = document.getElementById('ai-quota-trigger');
        quotaMini = document.getElementById('ai-quota-mini');
        quotaPop = document.getElementById('ai-quota-pop');
        quotaPopText = document.getElementById('ai-quota-pop-text');
        quotaPopBar = document.getElementById('ai-quota-pop-bar');
        quotaInline = document.getElementById('ai-quota-inline');
        quotaInlineText = document.getElementById('ai-quota-inline-text');
        quotaInlineActions = document.getElementById('ai-quota-inline-actions');
        quotaAllowBtn = document.getElementById('ai-quota-allow-btn');
        quotaBlockBtn = document.getElementById('ai-quota-block-btn');
        quotaPaidPill = document.getElementById('ai-quota-paid-pill');
    };

    const init = async (config) => {
        cfg = config || window.ZeeUAI_CONFIG || {};
        strings = cfg.strings || {};
        queryClient = window.ZeeUAIQueryClient;
        if (!queryClient) throw new Error('ZeeUAIQueryClient is required.');
        cacheDom();
        chartView = window.ZeeUAIChart?.create({
            canvas: chartCanvas,
            placeholder: vizPlaceholder,
            typeSelect: chartTypeSelect,
            summary: chartSummary,
            strings
        }) || null;
        attachEvents();
        cancelBtn?.addEventListener('click', () => queryClient.cancelActiveQuery());
        try {
            const quota = await getQuotaStatus();
            if (quota?.success) updateQuotaUi(quota);
        } catch (_) {
            // Ignore: quota panel stays hidden if status endpoint is unavailable.
        }
    };

    return { init };
})();
