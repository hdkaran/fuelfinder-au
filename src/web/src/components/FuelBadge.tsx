import type { ReactNode } from 'react';
import { Check, X, Minus } from 'lucide-react';
import type { FuelType } from '../types';
import styles from './FuelBadge.module.css';

function availabilityIcon(available: boolean | null): { icon: ReactNode; cls: string } {
  if (available === true)  return { icon: <Check size={10} strokeWidth={2.5} />, cls: styles.yes };
  if (available === false) return { icon: <X    size={10} strokeWidth={2.5} />, cls: styles.no };
  return { icon: <Minus size={10} strokeWidth={2.5} />, cls: styles.unknown };
}

interface Props {
  fuelType: FuelType;
  available: boolean | null;
}

export default function FuelBadge({ fuelType, available }: Props) {
  const { icon, cls } = availabilityIcon(available);
  const label = fuelType === 'Premium' ? 'Prem' : fuelType;

  return (
    <span className={`${styles.badge} ${cls}`}>
      {label} <span className={styles.icon}>{icon}</span>
    </span>
  );
}
