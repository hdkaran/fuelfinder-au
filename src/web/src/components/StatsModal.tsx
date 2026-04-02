import { X } from 'lucide-react';
import { useGetTodayReportsQuery, useGetAffectedStationsQuery } from '../api/fuelFinderApi';
import { formatMinutesAgo, pluralise } from '../utils/format';
import styles from './StatsModal.module.css';

export type StatsModalMode = 'reports' | 'stations';

const STATUS_LABEL: Record<string, string> = {
  available: 'Available',
  low: 'Running low',
  out: 'Out of fuel',
  queue: 'Long queue',
};

const STATUS_CLASS: Record<string, string> = {
  available: 'available',
  low: 'low',
  out: 'out',
  queue: 'low',
};

interface Props {
  mode: StatsModalMode | null;
  onClose: () => void;
}

export default function StatsModal({ mode, onClose }: Props) {
  const { data: reports, isLoading: reportsLoading } = useGetTodayReportsQuery(undefined, {
    skip: mode !== 'reports',
  });
  const { data: stations, isLoading: stationsLoading } = useGetAffectedStationsQuery(undefined, {
    skip: mode !== 'stations',
  });

  if (mode === null) return null;

  const isLoading = mode === 'reports' ? reportsLoading : stationsLoading;
  const title = mode === 'reports' ? "Today's Reports" : 'Affected Stations';

  return (
    <>
      <div className={styles.backdrop} onClick={onClose} />
      <div className={styles.sheet} role="dialog" aria-modal="true" aria-label={title}>
        <div className={styles.handle} />
        <div className={styles.header}>
          <h2 className={styles.title}>{title}</h2>
          <button className={styles.closeBtn} onClick={onClose} aria-label="Close">
            <X size={18} />
          </button>
        </div>

        <div className={styles.body}>
          {isLoading && (
            <ul className={styles.list}>
              {Array.from({ length: 4 }).map((_, i) => (
                <li key={i} className={styles.skeleton} />
              ))}
            </ul>
          )}

          {mode === 'reports' && !isLoading && reports && (
            reports.length === 0 ? (
              <p className={styles.empty}>No reports yet today.</p>
            ) : (
              <ul className={styles.list}>
                {reports.map((r) => (
                  <li key={r.id} className={styles.item}>
                    <div className={styles.itemMain}>
                      <span className={styles.itemName}>{r.stationName}</span>
                      <span className={`${styles.badge} ${styles[STATUS_CLASS[r.status] ?? 'low']}`}>
                        {STATUS_LABEL[r.status] ?? r.status}
                      </span>
                    </div>
                    <div className={styles.itemSub}>
                      {r.stationAddress} &nbsp;·&nbsp; {formatMinutesAgo(r.minutesAgo)}
                    </div>
                  </li>
                ))}
              </ul>
            )
          )}

          {mode === 'stations' && !isLoading && stations && (
            stations.length === 0 ? (
              <p className={styles.empty}>No stations affected today.</p>
            ) : (
              <ul className={styles.list}>
                {stations.map((s) => (
                  <li key={s.id} className={styles.item}>
                    <div className={styles.itemMain}>
                      <span className={styles.itemName}>{s.name}</span>
                      <span className={`${styles.badge} ${styles[STATUS_CLASS[s.latestStatus] ?? 'low']}`}>
                        {STATUS_LABEL[s.latestStatus] ?? s.latestStatus}
                      </span>
                    </div>
                    <div className={styles.itemSub}>
                      {s.suburb}, {s.state} &nbsp;·&nbsp; {pluralise(s.reportCount, 'report')}
                    </div>
                  </li>
                ))}
              </ul>
            )
          )}
        </div>
      </div>
    </>
  );
}
