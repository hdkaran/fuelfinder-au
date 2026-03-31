import { useState, useEffect } from 'react';
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

const ALL_AVAILABLE: Record<FuelType, boolean> = { Diesel: true, ULP: true, E10: true, Premium: true };
const ALL_UNAVAILABLE: Record<FuelType, boolean> = { Diesel: false, ULP: false, E10: false, Premium: false };

export default function ReportPage() {
  const { stationId } = useParams<{ stationId: string }>();
  const { data: station } = useGetStationQuery(stationId ?? skipToken);

  const [status, setStatus] = useState<ReportStatus | null>(null);
  const [fuelAvailable, setFuelAvailable] = useState<Record<FuelType, boolean>>(ALL_AVAILABLE);
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
        fuelTypes: FUEL_TYPES.map((ft) => ({ fuelType: ft, available: fuelAvailable[ft] })),
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
          <div className={styles.successIcon}>✓</div>
          <h2 className={styles.successTitle}>Report submitted</h2>
          <p className={styles.successSub}>Thanks for helping other drivers!</p>
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
              {FUEL_TYPES.map((ft) => (
                <button
                  key={ft}
                  className={`${styles.fuelRow} ${fuelAvailable[ft] ? styles.fuelYes : styles.fuelNo}`}
                  onClick={() => toggleFuel(ft)}
                >
                  <span className={styles.fuelName}>{ft}</span>
                  <span className={styles.fuelToggle}>
                    {fuelAvailable[ft] ? '✓ Available' : '✗ Out'}
                  </span>
                </button>
              ))}
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
