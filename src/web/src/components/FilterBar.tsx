import { useState } from 'react';
import { ChevronDown, X } from 'lucide-react';
import { RADIUS_OPTIONS, type RadiusValue } from './RadiusPicker';
import { type SortValue } from './SortPicker';
import { type StateFilter } from './StatePicker';
import FilterSheet from './FilterSheet';
import styles from './FilterBar.module.css';

// ── Constants ──────────────────────────────────────────────────────────────

const DEFAULT_RADIUS: RadiusValue = 5_000;
const DEFAULT_STATE: StateFilter = 'All';

const SORT_SEGMENTS: { label: string; value: SortValue; icon: string }[] = [
  { label: 'Nearest',   value: 'distance',  icon: '📍' },
  { label: 'Available', value: 'status',    icon: '✅' },
  { label: 'Freshest',  value: 'freshness', icon: '🕐' },
];

const DISTANCE_OPTIONS = RADIUS_OPTIONS.map((o) => ({
  label: o.label,
  value: String(o.value),
}));

const STATE_OPTIONS: { label: string; value: string }[] = [
  { label: 'All states', value: 'All'  },
  { label: 'NSW',        value: 'NSW'  },
  { label: 'VIC',        value: 'VIC'  },
  { label: 'QLD',        value: 'QLD'  },
  { label: 'WA',         value: 'WA'   },
  { label: 'SA',         value: 'SA'   },
  { label: 'TAS',        value: 'TAS'  },
  { label: 'NT',         value: 'NT'   },
  { label: 'ACT',        value: 'ACT'  },
];

// ── Component ──────────────────────────────────────────────────────────────

interface Props {
  sort: SortValue;
  onSortChange: (s: SortValue) => void;
  radius: RadiusValue;
  onRadiusChange: (r: RadiusValue) => void;
  stateFilter: StateFilter;
  onStateChange: (s: StateFilter) => void;
}

export default function FilterBar({
  sort, onSortChange,
  radius, onRadiusChange,
  stateFilter, onStateChange,
}: Props) {
  const [openSheet, setOpenSheet] = useState<'distance' | 'state' | null>(null);

  const isDistanceDefault = radius === DEFAULT_RADIUS;
  const isStateDefault    = stateFilter === DEFAULT_STATE;
  const hasActiveFilters  = !isDistanceDefault || !isStateDefault;

  const distanceLabel = RADIUS_OPTIONS.find((o) => o.value === radius)?.label ?? '5 km';
  const stateLabel    = stateFilter === 'All' ? 'All states' : stateFilter;

  function resetAll() {
    onRadiusChange(DEFAULT_RADIUS);
    onStateChange(DEFAULT_STATE);
  }

  return (
    <>
      <div className={styles.filterBar}>
        {/* ── Row 1: Segmented sort control ─────────────────────────────── */}
        <div className={styles.segmentedRow}>
          <div className={styles.segmented} role="group" aria-label="Sort stations">
            {SORT_SEGMENTS.map(({ label, value, icon }) => (
              <button
                key={value}
                className={`${styles.segment} ${sort === value ? styles.segmentActive : ''}`}
                onClick={() => onSortChange(value)}
                aria-pressed={sort === value}
              >
                {/* icon is decorative — excluded from accessible name */}
                <span className={styles.segmentIcon} aria-hidden="true">{icon}</span>
                {label}
              </button>
            ))}
          </div>
        </div>

        {/* ── Row 2: Context chips ───────────────────────────────────────── */}
        <div className={styles.chipRow} role="group" aria-label="Filter stations">
          {/* Distance chip + optional × */}
          <div className={styles.chipGroup}>
            <button
              className={`${styles.chip} ${!isDistanceDefault ? styles.chipActive : ''}`}
              onClick={() => setOpenSheet('distance')}
            >
              <span aria-hidden="true" className={styles.chipEmoji}>📍</span>
              {/* Text node contributes to accessible name; decorations do not */}
              {distanceLabel}
              <ChevronDown
                size={13}
                aria-hidden="true"
                className={`${styles.chipCaret} ${!isDistanceDefault ? styles.chipCaretHidden : ''}`}
              />
            </button>
            {!isDistanceDefault && (
              <button
                className={styles.chipX}
                aria-label="Reset distance filter"
                onClick={() => onRadiusChange(DEFAULT_RADIUS)}
              >
                <X size={11} strokeWidth={2.5} />
              </button>
            )}
          </div>

          {/* State chip + optional × */}
          <div className={styles.chipGroup}>
            <button
              className={`${styles.chip} ${!isStateDefault ? styles.chipActive : ''}`}
              onClick={() => setOpenSheet('state')}
            >
              <span aria-hidden="true" className={styles.chipEmoji}>🗺</span>
              {stateLabel}
              <ChevronDown
                size={13}
                aria-hidden="true"
                className={`${styles.chipCaret} ${!isStateDefault ? styles.chipCaretHidden : ''}`}
              />
            </button>
            {!isStateDefault && (
              <button
                className={styles.chipX}
                aria-label="Reset state filter"
                onClick={() => onStateChange(DEFAULT_STATE)}
              >
                <X size={11} strokeWidth={2.5} />
              </button>
            )}
          </div>

          {/* Reset all — only when any filter is active */}
          {hasActiveFilters && (
            <button className={styles.chipResetAll} onClick={resetAll}>
              Reset all
            </button>
          )}
        </div>
      </div>

      {/* Bottom sheets rendered via portal */}
      {openSheet === 'distance' && (
        <FilterSheet
          title="Distance"
          options={DISTANCE_OPTIONS}
          value={String(radius)}
          onSelect={(v) => {
            onRadiusChange(Number(v) as RadiusValue);
            setOpenSheet(null);
          }}
          onClose={() => setOpenSheet(null)}
        />
      )}
      {openSheet === 'state' && (
        <FilterSheet
          title="State"
          options={STATE_OPTIONS}
          value={stateFilter}
          onSelect={(v) => {
            onStateChange(v as StateFilter);
            setOpenSheet(null);
          }}
          onClose={() => setOpenSheet(null)}
        />
      )}
    </>
  );
}
