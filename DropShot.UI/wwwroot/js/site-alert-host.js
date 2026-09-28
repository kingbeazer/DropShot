// Publishes the SiteAlertHost element's live content height as the CSS custom
// property `--site-alert-height` on :root. Page-level sticky bands (e.g. the
// competition title bar) read this variable to offset their own `top` so the
// alert pushes them down instead of overlapping. A ResizeObserver tracks the
// 0fr↔1fr grid transition on .site-alert-host so the variable animates in sync.
(function () {
    const observers = new WeakMap();

    function setHeight(px) {
        document.documentElement.style.setProperty('--site-alert-height', px + 'px');
    }

    function attach(element) {
        if (!element || observers.has(element)) return;
        const ro = new ResizeObserver(entries => {
            for (const entry of entries) {
                setHeight(entry.contentRect.height);
            }
        });
        ro.observe(element);
        observers.set(element, ro);
    }

    function detach(element) {
        if (!element) return;
        const ro = observers.get(element);
        if (ro) {
            ro.disconnect();
            observers.delete(element);
        }
        setHeight(0);
    }

    window.dropshotSiteAlert = { attach, detach };
})();
