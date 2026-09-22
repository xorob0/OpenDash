'use client';

/**
 * Find your screen: the ten faces drawn to scale, one selected, its capture and its download.
 *
 * A buyer arrives knowing the size of their panel and nothing else, so the sizes are drawn as the
 * shapes they are, at one common scale, rather than listed as numbers. Every rectangle is sized
 * from the same virtual canvas, so a 1920 x 480 strip is four times the width of a 480 round and
 * the same height, which is the whole comparison a reader is making.
 *
 * Selecting a size shows its capture at its own pixel width. The way in is the plugin, which
 * installs every size; the single file is there for whoever wants only that one. The selection is
 * written to the URL hash, so a link can point at a size. Without JavaScript the default is
 * rendered by the server and the ruled list under the picker on the Screens page lists every size.
 */
import Image from 'next/image';
import Link from 'next/link';
import { useEffect, useRef, useState } from 'react';
import type { PickerFace } from '../lib/faces';
import { sizeLabel, weigh } from '../lib/packages';
import { INSTALL } from '../lib/site';
import frame from './Capture.module.css';
import styles from './ScreenPicker.module.css';

export interface ScreenPickerProps {
  faces: PickerFace[];
  /** The slug selected before anyone touches it: the base size. */
  initial: string;
}

export function ScreenPicker({ faces, initial }: ScreenPickerProps) {
  const [selected, setSelected] = useState(initial);
  const group = useRef<HTMLDivElement>(null);
  const face = faces.find((f) => f.slug === selected) ?? faces[0]!;

  // A hash names a size, so `/screens#opendash-1920x480` opens on it.
  useEffect(() => {
    const hash = window.location.hash.slice(1);
    if (faces.some((f) => f.slug === hash)) setSelected(hash);
  }, [faces]);

  const choose = (slug: string, focus = false) => {
    setSelected(slug);
    window.history.replaceState(null, '', `#${slug}`);
    if (focus) group.current?.querySelector<HTMLButtonElement>(`[data-slug="${slug}"]`)?.focus();
  };

  const onKeyDown = (event: React.KeyboardEvent, index: number) => {
    const last = faces.length - 1;
    let next: number | null = null;
    if (event.key === 'ArrowRight' || event.key === 'ArrowDown') next = index === last ? 0 : index + 1;
    if (event.key === 'ArrowLeft' || event.key === 'ArrowUp') next = index === 0 ? last : index - 1;
    if (event.key === 'Home') next = 0;
    if (event.key === 'End') next = last;
    if (next === null) return;
    event.preventDefault();
    choose(faces[next]!.slug, true);
  };

  return (
    <div className={styles.picker}>
      <div ref={group} role="radiogroup" aria-label="Face size" className={styles.shapes}>
        {faces.map((f, i) => {
          const on = f.slug === face.slug;
          return (
            <button
              key={f.slug}
              type="button"
              role="radio"
              aria-checked={on}
              tabIndex={on ? 0 : -1}
              data-slug={f.slug}
              className={[styles.shape, f.round ? styles.round : '', on ? styles.on : ''].filter(Boolean).join(' ')}
              style={{ '--w': f.width, '--h': f.height } as React.CSSProperties}
              onClick={() => choose(f.slug)}
              onKeyDown={(e) => onKeyDown(e, i)}
            >
              <span className={`num ${styles.size}`}>{sizeLabel(f)}</span>
            </button>
          );
        })}
      </div>

      <div className={styles.detail} aria-live="polite" key={face.slug}>
        <figure className={frame.figure}>
          <div
            className={[frame.frame, face.round ? frame.round : '', !face.round && face.width / face.height > 2.2 ? frame.pan : ''].filter(Boolean).join(' ')}
            style={{ aspectRatio: `${face.width} / ${face.height}`, maxWidth: `${face.width}px` }}
          >
            {face.capture ? (
              <Image src={face.capture} alt={`The ${sizeLabel(face)} face`} width={face.width} height={face.height} sizes="(min-width: 88rem) 84rem, 100vw" className={frame.img} />
            ) : (
              <div className={frame.missing} role="img" aria-label={`The ${sizeLabel(face)} face, not photographed yet`}>
                <span className="label">Not photographed yet</span>
              </div>
            )}
          </div>
        </figure>

        <div className={styles.about}>
          <p className={`h3 ${styles.name}`}>
            <span className="num">{sizeLabel(face)}</span>
          </p>
          <p className="prose">Install the plugin and every size comes with it, this one included.</p>
          <p className={styles.get}>
            <Link href={INSTALL.href} className={styles.install}>
              {INSTALL.label} the plugin
            </Link>
          </p>
          <p className={`prose ${styles.file}`}>
            {face.bytes !== undefined ? (
              <>
                Or just this file:{' '}
                <a href={`/downloads/${face.file}`} download className="link">
                  {face.file} <span className="num">{weigh(face.bytes)}</span>
                </a>
                . Default pages, no settings, no updates.
              </>
            ) : (
              <span className={styles.notBuilt}>The single file is not in this build.</span>
            )}
          </p>
        </div>
      </div>
    </div>
  );
}
