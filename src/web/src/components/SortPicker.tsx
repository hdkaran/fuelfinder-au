import styles from './RadiusPicker.module.css';

export const SORT_OPTIONS = [
  { label: 'Nearest',   value: 'distance'  },
  { label: 'Available', value: 'status'    },
  { label: 'Freshest',  value: 'freshness' },
] as const;

export type SortValue = typeof SORT_OPTIONS[number]['value'];

interface Props {
  value: SortValue;
  onChange: (sort: SortValue) => void;
}

export default function SortPicker({ value, onChange }: Props) {
  return (
    <div className={styles.bar} role="group" aria-label="Sort stations">
      {SORT_OPTIONS.map((opt) => (
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
