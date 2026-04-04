import { Link } from 'react-router-dom';
import { Navigation } from 'lucide-react';
import type { StationDto } from '../types';
import { formatDistance, formatMinutesAgo, pluralise } from '../utils/format';
import StatusPill from './StatusPill';
import FuelBadge from './FuelBadge';
import PriceTag from './PriceTag/PriceTag';
import styles from './StationCard.module.css';

const STATUS_CLASS: Record<string, string> = {
  available: styles.statusAvailable,
  low:       styles.statusLow,
  out:       styles.statusOut,
  unknown:   styles.statusUnknown,
};

interface Props {
  station: StationDto;
}

export default function StationCard({ station }: Props) {
  const footerParts = [
    formatMinutesAgo(station.lastReportMinutesAgo),
    station.reportCount > 0 ? pluralise(station.reportCount, 'report') : null,
  ].filter(Boolean).join(' · ');

  const mapsUrl =
    `https://www.google.com/maps/dir/?api=1&destination=${station.latitude},${station.longitude}`;

  return (
    <div className={`${styles.card} ${STATUS_CLASS[station.status] ?? ''}`}>
      <Link to={`/stations/${station.id}`} className={styles.cardBody}>
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

        {station.latestPrices.length > 0 && (
          <div className={styles.prices}>
            {station.latestPrices.map((p) => (
              <PriceTag
                key={p.fuelType}
                fuelType={p.fuelType}
                pricePerLitreCents={p.pricePerLitreCents}
                isStale={p.isStale}
              />
            ))}
          </div>
        )}
      </Link>

      <div className={styles.footer}>
        <span className={styles.footerText}>{footerParts}</span>
        <a
          href={mapsUrl}
          target="_blank"
          rel="noopener noreferrer"
          className={styles.directions}
          aria-label={`Get directions to ${station.name}`}
        >
          <Navigation size={12} /> Directions
        </a>
      </div>
    </div>
  );
}
