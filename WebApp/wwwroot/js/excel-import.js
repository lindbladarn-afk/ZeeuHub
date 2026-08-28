// Keeps Excel Import page interactions aligned with the portal's validation and edit-session flow.
(() => {
    const copyText = async (text) => {
        if (navigator.clipboard?.writeText) {
            await navigator.clipboard.writeText(text);
            return true;
        }

        const helper = document.createElement('textarea');
        helper.value = text;
        helper.setAttribute('readonly', 'readonly');
        helper.style.position = 'absolute';
        helper.style.left = '-9999px';
        document.body.appendChild(helper);
        helper.select();
        const succeeded = document.execCommand('copy');
        document.body.removeChild(helper);
        return succeeded;
    };

    const initExcelDatepickers = (scope = document) => {
        if (!window.jQuery || !window.jQuery.fn || typeof window.jQuery.fn.datepicker !== 'function') {
            return;
        }

        window.jQuery(scope)
            .find('input[data-fe-datepicker="true"]')
            .each(function () {
                const input = this;
                const $input = window.jQuery(input);

                if ($input.hasClass('hasDatepicker')) {
                    return;
                }

                const currentValue = input.value;
                input.setAttribute('type', 'text');
                input.setAttribute('autocomplete', 'off');
                input.setAttribute('placeholder', 'YYYY-MM-DD');

                $input.datepicker({
                    dateFormat: 'yy-mm-dd',
                    showOtherMonths: true,
                    selectOtherMonths: true,
                    showButtonPanel: true,
                    beforeShow: function () {
                        window.setTimeout(() => {
                            window.jQuery('#ui-datepicker-div').addClass('flowengine-datepicker');
                        }, 0);
                    },
                    onSelect: function () {
                        input.dispatchEvent(new Event('change', { bubbles: true }));
                    }
                });

                if (currentValue) {
                    $input.datepicker('setDate', currentValue);
                }
            });
    };

    initExcelDatepickers();

    document.addEventListener('click', async (event) => {
        const button = event.target instanceof Element
            ? event.target.closest('[data-excel-copy-target]')
            : null;

        if (!button) {
            return;
        }

        const targetId = button.getAttribute('data-excel-copy-target');
        const target = targetId ? document.getElementById(targetId) : null;
        const value = target?.textContent?.trim() || '';
        if (!value) {
            return;
        }

        event.preventDefault();
        event.stopPropagation();

        const defaultLabel = button.getAttribute('data-excel-copy-default-label') || 'Kopiera';
        const successLabel = button.getAttribute('data-excel-copy-success-label') || 'Kopierad';
        const iconDefault = '<i class="fas fa-copy" aria-hidden="true"></i>';
        const iconSuccess = '<i class="fas fa-check" aria-hidden="true"></i>';

        try {
            await copyText(value);
            button.setAttribute('aria-label', successLabel);
            button.title = successLabel;
            button.innerHTML = iconSuccess;
            window.setTimeout(() => {
                if (!button.isConnected) {
                    return;
                }
                button.setAttribute('aria-label', defaultLabel);
                button.title = defaultLabel;
                button.innerHTML = iconDefault;
            }, 1500);
        } catch {
            button.setAttribute('aria-label', defaultLabel);
            button.title = defaultLabel;
            button.innerHTML = iconDefault;
        }
    }, true);

    const form = document.getElementById('excel-upload-form');
    const dropZone = document.getElementById('excel-drop-zone');
    const fileInput = document.getElementById('excel-file-input');
    const fileButton = document.getElementById('excel-file-button');
    const fileInfo = document.getElementById('excel-file-info');
    const uploadBtn = document.getElementById('excel-upload-btn');
    const clearBtn = document.getElementById('excel-clear-btn');
    const confirmType = document.getElementById('excel-confirm-type');
    const importTypeSelect = document.getElementById('excel-import-type');
    const voucherPostingDate = document.getElementById('voucher-posting-date');
    const voucherPostingDateGroup = document.getElementById('voucher-posting-date-group');
    const voucherPostingDateManual = document.getElementById('voucher-posting-date-manual');
    const voucherReversalDate = document.getElementById('voucher-reversal-date');
    const voucherReversalDateGroup = document.getElementById('voucher-reversal-date-group');
    const voucherReversalDateManual = document.getElementById('voucher-reversal-date-manual');
    const manualTools = document.getElementById('excel-manual-tools');
    const manualImportType = document.getElementById('excel-manual-import-type');
    const runtimeStatusSlot = document.getElementById('excel-runtime-status-slot');
    const importLoader = document.getElementById('excelImportLoader');
    const pageParams = new URLSearchParams(window.location.search);
    const scrollTargetId = pageParams.get('scrollTarget') || '';
    const focusRuntimeKey = pageParams.get('focusRuntimeKey') || '';
    let runtimeDurationIntervalId = null;

    const findRuntimeDetailsByKey = (runtimeKey) => {
        if (!runtimeStatusSlot || !runtimeKey) {
            return null;
        }

        return Array.from(runtimeStatusSlot.querySelectorAll('details[data-runtime-key]'))
            .find((item) => item.dataset.runtimeKey === runtimeKey) || null;
    };

    const initRuntimeStatusPolling = () => {
        if (!runtimeStatusSlot) {
            return;
        }

        const pollUrl = runtimeStatusSlot.dataset.runtimeStatusUrl;
        const pollMs = Number.parseInt(runtimeStatusSlot.dataset.runtimeStatusPollMs || '3000', 10) || 3000;
        let intervalId = null;
        let inFlight = false;

        const hasActiveItems = () => !!runtimeStatusSlot.querySelector('[data-runtime-active="true"]');
        const hasRunningDurations = () => !!runtimeStatusSlot.querySelector('details[data-runtime-status="Running"] [data-runtime-duration="true"]');

        const formatDuration = (elapsedMs) => {
            const totalSeconds = Math.max(0, Math.floor(elapsedMs / 1000));
            const hours = Math.floor(totalSeconds / 3600);
            const minutes = Math.floor((totalSeconds % 3600) / 60);
            const seconds = totalSeconds % 60;
            const milliseconds = Math.max(0, Math.floor(elapsedMs % 1000));
            const pad2 = (value) => String(value).padStart(2, '0');
            const pad3 = (value) => String(value).padStart(3, '0');
            return `${pad2(hours)}:${pad2(minutes)}:${pad2(seconds)}.${pad3(milliseconds)}`;
        };

        const updateRuntimeDurations = () => {
            const now = Date.now();
            runtimeStatusSlot.querySelectorAll('[data-runtime-duration="true"][data-runtime-started-at]').forEach((element) => {
                const status = element.dataset.runtimeStatus || '';
                if (status !== 'Running') {
                    return;
                }

                const startedAtMs = Number.parseInt(element.dataset.runtimeStartedAt || '', 10);
                if (!Number.isFinite(startedAtMs) || startedAtMs <= 0) {
                    return;
                }

                const prefix = (element.dataset.runtimeDurationPrefix || 'Körtid:').trim();
                const updatedText = `${prefix} ${formatDuration(now - startedAtMs)}`;
                if ((element.textContent || '') !== updatedText) {
                    element.textContent = updatedText;
                }
            });
        };

        const ensureDurationTicker = () => {
            if (hasRunningDurations()) {
                if (runtimeDurationIntervalId === null) {
                    runtimeDurationIntervalId = window.setInterval(updateRuntimeDurations, 100);
                }
                updateRuntimeDurations();
                return;
            }

            if (runtimeDurationIntervalId !== null) {
                window.clearInterval(runtimeDurationIntervalId);
                runtimeDurationIntervalId = null;
            }
        };

        const initRuntimeRowBrowsers = () => {
            runtimeStatusSlot.querySelectorAll('[data-runtime-row-pager="true"]').forEach((pager) => {
                const details = pager.closest('details[data-runtime-key]');
                const table = details?.querySelector('[data-runtime-row-table="true"]');
                if (!details || !table) {
                    return;
                }

                const rows = Array.from(table.querySelectorAll('tbody tr[data-runtime-row="true"]'));
                const filterInvalid = details.querySelector('[data-runtime-filter-invalid="true"]');
                const pageInfo = details.querySelector('[data-runtime-row-page-info]');
                const pageIndicator = details.querySelector('[data-runtime-row-page-indicator]');
                const prevButton = details.querySelector('[data-runtime-row-prev="true"]');
                const nextButton = details.querySelector('[data-runtime-row-next="true"]');
                const pageSize = Number.parseInt(pager.dataset.runtimePageSize || '50', 10) || 50;
                let page = Number.parseInt(pager.dataset.runtimePage || '1', 10) || 1;
                let showOnlyInvalid = filterInvalid?.checked || false;

                const apply = () => {
                    const filteredRows = rows.filter((row) => {
                        if (!showOnlyInvalid) {
                            return true;
                        }

                        return row.dataset.runtimeRowValid !== 'true';
                    });

                    const totalPages = Math.max(1, Math.ceil(filteredRows.length / pageSize));
                    page = Math.min(Math.max(page, 1), totalPages);
                    const start = (page - 1) * pageSize;
                    const end = Math.min(start + pageSize, filteredRows.length);

                    rows.forEach((row) => {
                        row.hidden = true;
                    });
                    filteredRows.slice(start, end).forEach((row) => {
                        row.hidden = false;
                    });

                    if (pageInfo) {
                        pageInfo.textContent = filteredRows.length > 0
                            ? `Visar ${filteredRows.length === 0 ? 0 : start + 1}-${end} av ${filteredRows.length} rader`
                            : 'Inga rader att visa';
                    }

                    if (pageIndicator) {
                        pageIndicator.textContent = `Sida ${page} av ${totalPages}`;
                    }

                    if (prevButton) {
                        prevButton.disabled = page <= 1;
                    }

                    if (nextButton) {
                        nextButton.disabled = page >= totalPages;
                    }
                };

                if (filterInvalid) {
                    filterInvalid.addEventListener('change', () => {
                        showOnlyInvalid = filterInvalid.checked;
                        page = 1;
                        apply();
                    });
                }

                if (prevButton) {
                    prevButton.addEventListener('click', () => {
                        page = Math.max(1, page - 1);
                        apply();
                    });
                }

                if (nextButton) {
                    nextButton.addEventListener('click', () => {
                        const filteredRows = rows.filter((row) => !showOnlyInvalid || row.dataset.runtimeRowValid !== 'true');
                        const totalPages = Math.max(1, Math.ceil(filteredRows.length / pageSize));
                        page = Math.min(totalPages, page + 1);
                        apply();
                    });
                }

                apply();
            });
        };

        const runtimeRowsLoadingSelector = '[data-runtime-rows-loading="true"]';

        const setRuntimeRowsLoading = (container, isLoading, text = 'Laddar rader...') => {
            if (!container) {
                return;
            }

            const currentIndicator = container.querySelector(runtimeRowsLoadingSelector);
            if (!isLoading) {
                currentIndicator?.remove();
                return;
            }

            const indicator = currentIndicator || document.createElement('div');
            indicator.className = 'small text-muted mb-2';
            indicator.dataset.runtimeRowsLoading = 'true';
            indicator.setAttribute('role', 'status');
            indicator.setAttribute('aria-live', 'polite');
            indicator.textContent = text;

            if (!currentIndicator) {
                container.prepend(indicator);
            }
        };

        const showRuntimeRowsError = (container, hadContent) => {
            const message = '<div class="small text-danger" data-runtime-rows-error="true">Raderna kunde inte laddas.</div>';
            if (hadContent) {
                container.querySelector('[data-runtime-rows-error="true"]')?.remove();
                container.insertAdjacentHTML('afterbegin', message);
                return;
            }

            container.innerHTML = message;
        };

        const getRuntimeRowsLoadingText = (trigger) => {
            if (!trigger) {
                return 'Laddar rader...';
            }

            if (trigger.matches('[data-runtime-loaded-row-all]')) {
                return 'Laddar alla rader...';
            }

            if (trigger.matches('[data-runtime-loaded-row-prev], [data-runtime-loaded-row-next]')) {
                return 'Laddar sida...';
            }

            if (trigger.matches('[data-runtime-loaded-row-preview]')) {
                return 'Laddar 50 rader...';
            }

            return 'Laddar rader...';
        };

        const loadRuntimeRows = async (container, page = 1, showOnlyInvalidRows = false, trigger = null, showAllRows = false) => {
            const baseUrl = container?.dataset.runtimeRowsUrl || trigger?.dataset.runtimeRowsUrl || '';
            if (!container || !baseUrl) {
                return;
            }

            const url = new URL(baseUrl, window.location.origin);
            url.searchParams.set('page', String(Math.max(1, page)));
            url.searchParams.set('pageSize', '50');
            if (showAllRows) {
                url.searchParams.set('showAllRows', 'true');
            } else {
                url.searchParams.delete('showAllRows');
            }
            if (showOnlyInvalidRows) {
                url.searchParams.set('showOnlyInvalidRows', 'true');
            } else {
                url.searchParams.delete('showOnlyInvalidRows');
            }

            const loadingText = getRuntimeRowsLoadingText(trigger);
            const hadContent = (container.innerHTML || '').trim().length > 0;
            const originalText = trigger?.textContent;
            if (trigger) {
                trigger.disabled = true;
                trigger.textContent = loadingText;
            }
            container.setAttribute('aria-busy', 'true');
            setRuntimeRowsLoading(container, true, loadingText);

            try {
                const response = await fetch(url.toString(), {
                    method: 'GET',
                    credentials: 'same-origin',
                    headers: { 'X-Requested-With': 'XMLHttpRequest' },
                    cache: 'no-store'
                });

                if (!response.ok) {
                    showRuntimeRowsError(container, hadContent);
                    return;
                }

                container.innerHTML = (await response.text()).trim();
                container.dataset.runtimeCurrentPage = String(Math.max(1, page));
                container.dataset.runtimeShowInvalid = showOnlyInvalidRows ? 'true' : 'false';
                container.dataset.runtimeShowAll = showAllRows ? 'true' : 'false';
            } catch {
                showRuntimeRowsError(container, hadContent);
            } finally {
                setRuntimeRowsLoading(container, false);
                container.setAttribute('aria-busy', 'false');
                if (trigger) {
                    trigger.disabled = false;
                    trigger.textContent = originalText || 'Visa importerade rader';
                }
            }
        };

        runtimeStatusSlot.addEventListener('click', (event) => {
            const trigger = event.target instanceof Element
                ? event.target.closest('[data-runtime-load-rows], [data-runtime-loaded-row-prev], [data-runtime-loaded-row-next], [data-runtime-loaded-row-all], [data-runtime-loaded-row-preview]')
                : null;
            if (!trigger) {
                return;
            }

            event.preventDefault();
            event.stopPropagation();

            if (trigger.matches('[data-runtime-load-rows]')) {
                const targetId = trigger.dataset.runtimeRowsTarget || '';
                const container = targetId ? document.getElementById(targetId) : null;
                loadRuntimeRows(container, 1, false, trigger);
                return;
            }

            const container = trigger.closest('[data-runtime-loaded-rows]');
            const pager = container?.querySelector('[data-runtime-loaded-pager]');
            const currentPage = Number.parseInt(pager?.dataset.runtimePage || container?.dataset.runtimeCurrentPage || '1', 10) || 1;
            const showOnlyInvalidRows = (pager?.dataset.runtimeShowInvalid || container?.dataset.runtimeShowInvalid || 'false') === 'true';
            if (trigger.matches('[data-runtime-loaded-row-all]')) {
                loadRuntimeRows(container, 1, showOnlyInvalidRows, trigger, true);
                return;
            }
            if (trigger.matches('[data-runtime-loaded-row-preview]')) {
                loadRuntimeRows(container, 1, showOnlyInvalidRows, trigger, false);
                return;
            }

            const showAllRows = (pager?.dataset.runtimeShowAll || container?.dataset.runtimeShowAll || 'false') === 'true';
            const nextPage = trigger.matches('[data-runtime-loaded-row-next]')
                ? currentPage + 1
                : Math.max(1, currentPage - 1);
            loadRuntimeRows(container, nextPage, showOnlyInvalidRows, trigger, showAllRows);
        });

        runtimeStatusSlot.addEventListener('change', (event) => {
            const filter = event.target instanceof Element
                ? event.target.closest('[data-runtime-loaded-filter-invalid]')
                : null;
            if (!filter) {
                return;
            }

            const container = filter.closest('[data-runtime-loaded-rows]');
            const pager = container?.querySelector('[data-runtime-loaded-pager]');
            const showAllRows = (pager?.dataset.runtimeShowAll || container?.dataset.runtimeShowAll || 'false') === 'true';
            loadRuntimeRows(container, 1, filter.checked, null, showAllRows);
        });

        const loadOpenEmptyRuntimeRows = () => {
            runtimeStatusSlot.querySelectorAll('details[open][data-runtime-key] [data-runtime-loaded-rows]').forEach((container) => {
                if ((container.innerHTML || '').trim().length > 0) {
                    return;
                }

                loadRuntimeRows(container, 1, false, null, false);
            });
        };

        runtimeStatusSlot.addEventListener('toggle', (event) => {
            const item = event.target instanceof HTMLDetailsElement
                ? event.target
                : null;
            if (!item || !item.open) {
                return;
            }

            const container = item.querySelector('[data-runtime-loaded-rows]');
            if (!container || (container.innerHTML || '').trim().length > 0) {
                return;
            }

            loadRuntimeRows(container, 1, false, null, false);
        }, true);

        const collectOpenKeys = () => Array.from(runtimeStatusSlot.querySelectorAll('details[open][data-runtime-key]'))
            .map((item) => item.dataset.runtimeKey)
            .filter(Boolean);

        const collectLoadedRows = () => Array.from(runtimeStatusSlot.querySelectorAll('details[open][data-runtime-key] [data-runtime-loaded-rows]'))
            .filter((container) => (container.innerHTML || '').trim().length > 0)
            .map((container) => {
                const item = container.closest('details[data-runtime-key]');
                const pager = container.querySelector('[data-runtime-loaded-pager]');
                return {
                    key: item?.dataset.runtimeKey || '',
                    page: Number.parseInt(pager?.dataset.runtimePage || container.dataset.runtimeCurrentPage || '1', 10) || 1,
                    showOnlyInvalidRows: (pager?.dataset.runtimeShowInvalid || container.dataset.runtimeShowInvalid || 'false') === 'true',
                    showAllRows: (pager?.dataset.runtimeShowAll || container.dataset.runtimeShowAll || 'false') === 'true'
                };
            })
            .filter((state) => state.key.length > 0);

        const restoreOpenKeys = (keys) => {
            if (!keys.length) {
                return;
            }

            const keySet = new Set(keys);
            runtimeStatusSlot.querySelectorAll('details[data-runtime-key]').forEach((item) => {
                if (item.dataset.runtimeKey && keySet.has(item.dataset.runtimeKey)) {
                    item.open = true;
                }
            });
        };

        const focusRuntimeItem = (runtimeKey) => {
            if (!runtimeKey) {
                return false;
            }

            const target = findRuntimeDetailsByKey(runtimeKey);
            if (!target) {
                return false;
            }

            target.open = true;
            return true;
        };

        const stopPolling = () => {
            if (intervalId !== null) {
                window.clearInterval(intervalId);
                intervalId = null;
            }
        };

        const stopRuntimeActivity = () => {
            stopPolling();
            if (runtimeDurationIntervalId !== null) {
                window.clearInterval(runtimeDurationIntervalId);
                runtimeDurationIntervalId = null;
            }
        };

        const releaseRuntimeDom = () => {
            runtimeStatusSlot.querySelectorAll('[data-runtime-loaded-rows]').forEach((container) => {
                container.innerHTML = '';
                container.removeAttribute('data-runtime-current-page');
                container.removeAttribute('data-runtime-show-invalid');
                container.removeAttribute('data-runtime-show-all');
                container.setAttribute('aria-busy', 'false');
            });

            if (fileInput) {
                fileInput.value = '';
            }
        };

        const refresh = async () => {
            if (!pollUrl || inFlight) {
                return;
            }

            inFlight = true;
            runtimeStatusSlot.setAttribute('aria-busy', 'true');

            try {
                const response = await fetch(pollUrl, {
                    method: 'GET',
                    credentials: 'same-origin',
                    headers: { 'X-Requested-With': 'XMLHttpRequest' },
                    cache: 'no-store'
                });

                if (response.status === 401 || response.status === 403) {
                    stopPolling();
                    return;
                }

                if (!response.ok) {
                    return;
                }

                const html = (await response.text()).trim();
                const openKeys = collectOpenKeys();
                const loadedRows = collectLoadedRows();
                runtimeStatusSlot.innerHTML = html;
                restoreOpenKeys(openKeys);
                focusRuntimeItem(focusRuntimeKey);
                ensureDurationTicker();
                initRuntimeRowBrowsers();
                loadedRows.forEach((state) => {
                    const item = findRuntimeDetailsByKey(state.key);
                    const container = item?.querySelector('[data-runtime-loaded-rows]');
                    if (container) {
                        loadRuntimeRows(container, state.page, state.showOnlyInvalidRows, null, state.showAllRows);
                    }
                });
                loadOpenEmptyRuntimeRows();

                if (html.length === 0 || !hasActiveItems()) {
                    stopPolling();
                } else if (intervalId === null) {
                    intervalId = window.setInterval(refresh, pollMs);
                }
            } catch {
                // ignore transient refresh failures
            } finally {
                inFlight = false;
                runtimeStatusSlot.setAttribute('aria-busy', 'false');
            }
        };

        const ensurePolling = () => {
            if (hasActiveItems()) {
                if (intervalId === null) {
                    intervalId = window.setInterval(refresh, pollMs);
                }
                ensureDurationTicker();
            } else {
                stopPolling();
                ensureDurationTicker();
            }
        };

        const handleFocus = () => refresh();
        const handleVisibilityChange = () => {
            if (!document.hidden) {
                refresh();
            }
        };
        const handlePageShow = () => {
            refresh();
            ensurePolling();
            loadOpenEmptyRuntimeRows();
        };

        refresh();
        ensurePolling();
        initRuntimeRowBrowsers();
        window.addEventListener('focus', handleFocus);
        window.addEventListener('pageshow', handlePageShow);
        window.addEventListener('pagehide', () => {
            stopRuntimeActivity();
            releaseRuntimeDom();
        });
        window.addEventListener('beforeunload', () => {
            stopRuntimeActivity();
            releaseRuntimeDom();
        });
        document.addEventListener('visibilitychange', handleVisibilityChange);
    };

    if (form && fileInput && fileButton && fileInfo && uploadBtn && clearBtn && confirmType && dropZone) {
        let delayedSubmitTimer = null;
        let isSubmitting = false;

        const hideImportLoader = () => {
            if (!importLoader) {
                return;
            }

            importLoader.classList.remove('is-visible');
            importLoader.setAttribute('aria-hidden', 'true');
            importLoader.hidden = true;
        };

        const showImportLoader = () => {
            if (!importLoader) {
                return;
            }

            importLoader.hidden = false;
            importLoader.setAttribute('aria-hidden', 'false');
            window.requestAnimationFrame(() => {
                importLoader.classList.add('is-visible');
            });
        };

        const getSelectedFiles = () => Array.from(fileInput.files || []);

        const formatFileSize = (file) => {
            const sizeKb = Math.max(Math.round(file.size / 1024), 1);
            return `${file.name} (${sizeKb} KB)`;
        };

        const updateState = (files) => {
            const selectedFiles = Array.from(files || []);
            const hasFile = selectedFiles.length > 0;
            if (hasFile) {
                if (selectedFiles.length === 1) {
                    fileInfo.textContent = formatFileSize(selectedFiles[0]);
                } else {
                    const totalKb = Math.max(Math.round(selectedFiles.reduce((sum, file) => sum + file.size, 0) / 1024), 1);
                    fileInfo.textContent = `${selectedFiles.length} filer valda (${totalKb} KB totalt)`;
                }
                clearBtn.disabled = false;
                dropZone.classList.add('has-file');
            } else {
                fileInfo.textContent = fileInfo.dataset.emptyText || 'Ingen fil vald ännu.';
                clearBtn.disabled = true;
                dropZone.classList.remove('has-file');
            }
            const needsVoucherDate = importTypeSelect && importTypeSelect.value === 'voucher';
            const hasVoucherDate = !needsVoucherDate || (voucherPostingDate && voucherPostingDate.value);
            uploadBtn.disabled = !(hasFile && confirmType.checked && hasVoucherDate);
        };

        const attachFiles = (files) => {
            const selectedFiles = Array.from(files || []);
            if (selectedFiles.length === 0) return;
            const dt = new DataTransfer();
            selectedFiles.forEach((file) => dt.items.add(file));
            fileInput.files = dt.files;
            updateState(fileInput.files);
        };

        fileButton.addEventListener('click', () => fileInput.click());
        fileInput.addEventListener('change', (e) => updateState(e.target.files));
        confirmType.addEventListener('change', () => updateState(getSelectedFiles()));
        if (voucherPostingDate) {
            voucherPostingDate.addEventListener('change', () => {
                if (voucherPostingDateManual) {
                    voucherPostingDateManual.value = voucherPostingDate.value || '';
                }
                updateState(getSelectedFiles());
            });
        }
        if (voucherReversalDate) {
            voucherReversalDate.addEventListener('change', () => {
                if (voucherReversalDateManual) {
                    voucherReversalDateManual.value = voucherReversalDate.value || '';
                }
            });
        }

        clearBtn.addEventListener('click', () => {
            fileInput.value = '';
            updateState([]);
        });

        const handleDrop = (e) => {
            e.preventDefault();
            dropZone.classList.remove('is-dragover');
            if (e.dataTransfer.files.length === 0) return;
            attachFiles(e.dataTransfer.files);
        };

        dropZone.addEventListener('dragover', (e) => {
            e.preventDefault();
            dropZone.classList.add('is-dragover');
        });

        dropZone.addEventListener('dragleave', () => dropZone.classList.remove('is-dragover'));
        dropZone.addEventListener('drop', handleDrop);

        form.addEventListener('submit', (event) => {
            if (isSubmitting) {
                event.preventDefault();
                return;
            }

            event.preventDefault();
            isSubmitting = true;
            sessionStorage.setItem('excelImportScroll', '1');
            showImportLoader();
            if (uploadBtn) {
                uploadBtn.disabled = true;
                uploadBtn.textContent = uploadBtn.dataset.processingText || 'Startar import...';
            }
            if (fileButton) {
                fileButton.disabled = true;
            }
            if (clearBtn) {
                clearBtn.disabled = true;
            }

            if (delayedSubmitTimer !== null) {
                window.clearTimeout(delayedSubmitTimer);
            }

            delayedSubmitTimer = window.setTimeout(() => {
                form.submit();
            }, 1500);
        });
        document.querySelectorAll('.excel-create-edit-form').forEach((editFormEl) => {
            editFormEl.addEventListener('submit', () => {
                sessionStorage.setItem('excelImportScroll', '1');
            });
        });

        if (importTypeSelect && manualTools) {
            const hasResult = document.getElementById('excel-import-result') || document.getElementById('excel-edit-table');
            const toggleManualTools = () => {
                const isVoucher = importTypeSelect.value === 'voucher';
                manualTools.classList.remove('d-none');
                if (manualImportType) {
                    manualImportType.value = importTypeSelect.value || '';
                }
                if (voucherPostingDateGroup) {
                    voucherPostingDateGroup.classList.toggle('d-none', !isVoucher);
                    if (voucherPostingDate) {
                        voucherPostingDate.required = isVoucher;
                    }
                }
                if (voucherReversalDateGroup) {
                    voucherReversalDateGroup.classList.toggle('d-none', !isVoucher);
                }
                if (voucherPostingDateManual && voucherPostingDate) {
                    voucherPostingDateManual.value = voucherPostingDate.value || '';
                }
                if (voucherReversalDateManual && voucherReversalDate) {
                    voucherReversalDateManual.value = voucherReversalDate.value || '';
                }
            };
            const handleImportTypeChange = () => {
                sessionStorage.setItem('excelImportType', importTypeSelect.value);
                toggleManualTools();
                updateState(getSelectedFiles());
                if (!hasResult) {
                    return;
                }

                const url = new URL(window.location.href);
                url.search = '';
                url.searchParams.set('importType', importTypeSelect.value);
                url.hash = '';
                window.location.href = url.toString();
            };
            importTypeSelect.addEventListener('change', handleImportTypeChange);
            if (hasResult) {
                sessionStorage.setItem('excelImportType', importTypeSelect.value);
            } else {
                const storedType = sessionStorage.getItem('excelImportType');
                if (storedType && storedType !== importTypeSelect.value) {
                    importTypeSelect.value = storedType;
                }
            }
            toggleManualTools();
        }

        updateState(getSelectedFiles());
        window.addEventListener('pageshow', () => {
            isSubmitting = false;
            if (delayedSubmitTimer !== null) {
                window.clearTimeout(delayedSubmitTimer);
                delayedSubmitTimer = null;
            }
            hideImportLoader();
        });
    }

    initRuntimeStatusPolling();

    const scrollTarget = scrollTargetId ? document.getElementById(scrollTargetId) : null;
    if (scrollTarget) {
        sessionStorage.removeItem('excelImportScroll');
        window.requestAnimationFrame(() => {
            scrollTarget.scrollIntoView({ behavior: 'smooth', block: 'start' });
            if (focusRuntimeKey && runtimeStatusSlot) {
                const target = findRuntimeDetailsByKey(focusRuntimeKey);
                if (target) {
                    target.open = true;
                }
            }
        });
    } else {
        const result = document.getElementById('excel-import-result') || document.querySelector('[data-excel-import-result="true"]');
        if (result) {
            const shouldScroll = sessionStorage.getItem('excelImportScroll') === '1';
            if (shouldScroll) {
                result.scrollIntoView({ behavior: 'smooth', block: 'start' });
            }
            sessionStorage.removeItem('excelImportScroll');
        }
    }

    const editSessionForm = document.getElementById('excel-edit-session-form');
    const rowsJsonInput = document.getElementById('excel-rows-json');
    const editTable = document.getElementById('excel-edit-table');
    const cancelEditLink = document.querySelector('[data-excel-edit-cancel="true"]');
    const editStateScript = document.getElementById('excel-edit-state');
    const pageInfo = document.getElementById('excel-page-info');
    const pageIndicator = document.getElementById('excel-page-indicator');
    const pagePrev = document.getElementById('excel-page-prev');
    const pageNext = document.getElementById('excel-page-next');
    const filterInvalid = document.getElementById('excel-filter-invalid');

    if (cancelEditLink) {
        cancelEditLink.addEventListener('click', () => {
            sessionStorage.removeItem('excelImportScroll');
        });
    }

    if (editTable && editStateScript) {
        initExcelDatepickers(editSessionForm || document);

        const escapeHtml = (value) => String(value ?? '')
            .replace(/&/g, '&amp;')
            .replace(/</g, '&lt;')
            .replace(/>/g, '&gt;')
            .replace(/"/g, '&quot;')
            .replace(/'/g, '&#39;');

        const rawState = JSON.parse(editStateScript.textContent || '{}');
        const state = {
            rows: Array.isArray(rawState.rows)
                ? rawState.rows.map((row, index) => ({
                    rowNo: Number.parseInt(String(row.rowNo ?? row.RowNo ?? index + 1), 10) || index + 1,
                    isValid: Boolean(row.isValid ?? row.IsValid),
                    errorMessage: String(row.errorMessage ?? row.ErrorMessage ?? ''),
                    data: row.data ?? row.Data ?? {}
                }))
                : [],
            page: Number.parseInt(String(rawState.page ?? '1'), 10) || 1,
            pageSize: Number.parseInt(String(rawState.pageSize ?? '50'), 10) || 50,
            showOnlyInvalidRows: Boolean(rawState.showOnlyInvalidRows),
            serverPaged: Boolean(rawState.serverPaged),
            totalCount: Number.parseInt(String(rawState.totalCount ?? '0'), 10) || 0,
            filteredCount: Number.parseInt(String(rawState.filteredCount ?? '0'), 10) || 0,
            totalPages: Number.parseInt(String(rawState.totalPages ?? '1'), 10) || 1,
            importType: String(rawState.importType || editTable.dataset.importType || '').toLowerCase(),
            importTypeDefinition: rawState.importTypeDefinition || {},
            rowHeaders: Array.isArray(rawState.rowHeaders) ? rawState.rowHeaders : [],
            canEdit: Boolean(rawState.canEdit)
        };

        const navigateServerPage = (page, showOnlyInvalidRows = state.showOnlyInvalidRows) => {
            const url = new URL(window.location.href);
            url.searchParams.set('page', String(Math.max(1, page)));
            url.searchParams.set('pageSize', String(state.pageSize));
            if (showOnlyInvalidRows) {
                url.searchParams.set('showOnlyInvalidRows', 'true');
            } else {
                url.searchParams.delete('showOnlyInvalidRows');
            }
            url.hash = 'excel-edit-table';
            window.location.href = url.toString();
        };

        const asStringArray = (value) => Array.isArray(value)
            ? value.map((item) => String(item ?? '')).filter((item) => item.length > 0)
            : [];

        const editRules = {
            requiredHeaders: asStringArray(state.importTypeDefinition.requiredHeaders ?? state.importTypeDefinition.RequiredHeaders),
            numericHeaders: asStringArray(state.importTypeDefinition.numericHeaders ?? state.importTypeDefinition.NumericHeaders),
            percentHeaders: asStringArray(state.importTypeDefinition.percentHeaders ?? state.importTypeDefinition.PercentHeaders),
            zeroOrOneHeaders: asStringArray(state.importTypeDefinition.zeroOrOneHeaders ?? state.importTypeDefinition.ZeroOrOneHeaders),
            threeLetterCodeHeaders: asStringArray(state.importTypeDefinition.threeLetterCodeHeaders ?? state.importTypeDefinition.ThreeLetterCodeHeaders),
            requireVoucherDebitOrCredit: Boolean(state.importTypeDefinition.requireVoucherDebitOrCredit ?? state.importTypeDefinition.RequireVoucherDebitOrCredit),
            validateBudgetPeriod: Boolean(state.importTypeDefinition.validateBudgetPeriod ?? state.importTypeDefinition.ValidateBudgetPeriod)
        };

        const requiredHeaders = new Set(editRules.requiredHeaders);

        const isNumeric = (value) => {
            let v = (value || '').trim();
            if (v.length === 0) return true;
            v = v.replace(/[−–]/g, '-').replace(/[\s\u00A0]/g, '');
            if (v === '-' || v === '+') return false;

            if (v[0] === '-' || v[0] === '+') {
                v = v.slice(1);
            }
            if (v.length === 0) return false;

            v = v.replace(/'/g, '');
            const lastComma = v.lastIndexOf(',');
            const lastDot = v.lastIndexOf('.');
            const decimalIndex = Math.max(lastComma, lastDot);
            if (decimalIndex !== -1) {
                const intPart = v.slice(0, decimalIndex).replace(/[.,]/g, '');
                const fracPart = v.slice(decimalIndex + 1).replace(/[.,]/g, '');
                if (fracPart.length === 0) return false;
                v = `${intPart}.${fracPart}`;
            } else {
                v = v.replace(/[.,]/g, '');
            }

            return /^\d+(?:\.\d+)?$/.test(v);
        };

        const isZeroOrOne = (value) => {
            const v = (value || '').trim();
            return v.length === 0 || v === '0' || v === '1';
        };

        const rowHasAnyValue = (data) => Object.values(data || {}).some((v) => String(v ?? '').trim().length > 0);

        const getRowValidationErrors = (data) => {
            const errors = new Set();
            const normalized = data || {};

            requiredHeaders.forEach((header) => {
                const value = String(normalized[header] ?? '').trim();
                if (!value) errors.add(`Fältet ${header} är obligatoriskt.`);
            });

            if (editRules.requireVoucherDebitOrCredit) {
                const debit = String(normalized.Debit ?? '').trim();
                const credit = String(normalized.Credit ?? '').trim();
                if (!debit && !credit) errors.add('Debet eller kredit måste anges.');
            }

            editRules.numericHeaders.forEach((header) => {
                if (!isNumeric(String(normalized[header] ?? ''))) {
                    errors.add(`Fältet ${header} måste vara ett giltigt tal.`);
                }
            });

            editRules.zeroOrOneHeaders.forEach((header) => {
                if (!isZeroOrOne(String(normalized[header] ?? ''))) {
                    errors.add(`Fältet ${header} får bara vara 0 eller 1.`);
                }
            });

            editRules.threeLetterCodeHeaders.forEach((header) => {
                const value = String(normalized[header] ?? '').trim();
                if (value && value.length !== 3) {
                    errors.add(`Fältet ${header} måste bestå av tre bokstäver.`);
                }
            });

            editRules.percentHeaders.forEach((header) => {
                const value = String(normalized[header] ?? '');
                const trimmed = value.trim();
                if (!isNumeric(value)) {
                    errors.add(`Fältet ${header} måste vara ett giltigt tal.`);
                } else if (trimmed) {
                    const num = parseFloat(trimmed.replace(',', '.'));
                    if (Number.isNaN(num) || num < 0 || num > 100) {
                        errors.add(`Fältet ${header} måste vara mellan 0 och 100.`);
                    }
                }
            });

            if (editRules.validateBudgetPeriod) {
                const period = String(normalized.Period ?? '').trim();
                if (period) {
                    const parsed = parseInt(period, 10);
                    if (Number.isNaN(parsed) || parsed < 1 || parsed > 12) {
                        errors.add('Period måste vara mellan 1 och 12.');
                    }
                }
            }

            return Array.from(errors);
        };

        state.rows.forEach((row) => {
            if (!row.isValid && !row.errorMessage) {
                row.errorMessage = getRowValidationErrors(row.data).join(' ');
            }
        });

        const getFilteredEntries = () => state.rows
            .map((row, index) => ({ row, index }))
            .filter((entry) => !state.showOnlyInvalidRows || !entry.row.isValid);

        const getCurrentPageEntries = () => {
            const filtered = getFilteredEntries();
            if (state.serverPaged) {
                state.totalPages = Math.max(1, state.totalPages);
                state.page = Math.min(Math.max(state.page, 1), state.totalPages);
                return {
                    filtered,
                    totalPages: state.totalPages,
                    pageEntries: filtered
                };
            }

            const totalPages = Math.max(1, Math.ceil(filtered.length / state.pageSize));
            state.page = Math.min(Math.max(state.page, 1), totalPages);
            const start = (state.page - 1) * state.pageSize;
            return {
                filtered,
                totalPages,
                pageEntries: filtered.slice(start, start + state.pageSize)
            };
        };

        const buildCellHtml = (header, value, required, emptyColumn, widthChars) => {
            const classes = ['excel-edit-cell'];
            if (required) classes.push('excel-required-cell');
            if (emptyColumn) classes.push('excel-empty-column');
            if (String(value ?? '').trim().length === 0) classes.push('excel-cell-empty');

            return `<td contenteditable="true" spellcheck="false" class="${classes.join(' ')}" data-header="${escapeHtml(header)}" data-required="${required ? 'true' : 'false'}" data-placeholder="${required ? 'Måste fyllas i' : ''}" style="--excel-col-ch:${widthChars};">${escapeHtml(value ?? '')}</td>`;
        };

        const buildReadOnlyCellHtml = (header, value, required, emptyColumn, widthChars) => {
            const classes = ['excel-read-cell'];
            if (required) classes.push('excel-required-cell');
            if (emptyColumn) classes.push('excel-empty-column');
            return `<td class="${classes.join(' ')}" style="--excel-col-ch:${widthChars};"><span class="excel-read-cell-value">${escapeHtml(String(value ?? '').trim() ? value : '–')}</span></td>`;
        };

        const syncCellEmptyState = (cell) => {
            if (!cell || !(cell instanceof HTMLElement)) {
                return;
            }

            const hasValue = (cell.textContent || '').trim().length > 0;
            cell.classList.toggle('excel-cell-empty', !hasValue);
        };

        const getHeaders = () => state.rowHeaders.length > 0
            ? state.rowHeaders
            : Array.from(editTable.querySelectorAll('thead th')).slice(editSessionForm ? 2 : 1).map((th) => (th.textContent || '').trim());

        const renderPage = () => {
            const body = editTable.querySelector('tbody');
            if (!body) return;

            const headers = getHeaders();
            const { filtered, totalPages, pageEntries } = getCurrentPageEntries();
            const emptyColumnHeaders = headers.filter((header) => state.rows.every((row) => {
                const value = row.data ? row.data[header] : '';
                return String(value ?? '').trim().length === 0;
            }));
            const headerWidths = headers.reduce((acc, header) => {
                const longestValueLength = state.rows
                    .map((row) => String(row.data?.[header] ?? '').trim().length)
                    .reduce((max, len) => Math.max(max, len), 0);
                const estimatedChars = Math.max(header.length, longestValueLength);
                acc[header] = emptyColumnHeaders.includes(header)
                    ? Math.min(Math.max(estimatedChars, 4), 14)
                    : Math.min(Math.max(estimatedChars + 2, 8), 18);
                return acc;
            }, {});

            const rowsHtml = pageEntries.length === 0
                ? `<tr class="excel-empty-row"><td colspan="${headers.length + (state.canEdit ? 2 : 1)}" class="text-muted small">Inga rader matchar filtret.</td></tr>`
                : pageEntries.map(({ row, index }) => {
                    const rowClass = row.isValid ? 'row-valid' : 'row-invalid';
                    const errorMessage = row.errorMessage || 'Raden klarade inte valideringen.';
                    const cells = headers.map((header) => {
                        const value = row.data ? row.data[header] : '';
                        return state.canEdit
                            ? buildCellHtml(header, value, requiredHeaders.has(header), emptyColumnHeaders.includes(header), headerWidths[header] || 10)
                            : buildReadOnlyCellHtml(header, value, requiredHeaders.has(header), emptyColumnHeaders.includes(header), headerWidths[header] || 10);
                    }).join('');

                    const mainRow = state.canEdit
                        ? `<tr class="${rowClass}" data-row-index="${index}" data-rowno="${row.rowNo}"><td class="text-start"><button type="button" class="excel-delete-row excel-delete-icon" aria-label="Ta bort"><span aria-hidden="true">−</span></button></td><td>${escapeHtml(row.rowNo)}</td>${cells}</tr>`
                        : `<tr class="${rowClass}" data-row-index="${index}" data-rowno="${row.rowNo}"><td>${escapeHtml(row.rowNo)}</td>${cells}</tr>`;
                    const errorRow = row.isValid
                        ? ''
                        : `<tr class="excel-row-error-row" data-error-for-index="${index}"><td colspan="${headers.length + (state.canEdit ? 2 : 1)}"><span class="excel-row-error-message"><i class="fa fa-exclamation-circle" aria-hidden="true"></i>${escapeHtml(errorMessage)}</span></td></tr>`;

                    return mainRow + errorRow;
                }).join('');

            const addRowHtml = state.canEdit
                ? `<tr class="excel-add-row-row" data-rowno="0"><td class="text-start"><button type="button" class="excel-add-row-btn excel-add-icon" aria-label="Lägg till rad"><span aria-hidden="true">+</span></button></td><td></td>${headers.map((header) => {
                    const widthChars = headerWidths[header] || 10;
                    const required = requiredHeaders.has(header);
                    const emptyColumn = emptyColumnHeaders.includes(header);
                    return `<td class="excel-edit-cell ${required ? 'excel-required-cell' : ''} ${emptyColumn ? 'excel-empty-column' : ''} excel-cell-empty" data-header="${escapeHtml(header)}" data-required="${required ? 'true' : 'false'}" data-placeholder="${required ? 'Måste fyllas i' : ''}" style="--excel-col-ch:${widthChars};"></td>`;
                }).join('')}</tr>`
                : '';

            body.innerHTML = rowsHtml + addRowHtml;

            if (pageInfo) {
                const visibleCount = state.serverPaged ? state.filteredCount : filtered.length;
                pageInfo.textContent = visibleCount > 0
                    ? `Visar ${((state.page - 1) * state.pageSize) + 1}-${Math.min(state.page * state.pageSize, visibleCount)} av ${visibleCount} rader`
                    : 'Inga rader att visa';
            }
            if (pageIndicator) {
                pageIndicator.textContent = `Sida ${state.page} av ${totalPages}`;
            }
            if (pagePrev) {
                pagePrev.disabled = state.page <= 1;
            }
            if (pageNext) {
                pageNext.disabled = state.page >= totalPages;
            }
            if (filterInvalid) {
                filterInvalid.checked = state.showOnlyInvalidRows;
            }

            body.querySelectorAll('tr[data-row-index]').forEach((rowEl) => {
                const index = Number.parseInt(rowEl.dataset.rowIndex || '', 10);
                if (!Number.isFinite(index)) return;
                const row = state.rows[index];
                if (!row) return;
                rowEl.classList.toggle('row-valid', row.isValid);
                rowEl.classList.toggle('row-invalid', !row.isValid);
            });
        };

        const syncVisibleRowsFromDom = () => {
            editTable.querySelectorAll('tbody tr[data-row-index]').forEach((rowEl) => {
                const index = Number.parseInt(rowEl.dataset.rowIndex || '', 10);
                if (!Number.isFinite(index) || !state.rows[index]) {
                    return;
                }

                const data = {};
                rowEl.querySelectorAll('td[data-header]').forEach((cell) => {
                    const header = cell.dataset.header || '';
                    const value = (cell.textContent || '').trim();
                    data[header] = value;
                    syncCellEmptyState(cell);
                });
                state.rows[index].data = data;
                const rowErrors = getRowValidationErrors(data);
                state.rows[index].isValid = rowErrors.length === 0;
                state.rows[index].errorMessage = rowErrors.join(' ');
                rowEl.classList.toggle('row-valid', state.rows[index].isValid);
                rowEl.classList.toggle('row-invalid', !state.rows[index].isValid);

                const existingErrorRow = editTable.querySelector(`tr[data-error-for-index="${index}"]`);
                if (state.rows[index].isValid) {
                    existingErrorRow?.remove();
                } else {
                    const message = state.rows[index].errorMessage || 'Raden klarade inte valideringen.';
                    const messageHtml = `<i class="fa fa-exclamation-circle" aria-hidden="true"></i>${escapeHtml(message)}`;
                    if (existingErrorRow) {
                        const messageElement = existingErrorRow.querySelector('.excel-row-error-message');
                        if (messageElement) messageElement.innerHTML = messageHtml;
                    } else {
                        rowEl.insertAdjacentHTML(
                            'afterend',
                            `<tr class="excel-row-error-row" data-error-for-index="${index}"><td colspan="${getHeaders().length + (state.canEdit ? 2 : 1)}"><span class="excel-row-error-message">${messageHtml}</span></td></tr>`);
                    }
                }
            });
        };

        const updateSummary = () => {
            const totalRows = state.rows.length;
            const invalidRows = state.rows.filter((row) => !row.isValid).length;
            const validRows = totalRows - invalidRows;

            const validEl = document.getElementById('excel-valid-count');
            const invalidEl = document.getElementById('excel-invalid-count');
            const totalEl = document.getElementById('excel-total-count');
            if (validEl) validEl.textContent = `OK: ${validRows}`;
            if (invalidEl) invalidEl.textContent = `Fel: ${invalidRows}`;
            if (totalEl) totalEl.textContent = `Totalt: ${totalRows}`;

            const validValidationEl = document.getElementById('excel-valid-count-validation');
            const invalidValidationEl = document.getElementById('excel-invalid-count-validation');
            if (validValidationEl) validValidationEl.textContent = `Godkända: ${validRows}`;
            if (invalidValidationEl) invalidValidationEl.textContent = `Fel: ${invalidRows}`;

            const alertEl = document.getElementById('excel-import-result');
            if (alertEl && alertEl.dataset.allowUpdate === 'true') {
                alertEl.classList.toggle('module-result--danger', invalidRows > 0);
                alertEl.classList.toggle('module-result--success', invalidRows === 0);
            }

            const submitBtn = editSessionForm?.querySelector('button[type="submit"]');
            if (submitBtn) {
                const disableSubmit = invalidRows > 0 || totalRows === 0;
                submitBtn.disabled = disableSubmit;
                submitBtn.title = disableSubmit ? 'Fixa alla fel innan du importerar.' : '';
            }

            document.querySelectorAll('.excel-row-badge[data-row-no]').forEach((badge) => {
                const rowNo = Number.parseInt(badge.dataset.rowNo || '0', 10);
                const row = state.rows.find((candidate) => candidate.rowNo === rowNo);
                const isRowValid = row ? row.isValid : false;
                const label = isRowValid ? (badge.dataset.okLabel || 'OK') : (badge.dataset.label || 'Rad');
                badge.textContent = `${label}: ${rowNo}`;
                badge.classList.toggle('bg-success', isRowValid);
                badge.classList.toggle('bg-danger', !isRowValid);
            });
        };

        const createEmptyRow = () => {
            const nextRowNo = state.rows.length > 0 ? Math.max(...state.rows.map((row) => row.rowNo)) + 1 : 1;
            const emptyData = state.rowHeaders.reduce((acc, header) => {
                acc[header] = '';
                return acc;
            }, {});
            const row = {
                rowNo: nextRowNo,
                isValid: false,
                errorMessage: getRowValidationErrors(emptyData).join(' '),
                data: emptyData
            };
            state.rows.push(row);
            return row;
        };

        const addRow = () => {
            createEmptyRow();
            state.page = Math.max(1, Math.ceil(state.rows.length / state.pageSize));
            renderPage();
            updateSummary();
        };

        const removeRowByIndex = (index) => {
            state.rows.splice(index, 1);
            const { filtered, totalPages } = getCurrentPageEntries();
            state.page = Math.min(state.page, totalPages);
            if (filtered.length === 0 && state.page > 1) {
                state.page -= 1;
            }
            renderPage();
            updateSummary();
        };

        editTable.addEventListener('click', (e) => {
            const addBtn = e.target.closest('.excel-add-row-btn');
            if (addBtn && state.canEdit) {
                addRow();
                return;
            }

            const deleteBtn = e.target.closest('.excel-delete-row');
            if (!deleteBtn || !state.canEdit) return;
            const row = deleteBtn.closest('tr[data-row-index]');
            if (!row) return;
            const index = Number.parseInt(row.dataset.rowIndex || '', 10);
            if (Number.isFinite(index)) {
                removeRowByIndex(index);
            }
        });

        editTable.addEventListener('input', () => {
            syncVisibleRowsFromDom();
            updateSummary();
        });
        editTable.addEventListener('blur', () => {
            syncVisibleRowsFromDom();
            updateSummary();
        }, true);

        editTable.addEventListener('paste', (e) => {
            if (!state.canEdit) return;
            const startCell = e.target.closest('td[data-header]');
            if (!startCell) return;

            const raw = e.clipboardData?.getData('text/plain') || '';
            if (!raw) return;

            const startRow = startCell.closest('tr[data-row-index]');
            const startIndex = Number.parseInt(startRow?.dataset.rowIndex || '', 10);
            if (!Number.isFinite(startIndex)) return;

            const headers = getHeaders();
            const startHeader = startCell.dataset.header || '';
            const startColIndex = headers.findIndex((header) => header === startHeader);
            if (startColIndex < 0) return;

            e.preventDefault();
            syncVisibleRowsFromDom();

            const lines = raw.replace(/\r\n/g, '\n').replace(/\r/g, '\n')
                .split('\n')
                .filter((line, index, arr) => line.length > 0 || index < arr.length - 1)
                .map((line) => line.split('\t'));

            lines.forEach((cols, rowOffset) => {
                const targetIndex = startIndex + rowOffset;
                while (state.rows.length <= targetIndex) {
                    createEmptyRow();
                }
                const targetRow = state.rows[targetIndex];
                cols.forEach((value, colOffset) => {
                    const header = headers[startColIndex + colOffset];
                    if (!header) return;
                    targetRow.data[header] = value;
                });
                const rowErrors = getRowValidationErrors(targetRow.data);
                targetRow.isValid = rowErrors.length === 0;
                targetRow.errorMessage = rowErrors.join(' ');
            });

            renderPage();
            updateSummary();
        });

        if (pagePrev) {
            pagePrev.addEventListener('click', () => {
                if (state.serverPaged) {
                    navigateServerPage(state.page - 1);
                    return;
                }

                syncVisibleRowsFromDom();
                state.page = Math.max(1, state.page - 1);
                renderPage();
                updateSummary();
            });
        }

        if (pageNext) {
            pageNext.addEventListener('click', () => {
                if (state.serverPaged) {
                    navigateServerPage(state.page + 1);
                    return;
                }

                syncVisibleRowsFromDom();
                const totalPages = Math.max(1, Math.ceil(getFilteredEntries().length / state.pageSize));
                state.page = Math.min(totalPages, state.page + 1);
                renderPage();
                updateSummary();
            });
        }

        if (filterInvalid) {
            filterInvalid.addEventListener('change', () => {
                if (state.serverPaged) {
                    navigateServerPage(1, filterInvalid.checked);
                    return;
                }

                syncVisibleRowsFromDom();
                state.showOnlyInvalidRows = filterInvalid.checked;
                state.page = 1;
                renderPage();
                updateSummary();
            });
        }

        if (editSessionForm && rowsJsonInput) {
            editSessionForm.addEventListener('submit', (e) => {
                syncVisibleRowsFromDom();
                const rows = state.rows
                    .filter((row) => rowHasAnyValue(row.data))
                    .map((row) => ({
                        rowNo: row.rowNo,
                        data: row.data
                    }));

                if (rows.length === 0) {
                    e.preventDefault();
                    alert('Minst en rad måste innehålla data innan import.');
                    return;
                }

                rowsJsonInput.value = JSON.stringify(rows);
            });
        }

        renderPage();
        updateSummary();
    }
})();
