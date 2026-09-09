/* =========================================================
   Chart tooltips for the admin Payment Insights section.

   Replaces the browser's native title attribute, which is slow
   to appear, unstyled, and cannot be shown on SVG donut slices
   in a useful way. One tooltip element is reused for every
   chart and follows the cursor.
   ========================================================= */

(function () {
    'use strict';

    var HOST_SELECTOR = '[data-tip]';

    var tip = null;
    var current = null;

    function ensureTip() {
        if (tip) {
            return tip;
        }

        tip = document.createElement('div');
        tip.className = 'apd-tip';
        tip.setAttribute('role', 'tooltip');
        tip.innerHTML =
            '<span class="apd-tip-title"></span>' +
            '<span class="apd-tip-value"></span>';

        document.body.appendChild(tip);

        return tip;
    }

    function show(host) {
        var el = ensureTip();

        el.querySelector('.apd-tip-title').textContent =
            host.getAttribute('data-tip-title') || '';

        el.querySelector('.apd-tip-value').textContent =
            host.getAttribute('data-tip') || '';

        el.classList.add('is-open');
        current = host;
    }

    function hide() {
        if (tip) {
            tip.classList.remove('is-open');
        }

        current = null;
    }

    function move(event) {
        if (!tip || !current) {
            return;
        }

        var pad = 14;
        var rect = tip.getBoundingClientRect();

        var x = event.clientX + pad;
        var y = event.clientY - rect.height - pad;

        // Keep it on screen near the edges.
        if (x + rect.width > window.innerWidth - 8) {
            x = event.clientX - rect.width - pad;
        }

        if (y < 8) {
            y = event.clientY + pad;
        }

        tip.style.transform =
            'translate(' + Math.round(x) + 'px,' + Math.round(y) + 'px)';
    }

    /* Delegated, so it also covers markup swapped in by the
       live-filtering script without needing to re-bind. */

    document.addEventListener('mouseover', function (event) {
        var host = event.target.closest
            ? event.target.closest(HOST_SELECTOR)
            : null;

        // SVG elements in older engines lack closest(); walk up manually.
        if (!host && event.target.correspondingUseElement === undefined) {
            var node = event.target;

            while (node && node !== document) {
                if (node.getAttribute && node.getAttribute('data-tip')) {
                    host = node;
                    break;
                }

                node = node.parentNode;
            }
        }

        if (host && host !== current) {
            show(host);
            move(event);
        }
    });

    document.addEventListener('mouseout', function (event) {
        if (!current) {
            return;
        }

        var to = event.relatedTarget;

        if (!to || !current.contains || !current.contains(to)) {
            hide();
        }
    });

    document.addEventListener('mousemove', move);

    // Scrolling with a tooltip open leaves it stranded.
    window.addEventListener('scroll', hide, true);
})();