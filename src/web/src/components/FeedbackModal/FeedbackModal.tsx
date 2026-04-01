import { useState, type FormEvent } from 'react';
import { X } from 'lucide-react';
import styles from './FeedbackModal.module.css';

const SUBJECT_OPTIONS = [
  'Bug report',
  'Feature request',
  'Data / station issue',
  'Other',
] as const;

type SubjectOption = (typeof SUBJECT_OPTIONS)[number];

interface Props {
  open: boolean;
  onClose: () => void;
}

export default function FeedbackModal({ open, onClose }: Props) {
  const [subject, setSubject] = useState<SubjectOption>('Bug report');
  const [description, setDescription] = useState('');

  if (!open) return null;

  function handleSubmit(e: FormEvent<HTMLFormElement>) {
    e.preventDefault();
    const title = encodeURIComponent(subject);
    const body = encodeURIComponent(description.trim());
    const url = `https://github.com/hdkaran/fuelfinder-au/issues/new?title=${title}&body=${body}`;
    window.open(url, '_blank', 'noopener,noreferrer');
    onClose();
  }

  return (
    <>
      <div className={styles.backdrop} onClick={onClose} />
      <div className={styles.sheet} role="dialog" aria-modal="true" aria-label="Report a bug or suggest a feature">
        <div className={styles.handle} />
        <div className={styles.header}>
          <h2 className={styles.title}>Send feedback</h2>
          <button className={styles.closeBtn} onClick={onClose} aria-label="Close" type="button">
            <X size={18} />
          </button>
        </div>

        <form className={styles.body} onSubmit={handleSubmit}>
          <div className={styles.field}>
            <label className={styles.label} htmlFor="feedback-subject">
              Subject
            </label>
            <select
              id="feedback-subject"
              className={styles.select}
              value={subject}
              onChange={(e) => setSubject(e.target.value as SubjectOption)}
            >
              {SUBJECT_OPTIONS.map((opt) => (
                <option key={opt} value={opt}>{opt}</option>
              ))}
            </select>
          </div>

          <div className={styles.field}>
            <label className={styles.label} htmlFor="feedback-description">
              Description
            </label>
            <textarea
              id="feedback-description"
              className={styles.textarea}
              placeholder="Describe the issue or feature…"
              value={description}
              onChange={(e) => setDescription(e.target.value)}
              rows={5}
            />
          </div>

          <button
            type="submit"
            className={styles.submitBtn}
            disabled={description.trim().length === 0}
          >
            Open GitHub issue
          </button>
        </form>
      </div>
    </>
  );
}
