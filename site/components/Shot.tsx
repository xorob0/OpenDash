/**
 * Shot: one capture of a package, framed and labelled with the size it was drawn at.
 *
 * Every image this component draws is a photograph of the package rendering live telemetry through
 * SimHub's own renderer at its own size — `bun run shots` on the Windows VM. None is a mock-up and
 * none is a recreation, which is worth the frame saying: the size under the picture is the size the
 * pixels actually are, so a reader can tell that the 850 is a smaller screen rather than a smaller
 * picture of the same screen.
 *
 * The frame is a 1 px rule, never a box with a shadow, and the frame is capped at the capture's own
 * pixel width so a face is never drawn larger than the panel it was photographed on. That cap is
 * what makes a gallery of faces legible as a gallery: a 480 round beside a 1920 face reads as the
 * much smaller screen it is, instead of both filling the column and looking like the same product
 * at two crops. It also stops the small ones going soft, which is what upscaling a dash face does.
 *
 * A wide face is pannable on a phone. Fitting a 1920 x 480 strip into a 358 px column leaves it
 * 89 px tall, which is not a screenshot of a dashboard so much as a picture of where one was. Below
 * the breakpoint those are given a readable height and allowed to overflow their frame sideways, so
 * the reader drags across the face instead of squinting at all of it at once. Anything squarer
 * than 2.2:1 is legible fitted and is left alone.
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

/** Wider than this and the picture is not legible fitted to a phone's column. */
const PANNABLE_ASPECT = 2.2;

export function Shot({ src, alt, width, height, caption, round, priority, sizes = '(min-width: 88rem) 84rem, 100vw' }: ShotProps) {
  const pannable = !round && width / height > PANNABLE_ASPECT;
  return (
    <figure className={styles.figure}>
      <div
        className={[styles.frame, round ? styles.round : '', pannable ? styles.pan : ''].filter(Boolean).join(' ')}
        style={{ aspectRatio: `${width} / ${height}`, maxWidth: `${width}px` }}
      >
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
