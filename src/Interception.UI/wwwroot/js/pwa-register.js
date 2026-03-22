// pwa-register.js
// service-worker.js має бути в корені wwwroot/ (не в /js/)
// щоб мати scope '/' на весь застосунок.

if ('serviceWorker' in navigator) {
    window.addEventListener('load', () => {
        navigator.serviceWorker
            .register('/service-worker.js')
            .then(reg => console.debug('[PWA] Service worker registered:', reg.scope))
            .catch(err => console.warn('[PWA] Service worker registration failed:', err));
    });
}
