// service-worker.js (development)
// Network-only. No offline cache.
self.addEventListener('install', () => self.skipWaiting());
self.addEventListener('activate', event => {
  event.waitUntil(self.clients.claim());
});
// No fetch handler.
