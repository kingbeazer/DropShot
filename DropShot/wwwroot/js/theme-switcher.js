// DropShot – Theme Switcher
// Persists user preference in localStorage and toggles data-theme attribute.
// Re-applies after Blazor enhanced navigation to prevent reset.

(function () {
    var STORAGE_KEY = 'dropshot-theme';

    function getPreferred() {
        return localStorage.getItem(STORAGE_KEY) || 'dark';
    }

    function applyTheme(theme) {
        if (theme === 'light') {
            document.documentElement.setAttribute('data-theme', 'light');
        } else {
            document.documentElement.removeAttribute('data-theme');
        }
        updateToggleLabels(theme);
    }

    function updateToggleLabels(theme) {
        document.querySelectorAll('.theme-toggle-label').forEach(function (el) {
            el.textContent = theme === 'light' ? 'Dark Mode' : 'Light Mode';
        });
    }

    // Apply saved theme immediately
    applyTheme(getPreferred());

    // Re-apply after Blazor enhanced navigation. Blazor patches the DOM to
    // match the server response, which strips the data-theme attribute.
    // The correct API is Blazor.addEventListener, NOT document.addEventListener.
    function hookBlazorNavigation() {
        if (typeof Blazor !== 'undefined' && Blazor.addEventListener) {
            Blazor.addEventListener('enhancedload', function () {
                applyTheme(getPreferred());
            });
        } else {
            // Blazor not ready yet, retry shortly
            setTimeout(hookBlazorNavigation, 100);
        }
    }
    hookBlazorNavigation();

    // Belt-and-suspenders: watch for the attribute being removed and restore it.
    // This catches any edge case where enhanced navigation strips it before
    // the enhancedload event fires.
    var observer = new MutationObserver(function (mutations) {
        var theme = getPreferred();
        if (theme === 'light' && !document.documentElement.hasAttribute('data-theme')) {
            // Temporarily disconnect to avoid infinite loop
            observer.disconnect();
            document.documentElement.setAttribute('data-theme', 'light');
            updateToggleLabels('light');
            observer.observe(document.documentElement, { attributes: true, attributeFilter: ['data-theme'] });
        }
    });
    observer.observe(document.documentElement, { attributes: true, attributeFilter: ['data-theme'] });

    // Expose toggle function globally for onclick. Optional `sourceEl` (the
    // <button> that fired the click) lets us close the containing nav
    // dropdown so the menu doesn't stay open after the user picks a mode.
    window.toggleDropShotTheme = function (sourceEl) {
        var current = getPreferred();
        var next = current === 'dark' ? 'light' : 'dark';
        localStorage.setItem(STORAGE_KEY, next);
        applyTheme(next);

        if (sourceEl && typeof sourceEl.blur === 'function') {
            sourceEl.blur();
            var dropdown = sourceEl.closest && sourceEl.closest('.nav-dropdown');
            if (dropdown) {
                // The dropdown is shown via :hover / :focus-within CSS, so
                // simply blurring the button isn't enough on desktop where
                // the cursor still hovers the menu. Briefly mark it as
                // suppressed so a matching CSS rule force-hides it; the
                // class self-clears so the dropdown can reopen normally.
                dropdown.classList.add('nav-dropdown-suppressed');
                setTimeout(function () {
                    dropdown.classList.remove('nav-dropdown-suppressed');
                }, 500);
            }
        }
    };
})();
