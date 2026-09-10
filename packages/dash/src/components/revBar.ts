/**
 * revBar: fifteen segments with integer-snapped edges, in two layers that never show together.
 * `shiftLights` (OpenDash.ShiftLights true, the default) lights three colour bands from SimHub's
 * per-car shift progress values and flashes the last band at redline. `rpmBar` (setting false)
 * shows the same segments as a plain RPM bar in text.secondary. ADR 0004: SimHub exposes the
 * shift bands as progress values, not as bar percentages, so this is the honest per-car rendering.
 */
import type { Hex, Item, LayerItem } from '../generator.ts';
import { ncalc } from '../generator.ts';
import { withBindings, type Expr } from '../bind.ts';
import { setting } from '../contract.ts';
import { rect, snapEdges } from '../design/geometry.ts';
import { segment } from '../elements/segment.ts';
import { ds } from '../tokens.ts';

const { game, gt, eq, mul, num, iff, str, not } = ncalc;

export interface RevBarFrame {
  left: number;
  top: number;
  width: number;
  height: number;
  /** Gap between segments: 8 at 1920, 6 at 1280, 4 at 850 and below. */
  gap: number;
}

/** Half period of the redline flash in ms: 1000 / flashHz / 2, floored (62 at 8 Hz). */
export const REDLINE_BLINK_MS = Math.floor(1000 / ds.shiftLights.flashHz / 2);

const STAGE_COLORS: readonly Hex[] = [ds.purpose.shift.stage1, ds.purpose.shift.stage2, ds.purpose.shift.stage3];

const pad2 = (k: number): string => String(k + 1).padStart(2, '0');

/** `if(lit, colour, unlit)` */
const litColor = (lit: Expr, color: Hex): Expr => iff(lit, str(color), str(ds.purpose.shift.unlit));

export function revBar(frame: RevBarFrame, prefix = 'revBar'): Item[] {
  const count = ds.shiftLights.segments;
  const spans = snapEdges(frame.left, frame.width, count, frame.gap);
  const stageOf = (k: number): number => Math.min(2, Math.floor((k * 3) / count));
  const stageStart = (s: number): number => spans.findIndex((_, k) => stageOf(k) === s);
  const stageCount = (s: number): number => spans.filter((_, k) => stageOf(k) === s).length;
  const redline = eq(game('CarSettings_RPMRedLineReached'), num(1));

  const shiftSegments = spans.map((span, k) => {
    const stage = stageOf(k);
    const color = STAGE_COLORS[stage] ?? ds.purpose.shift.stage3;
    const local = k - stageStart(stage);
    const lit =
      stage === 0
        ? gt(mul(game('CarSettings_RPMShiftLight1'), num(stageCount(0))), num(local))
        : stage === 1
          ? gt(mul(game('CarSettings_RPMShiftLight2'), num(stageCount(1))), num(local))
          : redline;
    return segment(`${prefix}.shift.${pad2(k)}`, rect(span.left, frame.top, span.width, frame.height), ds.purpose.shift.unlit, {
      colorBind: litColor(lit, color),
      ...(stage === 2 ? { blinkBind: redline, blinkDelayMs: REDLINE_BLINK_MS } : {}),
    });
  });

  const rpmSegments = spans.map((span, k) => {
    const threshold = Math.round((k * 100 * 100) / count) / 100;
    const lit = gt(game('CarSettings_CurrentDisplayedRPMPercent'), num(threshold));
    return segment(`${prefix}.rpm.${pad2(k)}`, rect(span.left, frame.top, span.width, frame.height), ds.purpose.shift.unlit, {
      colorBind: litColor(lit, ds.color.text.secondary),
    });
  });

  const shiftLights: LayerItem = {
    kind: 'layer',
    name: `${prefix}.shiftLights`,
    children: shiftSegments,
    ...withBindings({ Visible: setting.shiftLights() }),
  };
  const rpmBar: LayerItem = {
    kind: 'layer',
    name: `${prefix}.rpmBar`,
    children: rpmSegments,
    ...withBindings({ Visible: not(setting.shiftLights()) }),
  };
  return [shiftLights, rpmBar];
}
