/**
 * Zone A's four pages: the one a driver reads by reflex.
 *
 * This is why zone A is a narrow column rather than a third of the screen. The gear wants height,
 * not width — a 260 px glyph needs about 136 px of cell and 312 px of line box — so a column 380
 * wide and 314 tall is exactly the shape it is for, and the width left over goes to the two zones
 * that show tables.
 *
 * None of these draws a header. The other three zones carry the zone letter and the page name in a
 * 22 px line, and spending that here would cost the gear its size for the sake of saying "gear".
 * What zone A does when its page changes is the question XOR-103 owns.
 */
import type { Item, Rect } from '../generator.ts';
import { ncalc } from '../generator.ts';
import { gear as gearComponent, GEAR_CHARS } from '../components/gear.ts';
import { boxSlack, cells, gearCells, monoWidth } from '../design/metrics.ts';
import { rect } from '../design/geometry.ts';
import { label } from '../elements/label.ts';
import { numeral } from '../elements/numeral.ts';
import { unit } from '../elements/unit.ts';
import { densityOf } from '../second/density.ts';
import { LINE_SPACING } from '../design/metrics.ts';
import { CHARS } from '../second/values.ts';
import { pageBuilder } from '../modules/index.ts';
import { shapeOf } from '../second/shape.ts';
import { ds } from '../tokens.ts';

const { game, fmt, isnull, num, str, iff, eq, add, sub } = ncalc;

/** The weight the speed page draws its one big value in, and the weight it is measured in. */
const SPEED_WEIGHT = 'Bold' as const;

/** The largest gear that fits a box, bounded by the line box rather than by the box itself. */
export const gearSizeIn = (frame: Rect): number => {
  const byHeight = Math.floor(frame.height * 0.78);
  // The cell is a letter wide, because SimHub reports "N" and "R" as well as a number.
  const byWidth = Math.floor((frame.width - 8) / 0.68);
  return Math.max(72, Math.min(ds.size.gear, byHeight, byWidth));
};

/** A1: the gear with its neighbours ghosted either side, the speed under it, the revs under that. */
function gearSpeedRevs(frame: Rect, prefix: string): Item[] {
  const d = densityOf('zone');
  const speedH = d.big;
  const revsH = d.small;
  const labelH = d.labelSm;
  const below = speedH + labelH + revsH + d.fieldGap * 3;
  const gearBox = rect(frame.left, frame.top, frame.width, Math.max(72, frame.height - below - d.gapY));
  const size = gearSizeIn(gearBox);

  const items: Item[] = [...neighbours(gearBox, size, prefix), ...gearComponent(gearBox, size, `${prefix}main`)];

  const speedTop = gearBox.top + gearBox.height + d.gapY;
  items.push(
    numeral(`${prefix}speed`, '187', frame.left, speedTop, speedH, CHARS.speed, {
      bind: fmt(isnull(game('SpeedKmh'), num(0)), '0'),
      maxWidth: frame.width,
      hAlign: 'center',
    }),
    unit(`${prefix}speed.unit`, 'KM/H', frame.left, speedTop + speedH + d.fieldGap, frame.width, { hAlign: 'center', bind: game('SpeedLocalUnit') }),
    numeral(`${prefix}revs`, '7,420', frame.left, speedTop + speedH + labelH + d.fieldGap * 2, revsH, CHARS.rpm, {
      bind: fmt(isnull(game('Rpms'), num(0)), '#,##0'),
      color: ds.color.text.secondary,
      maxWidth: frame.width,
      hAlign: 'center',
    }),
  );
  return items;
}

/**
 * The gear below and above, ghosted either side of the one a driver is in.
 *
 * Drawn rather than described: it is what tells a driver at a glance which way the box is going,
 * and it costs two text items. SimHub reports the gear as a number, so the neighbours are that
 * number plus and minus one; at the ends of the box they show nothing rather than 0 or 7.
 */
function neighbours(frame: Rect, size: number, prefix: string): Item[] {
  const small = Math.round(size * 0.34);
  const mono = gearCells(small);
  const width = monoWidth(mono, GEAR_CHARS);
  const gearValue = isnull(game('Gear'), num(0));
  const top = frame.top + (frame.height - small) / 2;
  // The box takes four pixels beyond its cell so WPF clips nothing, so the right neighbour is
  // inset by that much more than the left one.
  const inset = 2;
  const boxSlack = 4;
  const sides = [
    { id: 'below', x: frame.left + inset, value: sub(gearValue, num(1)), hide: eq(gearValue, num(0)) },
    { id: 'above', x: frame.left + frame.width - width - inset - boxSlack, value: add(gearValue, num(1)), hide: eq(gearValue, num(0)) },
  ];
  return sides.map((side) =>
    numeral(`${prefix}gear.${side.id}`, side.id === 'below' ? '3' : '5', side.x, top, small, GEAR_CHARS, {
      mono,
      // Ghosted with the dim ink rather than with opacity: SimHub's opacity is an item property
      // and the dim colour is the token for exactly this -- something present but not being read.
      color: ds.color.text.dim,
      maxWidth: width + 4,
      bind: iff(side.hide, str(''), fmt(side.value, '0')),
    }),
  );
}

/** A2: the gear alone, as large as the column allows. */
function gearAlone(frame: Rect, prefix: string): Item[] {
  return gearComponent(frame, gearSizeIn(frame), `${prefix}main`);
}

/** A3: the speed as the largest value, with the gear demoted to a small readout beside its label. */
function speedPage(frame: Rect, prefix: string): Item[] {
  const d = densityOf('zone');
  const labelH = d.labelSm;
  const gearH = d.big;
  // Bounded by the width and by what has to sit under it, not by the height alone. Speed is three
  // digits in cells the tokens size, and choosing from the height alone put a 384 px value in a
  // 340 px column -- which WPF clips to two digits and a half -- and a block taller than a 194 px
  // zone, which started seven pixels above the canvas.
  const below = labelH + d.gapY + gearH + d.fieldGap + labelH;
  const byHeight = Math.floor((frame.height - below - d.fieldGap) / LINE_SPACING);
  // Solved by trying rather than by algebra: a cell is rounded up per character and the box takes
  // a slack beyond the cells, so a size derived from the em fraction alone lands a few pixels over.
  //
  // Measured in SPEED_WEIGHT, which is the weight it is drawn in. Bold cells are wider than
  // SemiBold ones -- 114 px against 109 at size 231 -- and measuring the wrong face is how a box
  // comes out two pixels short and WPF takes the edge off the last digit.
  let size = Math.max(24, Math.min(ds.size.gear, byHeight));
  while (size > 24 && monoWidth(cells(SPEED_WEIGHT, size), CHARS.speed) + boxSlack(size) > frame.width) size -= 1;
  const blockHeight = size + d.fieldGap + labelH + d.gapY + gearH + d.fieldGap + labelH;
  const speedTop = frame.top + Math.max(0, (frame.height - blockHeight) / 2);
  return [
    numeral(`${prefix}speed`, '187', frame.left, speedTop, size, CHARS.speed, {
      bind: fmt(isnull(game('SpeedKmh'), num(0)), '0'),
      weight: SPEED_WEIGHT,
      maxWidth: frame.width,
      hAlign: 'center',
    }),
    unit(`${prefix}speed.unit`, 'KM/H', frame.left, speedTop + size + d.fieldGap, frame.width, { hAlign: 'center', bind: game('SpeedLocalUnit') }),
    numeral(`${prefix}gear`, '4', frame.left, speedTop + size + labelH + d.gapY, gearH, GEAR_CHARS, {
      mono: gearCells(gearH),
      bind: game('Gear'),
      maxWidth: frame.width,
      hAlign: 'center',
    }),
    label(`${prefix}gear.label`, 'GEAR', frame.left, speedTop + size + labelH + d.gapY + gearH + d.fieldGap, frame.width, { size: labelH, hAlign: 'center' }),
  ];
}

/**
 * A4: the track map with every car on it.
 *
 * The track module draws its own title on the line above its map, which on a companion page sits
 * inside the module's frame. Zone A's frame starts at the very top of its dashboard, so the title's
 * line box would reach a pixel above the canvas; the frame is inset by the label height to give it
 * somewhere to sit.
 */
function trackPage(frame: Rect, prefix: string): Item[] {
  const d = densityOf('zone');
  const inner = rect(frame.left, frame.top + d.labelSm, frame.width, Math.max(0, frame.height - d.labelSm));
  return pageBuilder('track')({ frame: inner, density: 'zone', prefix, shape: shapeOf(inner) });
}

const PAGES: Record<string, (frame: Rect, prefix: string) => Item[]> = {
  gearSpeedRevs,
  gearAlone,
  speed: speedPage,
  track: trackPage,
};

/** One of zone A's four pages, drawn in `frame`. */
export function zoneAPage(id: string, frame: Rect, prefix: string): Item[] {
  const draw = PAGES[id];
  if (!draw) throw new RangeError(`zone A has no page "${id}"`);
  return draw(frame, prefix);
}
