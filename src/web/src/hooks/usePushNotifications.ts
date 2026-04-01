import { useState, useEffect } from 'react';
import { useSubscribePushMutation, useUnsubscribePushMutation } from '../api/fuelFinderApi';

// Public VAPID key — safe to expose in client code
const VAPID_PUBLIC_KEY = 'BHKvJHobqbEm1_vmdefR66h-xatsulZLGUZfTLg2BSIrghpnjTL1p2RO4PrKYYFeMIhzh0WECvxhcz5dRRBJ7eY';

function urlBase64ToUint8Array(base64String: string): ArrayBuffer {
  const padding = '='.repeat((4 - (base64String.length % 4)) % 4);
  const base64 = (base64String + padding).replace(/-/g, '+').replace(/_/g, '/');
  const rawData = atob(base64);
  const buffer = new ArrayBuffer(rawData.length);
  const view = new Uint8Array(buffer);
  for (let i = 0; i < rawData.length; i++) view[i] = rawData.charCodeAt(i);
  return buffer;
}

interface Coords { lat: number; lng: number }

export function usePushNotifications(coords: Coords | null) {
  const isSupported =
    typeof window !== 'undefined' &&
    'Notification' in window &&
    'serviceWorker' in navigator &&
    'PushManager' in window;

  const [permission, setPermission] = useState<NotificationPermission>(
    isSupported ? Notification.permission : 'denied',
  );
  const [isSubscribed, setIsSubscribed] = useState(false);

  const [subscribePush]   = useSubscribePushMutation();
  const [unsubscribePush] = useUnsubscribePushMutation();

  // Check whether there's already an active subscription on mount
  useEffect(() => {
    if (!isSupported) return;
    navigator.serviceWorker.ready.then((reg) =>
      reg.pushManager.getSubscription().then((sub) => setIsSubscribed(sub !== null)),
    );
  }, [isSupported]);

  async function subscribe() {
    if (!isSupported || !coords) return;

    const reg = await navigator.serviceWorker.ready;
    const sub = await reg.pushManager.subscribe({
      userVisibleOnly: true,
      applicationServerKey: urlBase64ToUint8Array(VAPID_PUBLIC_KEY),
    });

    const json = sub.toJSON() as { endpoint: string; keys: { p256dh: string; auth: string } };
    await subscribePush({
      endpoint:  json.endpoint,
      p256dh:    json.keys.p256dh,
      auth:      json.keys.auth,
      latitude:  coords.lat,
      longitude: coords.lng,
    });

    setPermission('granted');
    setIsSubscribed(true);
  }

  async function unsubscribe() {
    if (!isSupported) return;

    const reg = await navigator.serviceWorker.ready;
    const sub = await reg.pushManager.getSubscription();
    if (sub) {
      await unsubscribePush({ endpoint: sub.endpoint });
      await sub.unsubscribe();
    }
    setIsSubscribed(false);
  }

  return { isSupported, permission, isSubscribed, subscribe, unsubscribe };
}
