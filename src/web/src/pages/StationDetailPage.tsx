import { useParams, Link } from 'react-router-dom';
import { skipToken } from '@reduxjs/toolkit/query/react';
import { MapPin, Clock, Navigation, AlertCircle } from 'lucide-react';
import { Helmet } from 'react-helmet-async';
import { useGetStationQuery, useGetRecentReportsQuery } from '../api/fuelFinderApi';
import PageHeader from '../components/PageHeader';
import StatusPill from '../components/StatusPill';
import FuelBadge from '../components/FuelBadge';
import PriceTag from '../components/PriceTag/PriceTag';
import { formatDistance, formatMinutesAgo, pluralise } from '../utils/format';
import type { FuelType, PriceDto } from '../types';
import styles from './StationDetailPage.module.css';

const FUEL_TYPE_SCHEMA_LABEL: Record<FuelType, string> = {
  ULP:     'Unleaded',
  E10:     'E10',
  Diesel:  'Diesel',
  Premium: 'Premium Unleaded',
};

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

function PricesSection({ prices }: { prices: PriceDto[] }) {
  if (prices.length === 0) {
    return (
      <section className={styles.section}>
        <h2 className={styles.sectionTitle}>Current Prices</h2>
        <p className={styles.noPrices}>No pricing data available for this station.</p>
      </section>
    );
  }

  const mostRecentUtc = prices.reduce<string | null>((acc, p) => {
    if (!acc) return p.recordedAtUtc;
    return p.recordedAtUtc > acc ? p.recordedAtUtc : acc;
  }, null);

  const updatedMinutesAgo = mostRecentUtc
    ? Math.floor((Date.now() - new Date(mostRecentUtc).getTime()) / 60_000)
    : null;

  const allStale = prices.every((p) => p.isStale);

  return (
    <section className={styles.section}>
      <div className={styles.priceHeader}>
        <h2 className={styles.sectionTitle}>Current Prices</h2>
        {updatedMinutesAgo !== null && (
          <span className={styles.priceUpdated}>
            <Clock size={11} /> Updated {formatMinutesAgo(updatedMinutesAgo)}
          </span>
        )}
      </div>
      {allStale && (
        <div className={styles.staleWarning}>
          <AlertCircle size={13} />
          Prices may be outdated — data refreshes every 30 minutes
        </div>
      )}
      <div className={styles.priceRow}>
        {prices.map((p) => (
          <PriceTag
            key={p.fuelType}
            fuelType={p.fuelType}
            pricePerLitreCents={p.pricePerLitreCents}
            isStale={p.isStale}
            size="large"
          />
        ))}
      </div>
    </section>
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

  const availableFuels = station.fuelAvailability
    .filter((fa) => fa.available === true)
    .map((fa) => FUEL_TYPE_SCHEMA_LABEL[fa.fuelType]);

  const stationJsonLd = {
    '@context': 'https://schema.org',
    '@type': 'GasStation',
    name: station.name,
    address: {
      '@type': 'PostalAddress',
      streetAddress: station.address,
      addressLocality: station.suburb,
      addressRegion: station.state,
      addressCountry: 'AU',
    },
    geo: {
      '@type': 'GeoCoordinates',
      latitude: station.latitude,
      longitude: station.longitude,
    },
    fuelType: availableFuels,
    url: `https://fuelstock.com.au/stations/${station.id}`,
  };

  const pageDescription = `Check fuel availability at ${station.name} in ${station.suburb}, ${station.state}. ${availableFuels.length > 0 ? `Available: ${availableFuels.join(', ')}.` : ''} Crowdsourced reports from FuelStock.`;

  return (
    <div className={styles.page}>
      <Helmet>
        <title>{station.name}, {station.suburb} — FuelStock</title>
        <meta name="description" content={pageDescription} />
        <link rel="canonical" href={`https://fuelstock.com.au/stations/${station.id}`} />
        <script type="application/ld+json">{JSON.stringify(stationJsonLd)}</script>
      </Helmet>
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

        {/* Current prices */}
        <PricesSection prices={station.latestPrices} />

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
                    {formatMinutesAgo(r.minutesAgo)}
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
