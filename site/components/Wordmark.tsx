/**
 * The wordmark: the mark, then "open" against "Dash" bold.
 *
 * The mark is media/logo.svg inlined rather than linked, because it is drawn in
 * purpose.ui.accent and an <img> could not take the colour from the surface it sits on. The path
 * is that file's path character for character; the plugin holds a third copy as
 * MarkShape.PathData. test/mark.test.ts holds this file, the favicon and the OpenGraph image to
 * media/logo.svg, so change that file first.
 */
import styles from './Wordmark.module.css';

export function Wordmark({ size = 24 }: { size?: number }) {
  return (
    <span className={styles.mark} style={{ '--wm': `${size}px` } as React.CSSProperties}>
      <svg viewBox="0 0 32 32" width={size} height={size} aria-hidden="true" focusable="false">
        <path
          fill="currentColor"
          fillRule="evenodd"
          d="M1,27 L1,5 L31,5 L31,27 Z M1,11 L1,8.5 L31,8.5 L31,11 Z M1,24.5 L1,22 L31,22 L31,24.5 Z M8.25,22 L8.25,11 L10.75,11 L10.75,22 Z M21.25,22 L21.25,11 L23.75,11 L23.75,22 Z"
        />
      </svg>
      <span className={styles.word}>
        <span className={styles.open}>open</span>
        <span className={styles.dash}>Dash</span>
      </span>
    </span>
  );
}
