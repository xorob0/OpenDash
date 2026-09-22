'use client';

/**
 * Find your screen: every face drawn to scale, each one showing what it draws.
 *
 * A buyer arrives knowing the size of their panel and nothing else, so the sizes are the shapes
 * they are, at one common scale: a 1920 x 480 strip is four times the width of a 480 round and the
 * same height, which is the whole comparison a reader is making. Each cell is that face's own
 * capture rather than an empty rectangle, and each face shows different pages, so the wall says
 * what twenty-one pages means as well as what ten sizes means.
 *
 * Choosing one plays its clip in the cell it is already in. The picture the cell was showing is
 * the clip's first frame, so nothing jumps; with motion reduced, or with no clip for that size,
 * the picture simply stays. The choice is written to the URL hash, so a link can point at a size.
 */
import Image from 'next/image';
import Link from 'next/link';
import { useEffect, useRef, useState } from 'react';
import type { PickerFace } from '../lib/faces';
import { sizeLabel, weigh } from '../lib/packages';
import { INSTALL } from '../lib/site';
import styles from './ScreenPicker.module.css';

export interface ScreenPickerProps {
  faces: PickerFace[];
  /** The size chosen before anyone touches it. */
  initial: string;
}

export function ScreenPicker({ faces, initial }: ScreenPickerProps) {
  const [selected, setSelected] = useState(initial);
  const [motion, setMotion] = useState(true);
  const group = useRef<HTMLDivElement>(null);
  const face = faces.find((f) => f.slug === selected) ?? faces[0]!;

  useEffect(() => {
    const reduced = window.matchMedia('(prefers-reduced-motion: reduce)');
    setMotion(!reduced.matches);
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
          const playing = on && motion && f.clip !== null;
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
              {f.capture ? (
                <Image
                  src={f.capture}
                  alt={`The ${sizeLabel(f)} face`}
                  width={f.width}
                  height={f.height}
                  sizes="(min-width: 62rem) 50vw, 90vw"
                  className={styles.picture}
                />
              ) : null}
              {playing ? (
                <video className={styles.video} width={f.width} height={f.height} autoPlay muted loop playsInline preload="none" aria-hidden="true">
                  <source src={f.clip!.webm} type="video/webm" />
                  <source src={f.clip!.mp4} type="video/mp4" />
                </video>
              ) : null}
              <span className={`num ${styles.size}`}>{sizeLabel(f)}</span>
            </button>
          );
        })}
      </div>

      <p className={styles.get} aria-live="polite">
        <Link href={INSTALL.href} className={styles.install}>
          {INSTALL.label} the plugin
        </Link>
        <span className={styles.file}>
          Every size comes with it.{' '}
          {face.bytes !== undefined ? (
            <>
              Or just{' '}
              <a href={`/downloads/${face.file}`} download className="link">
                {face.file} <span className="num">{weigh(face.bytes)}</span>
              </a>
              .
            </>
          ) : null}
        </span>
      </p>
    </div>
  );
}
