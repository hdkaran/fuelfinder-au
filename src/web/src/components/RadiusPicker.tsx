import styles from './RadiusPicker.module.css';

export const RADIUS_OPTIONS = [
  { label: '2 km',  value: 2_000 },
  { label: '5 km',  value: 5_000 },
  { label: '10 km', value: 10_000 },
  { label: '25 km', value: 25_000 },
  { label: '50 km', value: 50_000 },
] as const;

export type RadiusValue = typeof RADIUS_OPTIONS[number]['value'];

interface Props {
  value: RadiusValue;
  onChange: (radius: RadiusValue) => void;
}

export default function RadiusPicker({ value, onChange }: Props) {
  return (
    <div className={styles.bar} role="group" aria-label="Search radius">
      {RADIUS_OPTIONS.map((opt) => (
        <button
          key={opt.value}
          className={`${styles.pill} ${value === opt.value ? styles.active : ''}`}
          onClick={() => onChange(opt.value)}
          aria-pressed={value === opt.value}
        >
          {opt.label}
        </button>
      ))}
    </div>
  );
}
