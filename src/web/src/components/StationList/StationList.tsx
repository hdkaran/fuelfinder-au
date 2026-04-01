import styles from './StationList.module.css';
import { StationDto } from '../../types';

interface StationListProps {
  stations: StationDto[];
}

export function StationList({ stations }: StationListProps) {
  if (stations.length === 0) {
    return <p className={styles.empty}>No stations found nearby.</p>;
  }
  return (
    <ul className={styles.list}>
      {stations.map((station) => (
        <li key={station.id} className={styles.card}>
          <span className={styles.name}>{station.name}</span>
          <span className={styles.address}>{station.address}, {station.suburb}</span>
        </li>
      ))}
    </ul>
  );
}
