(function () {
    let observer = null;
    let currentNavId = null;

    window.initSetupNav = function (navId, sentinelId) {
        const nav = document.getElementById(navId);
        const sentinel = document.getElementById(sentinelId);
        if (!nav || !sentinel) return;

        disposeSetupNav();
        currentNavId = navId;

        observer = new IntersectionObserver(function (entries) {
            for (const entry of entries) {
                // Show nav once the sentinel (Details section) has scrolled
                // above the top of the viewport.
                if (!entry.isIntersecting && entry.boundingClientRect.top < 0) {
                    nav.classList.add('setup-nav--visible');
                } else {
                    nav.classList.remove('setup-nav--visible');
                }
            }
        }, { threshold: 0 });

        observer.observe(sentinel);
    };

    window.disposeSetupNav = function () {
        if (observer) {
            observer.disconnect();
            observer = null;
        }
        if (currentNavId) {
            const nav = document.getElementById(currentNavId);
            if (nav) nav.classList.remove('setup-nav--visible');
            currentNavId = null;
        }
    };

    window.scrollToSectionId = function (id) {
        const el = document.getElementById(id);
        if (el) el.scrollIntoView({ behavior: 'smooth', block: 'start' });
    };
})();
