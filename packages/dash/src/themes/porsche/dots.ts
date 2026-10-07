/**
 * The car's shift lights on the screen: sixteen round dots that light from both ends towards the
 * middle, three green, three yellow and two red on each side, and all sixteen blue and flashing at
 * the shift point. This is the one place the theme reverses ADR 0018's choice of openDash colours
 * on the rev bar, as #205 records: yellow is the car's, put on `purpose.shift.stage2` by the overlay.
 *
 * Each side is the house rev bar of eight segments, with every ladder the house draws and the
 * plain RPM bar behind them, laid from the outer dot inwards and rounded into dots. Eight segments
 * fall into the house's thirds as three, three and two, which is the car's sequence, so the timing
 * is the house's and only the shape and the order are the car's.
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
import { ds } from '../../tokens.ts';

const { and, eq, iff, str } = ncalc;

/** Sixteen on the car, eight a side. */
const PER_SIDE = 8;

/** The rect of dot `k` of sixteen, counted from the left, in a row of `row`. */
function dot(row: Rect, k: number): Rect {
  const size = row.height;
  const pitch = (row.width - size) / (2 * PER_SIDE - 1);
  return roundRect(rect(row.left + k * pitch, row.top, size, size));
}

/** A house segment made round: the same item, its corners the dot's radius. */
const rounded = (layer: LayerItem, radius: number): LayerItem => ({
  ...layer,
  children: layer.children.map((child) => (child.kind === 'rect' ? ({ ...child, border: { ...child.border, radius } } satisfies RectangleItem) : child)),
});

export function porscheDots(ctx: FaceContext): Item[] {
  const row = regionRect(ctx.regions, 'revBar');
  const mode = zoneSetting.revBar(ctx.face);
  const radius = row.height / 2;
  const side = (prefix: string, order: (i: number) => number): LayerItem[] =>
    revLayers(prefix, Array.from({ length: PER_SIDE }, (_, i) => ({ rect: dot(row, order(i)) })), mode).map((layer) => rounded(layer, radius));
  // At the shift point the car turns every dot blue and flashes them, over whichever ladder lit
  // them: the measured bar's own flash where the rig reads one, the house's either-ladder over-rev
  // where it does not, and never on the plain RPM bar, which reports revs rather than a shift.
  const point = withMoreBindings({
    kind: 'layer',
    name: 'revBar.shiftPoint',
    blink: { enabled: true, delayMs: REDLINE_BLINK_MS },
    children: Array.from({ length: 2 * PER_SIDE }, (_, k): RectangleItem => ({
      kind: 'rect',
      name: `revBar.shiftPoint.${String(k + 1).padStart(2, '0')}`,
      rect: dot(row, k),
      backgroundColor: ds.color.info.primary,
      border: { radius },
    })),
  } satisfies LayerItem, { Visible: and(eq(mode, str('shift')), iff(carLadderOnScreens(), carLadderFlash(), overRevEither())) });
  return [...side('revBar.left', (i) => i), ...side('revBar.right', (i) => 2 * PER_SIDE - 1 - i), point];
}
