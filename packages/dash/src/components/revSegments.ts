/**
 * What the rev bar and the rev arc share: fifteen segments in two layers that never show
 * together. `shiftLights` (OpenDash.ShiftLights true, the default) lights three colour bands
 * from SimHub's per-car shift progress values and flashes the last band at redline; `rpmBar`
 * (setting false) shows the same segments as a plain RPM bar in text.secondary. The components
 * only differ in where the segments go: a row of snapped spans, or a circle with a rotation
 * per segment. ADR 0004: SimHub exposes the shift bands as progress values, not as bar
 * percentages, so this is the honest per-car rendering.
 */
import type { Hex, LayerItem, Rect } from '../generator.ts';
import { ncalc } from '../generator.ts';
import { withBindings, type Expr } from '../bind.ts';
import { setting } from '../contract.ts';
import { segment, type SegmentOptions } from '../elements/segment.ts';
import { ds } from '../tokens.ts';

const { game, gt, eq, mul, num, iff, str, not } = ncalc;

/** Half period of the redline flash in ms: 1000 / flashHz / 2, floored (62 at 8 Hz). */
export const REDLINE_BLINK_MS = Math.floor(1000 / ds.shiftLights.flashHz / 2);

const STAGE_COLORS: readonly Hex[] = [ds.purpose.shift.stage1, ds.purpose.shift.stage2, ds.purpose.shift.stage3];

const pad2 = (k: number): string => String(k + 1).padStart(2, '0');

/** `if(lit, colour, unlit)` */
const litColor = (lit: Expr, color: Hex): Expr => iff(lit, str(color), str(ds.purpose.shift.unlit));

/**
 * One band of the shift model, highest first. The rev bar lights its segments *within* a band;
 * anything that has only one thing to colour — the gear on the flag box — asks which band the
 * engine is in, and that question is answered here so there is one shift model rather than two.
 *
 * The thresholds are the band boundaries the bar already uses: a band is entered the moment its
 * first segment lights, which for stage 1 and 2 is their progress value leaving zero (ADR 0004:
 * these are progress values, not bar percentages). `rest` is the state below all of them and is
 * always raised, so ranking the list always lands somewhere.
 */
export interface ShiftBand {
  id: 'redline' | 'stage2' | 'stage1' | 'rest';
  colour: Hex;
  /** True when the engine is in this band or above it. */
  raised: Expr;
  /** The redline band flashes; the others do not. */
  blink: boolean;
}

export function shiftBands(): ShiftBand[] {
  return [
    { id: 'redline', colour: ds.purpose.shift.stage3, raised: eq(game('CarSettings_RPMRedLineReached'), num(1)), blink: true },
    { id: 'stage2', colour: ds.purpose.shift.stage2, raised: gt(game('CarSettings_RPMShiftLight2'), num(0)), blink: false },
    { id: 'stage1', colour: ds.purpose.shift.stage1, raised: gt(game('CarSettings_RPMShiftLight1'), num(0)), blink: false },
    // Below the first band there is nothing to report, so the digit is simply readable.
    { id: 'rest', colour: ds.color.text.primary, raised: 'true', blink: false },
  ];
}

/** Where segment k of `count` goes: its rect, and for a segment on a circle its rotation. */
export interface RevSegmentPlacement {
  rect: Rect;
  rotation?: number;
}

/** The bindings of one segment in each layer. */
export interface RevSegmentOptions {
  shift: SegmentOptions;
  rpm: SegmentOptions;
}

/** The shift stage (0, 1, 2) of segment k of `count`: thirds, the last third taking any remainder. */
export const stageOf = (k: number, count: number): number => Math.min(2, Math.floor((k * 3) / count));

/**
 * Bindings of segment k of `count`. Stage 1 segments light as `RPMShiftLight1 * n1 > local`,
 * stage 2 as `RPMShiftLight2 * n2 > local`, stage 3 at redline (with the redline flash); the
 * plain RPM bar lights a segment when the displayed RPM percentage passes `k * 100 / count`.
 */
export function revSegmentOptions(k: number, count: number): RevSegmentOptions {
  const stage = stageOf(k, count);
  const indexes = Array.from({ length: count }, (_, i) => i);
  const stageStart = indexes.findIndex((i) => stageOf(i, count) === stage);
  const stageCount = indexes.filter((i) => stageOf(i, count) === stage).length;
  const local = k - stageStart;
  const color = STAGE_COLORS[stage] ?? ds.purpose.shift.stage3;
  const redline = eq(game('CarSettings_RPMRedLineReached'), num(1));
  const lit =
    stage === 0
      ? gt(mul(game('CarSettings_RPMShiftLight1'), num(stageCount)), num(local))
      : stage === 1
        ? gt(mul(game('CarSettings_RPMShiftLight2'), num(stageCount)), num(local))
        : redline;
  const threshold = Math.round((k * 100 * 100) / count) / 100;
  const rpmLit = gt(game('CarSettings_CurrentDisplayedRPMPercent'), num(threshold));
  return {
    shift: { colorBind: litColor(lit, color), ...(stage === 2 ? { blinkBind: redline, blinkDelayMs: REDLINE_BLINK_MS } : {}) },
    rpm: { colorBind: litColor(rpmLit, ds.color.text.secondary) },
  };
}

/** The two layers, `<prefix>.shiftLights` and `<prefix>.rpmBar`, each holding one segment per placement. */
export function revLayers(prefix: string, placements: readonly RevSegmentPlacement[]): [LayerItem, LayerItem] {
  const count = placements.length;
  const build = (layer: 'shift' | 'rpm'): LayerItem['children'] =>
    placements.map((p, k) => segment(`${prefix}.${layer}.${pad2(k)}`, p.rect, ds.purpose.shift.unlit, { ...revSegmentOptions(k, count)[layer], rotation: p.rotation }));
  const shiftLights: LayerItem = {
    kind: 'layer',
    name: `${prefix}.shiftLights`,
    children: build('shift'),
    ...withBindings({ Visible: setting.shiftLights() }),
  };
  const rpmBar: LayerItem = {
    kind: 'layer',
    name: `${prefix}.rpmBar`,
    children: build('rpm'),
    ...withBindings({ Visible: not(setting.shiftLights()) }),
  };
  return [shiftLights, rpmBar];
}
