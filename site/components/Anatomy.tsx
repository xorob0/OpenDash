'use client';

/**
 * The anatomy of a face: the real capture of the base size, with each of its parts called out.
 *
 * Every rectangle is the layout module's own, passed in from the generated HERO_FACE as
 * percentages of the picture, so the highlight lands on the part it names at any width and a
 * layout change that moves a zone moves the highlight with it. Nothing advances on its own: the
 * reader chooses a part by hovering, tapping, or with the arrow keys on the tab strip.
 */
import Image from 'next/image';
import { useState, type KeyboardEvent } from 'react';
import type { AnatomyPart } from '../lib/anatomy';
import styles from './Anatomy.module.css';

export interface AnatomyProps {
  src: string;
  alt: string;
  width: number;
  height: number;
  parts: AnatomyPart[];
}

export function Anatomy({ src, alt, width, height, parts }: AnatomyProps) {
  const [active, setActive] = useState(0);
  const part = parts[active] ?? parts[0]!;

  const pct = (p: AnatomyPart) => ({
    left: `${(p.rect.left / width) * 100}%`,
    top: `${(p.rect.top / height) * 100}%`,
    width: `${(p.rect.width / width) * 100}%`,
    height: `${(p.rect.height / height) * 100}%`,
  });

  /** Arrow keys move between tabs and move focus with them, wrapping at both ends. */
  const onTabKey = (index: number) => (event: KeyboardEvent<HTMLButtonElement>) => {
    const last = parts.length - 1;
    let next: number | null = null;
    if (event.key === 'ArrowRight' || event.key === 'ArrowDown') next = index === last ? 0 : index + 1;
    if (event.key === 'ArrowLeft' || event.key === 'ArrowUp') next = index === 0 ? last : index - 1;
    if (event.key === 'Home') next = 0;
    if (event.key === 'End') next = last;
    if (next === null) return;
    event.preventDefault();
    setActive(next);
    document.getElementById(`anatomy-tab-${parts[next]!.id}`)?.focus();
  };

  return (
    <div className={styles.wrap}>
      <div className={styles.stage} style={{ maxWidth: `${width}px` }}>
        <div className={styles.canvas} style={{ aspectRatio: `${width} / ${height}` }}>
          <Image src={src} alt={alt} width={width} height={height} sizes="(min-width: 88rem) 84rem, 100vw" className={styles.img} />
          {/*
            The dimming is one element with a hole rather than four strips around the part: a
            box-shadow spread far larger than the stage paints everything outside the highlight.
          */}
          <div className={styles.mask} style={pct(part)} aria-hidden="true" />
          {parts.map((p, i) => (
            <button
              key={p.id}
              type="button"
              className={styles.hit}
              style={pct(p)}
              onMouseEnter={() => setActive(i)}
              onClick={() => setActive(i)}
              // The tab strip is the keyboard control for these parts; the hit regions stay out of
              // the tab order rather than being a second set of stops that say the same things.
              tabIndex={-1}
              aria-hidden="true"
            />
          ))}
        </div>
      </div>

      <div className={styles.readout}>
        <div className={styles.tabs} role="tablist" aria-label="The parts of the face">
          {parts.map((p, i) => (
            <button
              key={p.id}
              id={`anatomy-tab-${p.id}`}
              role="tab"
              type="button"
              aria-selected={i === active}
              aria-controls="anatomy-panel"
              tabIndex={i === active ? 0 : -1}
              className={styles.tab}
              onClick={() => setActive(i)}
              onKeyDown={onTabKey(i)}
            >
              {p.tag}
            </button>
          ))}
        </div>
        <div className={styles.text} id="anatomy-panel" role="tabpanel" aria-labelledby={`anatomy-tab-${part.id}`} aria-live="polite" tabIndex={0}>
          <h3 className="h3">{part.name}</h3>
          <p className="prose">{part.body}</p>
        </div>
      </div>
    </div>
  );
}
