'use client';

/**
 * The fake Rig and Data panel beside the demo: the plugin's settings for one face, as controls that
 * write the same properties the plugin writes. `lib/demo/panel.ts` holds the behaviour; this is
 * only its controls.
 */
import { useRef } from 'react';
import {
  beginGlance,
  cycle,
  endGlance,
  faceByPrefix,
  setPage,
  setPageEnabled,
  update,
  type PanelCatalogue,
  type PanelState,
} from '../lib/demo/panel';
import styles from './Demo.module.css';

export interface DemoPanelProps {
  catalogue: PanelCatalogue;
  state: PanelState;
  onChange: (next: (s: PanelState) => PanelState) => void;
  /** How many slots a round face reads. */
  slots: number;
}

const ZONE_NAMES: Record<string, string> = { A: 'Zone A', B: 'Zone B', C: 'Zone C', D: 'Band D' };
const SLOT_LABELS: Record<string, string> = { Left1: 'Bar, left', Left2: 'Bar, left inner', Right1: 'Bar, right', Right2: 'Bar, right inner' };

/** `lastLap` -> `Last lap`; a word the contract spells in camel case, as a reader would. */
const words = (value: string | boolean): string => {
  if (typeof value === 'boolean') return value ? 'On' : 'Off';
  const spaced = value.replace(/([a-z])([A-Z])/g, '$1 $2').toLowerCase();
  return spaced.charAt(0).toUpperCase() + spaced.slice(1);
};

function Select<T extends string | number | boolean>({
  label,
  value,
  options,
  onChange,
  compact,
}: {
  label: string;
  value: T;
  options: readonly { value: T; label: string }[];
  onChange: (v: T) => void;
  compact?: boolean;
}) {
  return (
    <label className={`${styles.field} ${compact ? styles.compact : ''}`}>
      <span className={styles.fieldLabel}>{label}</span>
      <select
        className={styles.select}
        value={String(value)}
        onChange={(e) => {
          const found = options.find((o) => String(o.value) === e.target.value);
          if (found) onChange(found.value);
        }}
      >
        {options.map((o) => (
          <option key={String(o.value)} value={String(o.value)}>
            {o.label}
          </option>
        ))}
      </select>
    </label>
  );
}

export function DemoPanel({ catalogue, state, onChange, slots }: DemoPanelProps) {
  const face = faceByPrefix(catalogue, state.face);
  const holding = useRef(false);
  const hold = (on: boolean) => {
    if (holding.current === on) return;
    holding.current = on;
    onChange(on ? beginGlance : endGlance);
  };
  const glanceOptions = catalogue.zones.flatMap((z, i) => z.pages.map((p) => ({ value: i * 100 + p.number, label: `${ZONE_NAMES[z.letter]}: ${p.name}` })));
  const barSlots = face ? catalogue.barSlots.filter((s) => face.hasBar && (face.barFieldsPerEnd === 2 || s.endsWith('1'))) : [];

  return (
    <div className={styles.panel}>
      {face ? (
        <section className={styles.group} aria-labelledby="demo-zones">
          <h2 id="demo-zones" className={styles.groupTitle}>
            Screens: {face.width} × {face.height}
          </h2>
          {catalogue.zones.map((z, i) => (
            <div key={z.letter} className={styles.zone}>
              <Select compact label={ZONE_NAMES[z.letter]!} value={state.zones[i]!} options={z.pages.map((p) => ({ value: p.number, label: p.name }))} onChange={(v) => onChange((s) => setPage(catalogue, s, z.letter, v))} />
              <div className={styles.wheel} role="group" aria-label={`Wheel buttons for ${ZONE_NAMES[z.letter]}`}>
                <button type="button" className={styles.wheelButton} onClick={() => onChange((s) => cycle(catalogue, s, z.letter, -1))} aria-label={`${ZONE_NAMES[z.letter]}, previous page`}>
                  ‹
                </button>
                <button type="button" className={styles.wheelButton} onClick={() => onChange((s) => cycle(catalogue, s, z.letter, 1))} aria-label={`${ZONE_NAMES[z.letter]}, next page`}>
                  ›
                </button>
              </div>
              <details className={styles.pages}>
                <summary>Pages in the cycle</summary>
                <ul className={styles.pageList}>
                  {z.pages.map((p) => {
                    const on = Math.floor(state.masks[i]! / 2 ** p.number) % 2 === 1;
                    return (
                      <li key={p.number}>
                        <label className={styles.check}>
                          <input type="checkbox" checked={on} onChange={(e) => onChange((s) => setPageEnabled(catalogue, s, z.letter, p.number, e.target.checked))} />
                          {p.name}
                        </label>
                      </li>
                    );
                  })}
                </ul>
                <label className={styles.check}>
                  <input type="checkbox" checked={state.classOnly[i]!} onChange={(e) => onChange((s) => update(s, 'classOnly', s.classOnly.map((v, k) => (k === i ? e.target.checked : v))))} />
                  Lists show my class only
                </label>
              </details>
            </div>
          ))}

          <div className={styles.glance}>
            <Select label="Quick glance" value={state.quickGlance} options={glanceOptions} onChange={(v) => onChange((s) => update(s, 'quickGlance', v))} />
            <button
              type="button"
              className={`${styles.holdButton} ${state.glance ? styles.held : ''}`}
              aria-pressed={state.glance !== null}
              onPointerDown={(e) => {
                e.currentTarget.setPointerCapture(e.pointerId);
                hold(true);
              }}
              onPointerUp={() => hold(false)}
              onPointerCancel={() => hold(false)}
              onLostPointerCapture={() => hold(false)}
              onKeyDown={(e) => {
                if (e.key === ' ' || e.key === 'Enter') {
                  e.preventDefault();
                  hold(true);
                }
              }}
              onKeyUp={(e) => {
                if (e.key === ' ' || e.key === 'Enter') hold(false);
              }}
              onBlur={() => hold(false)}
            >
              Hold to glance
            </button>
          </div>

          {barSlots.map((slot) => {
            const i = catalogue.barSlots.indexOf(slot);
            return (
              <Select
                key={slot}
                label={SLOT_LABELS[slot] ?? slot}
                value={state.barFields[i]!}
                options={catalogue.barFields.map((f) => ({ value: f.number, label: f.name }))}
                onChange={(v) => onChange((s) => update(s, 'barFields', s.barFields.map((x, k) => (k === i ? v : x))))}
              />
            );
          })}
          <Select label="Flags" value={state.flagFormat} options={catalogue.flagFormats.map((v) => ({ value: v, label: v === 'band' ? 'Over the band' : 'Over the whole face' }))} onChange={(v) => onChange((s) => update(s, 'flagFormat', v))} />
          <Select label="Lap review" value={state.lapReview} options={catalogue.lapReviewModes.map((v) => ({ value: v, label: v === 'off' ? 'Off' : v === 'race' ? 'In a race' : 'In every session' }))} onChange={(v) => onChange((s) => update(s, 'lapReview', v))} />
          <Select label="Rev bar" value={state.revBar} options={catalogue.revBarModes.map((v) => ({ value: v, label: words(v) }))} onChange={(v) => onChange((s) => update(s, 'revBar', v))} />
        </section>
      ) : (
        <section className={styles.group} aria-labelledby="demo-slots">
          <h2 id="demo-slots" className={styles.groupTitle}>
            Slots
          </h2>
          {catalogue.slotNames.slice(0, slots).map((name, i) => (
            <Select
              key={name}
              label={`Slot ${i + 1}`}
              value={state.slots[i]!}
              options={catalogue.cards.map((c) => ({ value: c.number, label: c.name }))}
              onChange={(v) => onChange((s) => update(s, 'slots', s.slots.map((x, k) => (k === i ? v : x))))}
            />
          ))}
        </section>
      )}

      <section className={styles.group} aria-labelledby="demo-data">
        <h2 id="demo-data" className={styles.groupTitle}>
          Data, every screen
        </h2>
        {catalogue.rig
          .filter((c) => !(face && c.setting === 'RevBar'))
          .map((c) => (
            <Select
              key={c.setting}
              label={c.label}
              value={state.rig[c.setting] ?? c.default}
              options={c.values.map((v) => ({ value: v, label: words(v) }))}
              onChange={(v) => onChange((s) => update(s, 'rig', { ...s.rig, [c.setting]: v }))}
            />
          ))}
      </section>
    </div>
  );
}
