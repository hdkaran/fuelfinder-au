import { useParams, Link } from 'react-router-dom';
import { Helmet } from 'react-helmet-async';
import { useGetNearbyStationsQuery } from '../api/fuelFinderApi';
import { suburbByStateSlug } from '../data/suburbCentroids';
import StationCard from '../components/StationCard';
import StationCardSkeleton from '../components/StationCardSkeleton';
import { AlertTriangle } from 'lucide-react';
import styles from './SuburbPage.module.css';

const DEFAULT_RADIUS = 10_000;
const SKELETON_COUNT = 4;

function buildItemListJsonLd(
  suburbDisplay: string,
  state: string,
  stationIds: string[],
): object {
  return {
    '@context': 'https://schema.org',
    '@type': 'ItemList',
    name: `Petrol Stations in ${suburbDisplay}, ${state}`,
    itemListElement: stationIds.map((id, index) => ({
      '@type': 'ListItem',
      position: index + 1,
      url: `https://fuelstock.com.au/stations/${id}`,
    })),
  };
}

export default function SuburbPage() {
  const { state = '', suburb = '' } = useParams<{ state: string; suburb: string }>();
  const key = `${state.toLowerCase()}/${suburb.toLowerCase()}`;
  const centroid = suburbByStateSlug.get(key);

  const {
    data: stations,
    isLoading,
    isError,
  } = useGetNearbyStationsQuery(
    centroid
      ? { lat: centroid.lat, lng: centroid.lng, radius: DEFAULT_RADIUS }
      : { lat: 0, lng: 0, radius: DEFAULT_RADIUS },
    { skip: !centroid },
  );

  if (!centroid) {
    return (
      <div className={styles.page}>
        <div className={styles.notFound}>
          <h1>Suburb not found</h1>
          <p>We don&rsquo;t have data for <strong>{suburb}</strong>, {state.toUpperCase()}.</p>
          <Link to="/" className={styles.homeLink}>Go to home</Link>
        </div>
      </div>
    );
  }

  const pageTitle = `Petrol & Diesel Availability in ${centroid.displayName}, ${centroid.state} — FuelStock`;
  const pageDescription = `Check which petrol stations in ${centroid.displayName}, ${centroid.state} have fuel right now. Real-time crowdsourced reports for Diesel, ULP, E10, and Premium near ${centroid.postcode}.`;
  const canonicalUrl = `https://fuelstock.com.au/suburbs/${centroid.state.toLowerCase()}/${centroid.slug}`;

  const itemListJsonLd = stations && stations.length > 0
    ? buildItemListJsonLd(centroid.displayName, centroid.state, stations.map((s) => s.id))
    : null;

  return (
    <div className={styles.page}>
      <Helmet>
        <title>{pageTitle}</title>
        <meta name="description" content={pageDescription} />
        <link rel="canonical" href={canonicalUrl} />
        {itemListJsonLd && (
          <script type="application/ld+json">{JSON.stringify(itemListJsonLd)}</script>
        )}
      </Helmet>

      <header className={styles.header}>
        <Link to="/" className={styles.backLink}>← Back</Link>
        <h1 className={styles.heading}>
          Petrol &amp; Diesel Availability in {centroid.displayName}, {centroid.state}
        </h1>
        <p className={styles.subheading}>
          Real-time crowdsourced fuel reports within 10&nbsp;km of {centroid.displayName}
        </p>
      </header>

      <main className={styles.main}>
        {isLoading && (
          <ul className={styles.list}>
            {Array.from({ length: SKELETON_COUNT }).map((_, i) => (
              <li key={i}><StationCardSkeleton /></li>
            ))}
          </ul>
        )}

        {isError && (
          <div className={styles.error}>
            <AlertTriangle size={32} />
            <p>Couldn&rsquo;t load stations. Please try again.</p>
            <Link to="/" className={styles.homeLink}>Go to home</Link>
          </div>
        )}

        {!isLoading && !isError && stations && stations.length === 0 && (
          <div className={styles.empty}>
            <p>No stations reported within 10&nbsp;km of {centroid.displayName}.</p>
            <Link to="/" className={styles.homeLink}>Search near you</Link>
          </div>
        )}

        {!isLoading && !isError && stations && stations.length > 0 && (
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
