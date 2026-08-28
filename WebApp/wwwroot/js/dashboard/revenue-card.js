// Client-side rendering for the dashboard revenue chart. The server owns periods/copy so UI and data stay aligned.
window.ZeeUDashboardRevenueCard = (function () {
    let revenueChart = null;
    let miniCharts = [];
    let currentPeriod = 'week';
    let themeObserver = null;

    const getCard = () => document.querySelector('.revenue-chart-card');

    const getLocale = () => (document.documentElement.lang || '').startsWith('en') ? 'en-US' : 'sv-SE';

    const getCopy = () => {
        const card = getCard();
        return {
            chartLabel: card?.dataset.chartLabel || 'Omsättning',
            weekLabel: card?.dataset.weekLabel || 'Vecka',
            monthLabel: card?.dataset.monthLabel || 'Månad',
            quarterLabel: card?.dataset.quarterLabel || 'Kvartal',
            allLabel: card?.dataset.allLabel || 'Totalt',
            weekDescription: card?.dataset.weekDescription || '',
            monthDescription: card?.dataset.monthDescription || '',
            quarterDescription: card?.dataset.quarterDescription || '',
            allDescription: card?.dataset.allDescription || '',
            unitMsek: card?.dataset.unitMsek || 'Mkr',
            unitKsek: card?.dataset.unitKsek || 'tkr',
            unitSek: card?.dataset.unitSek || 'kr'
        };
    };

    const getPayload = () => {
        const source = document.getElementById('dashboardRevenueData');
        if (!source?.textContent) return null;

        try {
            return JSON.parse(source.textContent);
        } catch {
            return null;
        }
    };

    const ensureSeries = (series) => {
        if (!series || !series.labels || series.labels.length === 0) {
            return { labels: ['-'], values: [0], xTitle: series?.xTitle ?? '' };
        }

        return series;
    };

    const normalizeSeries = (series) => {
        const safe = ensureSeries(series);
        return {
            labels: safe.labels,
            data: safe.values || safe.data || [],
            xTitle: safe.xTitle || ''
        };
    };

    const chooseChartUnit = (maxValMsek, copy) => {
        if (maxValMsek >= 5) return { label: copy.unitMsek, factor: 1, digits: 1 };
        if (maxValMsek >= 0.005) return { label: copy.unitKsek, factor: 1000, digits: 0 };
        return { label: copy.unitSek, factor: 1000000, digits: 0 };
    };

    const chooseSummaryUnit = (totalValMsek, maxValMsek, copy) => {
        if (Math.max(totalValMsek, maxValMsek) >= 1) return { label: copy.unitMsek, factor: 1, digits: 1 };
        if (Math.max(totalValMsek, maxValMsek) >= 0.01) return { label: copy.unitKsek, factor: 1000, digits: 0 };
        return { label: copy.unitSek, factor: 1000000, digits: 0 };
    };

    const formatNumber = (value, digits) => (value ?? 0).toLocaleString(getLocale(), {
        minimumFractionDigits: digits,
        maximumFractionDigits: digits
    });

    const formatMetricValue = (valueMsek, unit) => `${formatNumber((Number(valueMsek) || 0) * unit.factor, unit.digits)} ${unit.label}`;

    const getXAxisTickStep = (chartWidth, labelCount, period) => {
        if (labelCount <= 0) return 1;

        if (period === 'week') {
            if (chartWidth <= 360) return 4;
            if (chartWidth <= 460) return 3;
            if (chartWidth <= 560) return 2;
            return 1;
        }

        if (period === 'month') {
            if (chartWidth <= 380 && labelCount > 6) return 2;
            if (chartWidth <= 500 && labelCount > 8) return 2;
        }

        return 1;
    };

    const formatXAxisTickLabel = (label, index, labels, chartWidth, period) => {
        const tickStep = getXAxisTickStep(chartWidth, labels.length, period);
        if (index % tickStep !== 0) {
            return '';
        }

        const safeLabel = String(label ?? '');
        if (period === 'month') {
            const parts = safeLabel.split(' ');
            if (parts.length >= 2) {
                const year = parts[parts.length - 1];
                const month = parts.slice(0, -1).join(' ');

                if (chartWidth <= 420) {
                    return month;
                }

                return [month, year];
            }

            return safeLabel;
        }

        if (period !== 'week') {
            return safeLabel;
        }

        const parts = safeLabel.split(' • ');
        if (parts.length !== 2) {
            return safeLabel;
        }

        if (chartWidth <= 420) {
            return parts[0];
        }

        return [parts[0], parts[1]];
    };

    const getXAxisTickFontSize = (chartWidth, period) => {
        if (period === 'week') {
            if (chartWidth <= 420) return 10;
            if (chartWidth <= 560) return 11;
        }

        if (period === 'month') {
            if (chartWidth <= 420) return 10;
            if (chartWidth <= 560) return 11;
        }

        if (chartWidth <= 420) return 10;
        return 12;
    };

    const getChartFontFamily = (canvas) => {
        const source = canvas?.closest('.dashboard-readable') || canvas?.closest('.card') || document.body;
        return window.getComputedStyle(source).fontFamily || 'inherit';
    };

    const getTooltipElement = (chart) => {
        const parent = chart.canvas.parentNode;
        if (!parent) return null;

        let tooltipEl = parent.querySelector('.rev-chart-tooltip');
        if (!tooltipEl) {
            tooltipEl = document.createElement('div');
            tooltipEl.className = 'rev-chart-tooltip';
            parent.appendChild(tooltipEl);
        }

        return tooltipEl;
    };

    const renderExternalTooltip = (context, copy, unit) => {
        const { chart, tooltip } = context;
        const tooltipEl = getTooltipElement(chart);
        if (!tooltipEl) return;

        if (!tooltip || tooltip.opacity === 0 || !tooltip.dataPoints?.length) {
            tooltipEl.classList.remove('is-visible');
            tooltipEl.setAttribute('aria-hidden', 'true');
            return;
        }

        const point = tooltip.dataPoints[0];
        const rawValue = Number(point.dataset.rawValuesMsek?.[point.dataIndex] ?? 0);

        tooltipEl.innerHTML = `
            <div class="rev-chart-tooltip-label">${point.label}</div>
            <div class="rev-chart-tooltip-metric">${copy.chartLabel}</div>
            <div class="rev-chart-tooltip-value">${formatMetricValue(rawValue, unit)}</div>
        `;

        const parent = chart.canvas.parentNode;
        const tooltipWidth = tooltipEl.offsetWidth;
        const tooltipHeight = tooltipEl.offsetHeight;
        const nextLeft = chart.canvas.offsetLeft + tooltip.caretX + 18;
        const nextTop = chart.canvas.offsetTop + tooltip.caretY - (tooltipHeight / 2);
        const safeLeft = Math.max(12, Math.min(nextLeft, parent.clientWidth - tooltipWidth - 12));
        const safeTop = Math.max(12, Math.min(nextTop, parent.clientHeight - tooltipHeight - 12));

        tooltipEl.style.left = `${safeLeft}px`;
        tooltipEl.style.top = `${safeTop}px`;
        tooltipEl.classList.add('is-visible');
        tooltipEl.setAttribute('aria-hidden', 'false');
    };

    const normalizeMiniChartValues = (values) => {
        const numericValues = (values || []).map((value) => Number(value) || 0);
        if (numericValues.length === 0) {
            return { values: [0], min: 0, max: 1 };
        }

        const minValue = Math.min(...numericValues);
        const maxValue = Math.max(...numericValues);
        const range = maxValue - minValue;

        if (range <= 0) {
            return {
                values: numericValues.map(() => 0),
                min: 0,
                max: 1
            };
        }

        return {
            values: numericValues.map((value) => (value - minValue) / range),
            min: 0,
            max: 1
        };
    };

    const renderMiniChart = (id, labels, data, color) => {
        const canvas = document.getElementById(id);
        if (!canvas || !data || data.length === 0) return;

        const ctx = canvas.getContext('2d');
        const gradient = ctx.createLinearGradient(0, 0, 0, 80);
        gradient.addColorStop(0, `${color}44`);
        gradient.addColorStop(1, `${color}00`);
        const chartSeries = normalizeMiniChartValues(data);

        const chart = new Chart(canvas, {
            type: 'line',
            data: {
                labels: labels || [],
                datasets: [{
                    data: chartSeries.values,
                    fill: true,
                    backgroundColor: gradient,
                    borderColor: color,
                    borderWidth: 2.5,
                    pointRadius: 0,
                    tension: 0.4,
                    cubicInterpolationMode: 'monotone',
                    borderCapStyle: 'round',
                    borderJoinStyle: 'round'
                }]
            },
            options: {
                layout: {
                    padding: { top: 4, right: 10, bottom: 0, left: 4 }
                },
                plugins: { legend: { display: false }, tooltip: { enabled: false } },
                scales: {
                    x: { display: false },
                    y: {
                        display: false,
                        min: chartSeries.min,
                        max: chartSeries.max,
                        beginAtZero: true
                    }
                },
                maintainAspectRatio: false,
                responsive: true,
                elements: {
                    line: {
                        borderCapStyle: 'round',
                        borderJoinStyle: 'round'
                    }
                }
            }
        });
        miniCharts.push(chart);
    };

    const updateSummary = (series, summaryUnit, chartUnit) => {
        const totalEl = document.getElementById('revSelectedTotal');
        const averageEl = document.getElementById('revSelectedAverage');
        const bestLabelEl = document.getElementById('revSelectedBestLabel');
        const bestValueEl = document.getElementById('revSelectedBestValue');
        const axisTitleEl = document.getElementById('revActiveAxisTitle');
        const unitEl = document.getElementById('revActiveUnit');
        const periodDescriptionEl = document.getElementById('revActivePeriodDescription');

        const values = series.data.map((value) => Number(value) || 0);
        const total = values.reduce((sum, value) => sum + value, 0);
        const avg = values.length > 0 ? total / values.length : 0;
        const bestValue = values.length > 0 ? Math.max(...values) : 0;
        const bestIndex = values.findIndex((value) => value === bestValue);
        const bestLabel = bestIndex >= 0 ? series.labels[bestIndex] : '–';

        if (totalEl) totalEl.textContent = formatMetricValue(total, summaryUnit);
        if (averageEl) averageEl.textContent = formatMetricValue(avg, summaryUnit);
        if (bestLabelEl) bestLabelEl.textContent = bestLabel || '–';
        if (bestValueEl) bestValueEl.textContent = formatMetricValue(bestValue, summaryUnit);
        if (axisTitleEl) axisTitleEl.textContent = series.xTitle || '–';
        if (unitEl) unitEl.textContent = chartUnit.label;
        if (periodDescriptionEl) {
            const copy = getCopy();
            const descriptions = {
                week: copy.weekDescription,
                month: copy.monthDescription,
                quarter: copy.quarterDescription,
                all: copy.allDescription
            };

            periodDescriptionEl.textContent = descriptions[currentPeriod] || '';
        }
    };

    const renderRevenueChart = (data) => {
        const canvas = document.getElementById('chartRevenue');
        if (!canvas || !data) return;

        const copy = getCopy();
        const weekSeries = { ...normalizeSeries(data.week), xTitle: copy.weekLabel };
        const monthSeries = { ...normalizeSeries(data.month), xTitle: copy.monthLabel };
        const quarterSeries = { ...normalizeSeries(data.quarter), xTitle: copy.quarterLabel };
        const totalValue = Number(data.totalRevenueMsek ?? 0) > 0
            ? Number(data.totalRevenueMsek)
            : (weekSeries.data || []).reduce((sum, value) => sum + (Number(value) || 0), 0);

        const revenueData = {
            week: weekSeries,
            month: monthSeries,
            quarter: quarterSeries,
            all: {
                labels: [copy.allLabel],
                data: [totalValue],
                xTitle: copy.allLabel
            }
        };

        const series = revenueData[currentPeriod];
        if (!series) return;

        if (revenueChart) {
            revenueChart.destroy();
        }

        const isDark = document.body.classList.contains('theme-dark') || document.documentElement.classList.contains('theme-dark');
        const axisColor = isDark ? '#cbd5e1' : '#334155';
        const gridY = isDark ? 'rgba(148, 163, 184, 0.16)' : 'rgba(15, 23, 42, 0.08)';
        const gridX = isDark ? 'rgba(148, 163, 184, 0.08)' : 'rgba(15, 23, 42, 0.05)';
        const chartFontFamily = getChartFontFamily(canvas);
        const chartWidth = canvas.parentElement?.clientWidth || canvas.clientWidth || 0;
        const numericValues = series.data.map((value) => Number(value) || 0);
        const maxVal = Math.max(...numericValues, 0);
        const totalVal = numericValues.reduce((sum, value) => sum + value, 0);
        const chartUnit = chooseChartUnit(maxVal, copy);
        const summaryUnit = chooseSummaryUnit(totalVal, maxVal, copy);
        const rawValuesMsek = series.data.map((value) => Number(value) || 0);
        const scaledValues = rawValuesMsek.map((value) => value * chartUnit.factor);

        updateSummary(series, summaryUnit, chartUnit);

        revenueChart = new Chart(canvas, {
            type: 'bar',
            data: {
                labels: series.labels,
                datasets: [{
                    label: copy.chartLabel,
                    data: scaledValues,
                    rawValuesMsek,
                    backgroundColor: isDark ? 'rgba(59, 130, 246, 0.82)' : 'rgba(37, 99, 235, 0.82)',
                    hoverBackgroundColor: isDark ? '#60a5fa' : '#3b82f6',
                    borderColor: isDark ? 'rgba(96, 165, 250, 0.95)' : 'rgba(37, 99, 235, 0.95)',
                    borderWidth: 1,
                    borderRadius: 10,
                    borderSkipped: false,
                    categoryPercentage: 0.72,
                    barPercentage: 0.9,
                    maxBarThickness: 56
                }]
            },
            options: {
                responsive: true,
                maintainAspectRatio: false,
                font: { family: chartFontFamily },
                interaction: {
                    mode: 'nearest',
                    axis: 'x',
                    intersect: false
                },
                layout: {
                    padding: {
                        top: 6,
                        right: 10,
                        bottom: 0,
                        left: 2
                    }
                },
                onHover: (event, elements, chart) => {
                    chart.canvas.style.cursor = elements.length ? 'pointer' : 'default';
                },
                plugins: {
                    legend: { display: false },
                    tooltip: {
                        enabled: false,
                        external: (context) => renderExternalTooltip(context, copy, chartUnit)
                    }
                },
                scales: {
                    y: {
                        beginAtZero: true,
                        min: 0,
                        grace: maxVal > 0 ? '4%' : 0,
                        border: { display: false },
                        title: {
                            display: true,
                            text: chartUnit.label,
                            color: axisColor,
                            font: { family: chartFontFamily, size: 12, weight: '700' },
                            padding: { bottom: 12 }
                        },
                        grid: {
                            color: gridY,
                            drawBorder: false,
                            tickLength: 0
                        },
                        ticks: {
                            color: axisColor,
                            font: { family: chartFontFamily, size: 12, weight: '600' },
                            padding: 10,
                            maxTicksLimit: 6,
                            callback: (value) => formatNumber(value, chartUnit.digits)
                        }
                    },
                    x: {
                        border: { display: false },
                        title: {
                            display: true,
                            text: series.xTitle,
                            color: axisColor,
                            font: { family: chartFontFamily, size: 12, weight: '700' },
                            padding: { top: 12 }
                        },
                        grid: {
                            color: gridX,
                            drawBorder: false
                        },
                        ticks: {
                            color: axisColor,
                            font: { family: chartFontFamily, size: getXAxisTickFontSize(chartWidth, currentPeriod), weight: '600' },
                            padding: 8,
                            maxRotation: 0,
                            minRotation: 0,
                            autoSkip: false,
                            callback: (value, index, ticks) => {
                                const label = series.labels[index] ?? ticks?.[index]?.label ?? '';
                                return formatXAxisTickLabel(label, index, series.labels, chartWidth, currentPeriod);
                            }
                        }
                    }
                }
            }
        });
    };

    const initPeriodButtons = (payload) => {
        const buttons = document.querySelectorAll('[data-rev-period]');
        buttons.forEach((button) => {
            button.addEventListener('click', () => {
                buttons.forEach((btn) => {
                    btn.classList.remove('active');
                    btn.setAttribute('aria-pressed', 'false');
                });

                button.classList.add('active');
                button.setAttribute('aria-pressed', 'true');
                currentPeriod = button.dataset.revPeriod || 'week';
                renderRevenueChart(payload);
            });
        });
    };

    const init = () => {
        miniCharts.forEach(chart => chart.destroy());
        miniCharts = [];
        const payload = getPayload();
        if (!payload) {
            revenueChart?.destroy();
            revenueChart = null;
            return;
        }

        renderMiniChart('chartKpiRevenue', payload.month?.labels, payload.month?.values, '#3b82f6');
        renderMiniChart('chartKpiAov', payload.aovLabels, payload.aovValues, '#10b981');
        initPeriodButtons(payload);
        renderRevenueChart(payload);

        if (!themeObserver) {
            themeObserver = new MutationObserver(() => {
                const currentPayload = getPayload();
                if (currentPayload) renderRevenueChart(currentPayload);
            });
            themeObserver.observe(document.body, { attributes: true, attributeFilter: ['class'] });
        }
    };

    return { init };
})();
