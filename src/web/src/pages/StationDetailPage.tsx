import { useParams, Link } from 'react-router-dom';
import { skipToken } from '@reduxjs/toolkit/query/react';
import { useGetStationQuery } from '../api/fuelFinderApi';
import StatusPill from '../components/StatusPill';
import FuelBadge from '../components/FuelBadge';
import { formatMinutesAgo, pluralise } from '../utils/format';
import styles from './StationDetailPage.module.css';

function PageMessage({ text }: { text: string }) {
  return (
    <div className={styles.page}>
      <div className={styles.centered}><p>{text}</p></div>
    </div>
  );
}

export default function StationDetailPage() {
  const { id } = useParams<{ id: string }>();
  const { data: station, isLoading } = useGetStationQuery(id ?? skipToken);

  if (isLoading) return <PageMessage text="Loading…" />;
  if (!station)  return <PageMessage text="Station not found." />;

  return (
    <div className={styles.page}>
      <header className={styles.header}>
        <Link to="/" className={styles.back}>← Back</Link>
        <h1 className={styles.name}>{station.name}</h1>
        <p className={styles.brand}>{station.brand}</p>
      </header>

      <main className={styles.main}>
        <div className={styles.statusRow}>
          <StatusPill status={station.status} />
          <span className={styles.lastSeen}>
            {formatMinutesAgo(station.lastReportMinutesAgo)}
            {station.reportCount > 0 && ` · ${pluralise(station.reportCount, 'report')}`}
          </span>
        </div>

        <p className={styles.address}>
          {station.address}, {station.suburb} {station.state}
        </p>

        <section className={styles.section}>
          <h2 className={styles.sectionTitle}>Fuel availability</h2>
          <div className={styles.fuels}>
            {station.fuelAvailability.map((fa) => (
              <FuelBadge key={fa.fuelType} fuelType={fa.fuelType} available={fa.available} />
            ))}
          </div>
        </section>

        <div className={styles.reportPlaceholder}>
          <p>Know the current status? Report it — Phase 5</p>
        </div>
      </main>
    </div>
  );
}
