import styles from './ReportBar.module.css';

interface ReportBarProps {
  onClick: () => void;
}

export function ReportBar({ onClick }: ReportBarProps) {
  return (
    <div className={styles.bar} role="region" aria-label="Report action">
      <button className={styles.reportBtn} onClick={onClick} type="button">
        Report Fuel Status
      </button>
    </div>
  );
}
