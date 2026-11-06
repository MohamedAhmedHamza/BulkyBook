/* Admin dashboard – Aurora theme-aware charts */
(function () {
    var charts = {}, state = { range: '30d' }, cache = {};
    var STATUS = { Pending: '#fbbf24', Approved: '#34d399', Processing: '#60a5fa', Shipped: '#a78bfa', Cancelled: '#fb7185', Refunded: '#f472b6', Unknown: '#94a3b8' };

    function css(n) { return AU.cssVar(n); }
    function money(v) { return '$' + Number(v || 0).toLocaleString(undefined, { maximumFractionDigits: 0 }); }
    function moneyFull(v) { return '$' + Number(v || 0).toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 }); }

    function setDefaults() {
        Chart.defaults.font.family = css('--au-font') || 'system-ui';
        Chart.defaults.color = css('--au-muted');
        Chart.defaults.animation.duration = 1100;
        Chart.defaults.animation.easing = 'easeOutQuart';
    }
    function gradient(ctx, area, c1, c2) {
        var g = ctx.createLinearGradient(0, area.top, 0, area.bottom);
        g.addColorStop(0, c1); g.addColorStop(1, c2); return g;
    }

    /* ---------- Sales studio ---------- */
    function drawSales(d) {
        var el = document.getElementById('salesChart'); if (!el) return;
        var accent = '#7c6cff', cyan = '#22d3ee';
        if (charts.sales) charts.sales.destroy();
        charts.sales = new Chart(el, {
            type: 'line',
            data: {
                labels: d.labels,
                datasets: [
                    {
                        label: 'This period', data: d.current, borderColor: cyan, borderWidth: 3, tension: .42, pointRadius: 0, pointHoverRadius: 6,
                        pointHoverBackgroundColor: '#fff', pointHoverBorderColor: cyan, pointHoverBorderWidth: 3, fill: true,
                        backgroundColor: function (c) { var a = c.chart.chartArea; return a ? gradient(c.chart.ctx, a, 'rgba(34,211,238,.38)', 'rgba(124,108,255,0)') : 'transparent'; }
                    },
                    { label: 'Previous period', data: d.previous, borderColor: css('--au-faint'), borderDash: [5, 6], borderWidth: 1.6, tension: .42, pointRadius: 0, pointHoverRadius: 4, fill: false }
                ]
            },
            options: {
                responsive: true, maintainAspectRatio: false, interaction: { mode: 'index', intersect: false },
                plugins: {
                    legend: { display: false },
                    tooltip: {
                        backgroundColor: css('--au-surface-solid'), titleColor: css('--au-text'), bodyColor: css('--au-muted'), borderColor: css('--au-border-strong'), borderWidth: 1,
                        padding: 14, cornerRadius: 14, displayColors: true, boxPadding: 5,
                        callbacks: { label: function (c) { return ' ' + c.dataset.label + ': ' + moneyFull(c.parsed.y); } }
                    }
                },
                scales: {
                    x: { grid: { display: false }, border: { display: false }, ticks: { maxTicksLimit: 9, padding: 8 } },
                    y: { beginAtZero: true, border: { display: false }, grid: { color: css('--au-chart-grid') }, ticks: { padding: 10, callback: function (v) { return money(v); }, maxTicksLimit: 6 } }
                }
            }
        });
        document.getElementById('rangeTotal').textContent = moneyFull(d.total) + ' in this range';
        var dl = document.getElementById('rangeDelta');
        if (d.deltaPct == null) { dl.innerHTML = '<span class="k-note">no earlier data to compare</span>'; }
        else {
            var up = d.deltaPct >= 0;
            dl.innerHTML = '<span class="delta ' + (up ? 'up' : 'down') + '"><i class="bi bi-arrow-' + (up ? 'up' : 'down') + '-short"></i>' + Math.abs(d.deltaPct) + '%</span><span class="k-note">vs the same stretch before</span>';
        }
    }

    function loadSales(range) {
        if (cache[range]) return Promise.resolve(cache[range]);
        return fetch('/Admin/Dashboard/GetSalesSeries?range=' + range, { credentials: 'same-origin' })
            .then(function (r) { return r.json(); }).then(function (d) { cache[range] = d; return d; });
    }

    /* ---------- KPI sparkline ---------- */
    function drawSpark(d) {
        var el = document.getElementById('sparkRevenue'); if (!el) return;
        if (charts.spark) charts.spark.destroy();
        charts.spark = new Chart(el, {
            type: 'line',
            data: { labels: d.labels, datasets: [{ data: d.current, borderColor: '#22d3ee', borderWidth: 2, tension: .45, pointRadius: 0, fill: false }] },
            options: { responsive: false, animation: { duration: 1400 }, plugins: { legend: { display: false }, tooltip: { enabled: false } }, scales: { x: { display: false }, y: { display: false, beginAtZero: true } }, events: [] }
        });
    }

    /* ---------- Status donut + category bars ---------- */
    function drawStatus(rows) {
        var el = document.getElementById('statusChart'); if (!el) return;
        if (charts.status) charts.status.destroy();
        var total = rows.reduce(function (a, r) { return a + r.count; }, 0);
        charts.status = new Chart(el, {
            type: 'doughnut',
            data: { labels: rows.map(function (r) { return r.status; }), datasets: [{ data: rows.map(function (r) { return r.count; }), backgroundColor: rows.map(function (r) { return STATUS[r.status] || STATUS.Unknown; }), borderWidth: 0, hoverOffset: 10, spacing: 3, borderRadius: 8 }] },
            options: { responsive: true, maintainAspectRatio: false, cutout: '74%', plugins: { legend: { display: false }, tooltip: { backgroundColor: css('--au-surface-solid'), titleColor: css('--au-text'), bodyColor: css('--au-muted'), borderColor: css('--au-border-strong'), borderWidth: 1, cornerRadius: 12, padding: 12 } } },
            plugins: [{
                id: 'center', afterDraw: function (c) {
                    var a = c.chartArea, x = (a.left + a.right) / 2, y = (a.top + a.bottom) / 2, g = c.ctx;
                    g.save(); g.textAlign = 'center'; g.fillStyle = css('--au-text'); g.font = '800 30px ' + css('--au-font'); g.fillText(total, x, y + 6);
                    g.fillStyle = css('--au-faint'); g.font = '600 11px ' + css('--au-font'); g.fillText('ORDERS', x, y + 26); g.restore();
                }
            }]
        });
        var lg = document.getElementById('statusLegend');
        if (lg) lg.innerHTML = rows.map(function (r) {
            return '<div class="d-flex align-items-center justify-content-between"><span class="d-flex align-items-center gap-2"><i style="width:10px;height:10px;border-radius:50%;background:' + (STATUS[r.status] || STATUS.Unknown) + ';box-shadow:0 0 10px ' + (STATUS[r.status] || STATUS.Unknown) + '"></i><span class="small fw-semibold">' + r.status + '</span></span><span class="small fw-bold">' + r.count + '</span></div>';
        }).join('') || '<div class="empty-s">No orders yet</div>';
    }
    function drawCategories(rows) {
        var box = document.getElementById('categoryBars'); if (!box) return;
        var max = Math.max.apply(null, rows.map(function (r) { return r.count; }).concat([1]));
        box.innerHTML = rows.slice(0, 7).map(function (r) {
            return '<div><div class="d-flex justify-content-between small fw-semibold"><span>' + r.category + '</span><span class="text-secondary">' + r.count + '</span></div><div class="mini-bar"><i data-w="' + Math.round(100 * r.count / max) + '"></i></div></div>';
        }).join('') || '<div class="empty-s">No categories yet</div>';
        setTimeout(function () { box.querySelectorAll('i[data-w]').forEach(function (b) { b.style.width = b.dataset.w + '%'; }); }, 200);
    }

    function boot() {
        setDefaults();
        loadSales(state.range).then(drawSales);
        loadSales('7d').then(drawSpark);
        fetch('/Admin/Dashboard/GetChartData', { credentials: 'same-origin' }).then(function (r) { return r.json(); }).then(function (d) {
            cache.chart = d; drawStatus(d.statusBreakdown); drawCategories(d.productsPerCategory);
        });
    }

    document.addEventListener('click', function (e) {
        var b = e.target.closest('#rangeSeg [data-range]'); if (!b) return;
        document.querySelectorAll('#rangeSeg [data-range]').forEach(function (x) { x.classList.toggle('active', x === b); });
        state.range = b.dataset.range;
        loadSales(state.range).then(drawSales);
    });
    document.addEventListener('au:theme', function () {
        setDefaults();
        if (cache[state.range]) drawSales(cache[state.range]);
        if (cache['7d']) drawSpark(cache['7d']);
        if (cache.chart) drawStatus(cache.chart.statusBreakdown);
    });
    $(boot);
})();
