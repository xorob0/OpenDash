/**
 * A strip shape as a row of squares: the sides, which are lamps, drawn hollow, and the centre,
 * which is the rev bar, drawn filled. Everything the site draws of a strip is this.
 */
import styles from './StripGlyph.module.css';

export interface StripGlyphProps {
  left: number;
  centre: number;
  right: number;
  /** Pixel size of one LED. */
  led?: number;
  gap?: number;
  title?: string;
}

export function StripGlyph({ left, centre, right, led = 10, gap = 3, title }: StripGlyphProps) {
  const n = left + centre + right;
  const width = n * led + (n - 1) * gap;
  return (
    <svg viewBox={`0 0 ${width} ${led}`} width={width} height={led} className={styles.glyph} role={title ? 'img' : undefined} aria-label={title} aria-hidden={title ? undefined : true}>
      {Array.from({ length: n }, (_, i) => {
        const side = i < left || i >= left + centre;
        return <rect key={i} x={i * (led + gap)} y={0} width={led} height={led} className={side ? styles.side : styles.centre} />;
      })}
    </svg>
  );
}
