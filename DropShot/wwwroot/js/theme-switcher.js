// DropShot – Theme Switcher
// Persists user preference in localStorage and toggles data-theme attribute.

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

    // Apply saved theme immediately to prevent flash
    applyTheme(getPreferred());

    // Expose toggle function globally for onclick
    window.toggleDropShotTheme = function () {
        var current = getPreferred();
        var next = current === 'dark' ? 'light' : 'dark';
        localStorage.setItem(STORAGE_KEY, next);
        applyTheme(next);
    };
})();
