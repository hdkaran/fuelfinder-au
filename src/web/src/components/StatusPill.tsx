import type { StationStatus } from '../types';
import styles from './StatusPill.module.css';

const LABELS: Record<StationStatus, string> = {
  available: 'Available',
  low: 'Low',
  out: 'Out of fuel',
  unknown: 'Unknown',
};

interface Props {
  status: StationStatus;
}

export default function StatusPill({ status }: Props) {
  return (
    <span className={`${styles.pill} ${styles[status]}`}>
      {LABELS[status]}
    </span>
  );
}
