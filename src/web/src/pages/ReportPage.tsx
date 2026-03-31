import { useState, useEffect } from 'react';
import { CheckCircle, Check, X as XIcon } from 'lucide-react';
import { useParams, Link } from 'react-router-dom';
import { skipToken } from '@reduxjs/toolkit/query/react';
import { useGetStationQuery, useSubmitReportMutation } from '../api/fuelFinderApi';
import PageHeader from '../components/PageHeader';
import type { ReportStatus, FuelType } from '../types';
import styles from './ReportPage.module.css';

const FUEL_TYPES: FuelType[] = ['Diesel', 'ULP', 'E10', 'Premium'];

const STATUS_OPTIONS: { value: ReportStatus; label: string; statusClass: string }[] = [
  { value: 'available', label: 'Fuel available', statusClass: styles.statusAvailable },
  { value: 'low',       label: 'Running low',    statusClass: styles.statusLow },
  { value: 'out',       label: 'Fuel out',        statusClass: styles.statusOut },
  { value: 'queue',     label: 'Long queue',      statusClass: styles.statusQueue },
];

const STATUS_LABEL: Record<ReportStatus, string> = {
  available: 'Fuel available',
  low:       'Running low',
  out:       'Fuel out',
  queue:     'Long queue',
};

// Fuel rows start unset (null = not chosen yet)
type FuelState = Record<FuelType, boolean | null>;
const ALL_UNSET: FuelState    = { Diesel: null, ULP: null, E10: null, Premium: null };
const ALL_AVAILABLE: FuelState = { Diesel: true, ULP: true, E10: true, Premium: true };
const ALL_UNAVAILABLE: FuelState = { Diesel: false, ULP: false, E10: false, Premium: false };

export default function ReportPage() {
  const { stationId } = useParams<{ stationId: string }>();
  const { data: station } = useGetStationQuery(stationId ?? skipToken);

  const [status, setStatus] = useState<ReportStatus | null>(null);
  const [fuelAvailable, setFuelAvailable] = useState<FuelState>(ALL_UNSET);
  const [coords, setCoords] = useState<{ lat: number; lng: number } | null>(null);
  const [submitReport, { isLoading, isSuccess, isError }] = useSubmitReportMutation();

  useEffect(() => {
    navigator.geolocation?.getCurrentPosition(
      (pos) => setCoords({ lat: pos.coords.latitude, lng: pos.coords.longitude }),
      () => { /* location denied — falls back to station coords on submit */ },
      { timeout: 10_000 },
    );
  }, []);

  function handleStatusSelect(s: ReportStatus) {
    setStatus(s);
    setFuelAvailable(s === 'out' ? ALL_UNAVAILABLE : ALL_AVAILABLE);
  }

  function toggleFuel(ft: FuelType) {
    setFuelAvailable((prev) => ({ ...prev, [ft]: !prev[ft] }));
  }

  async function handleSubmit() {
    if (!status || !stationId) return;
    try {
      await submitReport({
        stationId,
        status,
        fuelTypes: FUEL_TYPES.map((ft) => ({
          fuelType: ft,
          available: fuelAvailable[ft] ?? true,
        })),
        latitude:  coords?.lat ?? station?.latitude ?? 0,
        longitude: coords?.lng ?? station?.longitude ?? 0,
      }).unwrap();
    } catch {
      // isError from mutation captures this
    }
  }

  if (isSuccess) {
    return (
      <div className={styles.page}>
        <div className={styles.success}>
          <div className={styles.successIcon}>
            <CheckCircle size={40} strokeWidth={1.8} />
          </div>
          <h2 className={styles.successTitle}>Report submitted</h2>
          <p className={styles.successSub}>Thanks for helping other drivers!</p>

          {status && (
            <div className={styles.successSummary}>
              <span className={styles.successSummaryLabel}>You reported</span>
              <span className={styles.successSummaryValue}>
                {STATUS_LABEL[status]}
                {station && ` at ${station.name}`}
              </span>
            </div>
          )}

          <Link to={`/stations/${stationId}`} className={styles.successBtn}>
            Back to station
          </Link>
          <Link to="/" className={styles.successLink}>Go to home</Link>
        </div>
      </div>
    );
  }

  return (
    <div className={styles.page}>
      <PageHeader
        backTo={`/stations/${stationId}`}
        title="Report status"
        subtitle={station?.name}
      />

      <main className={styles.main}>
        <section className={styles.section}>
          <h2 className={styles.sectionTitle}>Current status</h2>
          <div className={styles.statusGrid}>
            {STATUS_OPTIONS.map(({ value, label, statusClass }) => (
              <button
                key={value}
                className={`${styles.statusBtn} ${statusClass} ${status === value ? styles.selected : ''}`}
                onClick={() => handleStatusSelect(value)}
              >
                {label}
              </button>
            ))}
          </div>
        </section>

        {status && (
          <section className={styles.section}>
            <h2 className={styles.sectionTitle}>Fuel availability</h2>
            <div className={styles.fuelList}>
              {FUEL_TYPES.map((ft) => {
                const val = fuelAvailable[ft];
                const cls = val === true
                  ? styles.fuelYes
                  : val === false
                    ? styles.fuelNo
                    : styles.fuelUnset;
                return (
                  <button
                    key={ft}
                    className={`${styles.fuelRow} ${cls}`}
                    onClick={() => toggleFuel(ft)}
                  >
                    <span className={styles.fuelName}>{ft}</span>
                    <span className={styles.fuelToggle}>
                      {val === true  && <><Check  size={13} /> Available</>}
                      {val === false && <><XIcon  size={13} /> Out</>}
                      {val === null  && <span className={styles.fuelTap}>Tap to set</span>}
                    </span>
                  </button>
                );
              })}
            </div>
          </section>
        )}

        {isError && (
          <p className={styles.error}>Failed to submit — please try again.</p>
        )}

        <button
          className={styles.submitBtn}
          disabled={!status || isLoading}
          onClick={handleSubmit}
        >
          {isLoading ? 'Submitting…' : 'Submit report'}
        </button>
      </main>
    </div>
  );
}
