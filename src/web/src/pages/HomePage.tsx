import { useState, useEffect, useRef } from 'react';
import { useNavigate } from 'react-router-dom';
import { Helmet } from 'react-helmet-async';
import { RefreshCw, Fuel, MapPin, AlertTriangle, Search } from 'lucide-react';
import { skipToken } from '@reduxjs/toolkit/query/react';
import { useGetNearbyStationsQuery, useGetStatsSummaryQuery, useSearchStationsQuery } from '../api/fuelFinderApi';
import StationCard from '../components/StationCard';
import StationCardSkeleton from '../components/StationCardSkeleton';
import StationMap from '../components/StationMap';
import { ReportBar } from '../components/ReportBar/ReportBar';
import { RADIUS_OPTIONS, type RadiusValue } from '../components/RadiusPicker';
import { type SortValue } from '../components/SortPicker';
import SearchBar from '../components/SearchBar';
import NotificationBell from '../components/NotificationBell';
import { type StateFilter } from '../components/StatePicker';
import FilterBar from '../components/FilterBar';
import InstallBanner from '../components/InstallBanner';
import StatsModal, { type StatsModalMode } from '../components/StatsModal';
import FeedbackModal from '../components/FeedbackModal/FeedbackModal';
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
  const navigate = useNavigate();
  const [coords, setCoords] = useState<Coords | null>(null);
  const [geoError, setGeoError] = useState<string | null>(null);
  const [view, setView] = useState<View>('list');
  const [radius, setRadius] = useState<RadiusValue>(readStoredRadius);
  const [searchInput, setSearchInput] = useState('');
  const [searchQuery, setSearchQuery] = useState('');
  const [sort, setSort] = useState<SortValue>('distance');
  const [stateFilter, setStateFilter] = useState<StateFilter>('All');
  const [statsModal, setStatsModal] = useState<StatsModalMode | null>(null);
  const [showFeedback, setShowFeedback] = useState(false);
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
      setGeoError('Location not supported — search for a suburb or station above.');
      return;
    }
    navigator.geolocation.getCurrentPosition(
      (pos) => setCoords({ lat: pos.coords.latitude, lng: pos.coords.longitude }),
      () => setGeoError('Location unavailable — search for a suburb or station above.'),
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
  const filteredStations = rawStations && stateFilter !== 'All'
    ? rawStations.filter((s) => s.state === stateFilter)
    : rawStations;
  const stations = filteredStations && !isSearching ? sortStations(filteredStations, sort) : filteredStations;
  const stationsLoading = isSearching ? searchLoading : nearbyLoading;
  const stationsError = isSearching ? searchError : nearbyError;
  const hasStations = stations && stations.length > 0;
  const showSkeletons = stationsLoading;

  return (
    <div className={styles.page}>
      <Helmet>
        <title>Find Petrol &amp; Diesel Near You — FuelFinder AU</title>
        <meta name="description" content="Find petrol stations with fuel near you in Australia. Real-time crowdsourced availability for Diesel, ULP, E10, and Premium — petrol near me australia." />
        <link rel="canonical" href="https://fuelstock.com.au/" />
      </Helmet>
      <InstallBanner />
      <header className={styles.header}>
        <div className={styles.headerTop}>
          <div>
            <h1 className={styles.title}>
              <span className={styles.accent}>Fuel</span>Stock
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
                  aria-pressed={view === 'list'}
                >
                  List
                </button>
                <button
                  className={`${styles.toggleBtn} ${view === 'map' ? styles.toggleActive : ''}`}
                  onClick={() => setView('map')}
                  aria-pressed={view === 'map'}
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
                <RefreshCw size={17} />
              </button>
            )}
          </div>
        </div>
      </header>

      {stats && (
        <div className={styles.statsBanner}>
          <Fuel size={14} />
          <button
            className={styles.statsBannerBtn}
            onClick={() => setStatsModal('reports')}
          >
            {pluralise(stats.totalReportsToday, 'report')} today
          </button>
          <span className={styles.statsDot}>·</span>
          <button
            className={styles.statsBannerBtn}
            onClick={() => setStatsModal('stations')}
          >
            {pluralise(stats.stationsAffected, 'station')} affected
          </button>
        </div>
      )}

      <SearchBar value={searchInput} onChange={handleSearchChange} />

      {!isSearching && coords && (
        <FilterBar
          sort={sort}
          onSortChange={setSort}
          radius={radius}
          onRadiusChange={handleRadiusChange}
          stateFilter={stateFilter}
          onStateChange={setStateFilter}
        />
      )}

      <main className={styles.main}>
        {!isSearching && !coords && !geoError && (
          <div className={styles.centered}>
            <MapPin size={40} />
            <p>Getting your location…</p>
          </div>
        )}

        {geoError && !isSearching && (
          <p className={styles.locationHint}><MapPin size={14} /> {geoError}</p>
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
            <AlertTriangle size={40} />
            <p className={styles.errorText}>Couldn't load stations.</p>
            {!isSearching && (
              <button className={styles.retryBtn} onClick={() => refetch()}>Try again</button>
            )}
          </div>
        )}

        {!stationsLoading && !stationsError && isSearching && !hasStations && (
          <div className={styles.centered}>
            <Search size={40} />
            <p>No stations found for &ldquo;{searchQuery}&rdquo;.</p>
          </div>
        )}

        {!isSearching && coords && !stationsLoading && !stationsError && stations?.length === 0 && (
          <div className={styles.centered}>
            <Search size={40} />
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

      <ReportBar onClick={() => navigate('/report')} />

      <div className={styles.feedback}>
        <button
          type="button"
          className={styles.feedbackBtn}
          onClick={() => setShowFeedback(true)}
        >
          Report a bug or suggest a feature
        </button>
      </div>

      <StatsModal mode={statsModal} onClose={() => setStatsModal(null)} />
      <FeedbackModal open={showFeedback} onClose={() => setShowFeedback(false)} />
    </div>
  );
}
