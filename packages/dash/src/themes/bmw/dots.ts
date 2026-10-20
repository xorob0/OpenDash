/**
 * The car's shift lights on the screen (#203): ten round lights across the top, which light from both
 * ends towards the middle, green, green, yellow, then the last two red, and all ten blue and flashing
 * at the shift point; with the pit limiter on, the outer two of each side light magenta.
 *
 * Each side is the house rev bar of five segments, with every ladder the house draws and the plain
 * RPM bar behind them, laid from the outer light inwards and rounded into lights, as the Porsche's are.
 * The overlay puts the car's green, yellow and red on the shift stages, which is the BMW's reversal of
 * ADR 0018's openDash colours on the rev bar, as the Porsche's yellow is.
 */
import type { Item, LayerItem, Rect, RectangleItem } from '../../generator.ts';
import { ncalc } from '../../generator.ts';
import { withMoreBindings } from '../../bind.ts';
import { REDLINE_BLINK_MS, revLayers } from '../../components/revSegments.ts';
import { zone as zoneSetting } from '../../contract.ts';
import { rect, roundRect } from '../../design/geometry.ts';
import { regionRect } from '../anatomy.ts';
import type { FaceContext } from '../drawing.ts';
import { carLadderFlash, carLadderOnScreens, overRevEither } from '../../shift.ts';
import { pitLimiterOn } from '../../second/values.ts';
import { ds } from '../../tokens.ts';
import { bmwColour } from './register.ts';

const { and, eq, iff, str } = ncalc;

const PER_SIDE = 5;

/** The rect of light `k` of ten, counted from the left, in a row of `row`. */
function light(row: Rect, k: number): Rect {
  const size = row.height;
  const pitch = (row.width - size) / (2 * PER_SIDE - 1);
  return roundRect(rect(row.left + k * pitch, row.top, size, size));
}

const rounded = (layer: LayerItem, radius: number): LayerItem => ({
  ...layer,
  children: layer.children.map((child) => (child.kind === 'rect' ? ({ ...child, border: { ...child.border, radius } } satisfies RectangleItem) : child)),
});

export function bmwDots(ctx: FaceContext): Item[] {
  const row = regionRect(ctx.regions, 'revBar');
  const mode = zoneSetting.revBar(ctx.face);
  const radius = row.height / 2;
  const side = (prefix: string, order: (i: number) => number): LayerItem[] =>
    revLayers(prefix, Array.from({ length: PER_SIDE }, (_, i) => ({ rect: light(row, order(i)) })), mode).map((layer) => rounded(layer, radius));
  const lights = (name: string, ks: readonly number[], colour: RectangleItem['backgroundColor']): RectangleItem[] =>
    ks.map((k) => ({ kind: 'rect', name: `${name}.${String(k + 1).padStart(2, '0')}`, rect: light(row, k), backgroundColor: colour, border: { radius } }));
  const point = withMoreBindings({
    kind: 'layer',
    name: 'revBar.shiftPoint',
    blink: { enabled: true, delayMs: REDLINE_BLINK_MS },
    children: lights('revBar.shiftPoint', Array.from({ length: 2 * PER_SIDE }, (_, k) => k), ds.color.info.primary),
  } satisfies LayerItem, { Visible: and(eq(mode, str('shift')), iff(carLadderOnScreens(), carLadderFlash(), overRevEither())) });
  // The limiter's two outer lights a side, over the ladder, which the car shows instead of the revs.
  const limiter = withMoreBindings({
    kind: 'layer',
    name: 'revBar.limiter',
    children: [...lights('revBar.limiter', [0, 1, 2, 3, 4, 5, 6, 7, 8, 9], bmwColour('off')), ...lights('revBar.limiter.lit', [0, 1, 8, 9], bmwColour('magenta'))],
  } satisfies LayerItem, { Visible: pitLimiterOn() });
  return [...side('revBar.left', (i) => i), ...side('revBar.right', (i) => 2 * PER_SIDE - 1 - i), limiter, point];
}
