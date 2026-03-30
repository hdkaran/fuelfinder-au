import { usePushNotifications } from '../hooks/usePushNotifications';
import styles from './NotificationBell.module.css';

interface Props {
  coords: { lat: number; lng: number } | null;
}

export default function NotificationBell({ coords }: Props) {
  const { isSupported, permission, isSubscribed, subscribe, unsubscribe } =
    usePushNotifications(coords);

  if (!isSupported || permission === 'denied') return null;

  return (
    <button
      className={`${styles.bell} ${isSubscribed ? styles.active : ''}`}
      onClick={isSubscribed ? unsubscribe : subscribe}
      aria-label={isSubscribed ? 'Disable nearby fuel alerts' : 'Enable nearby fuel alerts'}
      title={isSubscribed ? 'Notifications on' : 'Get notified when fuel is reported near you'}
    >
      {isSubscribed ? '🔔' : '🔕'}
    </button>
  );
}
