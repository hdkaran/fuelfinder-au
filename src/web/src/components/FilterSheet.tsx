import { createPortal } from 'react-dom';
import { X } from 'lucide-react';
import styles from './FilterSheet.module.css';

interface Option {
  label: string;
  value: string;
}

interface Props {
  title: string;
  options: Option[];
  value: string;
  onSelect: (value: string) => void;
  onClose: () => void;
}

export default function FilterSheet({ title, options, value, onSelect, onClose }: Props) {
  return createPortal(
    <>
      <div className={styles.backdrop} onClick={onClose} aria-hidden="true" />
      <div
        className={styles.sheet}
        role="dialog"
        aria-modal="true"
        aria-label={`Choose ${title.toLowerCase()}`}
      >
        <div className={styles.handle} />
        <div className={styles.header}>
          <h2 className={styles.title}>{title}</h2>
          <button className={styles.closeBtn} onClick={onClose} aria-label="Close">
            <X size={18} />
          </button>
        </div>
        <div className={styles.optionsGrid}>
          {options.map((opt) => (
            <button
              key={opt.value}
              className={`${styles.option} ${opt.value === value ? styles.optionActive : ''}`}
              onClick={() => onSelect(opt.value)}
              aria-pressed={opt.value === value}
            >
              {opt.label}
            </button>
          ))}
        </div>
      </div>
    </>,
    document.body,
  );
}
