/* AURORA – shared UI behaviours (theme, sidebar, reveal, count-up, spotlight) */
(function () {
    var root = document.documentElement;
    var AU = window.AU = window.AU || {};

    AU.cssVar = function (n) { return getComputedStyle(root).getPropertyValue(n).trim(); };
    AU.theme = function () { return root.getAttribute('data-bs-theme') || 'dark'; };

    function setTheme(t) {
        root.setAttribute('data-bs-theme', t);
        try { localStorage.setItem('au-theme', t); } catch (e) { }
        document.querySelectorAll('[data-theme-icon]').forEach(function (i) { i.className = 'bi ' + (t === 'dark' ? 'bi-moon-stars' : 'bi-sun'); });
        document.dispatchEvent(new CustomEvent('au:theme', { detail: t }));
    }
    AU.setTheme = setTheme;

    document.addEventListener('click', function (e) {
        var tg = e.target.closest('[data-theme-toggle]');
        if (tg) { setTheme(AU.theme() === 'dark' ? 'light' : 'dark'); }

        var sb = e.target.closest('[data-sidebar-toggle]');
        if (sb) {
            var shell = document.querySelector('.ad-shell');
            if (shell) {
                shell.classList.toggle('collapsed');
                try { localStorage.setItem('au-sidebar', shell.classList.contains('collapsed') ? '1' : '0'); } catch (x) { }
            }
        }
    });

    document.addEventListener('DOMContentLoaded', function () {
        setTheme(AU.theme());

        var shell = document.querySelector('.ad-shell');
        try { if (shell && localStorage.getItem('au-sidebar') === '1') shell.classList.add('collapsed'); } catch (e) { }

        // greeting
        document.querySelectorAll('[data-greeting]').forEach(function (el) {
            var h = new Date().getHours();
            var g = h < 5 ? 'Good night' : h < 12 ? 'Good morning' : h < 18 ? 'Good afternoon' : 'Good evening';
            el.textContent = g + (el.dataset.greeting ? ', ' + el.dataset.greeting : '');
        });
        document.querySelectorAll('[data-today]').forEach(function (el) {
            el.textContent = new Date().toLocaleDateString(undefined, { weekday: 'long', month: 'long', day: 'numeric' });
        });

        // reveal on scroll (with stagger)
        var items = document.querySelectorAll('.reveal');
        if ('IntersectionObserver' in window) {
            var io = new IntersectionObserver(function (es) {
                es.forEach(function (en) { if (en.isIntersecting) { en.target.classList.add('in'); io.unobserve(en.target); } });
            }, { threshold: .08 });
            items.forEach(function (el, i) { if (!el.style.getPropertyValue('--d')) el.style.setProperty('--d', Math.min(i % 8, 7) * 70 + 'ms'); io.observe(el); });
        } else { items.forEach(function (el) { el.classList.add('in'); }); }

        // spotlight follow
        document.querySelectorAll('.spot').forEach(function (el) {
            el.addEventListener('pointermove', function (e) {
                var r = el.getBoundingClientRect();
                el.style.setProperty('--mx', (e.clientX - r.left) + 'px');
                el.style.setProperty('--my', (e.clientY - r.top) + 'px');
            });
        });

        // count-up  <span data-count="1234.5" data-prefix="$" data-decimals="2">
        document.querySelectorAll('[data-count]').forEach(function (el) {
            var end = parseFloat(el.dataset.count) || 0, dec = parseInt(el.dataset.decimals || '0', 10), pre = el.dataset.prefix || '';
            var fmt = function (v) { return pre + v.toLocaleString(undefined, { minimumFractionDigits: dec, maximumFractionDigits: dec }); };
            if (window.matchMedia('(prefers-reduced-motion: reduce)').matches) { el.textContent = fmt(end); return; }
            var t0 = null, dur = 1300;
            function step(ts) {
                if (!t0) t0 = ts;
                var p = Math.min((ts - t0) / dur, 1), eased = 1 - Math.pow(1 - p, 4);
                el.textContent = fmt(end * eased);
                if (p < 1) requestAnimationFrame(step);
            }
            requestAnimationFrame(step);
        });

        // animated bars  <i data-w="64">
        setTimeout(function () { document.querySelectorAll('.mini-bar > i[data-w]').forEach(function (b) { b.style.width = b.dataset.w + '%'; }); }, 250);

        // Ctrl/Cmd+K focuses sidebar search
        document.addEventListener('keydown', function (e) {
            if ((e.ctrlKey || e.metaKey) && e.key.toLowerCase() === 'k') {
                var s = document.getElementById('adSearch');
                if (s) { e.preventDefault(); s.focus(); }
            }
        });
    });
})();
