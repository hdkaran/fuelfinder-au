import { useState, useEffect, useRef } from 'react';
import { skipToken } from '@reduxjs/toolkit/query/react';
import { useGetNearbyStationsQuery, useGetStatsSummaryQuery, useSearchStationsQuery } from '../api/fuelFinderApi';
import StationCard from '../components/StationCard';
import StationCardSkeleton from '../components/StationCardSkeleton';
import StationMap from '../components/StationMap';
import RadiusPicker, { RADIUS_OPTIONS, type RadiusValue } from '../components/RadiusPicker';
import SortPicker, { type SortValue } from '../components/SortPicker';
import SearchBar from '../components/SearchBar';
import NotificationBell from '../components/NotificationBell';
import { pluralise } from '../utils/format';
import type { StationDto } from '../types';
import styles from './HomePage.module.css';

type View = 'list' | 'map';

const STATUS_ORDER: Record<StationDto['status'], number> = {
  available: 0,
  low: 1,
  out: 2,
  unknown: 3,
};

function sortStations(list: StationDto[], sort: SortValue): StationDto[] {
  const copy = [...list];
  if (sort === 'status') {
    return copy.sort((a, b) => STATUS_ORDER[a.status] - STATUS_ORDER[b.status]);
  }
  if (sort === 'freshness') {
    return copy.sort((a, b) => {
      if (a.lastReportMinutesAgo === null) return 1;
      if (b.lastReportMinutesAgo === null) return -1;
      return a.lastReportMinutesAgo - b.lastReportMinutesAgo;
    });
  }
  return copy; // 'distance' — API already returns sorted by distance
}

const SKELETON_COUNT = 4;
const STORAGE_KEY = 'fuelfinder:radius';
const DEFAULT_RADIUS: RadiusValue = 5_000;
const SEARCH_DEBOUNCE_MS = 400;
const SEARCH_MIN_CHARS = 2;

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
  const [searchInput, setSearchInput] = useState('');
  const [searchQuery, setSearchQuery] = useState('');
  const [sort, setSort] = useState<SortValue>('distance');
  const debounceRef = useRef<ReturnType<typeof setTimeout> | null>(null);

  function handleRadiusChange(newRadius: RadiusValue) {
    setRadius(newRadius);
    try { localStorage.setItem(STORAGE_KEY, String(newRadius)); } catch { /* ignore */ }
  }

  function handleSearchChange(value: string) {
    setSearchInput(value);
    if (debounceRef.current) clearTimeout(debounceRef.current);
    debounceRef.current = setTimeout(() => {
      setSearchQuery(value.trim());
    }, SEARCH_DEBOUNCE_MS);
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

  const isSearching = searchQuery.length >= SEARCH_MIN_CHARS;

  const {
    data: nearbyStations,
    isLoading: nearbyLoading,
    isError: nearbyError,
    refetch,
    isFetching,
  } = useGetNearbyStationsQuery(
    coords && !isSearching ? { lat: coords.lat, lng: coords.lng, radius } : skipToken,
    { pollingInterval: coords && !isSearching ? 120_000 : undefined },
  );

  const {
    data: searchResults,
    isLoading: searchLoading,
    isError: searchError,
  } = useSearchStationsQuery(
    isSearching
      ? { q: searchQuery, ...(coords ? { lat: coords.lat, lng: coords.lng } : {}) }
      : skipToken,
  );

  const { data: stats } = useGetStatsSummaryQuery(undefined, { pollingInterval: 60_000 });

  const rawStations = isSearching ? searchResults : nearbyStations;
  const stations = rawStations && !isSearching ? sortStations(rawStations, sort) : rawStations;
  const stationsLoading = isSearching ? searchLoading : nearbyLoading;
  const stationsError = isSearching ? searchError : nearbyError;
  const hasStations = stations && stations.length > 0;
  const showSkeletons = stationsLoading;

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
            <NotificationBell coords={coords} />
            {hasStations && !isSearching && (
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
            {hasStations && !isSearching && (
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

      <SearchBar value={searchInput} onChange={handleSearchChange} />

      {!isSearching && <RadiusPicker value={radius} onChange={handleRadiusChange} />}
      {!isSearching && hasStations && <SortPicker value={sort} onChange={setSort} />}

      <main className={styles.main}>
        {!isSearching && !coords && !geoError && (
          <div className={styles.centered}>
            <span className={styles.icon}>📍</span>
            <p>Getting your location…</p>
          </div>
        )}

        {!isSearching && geoError && (
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
            {!isSearching && (
              <button className={styles.retryBtn} onClick={() => refetch()}>Try again</button>
            )}
          </div>
        )}

        {!stationsLoading && !stationsError && isSearching && !hasStations && (
          <div className={styles.centered}>
            <span className={styles.icon}>🔍</span>
            <p>No stations found for &ldquo;{searchQuery}&rdquo;.</p>
          </div>
        )}

        {!isSearching && coords && !stationsLoading && !stationsError && stations?.length === 0 && (
          <div className={styles.centered}>
            <span className={styles.icon}>🔍</span>
            <p>No stations found within {RADIUS_OPTIONS.find((o) => o.value === radius)?.label}.</p>
            <p className={styles.hint}>Try a larger radius or be the first to report a station near you.</p>
          </div>
        )}

        {hasStations && (isSearching || view === 'list') && (
          <ul className={styles.list}>
            {stations.map((station) => (
              <li key={station.id}>
                <StationCard station={station} />
              </li>
            ))}
          </ul>
        )}

        {hasStations && !isSearching && view === 'map' && (
          <StationMap stations={stations} center={coords!} />
        )}
      </main>
    </div>
  );
}
