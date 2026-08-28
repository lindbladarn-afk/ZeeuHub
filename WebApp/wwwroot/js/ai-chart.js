// Builds accessible, responsive Intelligence charts from mixed SQL result sets.
window.ZeeUAIChart = (() => {
    const palette = [
        { fill: 'rgba(56, 189, 248, 0.62)', border: '#0284c7' },
        { fill: 'rgba(45, 212, 191, 0.56)', border: '#0f766e' },
        { fill: 'rgba(167, 139, 250, 0.56)', border: '#7c3aed' }
    ];

    const parseNumber = (value) => {
        if (typeof value === 'number') return Number.isFinite(value) ? value : null;
        if (typeof value !== 'string') return null;

        let normalized = value
            .trim()
            .replace(/[\s\u00a0]/g, '')
            .replace(/[^\d,.\-+]/g, '');
        if (!normalized || normalized === '-' || normalized === '+') return null;

        const comma = normalized.lastIndexOf(',');
        const dot = normalized.lastIndexOf('.');
        if (comma >= 0 && dot >= 0) {
            const decimalSeparator = comma > dot ? ',' : '.';
            const thousandsSeparator = decimalSeparator === ',' ? /\./g : /,/g;
            normalized = normalized.replace(thousandsSeparator, '');
            if (decimalSeparator === ',') normalized = normalized.replace(',', '.');
        } else if (comma >= 0) {
            normalized = normalized.replace(',', '.');
        }

        const parsed = Number(normalized);
        return Number.isFinite(parsed) ? parsed : null;
    };

    const isNumericColumn = (rows, index) => {
        if (!rows.length) return false;
        const values = rows.map(row => parseNumber(row?.[index]));
        const populated = values.filter(value => value !== null).length;
        return populated >= Math.max(1, Math.ceil(rows.length * 0.6));
    };

    const normalizeColumnTokens = (columnName) => String(columnName ?? '')
        .replace(/([a-zåäö])([A-ZÅÄÖ])/g, '$1 $2')
        .replace(/[\[\]_.\-]+/g, ' ')
        .trim()
        .toLocaleLowerCase('sv-SE')
        .split(/\s+/)
        .filter(Boolean);

    const identifierSuffixes = new Set([
        'id', 'pk', 'key',
        'no', 'nr', 'number', 'nummer',
        'code', 'kod'
    ]);

    const identifierEntities = new Set([
        'customer', 'kund',
        'supplier', 'leverantör', 'leverantor',
        'item', 'artikel',
        'product', 'produkt',
        'order',
        'invoice', 'faktura',
        'company', 'företag', 'foretag',
        'account', 'konto'
    ]);

    const isIdentifierColumn = (columnName) => {
        const tokens = normalizeColumnTokens(columnName);
        if (!tokens.length) return false;

        const normalized = tokens.join('');
        const lastToken = tokens[tokens.length - 1];
        return identifierSuffixes.has(lastToken) ||
            identifierEntities.has(normalized) ||
            /(?:pk|key|number|nummer|code|kod)$/.test(normalized) ||
            /^(?:customer|kund|supplier|leverantör|leverantor|item|artikel|product|produkt|order|invoice|faktura|company|företag|foretag|account|konto|user|row|record|ftg|cu|ar|su)(?:id|no|nr)$/.test(normalized);
    };

    const looksTemporal = (labels) => {
        if (!labels.length) return false;
        const temporal = labels.filter(label => {
            const value = String(label ?? '').trim();
            return /^\d{4}([-/]\d{1,2})?/.test(value) ||
                /^\d{1,2}[./-]\d{1,2}([./-]\d{2,4})?$/.test(value) ||
                /^(jan|feb|mar|apr|maj|jun|jul|aug|sep|okt|nov|dec)/i.test(value);
        }).length;
        return temporal >= Math.ceil(labels.length * 0.6);
    };

    const create = ({ canvas, placeholder, typeSelect, summary, strings = {} }) => {
        let activeChart = null;
        let current = null;

        const clear = (message) => {
            activeChart?.destroy();
            activeChart = null;
            current = null;
            if (canvas) canvas.hidden = true;
            if (placeholder) {
                placeholder.hidden = false;
                placeholder.textContent = message || strings.vizDefault || 'Ingen data att visualisera.';
            }
            if (summary) summary.textContent = '';
            if (typeSelect) typeSelect.disabled = true;
        };

        const resolveType = (requestedType, labels, datasets, preferredType) => {
            if (requestedType && requestedType !== 'auto') return requestedType;
            if (preferredType === 'comparison' || preferredType === 'bar') return 'bar';
            if (preferredType === 'line') return 'line';
            if (looksTemporal(labels)) return 'line';
            if (datasets.length === 1 && labels.length <= 6) return 'doughnut';
            if (labels.length > 10 || labels.some(label => String(label).length > 18)) return 'horizontalBar';
            return 'bar';
        };

        const renderCurrent = () => {
            if (!current || !canvas || typeof window.Chart === 'undefined') return;
            activeChart?.destroy();

            const dark = document.body.classList.contains('theme-dark');
            const textColor = dark ? '#cbd5e1' : '#334155';
            const gridColor = dark ? 'rgba(148,163,184,0.16)' : 'rgba(15,23,42,0.1)';
            const chartType = resolveType(
                typeSelect?.value || 'auto',
                current.labels,
                current.datasets,
                current.preferredType);
            const chartJsType = chartType === 'horizontalBar' ? 'bar' : chartType;
            const circular = chartJsType === 'doughnut';
            const horizontal = chartType === 'horizontalBar';
            const metricTitle = current.datasets.map(dataset => dataset.label).join(', ');

            const datasets = current.datasets.map((dataset, index) => ({
                ...dataset,
                backgroundColor: circular
                    ? current.labels.map((_, colorIndex) => palette[colorIndex % palette.length].fill)
                    : palette[index % palette.length].fill,
                borderColor: circular
                    ? current.labels.map((_, colorIndex) => palette[colorIndex % palette.length].border)
                    : palette[index % palette.length].border,
                borderWidth: 1.5,
                borderRadius: chartJsType === 'bar' ? 6 : 0,
                tension: chartJsType === 'line' ? 0.28 : 0,
                fill: false
            }));

            canvas.hidden = false;
            placeholder.hidden = true;
            activeChart = new window.Chart(canvas.getContext('2d'), {
                type: chartJsType,
                data: { labels: current.labels, datasets },
                options: {
                    responsive: true,
                    maintainAspectRatio: false,
                    indexAxis: chartType === 'horizontalBar' ? 'y' : 'x',
                    interaction: { mode: 'index', intersect: false },
                    plugins: {
                        legend: {
                            display: datasets.length > 1 || circular,
                            labels: { color: textColor, usePointStyle: true, boxWidth: 9 }
                        },
                        tooltip: {
                            callbacks: {
                                label: (context) => {
                                    const value = context.parsed?.y ?? context.parsed?.x ?? context.raw;
                                    return `${context.dataset.label}: ${new Intl.NumberFormat('sv-SE', { maximumFractionDigits: 2 }).format(value)}`;
                                }
                            }
                        }
                    },
                    scales: circular ? {} : {
                        x: {
                            beginAtZero: horizontal,
                            grid: { color: gridColor },
                            title: {
                                display: true,
                                text: horizontal ? metricTitle : current.labelName,
                                color: textColor
                            },
                            ticks: {
                                color: textColor,
                                maxRotation: chartType === 'line' ? 0 : 35,
                                callback: horizontal
                                    ? value => new Intl.NumberFormat('sv-SE', { notation: 'compact' }).format(value)
                                    : undefined
                            }
                        },
                        y: {
                            beginAtZero: !horizontal,
                            grid: { color: gridColor },
                            title: {
                                display: true,
                                text: horizontal ? current.labelName : metricTitle,
                                color: textColor
                            },
                            ticks: {
                                color: textColor,
                                callback: horizontal
                                    ? undefined
                                    : value => new Intl.NumberFormat('sv-SE', { notation: 'compact' }).format(value)
                            }
                        }
                    }
                }
            });

            if (summary) {
                const metricNames = current.datasets.map(dataset => dataset.label).join(', ');
                summary.textContent = `${current.labels.length} datapunkter. Visar ${metricNames} per ${current.labelName}.`;
            }
        };

        const render = (columns, rows, preferredType = null) => {
            if (!Array.isArray(columns) || columns.length < 2 || !Array.isArray(rows) || rows.length === 0) {
                clear(strings.vizNeedData);
                return;
            }

            const indexes = columns.map((_, index) => index);
            const numericIndexes = indexes
                .filter(index => isNumericColumn(rows, index));
            const metricIndexes = numericIndexes
                .filter(index => !isIdentifierColumn(columns[index]))
                .slice(0, 3);
            const labelIndex = indexes
                .find(index => !numericIndexes.includes(index) && !isIdentifierColumn(columns[index])) ??
                indexes.find(index => isIdentifierColumn(columns[index])) ??
                indexes.find(index => !metricIndexes.includes(index)) ??
                0;

            if (metricIndexes.length === 0) {
                clear(strings.vizNoNumeric);
                return;
            }

            if (preferredType === 'comparison' && rows.length === 1) {
                current = {
                    preferredType,
                    labelName: strings.comparisonLabel || 'Jämförelse',
                    labels: metricIndexes.map(index => String(columns[index])),
                    datasets: [{
                        label: strings.valueLabel || 'Värde',
                        data: metricIndexes.map(index => parseNumber(rows[0]?.[index]))
                    }]
                };
                if (typeSelect) typeSelect.disabled = false;
                renderCurrent();
                return;
            }

            const maxPoints = 30;
            const chartRows = rows.slice(0, maxPoints);
            current = {
                preferredType,
                labelName: String(columns[labelIndex]),
                labels: chartRows.map(row => String(row?.[labelIndex] ?? '–')),
                datasets: metricIndexes.map(index => ({
                    label: String(columns[index]),
                    data: chartRows.map(row => parseNumber(row?.[index]))
                }))
            };
            if (typeSelect) typeSelect.disabled = false;
            renderCurrent();
        };

        typeSelect?.addEventListener('change', renderCurrent);
        clear(strings.vizDefault);

        return { clear, render, refresh: renderCurrent };
    };

    return { create, parseNumber, isIdentifierColumn };
})();
