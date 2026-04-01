import styles from './StationCardSkeleton.module.css';

export default function StationCardSkeleton() {
  return (
    <div className={styles.card}>
      <div className={styles.top}>
        <div className={styles.nameGroup}>
          <div className={`${styles.bone} ${styles.name}`} />
          <div className={`${styles.bone} ${styles.brand}`} />
        </div>
        <div className={`${styles.bone} ${styles.pill}`} />
      </div>
      <div className={`${styles.bone} ${styles.meta}`} />
      <div className={styles.fuels}>
        <div className={`${styles.bone} ${styles.badge}`} />
        <div className={`${styles.bone} ${styles.badge}`} />
        <div className={`${styles.bone} ${styles.badge}`} />
        <div className={`${styles.bone} ${styles.badge}`} />
      </div>
    </div>
  );
}
