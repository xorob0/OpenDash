/**
 * What the rev bar and the rev arc share: fifteen segments in three layers, never more than one
 * of them showing. `shiftLights` (OpenDash.ShiftLights true, the default) lights three colour
 * bands and flashes the last one; `rpmBar` (setting false) shows the same segments as a plain RPM
 * bar in text.secondary. The components only differ in where the segments go: a row of snapped
 * spans, or a circle with a rotation per segment.
 *
 * The shift bands come from the car's own shift-light RPMs where it publishes them and from
 * SimHub's per-car bands where it does not (ADR 0014, and ADR 0004 for the fallback). That choice
 * is per frame, so the two ladders are two layers whose visibility is bound to it: whichever of
 * `shiftLights` and `shiftLightsSimHub` is visible in Dash Studio is the one in use, which is how
 * somebody debugging a car finds out which ladder it is on. The model itself is in shift.ts.
 */
import type { Hex, LayerItem, Rect } from '../generator.ts';
import { ncalc } from '../generator.ts';
import { withBindings, type Expr } from '../bind.ts';
import { setting } from '../contract.ts';
import { segment, type SegmentOptions } from '../elements/segment.ts';
import { mirrorAvailable, mirrorOverRev, mirrorStageLit, simhubRedline, simhubStageLit } from '../shift.ts';
import { ds } from '../tokens.ts';

const { game, gt, num, iff, str, not, and } = ncalc;

/** Half period of the redline flash in ms: 1000 / flashHz / 2, floored (62 at 8 Hz). */
export const REDLINE_BLINK_MS = Math.floor(1000 / ds.shiftLights.flashHz / 2);

const STAGE_COLORS: readonly Hex[] = [ds.purpose.shift.stage1, ds.purpose.shift.stage2, ds.purpose.shift.stage3];

const pad2 = (k: number): string => String(k + 1).padStart(2, '0');

/** `if(lit, colour, unlit)` */
const litColor = (lit: Expr, color: Hex): Expr => iff(lit, str(color), str(ds.purpose.shift.unlit));

/** Where segment k of `count` goes: its rect, and for a segment on a circle its rotation. */
export interface RevSegmentPlacement {
  rect: Rect;
  rotation?: number;
}

/** The bindings of one segment in each layer. */
export interface RevSegmentOptions {
  /** The car's own shift-light RPMs. ADR 0014. */
  shift: SegmentOptions;
  /** SimHub's per-car bands, for a car that publishes no ladder of its own. ADR 0004. */
  simhub: SegmentOptions;
  /** The plain RPM bar, with shift lights switched off. */
  rpm: SegmentOptions;
}

/** Which of the three layers a set of segment bindings belongs to. */
export type RevLayer = keyof RevSegmentOptions;

/** The shift stage (0, 1, 2) of segment k of `count`: thirds, the last third taking any remainder. */
export const stageOf = (k: number, count: number): number => Math.min(2, Math.floor((k * 3) / count));

/**
 * Bindings of segment k of `count`, in each of the three layers. Both shift ladders light the
 * same segment in the same colour and differ only in what decides it; the plain RPM bar lights a
 * segment when the displayed RPM percentage passes `k * 100 / count`.
 */
export function revSegmentOptions(k: number, count: number): RevSegmentOptions {
  const stage = stageOf(k, count);
  const indexes = Array.from({ length: count }, (_, i) => i);
  const stageStart = indexes.findIndex((i) => stageOf(i, count) === stage);
  const stageCount = indexes.filter((i) => stageOf(i, count) === stage).length;
  const local = k - stageStart;
  const color = STAGE_COLORS[stage] ?? ds.purpose.shift.stage3;

  const flash = (on: Expr): Pick<SegmentOptions, 'blinkBind' | 'blinkDelayMs'> | Record<string, never> =>
    stage === 2 ? { blinkBind: on, blinkDelayMs: REDLINE_BLINK_MS } : {};

  const threshold = Math.round((k * 100 * 100) / count) / 100;
  const rpmLit = gt(game('CarSettings_CurrentDisplayedRPMPercent'), num(threshold));

  return {
    shift: { colorBind: litColor(mirrorStageLit(stage, local, stageCount), color), ...flash(mirrorOverRev()) },
    simhub: { colorBind: litColor(simhubStageLit(stage, local, stageCount), color), ...flash(simhubRedline()) },
    rpm: { colorBind: litColor(rpmLit, ds.color.text.secondary) },
  };
}

/**
 * The three layers. `<prefix>.shiftLights` is the car's own ladder, `<prefix>.shiftLightsSimHub`
 * is SimHub's bands for a car that publishes none, and `<prefix>.rpmBar` is the plain bar with
 * the setting off. Exactly one is visible at a time.
 */
export function revLayers(prefix: string, placements: readonly RevSegmentPlacement[]): [LayerItem, LayerItem, LayerItem] {
  const count = placements.length;
  const build = (layer: RevLayer): LayerItem['children'] =>
    placements.map((p, k) => segment(`${prefix}.${layer}.${pad2(k)}`, p.rect, ds.purpose.shift.unlit, { ...revSegmentOptions(k, count)[layer], rotation: p.rotation }));

  const on = setting.shiftLights();
  const layer = (name: string, which: RevLayer, visible: Expr): LayerItem => ({
    kind: 'layer',
    name: `${prefix}.${name}`,
    children: build(which),
    ...withBindings({ Visible: visible }),
  });

  return [
    layer('shiftLights', 'shift', and(on, mirrorAvailable())),
    layer('shiftLightsSimHub', 'simhub', and(on, not(mirrorAvailable()))),
    layer('rpmBar', 'rpm', not(on)),
  ];
}
