import type { FuelType } from '../types';
import styles from './FuelBadge.module.css';

function availabilityStyle(available: boolean | null): { icon: string; cls: string } {
  if (available === true)  return { icon: '✓', cls: styles.yes };
  if (available === false) return { icon: '✗', cls: styles.no };
  return { icon: '?', cls: styles.unknown };
}

interface Props {
  fuelType: FuelType;
  available: boolean | null;
}

export default function FuelBadge({ fuelType, available }: Props) {
  const { icon, cls } = availabilityStyle(available);
  const label = fuelType === 'Premium' ? 'Prem' : fuelType;

  return (
    <span className={`${styles.badge} ${cls}`}>
      {label} <span className={styles.icon}>{icon}</span>
    </span>
  );
}
