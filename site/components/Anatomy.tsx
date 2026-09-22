'use client';

/**
 * The anatomy of a face: the real 1920 x 480 capture, with each of its parts called out in turn.
 *
 * Every rectangle below is read from `packages/dash/src/zones/faces/1920x480.ts`, which is itself
 * read off the design canvas artboard. They are percentages of the same 1920 x 480 the photograph
 * is, so the highlight lands on the part it names at any width the page is read at, and a layout
 * change that moves a zone shows up here as a highlight in the wrong place rather than as nothing.
 *
 * It advances on its own until somebody touches it, and then stops for good: an explainer that
 * keeps moving while you are reading the item you chose is an explainer that is fighting you.
 * With reduced motion asked for it never advances at all, and every part is reachable by keyboard.
 *
 * On a phone the stage is a horizontal scroller and the picture is drawn at a height a face can be
 * read at, which is why the image, the mask and the hit regions all live inside one `canvas`
 * element rather than being positioned against the stage: the percentages have to be percentages
 * of the picture, not of the window onto it. Choosing a part then scrolls it into view, so the tab
 * strip keeps working as the control when the part itself is off to one side.
 */
import { useEffect, useRef, useState, type KeyboardEvent } from 'react';
import Image from 'next/image';
import styles from './Anatomy.module.css';

/** The reference face's own coordinate system. Both numbers appear in the layout module. */
const W = 1920;
const H = 480;

interface Part {
  id: string;
  /** What the canvas calls it: the letter for a zone, the name for everything else. */
  tag: string;
  name: string;
  body: string;
  rect: { x: number; y: number; w: number; h: number };
}

const PARTS: Part[] = [
  {
    id: 'rev',
    tag: 'Rev bar',
    name: 'The shift lights of the car you are in',
    body: 'Fifteen segments in a recessed well, lit at the RPMs the sim publishes for that car, and falling back to SimHub’s per-car bands for a car that publishes none. It can show a plain RPM bar instead, or be switched off for a wheel that already has LEDs across its top — and when it goes, the well goes with it and the body grows into the room.',
    rect: { x: 0, y: 0, w: 1920, h: 48 },
  },
  {
    id: 'bar',
    tag: 'The bar',
    name: 'What does not change during a lap',
    body: 'Two fields at each end with their own catalogue — race time, lap, position, class — and between them the car settings your sim exposes: traction control, brake bias, ABS, engine map. The bar is the one part that does not cycle, which is what earns it the space.',
    rect: { x: 0, y: 48, w: 1920, h: 56 },
  },
  {
    id: 'zoneB',
    tag: 'Zone B',
    name: 'One of twenty-one pages',
    body: 'Lap times here: last, session best and your own best, with the lap count, the estimate and the delta under them. A wheel button pages it to any of the other twenty. Nothing about this is a menu.',
    rect: { x: 0, y: 105, w: 769, h: 314 },
  },
  {
    id: 'zoneA',
    tag: 'Zone A',
    name: 'The gear, where the eye already goes',
    body: 'The middle column holds the gear, because the gear is read by reflex rather than looked up. It can hold gear and speed, speed alone, or the track map instead — and the neighbours are ghosted either side so a change of gear is a movement, not a jump.',
    rect: { x: 770, y: 105, w: 380, h: 314 },
  },
  {
    id: 'zoneC',
    tag: 'Zone C',
    name: 'The same catalogue, a second time',
    body: 'The relative here, you in the middle, with the car number and class beside every driver. How many drivers it lists is what a bigger screen buys you: seven at 850 by 480, ten here, eighteen at 1280 by 720. A page sheds its secondary rows before it shrinks its numerals.',
    rect: { x: 1151, y: 105, w: 769, h: 314 },
  },
  {
    id: 'band',
    tag: 'Band D',
    name: 'Fuel, until a flag needs the room',
    body: 'A wide, shallow strip with eight pages that suit that shape, fuel by default. While a flag or an alert is out it takes the band over entirely, because an alert outranks fuel and the two should share one strip rather than each keep their own.',
    rect: { x: 0, y: 420, w: 1920, h: 60 },
  },
];

const pct = (part: Part) => ({
  left: `${(part.rect.x / W) * 100}%`,
  top: `${(part.rect.y / H) * 100}%`,
  width: `${(part.rect.w / W) * 100}%`,
  height: `${(part.rect.h / H) * 100}%`,
});

export function Anatomy({ src }: { src: string }) {
  const [active, setActive] = useState(0);
  const [held, setHeld] = useState(false);
  const stage = useRef<HTMLDivElement>(null);
  const highlight = useRef<HTMLDivElement>(null);

  useEffect(() => {
    if (held) return;
    if (window.matchMedia('(prefers-reduced-motion: reduce)').matches) return;
    const timer = window.setInterval(() => setActive((i) => (i + 1) % PARTS.length), 4200);
    return () => window.clearInterval(timer);
  }, [held]);

  // Bring the chosen part into view when the stage is narrower than the picture, which is the
  // phone case. `nearest` so a part already on screen does not jump.
  useEffect(() => {
    const box = stage.current;
    const mark = highlight.current;
    if (!box || !mark) return;
    if (box.scrollWidth <= box.clientWidth + 1) return;
    mark.scrollIntoView({ behavior: 'smooth', block: 'nearest', inline: 'center' });
  }, [active]);

  const choose = (index: number) => {
    setActive(index);
    setHeld(true);
  };

  /** Arrow keys move between tabs and move focus with them, wrapping at both ends. */
  const onTabKey = (index: number) => (event: KeyboardEvent<HTMLButtonElement>) => {
    const step = event.key === 'ArrowRight' ? 1 : event.key === 'ArrowLeft' ? -1 : event.key === 'Home' ? -index : event.key === 'End' ? PARTS.length - 1 - index : 0;
    if (step === 0) return;
    event.preventDefault();
    const next = (index + step + PARTS.length) % PARTS.length;
    choose(next);
    document.getElementById(`anatomy-tab-${PARTS[next]!.id}`)?.focus();
  };

  const part = PARTS[active]!;

  return (
    <div className={styles.wrap}>
      <div className={styles.stage} ref={stage}>
        <div className={styles.canvas}>
          <Image
            src={src}
            alt="The OpenDash face at 1920 by 480, photographed rendering live telemetry in SimHub"
            width={W}
            height={H}
            sizes="(min-width: 88rem) 84rem, 100vw"
            className={styles.img}
            priority
          />

          {/*
            The dimming is one element with a hole rather than four strips around the part: a
            box-shadow spread far larger than the stage paints everything outside the highlight, so
            the mask moves as a single animated rectangle and never seams at a corner.
          */}
          <div className={styles.mask} style={pct(part)} ref={highlight} aria-hidden="true" />

          {PARTS.map((p, i) => (
            <button
              key={p.id}
              type="button"
              className={styles.hit}
              style={pct(p)}
              onMouseEnter={() => choose(i)}
              onClick={() => choose(i)}
              // The tab strip below is the keyboard control for exactly these six parts, so these
              // stay out of the tab order rather than being a second set of six stops that say the
              // same things. They are hidden from assistive technology for the same reason.
              tabIndex={-1}
              aria-hidden="true"
            />
          ))}
        </div>
      </div>

      <div className={styles.readout}>
        <div className={styles.tabs} role="tablist" aria-label="The parts of the face">
          {PARTS.map((p, i) => (
            <button
              key={p.id}
              id={`anatomy-tab-${p.id}`}
              role="tab"
              type="button"
              aria-selected={i === active}
              aria-controls="anatomy-panel"
              // Only the selected tab is in the tab order; the arrow keys move between them, which
              // is what the tablist pattern asks for and what stops six buttons from swallowing six
              // presses of Tab on the way past.
              tabIndex={i === active ? 0 : -1}
              className={styles.tab}
              onClick={() => choose(i)}
              onKeyDown={onTabKey(i)}
            >
              {p.tag}
            </button>
          ))}
        </div>

        {/*
          Re-keyed on the part, so React replaces the node and the swap animation runs again. The
          live region is here rather than on the wrapper so that the announcement is the copy that
          changed, not the whole component including the tab strip.
        */}
        <div
          className={styles.text}
          key={part.id}
          id="anatomy-panel"
          role="tabpanel"
          aria-labelledby={`anatomy-tab-${part.id}`}
          aria-live="polite"
          tabIndex={0}
        >
          <h3 className="h3">{part.name}</h3>
          <p className="prose">{part.body}</p>
        </div>
      </div>
    </div>
  );
}
