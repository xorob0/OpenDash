/**
 * What the rev bar and the rev arc share: fifteen segments in three layers, never more than one
 * of them showing. Two of the three are the shift ladder and the third is the plain RPM bar.
 * `shiftLights` and `shiftLightsSimHub` are the ladder (OpenDash.RevBar `shift`, the default):
 * three colour bands, the last of which flashes at redline. `rpmBar` is what `rpm` and `off`
 * both land on, the same segments drawn as a plain RPM bar in text.secondary. The components
 * only differ in where the segments go: a row of snapped spans, or a circle with a rotation per
 * segment.
 *
 * The shift bands come from the car's own shift-light RPMs where it publishes them and from
 * SimHub's per-car bands where it does not (ADR 0014, and ADR 0004 for the fallback: SimHub
 * exposes those bands as progress values rather than bar percentages, so this is the honest
 * per-car rendering). That choice is per frame, so the two ladders are two layers whose
 * visibility is bound to it: whichever of `shiftLights` and `shiftLightsSimHub` is visible in
 * Dash Studio is the one in use, which is how somebody debugging a car finds out which ladder it
 * is on. The model itself is in shift.ts.
 *
 * `off` is not one of the three, because drawing nothing is not a layer. A face that carries no
 * rev bar is a different arrangement of the whole screen, which `zones/face.ts` builds as a
 * second screen, and the surfaces that have no such arrangement -- the rev arc, the companion's
 * speedo -- fall back to the plain RPM bar rather than going dark. #189.
 */
import type { Hex, LayerItem, Rect } from '../generator.ts';
import { ncalc } from '../generator.ts';
import { withBindings, type Expr } from '../bind.ts';
import { setting } from '../contract.ts';
import { segment, type SegmentOptions } from '../elements/segment.ts';
import { mirrorAvailable, mirrorOverRev, mirrorStageLit, overRevEither, simhubOverRev, simhubStageLit, stageEntered } from '../shift.ts';
import { ds } from '../tokens.ts';

const { and, eq, game, gt, iff, not, num, str } = ncalc;

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
 * first segment lights, which under the car's own ladder is RPM passing that band's threshold and
 * under SimHub's is its progress value leaving zero (ADR 0004: these are progress values, not bar
 * percentages). The choice between the two is the same per-frame test the bar makes as two layers,
 * carried here inside the expression because a digit has no second layer to put the fallback in.
 * `rest` is the state below all of them and is always raised, so ranking the list always lands
 * somewhere.
 *
 * The flash is carried the same way and is a *separate* threshold from the band, which is the
 * correction #284's review forced: see {@link ShiftBand.blink}.
 */
export interface ShiftBand {
  id: 'redline' | 'stage2' | 'stage1' | 'rest';
  colour: Hex;
  /** True when the engine is in this band or above it. */
  raised: Expr;
  /**
   * When this band flashes, or null for a band that never does.
   *
   * An expression rather than a boolean, and that is the whole of #284's review finding. The
   * redline band is *entered* at `Last` and *flashes* at `max(Blink, Last)`, and it stops flashing
   * in the last gear; a consumer handed a boolean has no way to know that and flashes on the band
   * instead — early, and in a gear the bar deliberately leaves solid. Carrying the expression here
   * is what makes the bar, the strip and the flag box's digit flash on one threshold.
   */
  blink: Expr | null;
}

export function shiftBands(): ShiftBand[] {
  return [
    { id: 'redline', colour: ds.purpose.shift.stage3, raised: stageEntered(2), blink: overRevEither() },
    { id: 'stage2', colour: ds.purpose.shift.stage2, raised: stageEntered(1), blink: null },
    { id: 'stage1', colour: ds.purpose.shift.stage1, raised: stageEntered(0), blink: null },
    // Below the first band there is nothing to report, so the digit is simply readable.
    { id: 'rest', colour: ds.color.text.primary, raised: 'true', blink: null },
  ];
}

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
  /** The plain RPM bar, which both `rpm` and `off` fall to. */
  rpm: SegmentOptions;
}

/** Which of the three layers a set of segment bindings belongs to. */
export type RevLayer = keyof RevSegmentOptions;

/** The shift stage (0, 1, 2) of segment k of `count`: thirds, the last third taking any remainder. */
export const stageOf = (k: number, count: number): number => Math.min(2, Math.floor((k * 3) / count));

/**
 * The colour a lit segment takes in a layer: its band's on either shift ladder, and text.secondary
 * on the plain RPM bar, which reports revs rather than a shift point and so belongs to no band.
 */
const litColourOf = (layer: RevLayer, k: number, count: number): Hex =>
  layer === 'rpm' ? ds.color.text.secondary : (STAGE_COLORS[stageOf(k, count)] ?? ds.purpose.shift.stage3);

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
  const color = litColourOf('shift', k, count);

  const flash = (on: Expr): Pick<SegmentOptions, 'blinkBind' | 'blinkDelayMs'> | Record<string, never> =>
    stage === 2 ? { blinkBind: on, blinkDelayMs: REDLINE_BLINK_MS } : {};

  const threshold = Math.round((k * 100 * 100) / count) / 100;
  const rpmLit = gt(game('CarSettings_CurrentDisplayedRPMPercent'), num(threshold));

  return {
    shift: { colorBind: litColor(mirrorStageLit(stage, local, stageCount), color), ...flash(mirrorOverRev()) },
    simhub: { colorBind: litColor(simhubStageLit(stage, local, stageCount), color), ...flash(simhubOverRev()) },
    rpm: { colorBind: litColor(rpmLit, litColourOf('rpm', k, count)) },
  };
}

/**
 * How many segments the build colours in, which is what the sheets draw and what Dash Studio shows.
 *
 * A dashboard carries a static colour per item as well as its binding, and the editor, the Overview
 * panel's thumbnails and any render made before a game is running show the static one. Leaving all
 * fifteen at `unlit` made the component the artboards lead with the component that previews as an
 * empty trough. Every rev-bar artboard defaults `litSegments` to nine, so nine is what is drawn; the
 * bindings replace it on the first frame, so the running dash is unaffected.
 */
export const SAMPLE_LIT = 9;

/**
 * The three layers. `<prefix>.shiftLights` is the car's own ladder, `<prefix>.shiftLightsSimHub`
 * is SimHub's bands for a car that publishes none, and `<prefix>.rpmBar` is the plain bar that
 * both `rpm` and `off` fall to. Exactly one is visible at a time.
 */
export function revLayers(prefix: string, placements: readonly RevSegmentPlacement[], mode: Expr = setting.revBar()): [LayerItem, LayerItem, LayerItem] {
  const count = placements.length;
  const build = (layer: RevLayer): LayerItem['children'] =>
    placements.map((p, k) =>
      segment(`${prefix}.${layer}.${pad2(k)}`, p.rect, k < SAMPLE_LIT ? litColourOf(layer, k, count) : ds.purpose.shift.unlit, {
        ...revSegmentOptions(k, count)[layer],
        rotation: p.rotation,
      }),
    );

  // Which screen is asking. A zone face passes its own `Face<size>RevBar` read, which falls back to
  // the rig's; the round faces' arc and the speedo module pass nothing and get the rig's.
  const on = eq(mode, str('shift'));
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
