/**
 * Shot: one capture of a package, framed and labelled with the size it was drawn at.
 *
 * Every image this component draws is a photograph of the package rendering live telemetry through
 * SimHub's own renderer at its own size — `bun run shots` on the Windows VM. None is a mock-up and
 * none is a recreation, which is worth the frame saying: the size under the picture is the size the
 * pixels actually are, so a reader can tell that the 850 is a smaller screen rather than a smaller
 * picture of the same screen.
 *
 * The frame is a 1 px rule, never a box with a shadow, and the image is never upscaled past 1:1 —
 * a dash face blown up past its own pixels looks soft in a way the panel never does.
 */
import Image from 'next/image';
import styles from './Shot.module.css';

export interface ShotProps {
  src: string;
  alt: string;
  width: number;
  height: number;
  /** Shown under the frame. Usually the size; a scenario when the point is the state, not the size. */
  caption?: string;
  /** A round face is the display itself, so it is masked to the disc rather than framed square. */
  round?: boolean;
  priority?: boolean;
  /** Passed to next/image; the default assumes a full-width figure in the page column. */
  sizes?: string;
}

export function Shot({ src, alt, width, height, caption, round, priority, sizes = '(min-width: 88rem) 84rem, 100vw' }: ShotProps) {
  return (
    <figure className={styles.figure}>
      <div className={`${styles.frame} ${round ? styles.round : ''}`} style={{ aspectRatio: `${width} / ${height}` }}>
        <Image
          src={src}
          alt={alt}
          width={width}
          height={height}
          sizes={sizes}
          priority={priority}
          className={styles.img}
        />
      </div>
      {caption ? <figcaption className={styles.caption}>{caption}</figcaption> : null}
    </figure>
  );
}
