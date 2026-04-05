// Minimal service worker required for PWA installability.
// DropShot is a server-rendered Blazor app so we don't cache assets offline.
self.addEventListener('fetch', () => {});
