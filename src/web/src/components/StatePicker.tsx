import styles from './StatePicker.module.css';

const STATES = ['All', 'NSW', 'VIC', 'QLD', 'WA', 'SA', 'TAS', 'NT', 'ACT'] as const;
export type StateFilter = (typeof STATES)[number];

interface Props {
  value: StateFilter;
  onChange: (state: StateFilter) => void;
}

export default function StatePicker({ value, onChange }: Props) {
  return (
    <div className={styles.wrapper}>
      <div className={styles.track}>
        {STATES.map((s) => (
          <button
            key={s}
            className={`${styles.pill} ${value === s ? styles.active : ''}`}
            onClick={() => onChange(s)}
          >
            {s}
          </button>
        ))}
      </div>
    </div>
  );
}
