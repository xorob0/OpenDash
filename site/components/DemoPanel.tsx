'use client';

/**
 * The fake Rig and Data panel beside the demo: the plugin's settings for one screen, as controls
 * that write the same properties the plugin writes. `lib/demo/panel.ts` holds the behaviour; this is
 * only its controls. Their names are the plugin's own (`plugin/OpenDash/PanelScreens.cs`).
 */
import { useRef } from 'react';
import {
  beginCompanionGlance,
  beginGlance,
  cycle,
  endCompanionGlance,
  endGlance,
  faceByPrefix,
  pitWallPagesFor,
  setCompanionStart,
  setModuleEnabled,
  setPage,
  setPageEnabled,
  setPitWallPage,
  setPitWallZone,
  update,
  updatePitWall,
  zonesOf,
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
  /** Whether a pit wall is the landscape one, which has three pages, or the portrait one. */
  landscape: boolean;
  /** SimHub's NextScreen (1) and PreviousScreen (-1), for a companion. */
  onNavigate: (direction: 1 | -1) => void;
}

const ZONE_NAMES: Record<string, string> = { A: 'Zone A', B: 'Zone B', C: 'Zone C', D: 'Band D' };
const SLOT_LABELS: Record<string, string> = { Left1: 'Bar, left', Left2: 'Bar, left inner', Right1: 'Bar, right', Right2: 'Bar, right inner' };
/** `PanelScreens.BarFlagLabels`, the companion's and the pit wall's three answers. */
const SCREEN_FLAG_LABELS: Record<string, string> = { off: 'Off', band: 'Bar', full: 'Full screen' };

/** `lastLap` -> `Last lap`; a word the contract spells in camel case, as a reader would. */
const words = (value: string | boolean): string => {
  if (typeof value === 'boolean') return value ? 'On' : 'Off';
  const spaced = value.replace(/([a-z])([A-Z])/g, '$1 $2').toLowerCase();
  return spaced.charAt(0).toUpperCase() + spaced.slice(1);
};

/** The time the companion's forces are measured in, the same clock the demo's loop reads. */
const now = (): number => performance.now();

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

/** A button held down for as long as a pointer or a key is, as a glance button on a wheel is. */
function HoldButton({ held, onHold, children }: { held: boolean; onHold: (on: boolean) => void; children: React.ReactNode }) {
  const holding = useRef(false);
  const hold = (on: boolean) => {
    if (holding.current === on) return;
    holding.current = on;
    onHold(on);
  };
  return (
    <button
      type="button"
      className={`${styles.holdButton} ${held ? styles.held : ''}`}
      aria-pressed={held}
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
      {children}
    </button>
  );
}

function FaceSettings({ catalogue, state, onChange }: Pick<DemoPanelProps, 'catalogue' | 'state' | 'onChange'>) {
  const face = faceByPrefix(catalogue, state.face)!;
  const zones = zonesOf(catalogue, state);
  const theme = catalogue.themes.find((t) => t.id === state.theme);
  const glanceOptions = zones.flatMap((z, i) => z.pages.map((p) => ({ value: i * 100 + p.number, label: `${ZONE_NAMES[z.letter]}: ${p.name}` })));
  const barSlots = catalogue.barSlots.filter((s) => face.hasBar && (face.barFieldsPerEnd === 2 || s.endsWith('1')));
  return (
    <section className={styles.group} aria-labelledby="demo-zones">
      <h2 id="demo-zones" className={styles.groupTitle}>
        Screens: {theme && theme.id !== 'default' ? `${theme.name}, ` : ''}
        {face.width} × {face.height}
      </h2>
      {zones.map((z, i) => (
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
        <HoldButton held={state.glance !== null} onHold={(on) => onChange(on ? beginGlance : endGlance)}>
          Hold to glance
        </HoldButton>
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
  );
}

function CompanionSettings({ catalogue, state, onChange, onNavigate }: Pick<DemoPanelProps, 'catalogue' | 'state' | 'onChange' | 'onNavigate'>) {
  const c = catalogue.companion;
  const on = state.companion.modules;
  const startChoices = c.modules.filter((_, i) => on[i]).map((m) => ({ value: m.number - 1, label: m.name }));
  return (
    <section className={styles.group} aria-labelledby="demo-companion">
      <h2 id="demo-companion" className={styles.groupTitle}>
        Companion
      </h2>
      <div className={styles.zone}>
        <span className={styles.fieldLabel}>Next module</span>
        <div className={styles.wheel} role="group" aria-label="SimHub's PreviousScreen and NextScreen">
          <button type="button" className={styles.wheelButton} onClick={() => onNavigate(-1)} aria-label="Previous module (PreviousScreen)">
            ‹
          </button>
          <button type="button" className={styles.wheelButton} onClick={() => onNavigate(1)} aria-label="Next module (NextScreen)">
            ›
          </button>
        </div>
      </div>
      <p className={styles.note}>SimHub pages a companion itself, as a tap on either half of the screen does. Tap the dashboard to try it.</p>
      <Select label="First module" value={state.companion.start} options={startChoices} onChange={(v) => onChange((s) => setCompanionStart(catalogue, s, v, now()))} />
      <div className={styles.glance}>
        <Select
          label="Quick glance"
          value={state.companion.glance}
          options={c.modules.map((m) => ({ value: m.number - 1, label: m.name }))}
          onChange={(v) => onChange((s) => ({ ...s, companion: { ...s.companion, glance: v } }))}
        />
        <HoldButton held={state.companion.held} onHold={(down) => onChange((s) => (down ? beginCompanionGlance(s) : endCompanionGlance(catalogue, s, now())))}>
          Hold to glance
        </HoldButton>
      </div>
      <Select
        label="Flags"
        value={state.companion.flagFormat}
        options={c.flagFormats.map((v) => ({ value: v, label: SCREEN_FLAG_LABELS[v] ?? words(v) }))}
        onChange={(v) => onChange((s) => ({ ...s, companion: { ...s.companion, flagFormat: v } }))}
      />
      <details className={styles.pages}>
        <summary>
          Modules ({on.filter(Boolean).length} / {c.modules.length})
        </summary>
        <ul className={styles.pageList}>
          {c.modules.map((m, i) => (
            <li key={m.id}>
              <label className={styles.check}>
                <input type="checkbox" checked={on[i]!} onChange={(e) => onChange((s) => setModuleEnabled(s, i, e.target.checked))} />
                {m.name}
              </label>
            </li>
          ))}
        </ul>
      </details>
    </section>
  );
}

function PitWallSettings({ catalogue, state, onChange, landscape }: Pick<DemoPanelProps, 'catalogue' | 'state' | 'onChange' | 'landscape'>) {
  const w = catalogue.pitWall;
  const pages = pitWallPagesFor(catalogue, landscape);
  return (
    <section className={styles.group} aria-labelledby="demo-pitwall">
      <h2 id="demo-pitwall" className={styles.groupTitle}>
        Pit wall{landscape ? '' : ', portrait'}
      </h2>
      {landscape ? <Select label="Page on screen" value={state.pitWall.page} options={pages.map((p, i) => ({ value: i, label: p.name }))} onChange={(v) => onChange((s) => setPitWallPage(s, v))} /> : null}
      {pages.map((page) => (
        <div key={page.id} className={styles.subgroup}>
          <h3 className={styles.subTitle}>{landscape ? `${page.name} zones` : 'Portrait layout'}</h3>
          {page.zones.map((z) => (
            <Select
              key={z.setting}
              compact
              label={z.kind === 'wide' ? 'Wide zone' : `Zone ${z.slot}`}
              value={state.pitWall.zones[z.setting] ?? z.fallback}
              options={(z.kind === 'wide' ? w.widePages : w.standardPages).map((p) => ({ value: p.number, label: p.name }))}
              onChange={(v) => onChange((s) => setPitWallZone(s, z.setting, v))}
            />
          ))}
        </div>
      ))}
      <label className={styles.check}>
        <input type="checkbox" checked={state.pitWall.classOnly} onChange={(e) => onChange((s) => updatePitWall(s, 'classOnly', e.target.checked))} />
        Lists show my class only
      </label>
      <Select label="Flags" value={state.pitWall.flagFormat} options={w.flagFormats.map((v) => ({ value: v, label: SCREEN_FLAG_LABELS[v] ?? words(v) }))} onChange={(v) => onChange((s) => updatePitWall(s, 'flagFormat', v))} />
      <label className={styles.field}>
        <span className={styles.fieldLabel}>Web view address</span>
        <input
          type="url"
          className={styles.select}
          value={state.pitWall.webViewUrl}
          placeholder="https://"
          onChange={(e) => {
            const value = e.target.value;
            onChange((s) => updatePitWall(s, 'webViewUrl', value));
          }}
        />
      </label>
    </section>
  );
}

export function DemoPanel({ catalogue, state, onChange, slots, landscape, onNavigate }: DemoPanelProps) {
  const face = faceByPrefix(catalogue, state.face);
  return (
    <div className={styles.panel}>
      {state.screen === 'companion' ? (
        <CompanionSettings catalogue={catalogue} state={state} onChange={onChange} onNavigate={onNavigate} />
      ) : state.screen === 'pitwall' ? (
        <PitWallSettings catalogue={catalogue} state={state} onChange={onChange} landscape={landscape} />
      ) : face ? (
        <FaceSettings catalogue={catalogue} state={state} onChange={onChange} />
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
