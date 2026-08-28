// Manages the dashboard editor as a reversible local draft without blocking saves on dashboard data refreshes.
window.ZeeUDashboardLayout = (function () {
    const maximumVisibleWidgets = 8;
    const historyLimit = 40;
    const dragStartDistance = 7;
    const sizeValues = { compact: 0, wide: 1, full: 2 };
    const sizeLabels = { compact: 'Liten', wide: 'Bred', full: 'Full bredd' };
    const sizeClasses = {
        compact: ['col-xl-4', 'col-lg-6', 'col-md-6'],
        wide: ['col-xl-8', 'col-lg-6', 'col-md-6'],
        full: ['col-12']
    };
    let draftWidgets = [];
    let initialWidgets = [];
    let undoStack = [];
    let redoStack = [];
    let pointerDrag = null;
    let toastTimer = null;

    const getAntiForgery = () => document.querySelector('#__af input[name="__RequestVerificationToken"]')?.value;
    const getGrid = () => document.querySelector('[data-dashboard-grid]');
    const isEditing = () => document.documentElement.classList.contains('dashboard-editing');
    const cloneWidgets = widgets => widgets.map(widget => ({ ...widget }));
    const widgetsEqual = (left, right) => JSON.stringify(left) === JSON.stringify(right);
    const getSizeName = size => Object.keys(sizeValues).find(key => sizeValues[key] === size) || 'compact';
    const getCatalogButton = widgetId => document.querySelector(`[data-dashboard-toggle="${widgetId}"]`);
    const getWidgetShell = widgetId => document.querySelector(`[data-dashboard-widget-id="${widgetId}"]`);
    const getWidgetTitle = widgetId => getCatalogButton(widgetId)?.dataset.dashboardCardTitle || widgetId;
    const getSupportedSizes = widgetId => {
        const configured = (getCatalogButton(widgetId)?.dataset.dashboardSupportedSizes || '')
            .split(',')
            .map(size => size.trim())
            .filter(size => Object.hasOwn(sizeValues, size));
        return configured.length > 0 ? configured : ['compact'];
    };
    const getAllowedSize = (widgetId, requestedSize) => {
        const supportedSizes = getSupportedSizes(widgetId);
        return supportedSizes.includes(requestedSize) ? requestedSize : supportedSizes[0];
    };

    const readWidgets = () => Array.from(getGrid()?.querySelectorAll('[data-dashboard-widget-id]') || [])
        .filter(element => !element.hidden)
        .map((element, index) => ({
            widgetId: element.dataset.dashboardWidgetId,
            sortOrder: (index + 1) * 10,
            size: sizeValues[element.dataset.dashboardWidgetSize] ?? sizeValues.compact
        }));

    const setStatus = (message, isError = false) => {
        const status = document.querySelector('[data-dashboard-layout-status]');
        if (!status) return;
        status.textContent = message || '';
        status.classList.toggle('text-danger', Boolean(isError));
        status.classList.toggle('text-success', !isError && Boolean(message));
    };

    const announce = message => {
        const region = document.querySelector('[data-dashboard-editor-announcement]');
        if (!region) return;
        region.textContent = '';
        window.requestAnimationFrame(() => { region.textContent = message || ''; });
    };

    const showToast = (message, isError = false) => {
        const toast = document.querySelector('[data-dashboard-toast]');
        if (!toast) return;
        window.clearTimeout(toastTimer);
        toast.textContent = message;
        toast.hidden = false;
        toast.classList.toggle('is-error', isError);
        toast.classList.add('is-visible');
        toastTimer = window.setTimeout(() => {
            toast.classList.remove('is-visible');
            window.setTimeout(() => { toast.hidden = true; }, 180);
        }, isError ? 6000 : 3200);
    };

    const preserveViewport = callback => {
        const scrollX = window.scrollX;
        const scrollY = window.scrollY;
        callback();
        window.scrollTo({ left: scrollX, top: scrollY, behavior: 'auto' });
        window.requestAnimationFrame(() => window.scrollTo({ left: scrollX, top: scrollY, behavior: 'auto' }));
    };

    const post = async (url, body) => {
        const response = await fetch(url, {
            method: 'POST',
            credentials: 'same-origin',
            headers: {
                'Content-Type': 'application/json',
                'RequestVerificationToken': getAntiForgery()
            },
            body: body ? JSON.stringify(body) : null
        });
        const payload = await response.json().catch(() => ({}));
        if (!response.ok) {
            throw new Error(payload.message || 'Kunde inte spara startsidan just nu.');
        }
        return payload;
    };

    const createLazyCardContent = widgetId => {
        const content = document.createElement('div');
        content.className = 'dashboard-card-content';
        content.dataset.dashboardCardContent = '';
        content.dataset.dashboardCardId = widgetId;
        content.dataset.dashboardCardRefreshUrl = `/Member/DashboardCard?cardId=${encodeURIComponent(widgetId)}`;
        content.dataset.dashboardLazyCard = 'true';
        content.dataset.url = content.dataset.dashboardCardRefreshUrl;
        content.dataset.loaded = 'idle';
        content.setAttribute('aria-busy', 'true');
        content.innerHTML = '<div class="card kpi-card h-100 dashboard-card-state"><div class="dashboard-card-state__body"><span class="dashboard-card-state__icon" aria-hidden="true"><i class="fa fa-spinner fa-spin"></i></span><div class="dashboard-card-state__copy"><div class="dashboard-card-state__eyebrow">Dashboardblock</div><h3 class="dashboard-card-state__title">Laddar block…</h3></div></div></div>';
        return content;
    };

    const finalizeSavedGrid = () => {
        const grid = getGrid();
        if (!grid) throw new Error('Startsidan sparades, men rutnätet kunde inte uppdateras.');

        const selectedIds = new Set(draftWidgets.map(widget => widget.widgetId));
        Array.from(grid.querySelectorAll('[data-dashboard-widget-id]')).forEach(shell => {
            if (!selectedIds.has(shell.dataset.dashboardWidgetId)) shell.remove();
        });

        draftWidgets.forEach(widget => {
            const shell = getWidgetShell(widget.widgetId);
            if (!shell) return;

            shell.hidden = false;
            setWidgetSize(shell, getSizeName(widget.size));
            if (!shell.classList.contains('dashboard-widget-preview')) {
                grid.appendChild(shell);
                return;
            }

            shell.classList.remove('dashboard-widget-preview');
            shell.replaceChildren(createLazyCardContent(widget.widgetId));
            grid.appendChild(shell);
        });

        window.ZeeUDashboard?.refreshCards?.();
    };

    const setWidgetSize = (shell, size) => {
        if (!shell) return;
        const allowedSize = getAllowedSize(shell.dataset.dashboardWidgetId, size);
        Object.values(sizeClasses).flat().forEach(cssClass => shell.classList.remove(cssClass));
        sizeClasses[allowedSize].forEach(cssClass => shell.classList.add(cssClass));
        shell.dataset.dashboardWidgetSize = allowedSize;
        const select = shell.querySelector('[data-dashboard-size]');
        if (select) select.value = allowedSize;
    };

    const createPreview = widget => {
        const button = getCatalogButton(widget.widgetId);
        const title = button?.dataset.dashboardCardTitle || widget.widgetId;
        const description = button?.dataset.dashboardCardDescription || 'Blocket laddas när startsidan sparas.';
        const shell = document.createElement('div');
        shell.className = 'dashboard-widget-shell dashboard-widget-preview';
        shell.dataset.dashboardWidgetId = widget.widgetId;
        shell.dataset.dashboardWidgetSize = 'compact';
        shell.innerHTML = `
            <div class="dashboard-widget-toolbar" data-dashboard-drag-surface aria-label="Ändra block">
                <div class="dashboard-widget-toolbar__identity">
                    <span class="dashboard-widget-drag-handle" aria-hidden="true" title="Dra till en ny plats"><i class="fa fa-arrows" aria-hidden="true"></i></span>
                    <span class="dashboard-widget-toolbar__title"></span>
                </div>
                <div class="dashboard-widget-toolbar__actions">
                    <button type="button" class="btn btn-sm btn-portal btn-portal-outline" data-dashboard-move="up" aria-label="Flytta upp"><i class="fa fa-arrow-up" aria-hidden="true"></i></button>
                    <button type="button" class="btn btn-sm btn-portal btn-portal-outline" data-dashboard-move="down" aria-label="Flytta ned"><i class="fa fa-arrow-down" aria-hidden="true"></i></button>
                    <select class="form-select form-select-sm" data-dashboard-size aria-label="Storlek"></select>
                    <button type="button" class="btn btn-sm btn-outline-danger" data-dashboard-remove aria-label="Ta bort"><i class="fa fa-times" aria-hidden="true"></i></button>
                </div>
            </div>
            <div class="card kpi-card p-3 dashboard-widget-preview__card">
                <h3 class="dashboard-card-title mb-2"></h3>
                <p class="dashboard-widget-preview__description mb-0"></p>
                <div class="dashboard-widget-preview__note">Innehållet laddas när du väljer Klar.</div>
            </div>`;
        shell.querySelector('.dashboard-widget-toolbar__title').textContent = title;
        shell.querySelector('.dashboard-widget-toolbar').setAttribute('aria-label', `Ändra ${title}`);
        shell.querySelector('.dashboard-card-title').textContent = title;
        shell.querySelector('.dashboard-widget-preview__description').textContent = description;
        const sizeSelect = shell.querySelector('[data-dashboard-size]');
        getSupportedSizes(widget.widgetId).forEach(size => {
            const option = document.createElement('option');
            option.value = size;
            option.textContent = sizeLabels[size];
            sizeSelect.appendChild(option);
        });
        return shell;
    };

    const hasUnsavedChanges = () => !widgetsEqual(draftWidgets, initialWidgets);

    const updateCatalog = () => {
        const selectedIds = new Set(draftWidgets.map(widget => widget.widgetId));
        const atLimit = draftWidgets.length >= maximumVisibleWidgets;
        document.querySelectorAll('[data-dashboard-toggle]').forEach(button => {
            const widgetId = button.dataset.dashboardToggle;
            const isSelected = selectedIds.has(widgetId);
            button.classList.toggle('is-selected', isSelected);
            button.setAttribute('aria-pressed', String(isSelected));
            button.disabled = !isSelected && atLimit;
            button.title = !isSelected && atLimit ? `Du kan som mest visa ${maximumVisibleWidgets} block.` : '';
            const action = button.querySelector('.dashboard-layout-catalog__action');
            if (action) {
                action.innerHTML = `<i class="fa ${isSelected ? 'fa-check' : 'fa-plus'}" aria-hidden="true"></i> ${isSelected ? 'Tillagd · ta bort' : 'Lägg till'}`;
            }
        });
        const counter = document.querySelector('[data-dashboard-selected-count]');
        if (counter) counter.textContent = `${draftWidgets.length} av ${maximumVisibleWidgets} valda`;
    };

    const applyCatalogFilter = () => {
        const search = (document.querySelector('[data-dashboard-search]')?.value || '').trim().toLocaleLowerCase('sv-SE');
        const activeCategory = document.querySelector('[data-dashboard-category-filter].is-active')?.dataset.dashboardCategoryFilter || 'all';
        let visibleCount = 0;
        document.querySelectorAll('[data-dashboard-toggle]').forEach(button => {
            const matchesCategory = activeCategory === 'all' || button.dataset.dashboardCategory === activeCategory;
            const matchesSearch = !search || (button.dataset.dashboardSearchText || '').includes(search);
            button.hidden = !(matchesCategory && matchesSearch);
            if (!button.hidden) visibleCount += 1;
        });
        const empty = document.querySelector('[data-dashboard-catalog-empty]');
        if (empty) empty.hidden = visibleCount > 0;
    };

    const updateHistoryControls = () => {
        const undoButton = document.querySelector('[data-dashboard-undo]');
        const redoButton = document.querySelector('[data-dashboard-redo]');
        const savebarStatus = document.querySelector('.dashboard-edit-savebar__status');
        if (undoButton) undoButton.disabled = undoStack.length === 0;
        if (redoButton) redoButton.disabled = redoStack.length === 0;
        if (savebarStatus) {
            savebarStatus.textContent = hasUnsavedChanges()
                ? 'Du har osparade ändringar'
                : 'Inga osparade ändringar';
        }
    };

    const renderDraft = ({ keepViewport = true } = {}) => {
        const grid = getGrid();
        if (!grid) return;
        const render = () => {
            const selectedIds = new Set(draftWidgets.map(widget => widget.widgetId));
            Array.from(grid.querySelectorAll('[data-dashboard-widget-id]')).forEach(shell => {
                if (!selectedIds.has(shell.dataset.dashboardWidgetId)) shell.hidden = true;
            });

            draftWidgets.forEach(widget => {
                let shell = getWidgetShell(widget.widgetId);
                if (!shell) {
                    shell = createPreview(widget);
                }
                shell.hidden = false;
                setWidgetSize(shell, getSizeName(widget.size));
                grid.appendChild(shell);
            });

            updateCatalog();
            applyCatalogFilter();
            updateHistoryControls();
            if (isEditing()) {
                setStatus(hasUnsavedChanges()
                    ? 'Du har osparade ändringar. Välj Klar när layouten känns rätt.'
                    : 'Redigeringsläget är aktivt.');
            }
        };

        if (keepViewport) preserveViewport(render);
        else render();
    };

    const applyMutation = (mutator, message) => {
        const before = cloneWidgets(draftWidgets);
        mutator();
        if (widgetsEqual(before, draftWidgets)) return false;
        undoStack.push(before);
        if (undoStack.length > historyLimit) undoStack.shift();
        redoStack = [];
        renderDraft();
        announce(message);
        return true;
    };

    const undo = () => {
        if (undoStack.length === 0) return;
        redoStack.push(cloneWidgets(draftWidgets));
        draftWidgets = undoStack.pop();
        renderDraft();
        announce('Senaste ändringen har ångrats.');
    };

    const redo = () => {
        if (redoStack.length === 0) return;
        undoStack.push(cloneWidgets(draftWidgets));
        draftWidgets = redoStack.pop();
        renderDraft();
        announce('Ändringen har gjorts om.');
    };

    const add = (widgetId, size) => {
        if (!widgetId || draftWidgets.some(widget => widget.widgetId === widgetId)) return;
        if (draftWidgets.length >= maximumVisibleWidgets) {
            setStatus(`Du kan som mest visa ${maximumVisibleWidgets} block.`, true);
            return;
        }
        const allowedSize = getAllowedSize(widgetId, size);
        applyMutation(() => {
            draftWidgets.push({
                widgetId,
                sortOrder: (draftWidgets.length + 1) * 10,
                size: sizeValues[allowedSize]
            });
        }, `${getWidgetTitle(widgetId)} har lagts till.`);
    };

    const remove = widgetId => {
        applyMutation(() => {
            draftWidgets = draftWidgets.filter(widget => widget.widgetId !== widgetId);
        }, `${getWidgetTitle(widgetId)} har tagits bort.`);
    };

    const move = (widgetId, direction) => {
        const index = draftWidgets.findIndex(widget => widget.widgetId === widgetId);
        const nextIndex = direction === 'up' ? index - 1 : index + 1;
        if (index < 0 || nextIndex < 0 || nextIndex >= draftWidgets.length) return;
        applyMutation(() => {
            [draftWidgets[index], draftWidgets[nextIndex]] = [draftWidgets[nextIndex], draftWidgets[index]];
        }, `${getWidgetTitle(widgetId)} har flyttats.`);
    };

    const changeSize = (widgetId, size) => {
        const nextSize = sizeValues[getAllowedSize(widgetId, size)];
        applyMutation(() => {
            const widget = draftWidgets.find(item => item.widgetId === widgetId);
            if (widget) widget.size = nextSize;
        }, `${getWidgetTitle(widgetId)} har ändrat storlek.`);
    };

    const resetHistory = () => {
        undoStack = [];
        redoStack = [];
        updateHistoryControls();
    };

    const activatePointerDrag = event => {
        if (!pointerDrag || pointerDrag.active) return;
        const rect = pointerDrag.shell.getBoundingClientRect();
        const placeholder = document.createElement('div');
        placeholder.className = 'dashboard-widget-placeholder';
        placeholder.dataset.dashboardWidgetSize = pointerDrag.shell.dataset.dashboardWidgetSize || 'compact';
        placeholder.style.height = `${rect.height}px`;
        placeholder.setAttribute('aria-hidden', 'true');
        pointerDrag.shell.parentElement.insertBefore(placeholder, pointerDrag.shell);

        pointerDrag.active = true;
        pointerDrag.placeholder = placeholder;
        pointerDrag.rect = rect;
        pointerDrag.shell.classList.add('dashboard-widget-shell--pointer-dragging');
        pointerDrag.shell.style.position = 'fixed';
        pointerDrag.shell.style.left = `${rect.left}px`;
        pointerDrag.shell.style.top = `${rect.top}px`;
        pointerDrag.shell.style.width = `${rect.width}px`;
        pointerDrag.shell.style.height = `${rect.height}px`;
        pointerDrag.shell.style.margin = '0';
        pointerDrag.shell.style.zIndex = '1200';
        pointerDrag.shell.style.pointerEvents = 'none';
        document.body.classList.add('dashboard-pointer-dragging');
        announce(`${getWidgetTitle(pointerDrag.widgetId)} flyttas. Släpp på önskad plats.`);
        event.preventDefault();
    };

    const movePointerPlaceholder = event => {
        const drag = pointerDrag;
        const grid = getGrid();
        if (!drag?.active || !grid || !drag.placeholder) return;
        const element = document.elementFromPoint(event.clientX, event.clientY);
        const target = element?.closest?.('[data-dashboard-widget-id]');
        if (!target || target === drag.shell || !grid.contains(target)) return;

        const targetRect = target.getBoundingClientRect();
        const placeholderRect = drag.placeholder.getBoundingClientRect();
        const sameVisualRow = Math.abs(placeholderRect.top - targetRect.top) < Math.min(placeholderRect.height, targetRect.height) / 2;
        const placeAfter = sameVisualRow
            ? event.clientX > targetRect.left + (targetRect.width / 2)
            : event.clientY > targetRect.top + (targetRect.height / 2);
        const logicalSiblings = Array.from(grid.children)
            .filter(element => element !== drag.shell && element !== drag.placeholder);
        const targetIndex = logicalSiblings.indexOf(target);
        const reference = logicalSiblings[targetIndex + (placeAfter ? 1 : 0)] || null;
        if (reference !== drag.placeholder.nextElementSibling) {
            grid.insertBefore(drag.placeholder, reference);
        }
    };

    const autoScrollDuringDrag = clientY => {
        const edge = Math.min(96, window.innerHeight * 0.16);
        if (clientY < edge) {
            window.scrollBy({ top: -Math.ceil((edge - clientY) / 5), behavior: 'auto' });
        } else if (clientY > window.innerHeight - edge) {
            window.scrollBy({ top: Math.ceil((clientY - (window.innerHeight - edge)) / 5), behavior: 'auto' });
        }
    };

    const cleanupPointerDrag = () => {
        const drag = pointerDrag;
        if (!drag) return;
        drag.placeholder?.remove();
        drag.shell.classList.remove('dashboard-widget-shell--pointer-dragging');
        ['position', 'left', 'top', 'width', 'height', 'margin', 'zIndex', 'pointerEvents', 'transform']
            .forEach(property => { drag.shell.style[property] = ''; });
        document.body.classList.remove('dashboard-pointer-dragging');
        try {
            if (drag.surface.hasPointerCapture?.(drag.pointerId)) {
                drag.surface.releasePointerCapture(drag.pointerId);
            }
        } catch {
            // The browser may already have released capture after pointer cancellation.
        }
        pointerDrag = null;
    };

    const finishPointerDrag = commit => {
        const drag = pointerDrag;
        if (!drag) return;
        if (!drag.active || !commit || !drag.placeholder) {
            cleanupPointerDrag();
            return;
        }

        const grid = getGrid();
        if (!grid) {
            cleanupPointerDrag();
            return;
        }
        grid.insertBefore(drag.shell, drag.placeholder);
        const orderedIds = Array.from(grid.querySelectorAll('[data-dashboard-widget-id]'))
            .filter(shell => !shell.hidden)
            .map(shell => shell.dataset.dashboardWidgetId);
        const currentById = new Map(draftWidgets.map(widget => [widget.widgetId, widget]));
        cleanupPointerDrag();
        applyMutation(() => {
            draftWidgets = orderedIds
                .map(widgetId => currentById.get(widgetId))
                .filter(Boolean)
                .map(widget => ({ ...widget }));
        }, `${getWidgetTitle(drag.widgetId)} har flyttats.`);
    };

    const handlePointerDown = event => {
        if (!isEditing() || event.isPrimary === false || event.button !== 0) return;
        if (event.target.closest('button, select, option, input, textarea, a')) return;
        const surface = event.target.closest('[data-dashboard-drag-surface]');
        const shell = surface?.closest('[data-dashboard-widget-id]');
        if (!surface || !shell || !getGrid()?.contains(shell)) return;

        pointerDrag = {
            pointerId: event.pointerId,
            widgetId: shell.dataset.dashboardWidgetId,
            shell,
            surface,
            startX: event.clientX,
            startY: event.clientY,
            active: false,
            placeholder: null,
            rect: null
        };
        try {
            surface.setPointerCapture?.(event.pointerId);
        } catch {
            // Pointer capture is an enhancement; document-level listeners still complete the drag.
        }
    };

    const handlePointerMove = event => {
        const drag = pointerDrag;
        if (!drag || drag.pointerId !== event.pointerId) return;
        const distance = Math.hypot(event.clientX - drag.startX, event.clientY - drag.startY);
        if (!drag.active && distance < dragStartDistance) return;
        if (!drag.active) activatePointerDrag(event);
        if (!pointerDrag?.active) return;

        event.preventDefault();
        drag.shell.style.transform = `translate3d(${event.clientX - drag.startX}px, ${event.clientY - drag.startY}px, 0)`;
        movePointerPlaceholder(event);
        autoScrollDuringDrag(event.clientY);
    };

    const init = () => {
        const toggle = document.querySelector('[data-dashboard-edit-toggle]');
        const panel = document.getElementById('dashboard-layout-panel');
        const stickySave = document.querySelector('[data-dashboard-save]');
        const cancelButton = document.querySelector('[data-dashboard-cancel]');
        const savebar = document.querySelector('[data-dashboard-savebar]');

        const leaveEditing = () => {
            finishPointerDrag(false);
            document.documentElement.classList.remove('dashboard-editing');
            if (panel) panel.hidden = true;
            toggle?.setAttribute('aria-expanded', 'false');
            if (toggle) toggle.innerHTML = '<i class="fa fa-sliders me-1" aria-hidden="true"></i> Anpassa startsida';
            if (savebar) savebar.setAttribute('aria-hidden', 'true');
        };

        const finishSavedLayout = successMessage => {
            finalizeSavedGrid();
            draftWidgets = readWidgets();
            initialWidgets = cloneWidgets(draftWidgets);
            resetHistory();
            leaveEditing();
            setStatus(successMessage);
            showToast(successMessage);
            toggle?.focus();
            return true;
        };

        const save = async () => {
            if (!hasUnsavedChanges()) {
                leaveEditing();
                setStatus('Inga ändringar behövde sparas.');
                showToast('Inga ändringar behövde sparas.');
                toggle?.focus();
                return;
            }

            if (draftWidgets.length > maximumVisibleWidgets) {
                setStatus(`Du kan som mest visa ${maximumVisibleWidgets} block.`, true);
                return;
            }

            const widgets = draftWidgets.map((widget, index) => ({
                ...widget,
                sortOrder: (index + 1) * 10
            }));
            try {
                stickySave?.setAttribute('disabled', 'disabled');
                cancelButton?.setAttribute('disabled', 'disabled');
                setStatus('Sparar startsidan…');
                await post('/Member/SaveDashboardLayout', { widgets });
                finishSavedLayout('Startsidan har sparats.');
            } catch (error) {
                setStatus(error.message, true);
                showToast(error.message, true);
            } finally {
                stickySave?.removeAttribute('disabled');
                cancelButton?.removeAttribute('disabled');
            }
        };

        const cancel = () => {
            finishPointerDrag(false);
            draftWidgets = cloneWidgets(initialWidgets);
            renderDraft();
            resetHistory();
            leaveEditing();
            setStatus('Ändringarna har inte sparats.');
            toggle?.focus();
        };

        toggle?.addEventListener('click', () => {
            if (!isEditing()) {
                draftWidgets = readWidgets();
                initialWidgets = cloneWidgets(draftWidgets);
                resetHistory();
                document.documentElement.classList.add('dashboard-editing');
                if (panel) panel.hidden = false;
                toggle.setAttribute('aria-expanded', 'true');
                toggle.innerHTML = '<i class="fa fa-times me-1" aria-hidden="true"></i> Avbryt redigering';
                if (savebar) savebar.setAttribute('aria-hidden', 'false');
                renderDraft();
                return;
            }
            cancel();
        });

        stickySave?.addEventListener('click', save);
        cancelButton?.addEventListener('click', cancel);
        document.querySelector('[data-dashboard-undo]')?.addEventListener('click', undo);
        document.querySelector('[data-dashboard-redo]')?.addEventListener('click', redo);

        document.addEventListener('click', event => {
            if (!isEditing() || !getGrid()?.contains(event.target)) return;
            const button = event.target.closest('button');
            const shell = event.target.closest('[data-dashboard-widget-id]');
            if (!button || !shell) return;
            if (button.matches('[data-dashboard-move]')) {
                event.preventDefault();
                move(shell.dataset.dashboardWidgetId, button.dataset.dashboardMove);
            } else if (button.matches('[data-dashboard-remove]')) {
                event.preventDefault();
                remove(shell.dataset.dashboardWidgetId);
            }
        });

        document.addEventListener('change', event => {
            if (!isEditing() || !event.target.matches('[data-dashboard-size]') || !getGrid()?.contains(event.target)) return;
            const shell = event.target.closest('[data-dashboard-widget-id]');
            changeSize(shell?.dataset.dashboardWidgetId, event.target.value);
        });

        document.addEventListener('pointerdown', handlePointerDown);
        document.addEventListener('pointermove', handlePointerMove, { passive: false });
        document.addEventListener('pointerup', event => {
            if (pointerDrag?.pointerId === event.pointerId) finishPointerDrag(true);
        });
        document.addEventListener('pointercancel', event => {
            if (pointerDrag?.pointerId === event.pointerId) finishPointerDrag(false);
        });

        document.querySelectorAll('[data-dashboard-toggle]').forEach(button => {
            button.addEventListener('click', () => {
                const widgetId = button.dataset.dashboardToggle;
                if (draftWidgets.some(widget => widget.widgetId === widgetId)) remove(widgetId);
                else add(widgetId, button.dataset.dashboardDefaultSize);
            });
        });

        document.querySelector('[data-dashboard-search]')?.addEventListener('input', applyCatalogFilter);
        document.querySelectorAll('[data-dashboard-category-filter]').forEach(button => {
            button.addEventListener('click', () => {
                document.querySelectorAll('[data-dashboard-category-filter]').forEach(filter => {
                    const active = filter === button;
                    filter.classList.toggle('is-active', active);
                    filter.setAttribute('aria-pressed', String(active));
                });
                applyCatalogFilter();
            });
        });

        document.querySelector('[data-dashboard-reset]')?.addEventListener('click', async () => {
            if (!window.confirm('Vill du återställa startsidan till standardlayouten? Din personliga layout tas bort.')) return;
            try {
                setStatus('Återställer startsidan…');
                const payload = await post('/Member/ResetDashboardLayout');
                draftWidgets = (payload.widgets || []).map(widget => ({
                    widgetId: widget.widgetId,
                    sortOrder: widget.sortOrder,
                    size: widget.size
                }));
                finishSavedLayout('Startsidan har återställts.');
            } catch (error) {
                setStatus(error.message, true);
                showToast(error.message, true);
            }
        });

        document.addEventListener('keydown', event => {
            if (!isEditing()) return;
            if (event.key !== 'Escape') return;
            event.preventDefault();
            cancel();
        });

        window.addEventListener('beforeunload', event => {
            if (!isEditing() || !hasUnsavedChanges()) return;
            event.preventDefault();
            event.returnValue = '';
        });
    };

    return { init };
})();
