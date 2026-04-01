import { useState, useEffect } from 'react';
import { X, Share, Download } from 'lucide-react';
import styles from './InstallBanner.module.css';

const DISMISSED_KEY = 'fuelfinder:install-dismissed';

// Minimal type for the deferred install prompt event
interface BeforeInstallPromptEvent extends Event {
  prompt(): Promise<void>;
  userChoice: Promise<{ outcome: 'accepted' | 'dismissed' }>;
}

function isIosSafari(): boolean {
  const ua = navigator.userAgent;
  return /iphone|ipad|ipod/i.test(ua) && /safari/i.test(ua) && !/crios|fxios/i.test(ua);
}

function isStandalone(): boolean {
  return (
    (typeof window.matchMedia === 'function' &&
      window.matchMedia('(display-mode: standalone)').matches) ||
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    (navigator as any).standalone === true
  );
}

export default function InstallBanner() {
  const [deferredPrompt, setDeferredPrompt] = useState<BeforeInstallPromptEvent | null>(null);
  const [showIos, setShowIos] = useState(false);
  const [visible, setVisible] = useState(false);

  useEffect(() => {
    // Already installed or user dismissed before
    if (isStandalone()) return;
    if (sessionStorage.getItem(DISMISSED_KEY)) return;

    if (isIosSafari()) {
      setShowIos(true);
      setVisible(true);
      return;
    }

    const handler = (e: Event) => {
      e.preventDefault();
      setDeferredPrompt(e as BeforeInstallPromptEvent);
      setVisible(true);
    };
    window.addEventListener('beforeinstallprompt', handler);
    return () => window.removeEventListener('beforeinstallprompt', handler);
  }, []);

  function dismiss() {
    sessionStorage.setItem(DISMISSED_KEY, '1');
    setVisible(false);
  }

  async function install() {
    if (!deferredPrompt) return;
    await deferredPrompt.prompt();
    const { outcome } = await deferredPrompt.userChoice;
    if (outcome === 'accepted') setVisible(false);
    setDeferredPrompt(null);
  }

  if (!visible) return null;

  return (
    <div className={styles.banner} role="complementary" aria-label="Install app">
      <div className={styles.inner}>
        {showIos ? (
          <>
            <Share size={16} className={styles.icon} aria-hidden="true" />
            <span className={styles.text}>
              Tap <strong>Share</strong> then <strong>Add to Home Screen</strong> to install
            </span>
          </>
        ) : (
          <>
            <Download size={16} className={styles.icon} aria-hidden="true" />
            <span className={styles.text}>Add FuelStock to your home screen</span>
            <button className={styles.installBtn} onClick={install}>Install</button>
          </>
        )}
      </div>
      <button className={styles.close} onClick={dismiss} aria-label="Dismiss">
        <X size={14} />
      </button>
    </div>
  );
}
