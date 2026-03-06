// service-worker.published.js
// Caution! See https://aka.ms/blazor-offline-considerations

self.importScripts('./service-worker-assets.js');

const cacheNamePrefix = 'offline-cache-';
const cacheName = `${cacheNamePrefix}${self.assetsManifest.version}`;

const offlineAssetsInclude = [
  /\.dll$/, /\.pdb$/, /\.wasm$/, /\.html$/, /\.js$/, /\.json$/, /\.css$/,
  /\.woff2?$/, /\.png$/, /\.jpe?g$/, /\.gif$/, /\.ico$/, /\.blat$/, /\.dat$/
];
const offlineAssetsExclude = [/^service-worker\.js$/, /^service-worker-assets\.js$/];

const base = "/";
const baseUrl = new URL(base, self.origin);
const manifestUrlList = self.assetsManifest.assets.map(a => new URL(a.url, baseUrl).href);

async function onInstall() {
  const assetsRequests = self.assetsManifest.assets
    .filter(a => offlineAssetsInclude.some(re => re.test(a.url)))
    .filter(a => !offlineAssetsExclude.some(re => re.test(a.url)))
    .map(a => new Request(a.url, { integrity: a.hash, cache: 'no-cache' }));

  await caches.open(cacheName).then(cache => cache.addAll(assetsRequests));
}

async function onActivate() {
  const cacheKeys = await caches.keys();
  await Promise.all(
    cacheKeys
      .filter(key => key.startsWith(cacheNamePrefix) && key !== cacheName)
      .map(key => caches.delete(key))
  );
}

async function onFetch(event) {
  const request = event.request;
  const url = new URL(request.url);

  if (request.method !== 'GET' || url.origin !== self.origin) return null;
  if (request.mode === 'navigate') return null;
  if (!manifestUrlList.includes(url.href)) return null;

  const cache = await caches.open(cacheName);
  const cached = await cache.match(request);
  if (cached) return cached;

  const response = await fetch(request);
  if (response && response.ok) cache.put(request, response.clone());
  return response;
}

self.addEventListener('install', event => event.waitUntil(onInstall()));
self.addEventListener('activate', event => event.waitUntil(onActivate()));
self.addEventListener('fetch', event => {
  event.respondWith(onFetch(event) || fetch(event.request));
});
