import { Link } from 'react-router-dom';
import { ArrowLeft } from 'lucide-react';
import styles from './PageHeader.module.css';

interface Props {
  backTo: string;
  title: string;
  subtitle?: string;
}

export default function PageHeader({ backTo, title, subtitle }: Props) {
  return (
    <header className={styles.header}>
      <Link to={backTo} className={styles.back}><ArrowLeft size={14} /> Back</Link>
      <h1 className={styles.title}>{title}</h1>
      {subtitle && <p className={styles.subtitle}>{subtitle}</p>}
    </header>
  );
}
