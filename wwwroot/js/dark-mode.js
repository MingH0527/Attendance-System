/* =========================================================
   Dark mode toggle (student portal).

   The class is applied to <html> by a small inline script in
   _Layout that runs before the page paints, so there is no
   flash of the light theme on load. This file only handles
   the switch itself.
   ========================================================= */

(function () {
    'use strict';

    var STORAGE_KEY = 'darkMode';

    var toggle = document.getElementById('darkModeToggle');

    if (!toggle) {
        return;
    }

    function isDark() {
        return document.documentElement.classList.contains('dark');
    }

    function apply(dark) {
        document.documentElement.classList.toggle('dark', dark);

        try {
            localStorage.setItem(STORAGE_KEY, dark ? 'on' : 'off');
        } catch (e) {
            /* private mode - the choice just won't survive a reload */
        }
    }

    // Reflect whatever the early script decided.
    toggle.checked = isDark();

    toggle.addEventListener('change', function () {
        apply(toggle.checked);
    });

    // Keep other tabs of the same portal in step.
    window.addEventListener('storage', function (event) {
        if (event.key !== STORAGE_KEY) {
            return;
        }

        var dark = event.newValue === 'on';

        document.documentElement.classList.toggle('dark', dark);
        toggle.checked = dark;
    });
})();
