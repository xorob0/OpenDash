'use client';

/**
 * An emulated LED strip: what a strip shows, drawn by the site rather than photographed.
 *
 * The centre is the rev bar, green to amber to red, flashing at the top of the sweep. The sides are
 * lamps of one LED each, counted from the outside in, and which role lands on which LED is the
 * generated `LAMPS` table, which is the profile's own. `live` runs a script through everything a
 * strip says on a lap; a `frame` draws one state and holds it.
 *
 * No strip is plugged into the test machine, so this is a drawing and the page says so. It is the
 * one picture on the site that is not a photograph.
 */
import { useEffect, useState } from 'react';
import { LAMPS } from '../lib/content.generated';
import { LOOP, scripted, type Ink, type Lit, type StripFrame } from '../lib/stripFrames';
import styles from './LedStrip.module.css';

export interface LedStripProps {
  left: number;
  centre: number;
  right: number;
  frame?: StripFrame;
  live?: boolean;
  /** Pixel size of one LED. */
  led?: number;
  gap?: number;
  caption?: boolean;
}

/** The ladder the centre climbs: green, then amber, then red. */
const rev = (i: number, n: number): Ink => (i / n < 0.5 ? 'good' : i / n < 0.8 ? 'caution' : 'danger');

/** What a lamp of a side shows: the first of its roles that has something to say. */
function lampInk(frame: StripFrame, side: 'left' | 'right', index: number, count: number): Lit | null {
  const lamp = LAMPS[Math.min(count, LAMPS.length - 1)]?.[index];
  if (!lamp) return null;
  for (const role of lamp.carries) {
    if (role === 'side' && frame.spotter?.[side]) return { ink: 'caution' };
    if (role === 'race' && frame.race) return frame.race;
    if (role === 'car' && frame.car) return frame.car;
    if (role === 'aid' && frame.aid) return frame.aid;
  }
  return null;
}

export function LedStrip({ left, centre, right, frame, live = false, led = 14, gap = 5, caption = true }: LedStripProps) {
  const [t, setT] = useState(0);
  const [reduced, setReduced] = useState(false);

  useEffect(() => {
    if (!live) return;
    const motion = window.matchMedia('(prefers-reduced-motion: reduce)');
    if (motion.matches) {
      setReduced(true);
      return;
    }
    let raf = 0;
    const start = performance.now();
    const tick = (now: number) => {
      setT(((now - start) / 1000) % LOOP);
      raf = requestAnimationFrame(tick);
    };
    raf = requestAnimationFrame(tick);
    return () => cancelAnimationFrame(raf);
  }, [live]);

  const f: StripFrame = frame ?? (live && !reduced ? scripted(t) : { label: 'Revs, in the car’s own colours', revs: 0.7 });
  // One clock for every blink on the strip, so two lamps blinking are in step, as the profile's are.
  const on = Math.floor(t * 4) % 2 === 0 || !live;
  const n = left + centre + right;
  const width = n * led + (n - 1) * gap;
  const lit = Math.round(f.revs * centre);

  const inkOf = (i: number): Ink | null => {
    if (f.strip) return f.strip.blink && !on ? null : f.strip.ink;
    if (i < left || i >= left + centre) {
      const side = i < left ? 'left' : 'right';
      const index = i < left ? i : n - 1 - i;
      const count = side === 'left' ? left : right;
      const lamp = lampInk(f, side, index, count);
      if (!lamp) return null;
      return lamp.blink && !on ? null : lamp.ink;
    }
    const c = i - left;
    if (c >= lit) return null;
    if (f.shift && !on) return null;
    return rev(c, centre);
  };

  return (
    <figure className={styles.figure}>
      <svg
        viewBox={`0 0 ${width} ${led}`}
        width={width}
        height={led}
        className={styles.strip}
        role="img"
        aria-label={`A ${left}/${centre}/${right} strip showing: ${f.label}`}
      >
        {Array.from({ length: n }, (_, i) => {
          const ink = inkOf(i);
          return <rect key={i} x={i * (led + gap)} y={0} width={led} height={led} className={`${styles.led} ${ink ? styles[ink] : ''}`} />;
        })}
      </svg>
      {caption ? <figcaption className={styles.caption}>{f.label}</figcaption> : null}
    </figure>
  );
}
