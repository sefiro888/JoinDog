const BUILD_VERSION = "ec6bd43efed10f296d2d7ffa";
const SHELL_CACHE = `joindog-shell-${BUILD_VERSION}`;
const SHELL_FILES = [
  "./",
  "./manifest.webmanifest",
  "./icons/join-dog-192.png",
  "./icons/join-dog-512.png",
  "./icons/join-dog-maskable-512.png"
];
const RUNTIME_FILES = ["./Build/docs.loader.js?v=ec6bd43efed10f296d2d7ffa", "./Build/docs.framework.js.unityweb?v=ec6bd43efed10f296d2d7ffa"];

self.addEventListener("install", event => {
  event.waitUntil(caches.open(SHELL_CACHE).then(cache => cache.addAll(SHELL_FILES.concat(RUNTIME_FILES))));
});

self.addEventListener("activate", event => {
  event.waitUntil(
    caches.keys()
      .then(keys => Promise.all(keys.filter(key => key.startsWith("joindog-shell-") && key !== SHELL_CACHE).map(key => caches.delete(key))))
      .then(() => self.clients.claim())
  );
});

self.addEventListener("message", event => {
  if (event.data && event.data.type === "SKIP_WAITING") self.skipWaiting();
});

self.addEventListener("fetch", event => {
  const request = event.request;
  if (request.method !== "GET") return;

  const url = new URL(request.url);
  const isUnityPayload = url.pathname.includes("/Build/") || url.pathname.includes("/StreamingAssets/");
  if (isUnityPayload) {
    event.respondWith(
      caches.match(request).then(cached => cached || fetch(request).then(response => {
        if (response.ok && RUNTIME_FILES.some(file => request.url.includes(file.replace("./", "")))) {
          const copy = response.clone();
          caches.open(SHELL_CACHE).then(cache => cache.put(request, copy));
        }
        return response;
      }))
    );
    return;
  }

  if (request.mode === "navigate") {
    event.respondWith(
      fetch(request)
        .then(response => {
          const copy = response.clone();
          caches.open(SHELL_CACHE).then(cache => cache.put("./", copy));
          return response;
        })
        .catch(() => caches.match("./"))
    );
    return;
  }

  if (url.origin === self.location.origin) {
    event.respondWith(
      caches.match(request).then(cached => cached || fetch(request).then(response => {
        if (response.ok && !url.pathname.endsWith("service-worker.js")) {
          const copy = response.clone();
          caches.open(SHELL_CACHE).then(cache => cache.put(request, copy));
        }
        return response;
      }))
    );
  }
});
