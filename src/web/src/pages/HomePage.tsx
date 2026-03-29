import { useState, useEffect } from 'react';
import { skipToken } from '@reduxjs/toolkit/query/react';
import { useGetNearbyStationsQuery, useGetStatsSummaryQuery } from '../api/fuelFinderApi';
import StationCard from '../components/StationCard';
import { pluralise } from '../utils/format';
import styles from './HomePage.module.css';

const RADIUS_METRES = 5000;

interface Coords {
  lat: number;
  lng: number;
}

export default function HomePage() {
  const [coords, setCoords] = useState<Coords | null>(null);
  const [geoError, setGeoError] = useState<string | null>(null);

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

  const { data: stations, isLoading: stationsLoading } = useGetNearbyStationsQuery(
    coords ? { lat: coords.lat, lng: coords.lng, radius: RADIUS_METRES } : skipToken,
    { pollingInterval: coords ? 120_000 : undefined },
  );

  const { data: stats } = useGetStatsSummaryQuery(undefined, { pollingInterval: 60_000 });

  return (
    <div className={styles.page}>
      <header className={styles.header}>
        <h1 className={styles.title}>
          <span className={styles.accent}>Fuel</span>Finder AU
        </h1>
        <p className={styles.subtitle}>Find fuel near you</p>
      </header>

      {stats && (
        <div className={styles.statsBanner}>
          ⛽ {pluralise(stats.totalReportsToday, 'report')} today
          &nbsp;·&nbsp;
          {pluralise(stats.stationsAffected, 'station')} affected
        </div>
      )}

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

        {coords && stationsLoading && (
          <div className={styles.centered}>
            <span className={styles.icon}>⛽</span>
            <p>Finding nearby stations…</p>
          </div>
        )}

        {coords && !stationsLoading && stations?.length === 0 && (
          <div className={styles.centered}>
            <span className={styles.icon}>🔍</span>
            <p>No stations found within 5 km.</p>
            <p className={styles.hint}>Be the first to report a station near you.</p>
          </div>
        )}

        {/* Station list */}
        {stations && stations.length > 0 && (
          <ul className={styles.list}>
            {stations.map((station) => (
              <li key={station.id}>
                <StationCard station={station} />
              </li>
            ))}
          </ul>
        )}
      </main>
    </div>
  );
}
