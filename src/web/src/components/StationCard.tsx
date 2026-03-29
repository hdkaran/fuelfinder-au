import { Link } from 'react-router-dom';
import type { StationDto } from '../types';
import { formatDistance, formatMinutesAgo, pluralise } from '../utils/format';
import StatusPill from './StatusPill';
import FuelBadge from './FuelBadge';
import styles from './StationCard.module.css';

interface Props {
  station: StationDto;
}

export default function StationCard({ station }: Props) {
  const footerParts = [
    formatMinutesAgo(station.lastReportMinutesAgo),
    station.reportCount > 0 ? pluralise(station.reportCount, 'report') : null,
  ].filter(Boolean).join(' · ');

  return (
    <Link to={`/stations/${station.id}`} className={styles.card}>
      <div className={styles.top}>
        <div className={styles.nameGroup}>
          <span className={styles.name}>{station.name}</span>
          <span className={styles.brand}>{station.brand}</span>
        </div>
        <StatusPill status={station.status} />
      </div>

      <p className={styles.meta}>
        {station.address} · {formatDistance(station.distanceMetres)}
      </p>

      <div className={styles.fuels}>
        {station.fuelAvailability.map((fa) => (
          <FuelBadge key={fa.fuelType} fuelType={fa.fuelType} available={fa.available} />
        ))}
      </div>

      <p className={styles.footer}>{footerParts}</p>
    </Link>
  );
}
