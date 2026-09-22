'use client';

/**
 * An emulated LED strip: what a side / centre / side strip shows, drawn by the site.
 *
 * The centre is the rev bar, green then amber then red, flashing at the limiter. The sides are
 * lamps: a flag, a car alongside, the pit limiter, ABS. `live` runs a short script through all of
 * them; a `frame` draws one state and holds it. It is drawn by the browser rather than
 * photographed, which the caption beside it says, because no strip is plugged into the test
 * machine and a film of one would show nothing a drawing does not.
 */
import { useEffect, useState } from 'react';
import { FRAMES, type Lamp, type StripFrame } from '../lib/stripFrames';
import styles from './LedStrip.module.css';

/** The script the live strip runs, in seconds: revs climb and drop with each gear, the sides tell the rest. */
const LOOP = 18;

function scripted(t: number): StripFrame {
  const gear = t % 3;
  let revs = 0.3 + 0.7 * Math.min(1, gear / 2.6);
  const blink = gear > 2.6;
  if (gear > 2.85) revs = 0.35;
  let left: Lamp = 'off';
  let right: Lamp = 'off';
  let sidesBlink = false;
  let label = 'Revs, in the car’s own colours';
  if (t >= 3 && t < 6) [left, right, label] = ['blue', 'blue', 'Blue flag'];
  else if (t >= 6 && t < 8.5) [left, right, sidesBlink, label] = ['yellow', 'yellow', true, 'Waved yellow'];
  else if (t >= 8.5 && t < 11) [left, label] = ['white', 'A car on your left'];
  else if (t >= 11 && t < 12.5) [left, right, sidesBlink, label] = ['white', 'white', true, 'ABS'];
  else if (t >= 12.5 && t < 15) [left, right, sidesBlink, label] = ['amber', 'amber', true, 'Pit limiter'];
  else if (t >= 15 && t < 17) [left, right, label] = ['red', 'red', 'Low fuel'];
  if (t >= 12.5 && t < 15) revs = 0.2;
  return { revs, blink, left, right, sidesBlink, label };
}

export interface LedStripProps {
  left: number;
  centre: number;
  right: number;
  frame?: StripFrame;
  live?: boolean;
  led?: number;
  gap?: number;
  /** Show the label under the strip. */
  caption?: boolean;
}

const centreColour = (i: number, n: number): string => (i / n < 0.5 ? 'green' : i / n < 0.8 ? 'amber' : 'red');

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

  const f: StripFrame = frame ?? (live && !reduced ? scripted(t) : FRAMES.blue!);
  const phase = Math.floor(t * 8) % 2 === 0;
  const n = left + centre + right;
  const width = n * led + (n - 1) * gap;
  const litCentre = Math.round(f.revs * centre);

  return (
    <figure className={styles.figure}>
      <svg viewBox={`0 0 ${width} ${led}`} width={width} height={led} className={styles.strip} role="img" aria-label={`${left}/${centre}/${right} strip showing: ${f.label}`}>
        {Array.from({ length: n }, (_, i) => {
          const isLeft = i < left;
          const isRight = i >= left + centre;
          let lamp: Lamp = 'off';
          if (isLeft) lamp = f.left;
          else if (isRight) lamp = f.right;
          else {
            const c = i - left;
            lamp = c < litCentre ? (centreColour(c, centre) as Lamp) : 'off';
            if (f.blink && lamp !== 'off' && !phase) lamp = 'off';
          }
          if ((isLeft || isRight) && f.sidesBlink && !phase) lamp = 'off';
          return <rect key={i} x={i * (led + gap)} y={0} width={led} height={led} className={`${styles.led} ${styles[lamp]}`} />;
        })}
      </svg>
      {caption ? <figcaption className={styles.caption}>{f.label}</figcaption> : null}
    </figure>
  );
}
