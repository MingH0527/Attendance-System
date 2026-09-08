/* =========================================================
   Student portal translation.

   The site is authored in English. When a student picks Malay
   or Chinese, this script collects the visible strings, sends
   them to /Language/Translate (Google Cloud Translation API)
   and swaps them in place. Results are cached in sessionStorage
   per language, and again server-side, so repeat visits are
   mostly free.
   ========================================================= */

(function () {
    'use strict';

    var config = window.appTranslation;

    if (!config) {
        return;
    }

    var SKIP_TAGS = {
        SCRIPT: 1, STYLE: 1, NOSCRIPT: 1, TEXTAREA: 1,
        CODE: 1, PRE: 1, SVG: 1, CANVAS: 1, IFRAME: 1
    };

    var ATTRIBUTES = ['placeholder', 'title', 'aria-label'];

    var CHUNK_SIZE = 60;
    var REVEAL_TIMEOUT_MS = 3000;

    var applying = false;
    var observer = null;

    /* ---------------- helpers ---------------- */

    function antiForgeryToken() {
        var field = document.querySelector(
            '#languageTokenForm input[name="__RequestVerificationToken"]');

        return field ? field.value : '';
    }

    // Collapse newlines / repeated spaces. A phrase written across several
    // lines in a .cshtml arrives here with the line breaks intact, so the
    // dictionary key must be built from the normalised form.
    function normalise(text) {
        return (text || '').trim().replace(/\s+/g, ' ');
    }

    function isTranslatable(text) {
        var trimmed = (text || '').trim();

        if (trimmed.length < 2) {
            return false;
        }

        // The source language is English, so anything without a
        // latin letter (numbers, dates, RM amounts, IDs) is skipped.
        return /[A-Za-z]/.test(trimmed);
    }

    function isInsideSkippedElement(element) {
        var current = element;

        while (current && current.nodeType === 1) {
            if (SKIP_TAGS[current.tagName]) {
                return true;
            }

            if (current.hasAttribute('data-no-translate')) {
                return true;
            }

            if (current.classList &&
                current.classList.contains('notranslate')) {
                return true;
            }

            current = current.parentElement;
        }

        return false;
    }

    function cacheGet(text) {
        try {
            return sessionStorage.getItem('tr:' + config.current + ':' + text);
        } catch (e) {
            return null;
        }
    }

    function cacheSet(text, value) {
        try {
            sessionStorage.setItem(
                'tr:' + config.current + ':' + text, value);
        } catch (e) {
            /* quota exceeded - carry on without the cache */
        }
    }

    /* ---------------- collection ---------------- */

    function collect(root) {
        var textNodes = [];
        var attributeTargets = [];

        var walker = document.createTreeWalker(
            root, NodeFilter.SHOW_TEXT, null);

        var node;

        while ((node = walker.nextNode())) {
            if (node.trDone) {
                continue;
            }

            if (!isTranslatable(node.nodeValue)) {
                continue;
            }

            if (isInsideSkippedElement(node.parentElement)) {
                continue;
            }

            textNodes.push(node);
        }

        var scope = [];

        if (root.nodeType === 1) {
            scope.push(root);
        }

        var selector = ATTRIBUTES
            .map(function (name) { return '[' + name + ']'; })
            .join(',');

        var found = root.querySelectorAll
            ? root.querySelectorAll(selector)
            : [];

        for (var i = 0; i < found.length; i++) {
            scope.push(found[i]);
        }

        scope.forEach(function (element) {
            if (isInsideSkippedElement(element)) {
                return;
            }

            ATTRIBUTES.forEach(function (name) {
                if (!element.hasAttribute(name)) {
                    return;
                }

                var value = element.getAttribute(name);

                if (!isTranslatable(value)) {
                    return;
                }

                if (element.getAttribute('data-tr-' + name) === '1') {
                    return;
                }

                attributeTargets.push({
                    element: element,
                    name: name,
                    value: value
                });
            });
        });

        return { textNodes: textNodes, attributes: attributeTargets };
    }

    /* ---------------- application ---------------- */

    function applyTranslations(items, map) {
        applying = true;

        if (observer) {
            observer.disconnect();
        }

        items.textNodes.forEach(function (node) {
            var original = node.nodeValue.trim();
            var replacement = map[normalise(node.nodeValue)];

            if (!replacement) {
                return;
            }

            // Preserve the surrounding whitespace of the original node.
            node.nodeValue = node.nodeValue.replace(
                original,
                function () { return replacement; });

            node.trDone = true;
        });

        items.attributes.forEach(function (target) {
            var replacement = map[normalise(target.value)];

            if (!replacement) {
                return;
            }

            target.element.setAttribute(target.name, replacement);
            target.element.setAttribute('data-tr-' + target.name, '1');
        });

        applying = false;

        if (observer) {
            startObserving();
        }
    }

    function requestTranslations(texts) {
        return fetch(config.translateUrl, {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
                'RequestVerificationToken': antiForgeryToken()
            },
            body: JSON.stringify({
                language: config.current,
                texts: texts
            })
        }).then(function (response) {
            if (!response.ok) {
                throw new Error('Translate failed: ' + response.status);
            }

            return response.json();
        });
    }

    function translateRoot(root) {
        var items = collect(root);

        if (items.textNodes.length === 0 &&
            items.attributes.length === 0) {
            return Promise.resolve();
        }

        var map = {};
        var missing = [];

        function consider(text) {
            var key = normalise(text);

            if (map[key] !== undefined ||
                missing.indexOf(key) !== -1) {
                return;
            }

            var cached = cacheGet(key);

            if (cached !== null) {
                map[key] = cached;
            } else {
                missing.push(key);
            }
        }

        items.textNodes.forEach(function (node) {
            consider(node.nodeValue);
        });

        items.attributes.forEach(function (target) {
            consider(target.value);
        });

        if (missing.length === 0) {
            applyTranslations(items, map);
            return Promise.resolve();
        }

        var chunks = [];

        for (var i = 0; i < missing.length; i += CHUNK_SIZE) {
            chunks.push(missing.slice(i, i + CHUNK_SIZE));
        }

        var sequence = Promise.resolve();

        chunks.forEach(function (chunk) {
            sequence = sequence.then(function () {
                return requestTranslations(chunk)
                    .then(function (data) {
                        var results = (data && data.translations) || [];

                        for (var j = 0; j < chunk.length; j++) {
                            var value = results[j];

                            if (typeof value === 'string' && value.length) {
                                map[chunk[j]] = value;

                                // Only cache real translations. An untranslated
                                // string comes back unchanged, and caching that
                                // would hide it after the dictionary is updated.
                                if (value !== chunk[j]) {
                                    cacheSet(chunk[j], value);
                                }
                            }
                        }
                    });
            });
        });

        return sequence
            .catch(function (error) {
                // Leave the page in English rather than half-broken.
                if (window.console) {
                    console.warn(error);
                }
            })
            .then(function () {
                applyTranslations(items, map);
            });
    }

    /* ---------------- dynamic content ---------------- */

    function startObserving() {
        observer.observe(document.body, {
            childList: true,
            subtree: true
        });
    }

    function watchForNewContent() {
        if (!window.MutationObserver) {
            return;
        }

        var queue = [];
        var timer = null;

        observer = new MutationObserver(function (mutations) {
            if (applying) {
                return;
            }

            mutations.forEach(function (mutation) {
                for (var i = 0; i < mutation.addedNodes.length; i++) {
                    var node = mutation.addedNodes[i];

                    if (node.nodeType === 1) {
                        queue.push(node);
                    }
                }
            });

            if (queue.length === 0 || timer) {
                return;
            }

            timer = setTimeout(function () {
                timer = null;

                var batch = queue.splice(0, queue.length);

                batch.forEach(function (element) {
                    if (element.isConnected) {
                        translateRoot(element);
                    }
                });
            }, 350);
        });

        startObserving();
    }

    /* ---------------- flash prevention ---------------- */

    function hideUntilReady() {
        var style = document.createElement('style');

        style.textContent =
            '.tr-pending .topbar,' +
            '.tr-pending .sidebar,' +
            '.tr-pending .content,' +
            '.tr-pending .program-page { visibility: hidden; }';

        document.head.appendChild(style);
        document.documentElement.classList.add('tr-pending');

        // Failsafe: never leave the page hidden if the API stalls.
        setTimeout(reveal, REVEAL_TIMEOUT_MS);
    }

    function reveal() {
        document.documentElement.classList.remove('tr-pending');
    }

    /* ---------------- cooldown notice ---------------- */

    var COOLDOWN_MS = 10000;
    var COOLDOWN_KEY = 'langSwitchedAt';

    function ensureToastStyles() {
        if (document.getElementById('langToastStyles')) {
            return;
        }

        var style = document.createElement('style');
        style.id = 'langToastStyles';

        style.textContent =
            '.lang-toast{position:fixed;left:50%;bottom:32px;z-index:9999;' +
            'transform:translateX(-50%) translateY(14px);opacity:0;' +
            'display:flex;align-items:center;gap:12px;max-width:min(420px,92vw);' +
            'padding:14px 18px;border-radius:14px;background:#12233d;color:#fff;' +
            'font-family:inherit;font-size:14px;line-height:1.5;' +
            'box-shadow:0 14px 40px rgba(10,25,50,.32);' +
            'transition:opacity .22s ease,transform .22s ease;pointer-events:none}' +
            '.lang-toast.is-open{opacity:1;transform:translateX(-50%) translateY(0)}' +
            '.lang-toast-icon{flex:0 0 26px;height:26px;display:grid;' +
            'place-items:center;border-radius:50%;background:rgba(255,255,255,.16);' +
            'font-size:14px}' +
            '.lang-toast-count{font-variant-numeric:tabular-nums;font-weight:700}';

        document.head.appendChild(style);
    }

    function showToast(message, durationMs) {
        ensureToastStyles();

        var existing = document.getElementById('langToast');

        if (existing) {
            existing.remove();
        }

        var toast = document.createElement('div');
        toast.id = 'langToast';
        toast.className = 'lang-toast';
        toast.setAttribute('data-no-translate', '');
        toast.setAttribute('role', 'status');

        var icon = document.createElement('span');
        icon.className = 'lang-toast-icon';
        icon.textContent = '\u23F3';

        var body = document.createElement('span');
        body.innerHTML = message;

        toast.appendChild(icon);
        toast.appendChild(body);
        document.body.appendChild(toast);

        // Next frame, so the opening transition actually runs.
        requestAnimationFrame(function () {
            toast.classList.add('is-open');
        });

        setTimeout(function () {
            toast.classList.remove('is-open');
            setTimeout(function () { toast.remove(); }, 260);
        }, durationMs || 4000);

        return toast;
    }

    function remainingCooldown() {
        var last = 0;

        try {
            last = parseInt(
                localStorage.getItem(COOLDOWN_KEY) || '0', 10) || 0;
        } catch (e) {
            return 0;
        }

        var elapsed = Date.now() - last;

        return elapsed >= COOLDOWN_MS ? 0 : COOLDOWN_MS - elapsed;
    }

    function markSwitched() {
        try {
            localStorage.setItem(COOLDOWN_KEY, String(Date.now()));
        } catch (e) {
            /* private mode - cooldown just won't persist */
        }
    }

    /* ---------------- language dropdown ---------------- */

    function wireDropdown() {
        var select = document.getElementById('languageSelect');

        if (!select) {
            return;
        }

        select.value = config.current;

        // Re-arm the lock if the page was reloaded mid-cooldown.
        var pending = remainingCooldown();

        if (pending > 0) {
            lockSelect(select, pending, true);
        }

        select.addEventListener('change', function () {
            var chosen = select.value;

            if (chosen === config.current) {
                return;
            }

            var wait = remainingCooldown();

            if (wait > 0) {
                select.value = config.current;

                showToast(
                    'Please wait <span class="lang-toast-count">' +
                    Math.ceil(wait / 1000) +
                    's</span> before switching language again.',
                    2600);

                return;
            }

            select.disabled = true;

            showToast(
                'Switching language\u2026 please do not change it again ' +
                'for 10 seconds.',
                3200);

            fetch(config.setUrl, {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    'RequestVerificationToken': antiForgeryToken()
                },
                body: JSON.stringify({ language: chosen })
            }).then(function (response) {
                if (!response.ok) {
                    throw new Error('Set failed: ' + response.status);
                }

                markSwitched();
                window.location.reload();
            }).catch(function (error) {
                select.disabled = false;
                select.value = config.current;

                if (window.console) {
                    console.warn(error);
                }

                showToast(
                    'Sorry, the language could not be changed. ' +
                    'Please try again.',
                    4000);
            });
        });
    }

    /// Disables the dropdown and counts down, so the cooldown is visible
    /// rather than a silent rejection.
    function lockSelect(select, durationMs, announce) {
        var seconds = Math.ceil(durationMs / 1000);

        select.disabled = true;

        var toast = announce
            ? showToast('', durationMs)
            : null;

        var timer = setInterval(function () {
            seconds -= 1;

            if (toast) {
                toast.lastChild.innerHTML =
                    'Language updated. You can switch again in ' +
                    '<span class="lang-toast-count">' + seconds +
                    's</span>.';
            }

            if (seconds <= 0) {
                clearInterval(timer);
                select.disabled = false;
            }
        }, 1000);

        setTimeout(function () {
            clearInterval(timer);
            select.disabled = false;
        }, durationMs + 200);
    }

    /* ---------------- start ---------------- */

    function start() {
        wireDropdown();

        if (config.current === 'en') {
            return;
        }

        translateRoot(document.body)
            .then(reveal, reveal);

        watchForNewContent();
    }

    if (config.current !== 'en') {
        hideUntilReady();
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', start);
    } else {
        start();
    }
})();
