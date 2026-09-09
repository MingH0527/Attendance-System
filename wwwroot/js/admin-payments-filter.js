/* =========================================================
   Live filtering for Overall Student Payments.

   Replaces only the results block, so the page never reloads
   and never jumps back to the top. The form still works
   without JavaScript - this is an enhancement on top of it.
   ========================================================= */

(function () {
    'use strict';

    var form = document.getElementById('paymentsFilters');
    var results = document.getElementById('paymentsResults');

    if (!form || !results) {
        return;
    }

    var endpoint = form.getAttribute('data-endpoint');
    var exportLink = document.getElementById('exportCsvLink');
    var searchInput = form.querySelector('input[name="search"]');
    var submitButton = form.querySelector('button[type="submit"]');

    var DEBOUNCE_MS = 300;

    var timer = null;
    var inFlight = null;
    var requestId = 0;

    /* ---------- helpers ---------- */

    function queryString(page) {
        var params = new URLSearchParams(new FormData(form));

        // Drop empties so the URL stays readable.
        var cleaned = new URLSearchParams();

        params.forEach(function (value, key) {
            if (value !== '' && value !== null) {
                cleaned.append(key, value);
            }
        });

        if (page && page !== '1') {
            cleaned.set('page', page);
        }

        return cleaned.toString();
    }

    // The export link lives outside the swapped markup, so it has to be
    // updated by hand whenever the filters change - otherwise it would
    // keep exporting whatever was filtered when the page first loaded.
    function syncExportLink() {
        if (!exportLink) {
            return;
        }

        var base = exportLink.getAttribute('data-endpoint');
        var query = queryString(null);

        exportLink.href = base + (query ? '?' + query : '');
    }

    function setBusy(busy) {
        results.classList.toggle('is-loading', busy);
    }

    function load(page, pushUrl) {
        var query = queryString(page);
        var id = ++requestId;

        // A newer keystroke makes an older request irrelevant.
        if (inFlight) {
            inFlight.abort();
        }

        var controller = new AbortController();
        inFlight = controller;

        setBusy(true);

        fetch(endpoint + (query ? '?' + query : ''), {
            headers: { 'X-Requested-With': 'XMLHttpRequest' },
            signal: controller.signal
        })
            .then(function (response) {
                if (!response.ok) {
                    throw new Error('Request failed: ' + response.status);
                }

                return response.text();
            })
            .then(function (html) {
                if (id !== requestId) {
                    return;
                }

                results.innerHTML = html;
                setBusy(false);
                syncExportLink();

                if (pushUrl !== false) {
                    var url =
                        window.location.pathname +
                        (query ? '?' + query : '');

                    window.history.replaceState({}, '', url);
                }
            })
            .catch(function (error) {
                if (error.name === 'AbortError') {
                    return;
                }

                setBusy(false);

                if (window.console) {
                    console.warn(error);
                }
            });
    }

    /* ---------- events ---------- */

    // Typing: wait until they pause, so we aren't firing per keystroke.
    if (searchInput) {
        searchInput.addEventListener('input', function () {
            clearTimeout(timer);
            timer = setTimeout(function () { load(1); }, DEBOUNCE_MS);
        });
    }

    // Dropdowns and dates: apply straight away.
    form.querySelectorAll('select, input[type="date"]')
        .forEach(function (field) {
            field.addEventListener('change', function () {
                clearTimeout(timer);
                load(1);
            });
        });

    // Enter in the search box should filter, not reload the page.
    form.addEventListener('submit', function (event) {
        event.preventDefault();
        clearTimeout(timer);
        load(1);
    });

    // Pager links are inside the swapped markup, so listen on the container.
    results.addEventListener('click', function (event) {
        var link = event.target.closest('a.apd-page');

        if (!link) {
            return;
        }

        event.preventDefault();

        var page =
            new URL(link.href, window.location.origin)
                .searchParams.get('page') || '1';

        load(page);
    });

    syncExportLink();

    // The button is redundant once live filtering is on.
    if (submitButton) {
        submitButton.hidden = true;
    }
})();