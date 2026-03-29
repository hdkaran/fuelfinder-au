import { useState, useEffect } from 'react';
import { skipToken } from '@reduxjs/toolkit/query/react';
import { useGetNearbyStationsQuery, useGetStatsSummaryQuery } from '../api/fuelFinderApi';
import StationCard from '../components/StationCard';
import StationCardSkeleton from '../components/StationCardSkeleton';
import StationMap from '../components/StationMap';
import RadiusPicker, { RADIUS_OPTIONS, type RadiusValue } from '../components/RadiusPicker';
import { pluralise } from '../utils/format';
import styles from './HomePage.module.css';

type View = 'list' | 'map';

const SKELETON_COUNT = 4;
const STORAGE_KEY = 'fuelfinder:radius';
const DEFAULT_RADIUS: RadiusValue = 5_000;

function readStoredRadius(): RadiusValue {
  try {
    const raw = localStorage.getItem(STORAGE_KEY);
    const parsed = Number(raw);
    return RADIUS_OPTIONS.some((o) => o.value === parsed)
      ? (parsed as RadiusValue)
      : DEFAULT_RADIUS;
  } catch {
    return DEFAULT_RADIUS;
  }
}

interface Coords {
  lat: number;
  lng: number;
}

export default function HomePage() {
  const [coords, setCoords] = useState<Coords | null>(null);
  const [geoError, setGeoError] = useState<string | null>(null);
  const [view, setView] = useState<View>('list');
  const [radius, setRadius] = useState<RadiusValue>(readStoredRadius);

  function handleRadiusChange(newRadius: RadiusValue) {
    setRadius(newRadius);
    try { localStorage.setItem(STORAGE_KEY, String(newRadius)); } catch { /* ignore */ }
  }

  useEffect(() => {
    if (!navigator.geolocation) {
      setGeoError('Geolocation is not supported by your browser.');
      return;
    }
    navigator.geolocation.getCurrentPosition(
      (pos) => setCoords({ lat: pos.coords.latitude, lng: pos.coords.longitude }),
      () => setGeoError('Enable location access to find nearby stations.'),
      { timeout: 10_000 },
    );
  }, []);

  const {
    data: stations,
    isLoading: stationsLoading,
    isError: stationsError,
    refetch,
    isFetching,
  } = useGetNearbyStationsQuery(
    coords ? { lat: coords.lat, lng: coords.lng, radius } : skipToken,
    { pollingInterval: coords ? 120_000 : undefined },
  );

  const { data: stats } = useGetStatsSummaryQuery(undefined, { pollingInterval: 60_000 });

  const hasStations = stations && stations.length > 0;
  const showSkeletons = coords && stationsLoading;

  return (
    <div className={styles.page}>
      <header className={styles.header}>
        <div className={styles.headerTop}>
          <div>
            <h1 className={styles.title}>
              <span className={styles.accent}>Fuel</span>Finder AU
            </h1>
            <p className={styles.subtitle}>Find fuel near you</p>
          </div>
          <div className={styles.headerActions}>
            {hasStations && (
              <div className={styles.toggle}>
                <button
                  className={`${styles.toggleBtn} ${view === 'list' ? styles.toggleActive : ''}`}
                  onClick={() => setView('list')}
                >
                  List
                </button>
                <button
                  className={`${styles.toggleBtn} ${view === 'map' ? styles.toggleActive : ''}`}
                  onClick={() => setView('map')}
                >
                  Map
                </button>
              </div>
            )}
            {hasStations && (
              <button
                className={`${styles.refreshBtn} ${isFetching ? styles.refreshing : ''}`}
                onClick={() => refetch()}
                disabled={isFetching}
                aria-label="Refresh"
              >
                ↻
              </button>
            )}
          </div>
        </div>
      </header>

      {stats && (
        <div className={styles.statsBanner}>
          ⛽ {pluralise(stats.totalReportsToday, 'report')} today
          &nbsp;·&nbsp;
          {pluralise(stats.stationsAffected, 'station')} affected
        </div>
      )}

      <RadiusPicker value={radius} onChange={handleRadiusChange} />

      <main className={styles.main}>
        {!coords && !geoError && (
          <div className={styles.centered}>
            <span className={styles.icon}>📍</span>
            <p>Getting your location…</p>
          </div>
        )}

        {geoError && (
          <div className={styles.centered}>
            <span className={styles.icon}>📍</span>
            <p className={styles.errorText}>{geoError}</p>
          </div>
        )}

        {showSkeletons && (
          <ul className={styles.list}>
            {Array.from({ length: SKELETON_COUNT }).map((_, i) => (
              <li key={i}><StationCardSkeleton /></li>
            ))}
          </ul>
        )}

        {stationsError && (
          <div className={styles.centered}>
            <span className={styles.icon}>⚠️</span>
            <p className={styles.errorText}>Couldn't load stations.</p>
            <button className={styles.retryBtn} onClick={() => refetch()}>Try again</button>
          </div>
        )}

        {coords && !stationsLoading && !stationsError && stations?.length === 0 && (
          <div className={styles.centered}>
            <span className={styles.icon}>🔍</span>
            <p>No stations found within {RADIUS_OPTIONS.find((o) => o.value === radius)?.label}.</p>
            <p className={styles.hint}>Try a larger radius or be the first to report a station near you.</p>
          </div>
        )}

        {hasStations && view === 'list' && (
          <ul className={styles.list}>
            {stations.map((station) => (
              <li key={station.id}>
                <StationCard station={station} />
              </li>
            ))}
          </ul>
        )}

        {hasStations && view === 'map' && (
          <StationMap stations={stations} center={coords!} />
        )}
      </main>
    </div>
  );
}
