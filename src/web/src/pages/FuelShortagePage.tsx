import { Link } from 'react-router-dom';
import { Helmet } from 'react-helmet-async';
import { Fuel, AlertTriangle, MapPin } from 'lucide-react';
import { useGetStatsSummaryQuery } from '../api/fuelFinderApi';
import { pluralise } from '../utils/format';
import styles from './FuelShortagePage.module.css';

const PAGE_TITLE = 'Petrol Shortage Australia — Which Stations Have Fuel | FuelStock';
const PAGE_DESCRIPTION =
  'Track the Australian petrol shortage in real time. See which stations near you have Diesel, ULP, E10, or Premium — crowdsourced reports updated every minute by drivers across Australia.';

export default function FuelShortagePage() {
  const { data: stats, isLoading } = useGetStatsSummaryQuery(undefined, {
    pollingInterval: 60_000,
  });

  return (
    <div className={styles.page}>
      <Helmet>
        <title>{PAGE_TITLE}</title>
        <meta name="description" content={PAGE_DESCRIPTION} />
        <link rel="canonical" href="https://fuelstock.com.au/fuel-shortage-australia" />
      </Helmet>

      <header className={styles.header}>
        <Link to="/" className={styles.backLink}>← Back to map</Link>
        <h1 className={styles.heading}>
          <span className={styles.accent}>Fuel</span>Stock — Petrol Shortage Australia
        </h1>
        <p className={styles.subheading}>
          Real-time crowdsourced petrol availability across Australia
        </p>
      </header>

      <main className={styles.main}>
        <section className={styles.liveSection}>
          <h2 className={styles.sectionTitle}>Live status</h2>

          {isLoading && (
            <div className={styles.statsRow}>
              <div className={styles.statCardSkeleton} />
              <div className={styles.statCardSkeleton} />
            </div>
          )}

          {stats && (
            <div className={styles.statsRow}>
              <div className={styles.statCard}>
                <Fuel size={24} className={styles.statIcon} />
                <span className={styles.statValue}>{stats.totalReportsToday.toLocaleString()}</span>
                <span className={styles.statLabel}>{pluralise(stats.totalReportsToday, 'report')} today</span>
              </div>
              <div className={styles.statCard}>
                <AlertTriangle size={24} className={styles.statIcon} />
                <span className={styles.statValue}>{stats.stationsAffected.toLocaleString()}</span>
                <span className={styles.statLabel}>{pluralise(stats.stationsAffected, 'station')} affected</span>
              </div>
            </div>
          )}
        </section>

        <section className={styles.infoSection}>
          <h2 className={styles.sectionTitle}>About the petrol shortage</h2>
          <p className={styles.bodyText}>
            During fuel shortage events in Australia, panic buying can empty petrol stations
            within hours. FuelStock lets drivers report real-time fuel availability at
            their local station so other drivers can check before they travel.
          </p>
          <p className={styles.bodyText}>
            Reports cover all fuel types — <strong>Diesel</strong>, <strong>ULP (Unleaded)</strong>,
            <strong> E10</strong>, and <strong>Premium</strong> — and include queue length
            observations so you know what to expect on arrival.
          </p>
        </section>

        <section className={styles.infoSection}>
          <h2 className={styles.sectionTitle}>Find fuel near you now</h2>
          <p className={styles.bodyText}>
            Use your location to see live crowdsourced fuel reports from stations within
            your chosen radius — updated every two minutes.
          </p>
          <Link to="/" className={styles.ctaBtn}>
            <MapPin size={16} />
            Find petrol near me
          </Link>
        </section>

        <section className={styles.infoSection}>
          <h2 className={styles.sectionTitle}>Check fuel by suburb</h2>
          <p className={styles.bodyText}>Looking for fuel in a specific area?</p>
          <ul className={styles.suburbLinks}>
            <li><Link to="/suburbs/nsw/sydney">Sydney, NSW</Link></li>
            <li><Link to="/suburbs/vic/melbourne">Melbourne, VIC</Link></li>
            <li><Link to="/suburbs/qld/brisbane">Brisbane, QLD</Link></li>
            <li><Link to="/suburbs/wa/perth">Perth, WA</Link></li>
            <li><Link to="/suburbs/sa/adelaide">Adelaide, SA</Link></li>
            <li><Link to="/suburbs/act/canberra">Canberra, ACT</Link></li>
          </ul>
        </section>
      </main>
    </div>
  );
}
