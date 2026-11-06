/* Orders board (kanban) – staff only. Cards link to the order page. */
(function () {
    var COLS = [
        { key: 'Pending',    title: 'Awaiting payment', sub: 'Created, not paid yet',        c: '#fbbf24', match: ['Pending'] },
        { key: 'Approved',   title: 'To pack',          sub: 'Paid, waiting for hands',      c: '#34d399', match: ['Approved'] },
        { key: 'Processing', title: 'In process',       sub: 'Being prepared',               c: '#60a5fa', match: ['Processing'] },
        { key: 'Shipped',    title: 'Shipped',          sub: 'With the carrier',             c: '#a78bfa', match: ['Shipped'] },
        { key: 'Closed',     title: 'Cancelled',        sub: 'Cancelled or refunded',        c: '#fb7185', match: ['Cancelled', 'Refunded'] }
    ];
    var MAX = 10;

    function esc(s) { return String(s == null ? '' : s).replace(/[&<>"']/g, function (c) { return ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' })[c]; }); }
    function ago(iso) {
        var t = new Date(iso).getTime(); if (isNaN(t)) return '';
        var m = Math.max(0, Math.round((Date.now() - t) / 60000));
        if (m < 1) return 'just now'; if (m < 60) return m + ' min ago'; if (m < 1440) return Math.round(m / 60) + ' h ago';
        if (m < 2880) return 'Yesterday'; return new Date(iso).toLocaleDateString(undefined, { month: 'short', day: 'numeric' });
    }
    function initials(n) { return String(n || '?').split(' ').filter(Boolean).slice(0, 2).map(function (x) { return x[0]; }).join(''); }

    function render(rows) {
        var host = document.getElementById('board'); if (!host) return;
        host.innerHTML = COLS.map(function (col, ci) {
            var list = rows.filter(function (r) { return col.match.indexOf(r.orderStatus) > -1; })
                .sort(function (a, b) { return new Date(b.orderDate) - new Date(a.orderDate); });
            var cards = list.slice(0, MAX).map(function (o, i) {
                return '<a class="o-card" href="/admin/order/details?orderId=' + o.id + '">' +
                    '<div class="o-top"><span>#' + o.id + '</span><span>' + esc(ago(o.orderDate)) + '</span></div>' +
                    '<div class="o-who"><span class="av c' + ((o.id % 4) + 1) + '">' + esc(initials(o.name)) + '</span><span><b>' + esc(o.name) + '</b><small>' + esc((o.applicationUser && o.applicationUser.email) || o.phoneNumber || '') + '</small></span></div>' +
                    '<div class="o-foot"><span class="amt">$' + Number(o.orderTotal).toFixed(2) + '</span><span class="o-go"><i class="bi bi-arrow-right"></i></span></div></a>';
            }).join('') || '<div class="empty-s"><i class="bi bi-inbox"></i>Nothing here</div>';
            var more = list.length > MAX ? '<div class="b-more">+' + (list.length - MAX) + ' more · use Table view</div>' : '';
            return '<section class="b-col glass reveal in" style="--c:' + col.c + ';--d:' + (ci * 70) + 'ms">' +
                '<div class="b-head"><span class="bd"></span><span>' + col.title + '</span><span class="bc">' + list.length + '</span></div>' +
                '<div class="b-sub">' + col.sub + '</div>' + cards + more + '</section>';
        }).join('');
    }

    $(function () {
        if (!document.getElementById('board')) return;
        $.getJSON('/admin/order/Getall?status=all').done(function (res) { render(res.data || []); })
            .fail(function () { document.getElementById('board').innerHTML = '<div class="empty-s glass p-4">Could not load orders.</div>'; });
    });
})();
