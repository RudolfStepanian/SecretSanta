// Service worker for push notifications: shows the message and opens the participant's page on click.
self.addEventListener('push', (event) => {
  const data = event.data ? event.data.json() : {};
  event.waitUntil(self.registration.showNotification(data.title || 'Secret Santa', {
    body: data.body || '',
    icon: '/img/icon-192.png',
    badge: '/img/icon-192.png',
    data: { url: data.url || '/' },
  }));
});

self.addEventListener('notificationclick', (event) => {
  event.notification.close();
  const url = new URL(event.notification.data.url, self.location.origin).href;
  event.waitUntil((async () => {
    const windows = await self.clients.matchAll({ type: 'window', includeUncontrolled: true });
    const open = windows.find((w) => w.url.split('#')[0] === url.split('#')[0]);
    if (!open) return self.clients.openWindow(url);
    await open.focus();
    try {
      await open.navigate(url);
    } catch {
      // A tab this worker does not control can't be navigated; it is focused at least.
    }
  })());
});
