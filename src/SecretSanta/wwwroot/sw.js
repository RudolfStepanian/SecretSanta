// Push notifications were removed. Browsers that installed the service worker for them get this version on their next
// visit, and it takes itself away together with its push subscription.
self.addEventListener('install', () => self.skipWaiting());
self.addEventListener('activate', (event) => event.waitUntil(self.registration.unregister()));
