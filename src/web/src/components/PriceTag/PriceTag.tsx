import { Clock } from 'lucide-react';
import type { FuelType } from '../../types';
import styles from './PriceTag.module.css';

interface Props {
  fuelType: FuelType;
  pricePerLitreCents: number;
  isStale: boolean;
  /** Pass size="large" for the detail page */
  size?: 'compact' | 'large';
}

export default function PriceTag({ fuelType, pricePerLitreCents, isStale, size = 'compact' }: Props) {
  return (
    <div className={`${styles.tag} ${size === 'large' ? styles.large : ''}`}>
      <span className={styles.price}>
        {pricePerLitreCents.toFixed(1)}¢
        {isStale && <Clock size={10} className={styles.staleIcon} aria-label="Price may be outdated" />}
      </span>
      <span className={styles.label}>{fuelType}</span>
    </div>
  );
}
