import { useParams, Link } from 'react-router-dom';
import { skipToken } from '@reduxjs/toolkit/query/react';
import { MapPin, Clock, Navigation } from 'lucide-react';
import { useGetStationQuery, useGetRecentReportsQuery } from '../api/fuelFinderApi';
import PageHeader from '../components/PageHeader';
import StatusPill from '../components/StatusPill';
import FuelBadge from '../components/FuelBadge';
import { formatDistance, formatMinutesAgo, pluralise } from '../utils/format';
import styles from './StationDetailPage.module.css';

const REPORT_STATUS_LABEL: Record<string, string> = {
  available: 'Fuel available',
  low:       'Running low',
  out:       'Fuel out',
  queue:     'Long queue',
};

const REPORT_STATUS_CLASS: Record<string, string> = {
  available: styles.reportAvailable,
  low:       styles.reportLow,
  out:       styles.reportOut,
  queue:     styles.reportQueue,
};

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
  const { data: recentReports } = useGetRecentReportsQuery(id ?? skipToken, {
    skip: !id,
  });

  if (isLoading) return <PageMessage text="Loading…" />;
  if (!station)  return <PageMessage text="Station not found." />;

  const mapsUrl = `https://www.google.com/maps/dir/?api=1&destination=${station.latitude},${station.longitude}`;

  return (
    <div className={styles.page}>
      <PageHeader backTo="/" title={station.name} subtitle={station.brand} />

      <main className={styles.main}>
        {/* Status row */}
        <div className={styles.statusRow}>
          <StatusPill status={station.status} />
          <span className={styles.lastSeen}>
            <Clock size={12} />
            {formatMinutesAgo(station.lastReportMinutesAgo)}
            {station.reportCount > 0 && ` · ${pluralise(station.reportCount, 'report')}`}
          </span>
        </div>

        {/* Address + directions */}
        <div className={styles.addressRow}>
          <span className={styles.address}>
            <MapPin size={13} />
            {station.address}, {station.suburb} {station.state}
            {station.distanceMetres > 0 && ` · ${formatDistance(station.distanceMetres)}`}
          </span>
          <a
            href={mapsUrl}
            target="_blank"
            rel="noopener noreferrer"
            className={styles.directionsBtn}
            aria-label={`Get directions to ${station.name}`}
          >
            <Navigation size={13} /> Directions
          </a>
        </div>

        {/* Fuel availability */}
        <section className={styles.section}>
          <h2 className={styles.sectionTitle}>Fuel availability</h2>
          <div className={styles.fuels}>
            {station.fuelAvailability.map((fa) => (
              <FuelBadge key={fa.fuelType} fuelType={fa.fuelType} available={fa.available} />
            ))}
          </div>
        </section>

        {/* Recent reports */}
        {recentReports && recentReports.length > 0 && (
          <section className={styles.section}>
            <h2 className={styles.sectionTitle}>Recent reports</h2>
            <ul className={styles.reportList}>
              {recentReports.slice(0, 5).map((r) => (
                <li key={r.id} className={styles.reportItem}>
                  <span className={`${styles.reportBadge} ${REPORT_STATUS_CLASS[r.status] ?? ''}`}>
                    {REPORT_STATUS_LABEL[r.status] ?? r.status}
                  </span>
                  <span className={styles.reportTime}>
                    {r.minutesAgo < 1 ? 'just now' : `${r.minutesAgo}m ago`}
                  </span>
                </li>
              ))}
            </ul>
          </section>
        )}
      </main>

      {/* Sticky report CTA */}
      <div className={styles.stickyFooter}>
        <Link to={`/report/${station.id}`} className={styles.reportBtn}>
          Report current status
        </Link>
      </div>
    </div>
  );
}
