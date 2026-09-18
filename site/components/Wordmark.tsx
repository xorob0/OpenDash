/**
 * The wordmark: the mark, then "open" light against "Dash" bold.
 *
 * The mark is media/logo.svg inlined rather than linked, because it is drawn in
 * purpose.ui.accent and an <img> could not take the colour from the surface it sits on. The path
 * is that file's path character for character; the plugin holds a third copy as
 * MarkShape.PathData, and MarkTests compares the two. Change media/logo.svg first.
 */
import styles from './Wordmark.module.css';

export function Wordmark({ size = 24 }: { size?: number }) {
  return (
    <span className={styles.mark} style={{ '--wm': `${size}px` } as React.CSSProperties}>
      <svg viewBox="0 0 32 32" width={size} height={size} aria-hidden="true" focusable="false">
        <path
          fill="currentColor"
          fillRule="evenodd"
          d="M1,27 L1,20 A15,15 0 0 1 31,20 L31,27 Z M14.173,17.621 L23.388,10.544 L18.751,21.197 A3,3 0 1 1 14.173,17.621 Z"
        />
      </svg>
      <span className={styles.word}>
        <span className={styles.open}>open</span>
        <span className={styles.dash}>Dash</span>
      </span>
    </span>
  );
}
