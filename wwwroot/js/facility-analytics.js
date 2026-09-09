(() => {
    const source = document.getElementById('facilityAnalyticsData');
    if (!source || typeof Chart === 'undefined') return;

    const data = JSON.parse(source.textContent || '{}');
    const palette = ['#315ee7', '#14a6b8', '#16a085', '#f59e0b', '#ef5f76', '#7c4fd7', '#64748b', '#0ea5e9'];

    Chart.defaults.font.family = 'Inter, system-ui, -apple-system, BlinkMacSystemFont, "Segoe UI", sans-serif';
    Chart.defaults.color = '#718096';
    Chart.defaults.borderColor = 'rgba(148, 163, 184, .18)';

    const common = {
        responsive: true,
        maintainAspectRatio: false,
        interaction: { mode: 'index', intersect: false },
        plugins: {
            legend: { labels: { usePointStyle: true, boxWidth: 8, boxHeight: 8, padding: 14 } },
            tooltip: { padding: 11, usePointStyle: true }
        },
        scales: {
            x: { grid: { display: false }, ticks: { maxRotation: 0, autoSkip: true } },
            y: { beginAtZero: true, ticks: { precision: 0 } }
        }
    };

    const clone = obj => JSON.parse(JSON.stringify(obj));
    const labels = rows => (rows || []).map(x => x.label);
    const values = rows => (rows || []).map(x => x.value);

    function hasAny(rows) {
        return (rows || []).some(x => Number(x.value) > 0 || Number(x.count) > 0);
    }

    function empty(canvasId, message = 'No facility booking data is available for this chart yet.') {
        const canvas = document.getElementById(canvasId);
        if (!canvas) return;
        const parent = canvas.parentElement;
        canvas.remove();
        const box = document.createElement('div');
        box.className = 'fa-empty-chart';
        box.textContent = message;
        parent.appendChild(box);
    }

    function line(canvasId, rows, label, suffix = '', fill = false) {
        if (!hasAny(rows)) return empty(canvasId);
        const options = clone(common);
        options.plugins.legend.display = false;
        options.plugins.tooltip.callbacks = {
            label: ctx => `${label}: ${ctx.parsed.y}${suffix}`
        };
        new Chart(document.getElementById(canvasId), {
            type: 'line',
            data: {
                labels: labels(rows),
                datasets: [{
                    label,
                    data: values(rows),
                    borderColor: palette[0],
                    backgroundColor: fill ? 'rgba(49,94,231,.12)' : 'rgba(49,94,231,.04)',
                    fill,
                    borderWidth: 3,
                    tension: .36,
                    pointRadius: 3,
                    pointHoverRadius: 5
                }]
            },
            options
        });
    }

    function bar(canvasId, rows, label, suffix = '', horizontal = false) {
        if (!hasAny(rows)) return empty(canvasId);
        const options = clone(common);
        options.indexAxis = horizontal ? 'y' : 'x';
        options.plugins.legend.display = false;
        options.plugins.tooltip.callbacks = { label: ctx => `${label}: ${horizontal ? ctx.parsed.x : ctx.parsed.y}${suffix}` };
        new Chart(document.getElementById(canvasId), {
            type: 'bar',
            data: {
                labels: labels(rows),
                datasets: [{
                    label,
                    data: values(rows),
                    backgroundColor: rows.map((_, i) => palette[i % palette.length]),
                    borderRadius: 8,
                    borderSkipped: false,
                    maxBarThickness: 48
                }]
            },
            options
        });
    }

    function doughnut(canvasId, rows, label, pie = false) {
        if (!hasAny(rows)) return empty(canvasId);
        new Chart(document.getElementById(canvasId), {
            type: pie ? 'pie' : 'doughnut',
            data: {
                labels: labels(rows),
                datasets: [{
                    label,
                    data: values(rows),
                    backgroundColor: rows.map((_, i) => palette[i % palette.length]),
                    borderWidth: 0,
                    hoverOffset: 6
                }]
            },
            options: {
                responsive: true,
                maintainAspectRatio: false,
                cutout: pie ? 0 : '62%',
                plugins: {
                    legend: { position: 'right', labels: { usePointStyle: true, boxWidth: 8, padding: 14 } },
                    tooltip: {
                        padding: 11,
                        callbacks: {
                            label: ctx => {
                                const total = ctx.dataset.data.reduce((a, b) => Number(a) + Number(b), 0);
                                const pct = total ? (Number(ctx.raw) * 100 / total).toFixed(1) : '0.0';
                                return `${ctx.label}: ${ctx.raw} (${pct}%)`;
                            }
                        }
                    }
                }
            }
        });
    }

    function multiLine(canvasId, chartData, valueLabel = 'Bookings') {
        const series = chartData?.series || [];
        const hasData = series.some(s => (s.values || []).some(v => Number(v) > 0));
        if (!hasData) return empty(canvasId);

        const options = clone(common);
        options.plugins.legend.position = 'top';
        options.plugins.tooltip.callbacks = { label: ctx => `${ctx.dataset.label}: ${ctx.parsed.y} ${valueLabel.toLowerCase()}` };

        new Chart(document.getElementById(canvasId), {
            type: 'line',
            data: {
                labels: chartData.labels || [],
                datasets: series.map((s, i) => ({
                    label: s.name,
                    data: s.values,
                    borderColor: palette[i % palette.length],
                    backgroundColor: palette[i % palette.length],
                    tension: .34,
                    borderWidth: 2.6,
                    pointRadius: 2.5,
                    pointHoverRadius: 5
                }))
            },
            options
        });
    }

    line('chartLast7', data.last7, 'Booking hours', ' hrs', true);
    bar('chartMonthlyHours', data.monthly, 'Booking hours', ' hrs');
    line('chartAllTimeHours', data.allTime, 'Booking hours', ' hrs', true);
    doughnut('chartStatusDistribution', data.statusDistribution, 'Bookings');
    multiLine('chartStatusTrend', data.statusTrend, 'Bookings');
    doughnut('chartCheckIn', data.checkIn, 'Bookings');
    bar('chartPeakHours', data.peakHours, 'Bookings');
    bar('chartDuration', data.duration, 'Bookings');
    bar('chartFacilityUsage', data.facilityUsage, 'Booking hours', ' hrs');

    const topFacilities = (data.topFacilities || []).map(x => ({
        label: `${x.label} (${x.hours} hrs)`,
        value: x.count,
        count: x.count
    }));
    bar('chartTopFacilities', topFacilities, 'Bookings', '', true);

    doughnut('chartBookingShare', data.bookingShare, 'Bookings', true);
    multiLine('chartFacilityLast7', data.facilityLast7, 'Bookings');
    multiLine('chartFacilityMonthly', data.facilityMonthly, 'Bookings');
    multiLine('chartFacilityAllTime', data.facilityAllTime, 'Bookings');

    const topStudents = (data.topStudents || []).map(x => ({
        label: `${x.label} (${x.count} bookings)`,
        value: x.hours,
        count: x.count
    }));
    bar('chartTopStudents', topStudents, 'Booking hours', ' hrs', true);

    doughnut('chartLeadTime', data.leadTime, 'Bookings', true);
})();
