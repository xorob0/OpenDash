/**
 * Capture: one photograph of a package, framed and labelled with the size it was drawn at.
 *
 * Every image this component draws is the package rendering live telemetry through SimHub's own
 * renderer at its own size, taken on the Windows VM by `bun run shots`. None is a mock-up. The
 * frame says so: the size under the picture is the size the pixels actually are, and a reader can
 * tell that the 850 is a smaller screen rather than a smaller picture of the same screen.
 *
 * The frame is a 1 px rule, never a box with a shadow, and it is capped at the capture's own pixel
 * width so a face is never drawn larger than the panel it was photographed on. That cap is what
 * makes a gallery of faces legible as a gallery: a 480 round beside a 1920 face reads as the much
 * smaller screen it is. It also stops the small ones going soft.
 *
 * A wide face is pannable on a phone. Fitting a 1920 x 480 strip into a 358 px column leaves it
 * 89 px tall, which is a picture of where a dashboard was rather than of a dashboard. Below the
 * breakpoint those are given a readable height and allowed to overflow sideways, so the reader
 * drags across the face. Anything squarer than 2.2:1 is legible fitted and is left alone.
 *
 * A capture the sidecar does not know is drawn as an empty frame that says so, rather than as a
 * broken image: the panel pictures arrive with the reshoot, and a page may ship before them.
 */
import Image from 'next/image';
import { CAPTURES, hasCapture, longDate } from '../lib/captures';
import { PANNABLE_ASPECT } from './frame';
import styles from './Capture.module.css';

export interface CaptureProps {
  /** The file under public/shots, e.g. `opendash-850x480.png`. */
  file: string;
  alt: string;
  width: number;
  height: number;
  /** Shown under the frame. Usually the size. */
  caption?: string;
  /** A round face is the display itself, so it is masked to the disc rather than framed square. */
  round?: boolean;
  priority?: boolean;
  /** Passed to next/image; the default assumes a full-width figure in the page column. */
  sizes?: string;
  /** Let the frame grow past the capture's own pixel width, for the one hero that earns it. */
  scale?: number;
}

export function Capture({ file, alt, width, height, caption, round, priority, sizes = '(min-width: 88rem) 84rem, 100vw', scale = 1 }: CaptureProps) {
  const pannable = !round && width / height > PANNABLE_ASPECT;
  const known = hasCapture(file);
  return (
    <figure className={styles.figure}>
      <div
        className={[styles.frame, round ? styles.round : '', pannable ? styles.pan : ''].filter(Boolean).join(' ')}
        style={{ aspectRatio: `${width} / ${height}`, maxWidth: `${Math.round(width * scale)}px` }}
      >
        {known ? (
          <Image src={`/shots/${file}`} alt={alt} width={width} height={height} sizes={sizes} priority={priority} className={styles.img} />
        ) : (
          <div className={styles.missing} role="img" aria-label={alt}>
            <span className="label">Not photographed yet</span>
          </div>
        )}
      </div>
      {caption ? <figcaption className={styles.caption}>{caption}</figcaption> : null}
    </figure>
  );
}

/**
 * The provenance line a gallery shows once: which version the pictures are from, and a warning
 * when that is not the version being served.
 */
export function CaptureNote({ served }: { served: string }) {
  const stale = CAPTURES.version !== '' && CAPTURES.version !== served;
  return (
    <p className={styles.note}>
      Every picture is the package itself, rendered by SimHub on a green-flag lap at Spa. Captured from {CAPTURES.version || 'an unknown version'} on{' '}
      {longDate(CAPTURES.date)}.{stale ? ` The download is ${served}.` : ''}
    </p>
  );
}
