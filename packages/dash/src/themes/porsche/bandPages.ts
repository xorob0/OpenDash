/**
 * Band D's own pages in the car's register, so that the foot reads as the same display as the
 * Porsche row beside them: every field the house page draws, each as the car draws `Brake Bias`, a
 * container that is a grey border and nothing else, a grey title cell at its left as tall as the
 * container holds, and the value bare on the black ground to its right. The cells are laid
 * across the foot from the band's left padding past its letter to its right padding, in a second line
 * where the first is full, and the lines are centred on the band's height.
 *
 * Nothing is left out that the house page draws at this width: the house lays the same fields in one
 * line of 1220 px, and two lines of the same room hold them at the car's sizes, which the conformance
 * harness checks field by field against the house page built alone. A field the sim does not publish
 * is hidden with its cell, and the cells after it keep their places, which is the band's `when: 'dim'`
 * reading of a rank rather than its `close` one; a cell sliding along a line as a car's settings come
 * and go would move the reading a driver looks for.
 *
 * The car page, D8, is the house's twelve lamps on one grey cell: a lamp is a box and a pictogram the
 * repository does not hold yet, and nothing in it is a field to set in the car's cells.
 *
 * **On a shorter or narrower foot** (#713) the cells follow the band, as the ticket's rule has a font
 * follow its box: a band too short for two lines lays one, and where the fields the house page draws
 * at this band do not fit in the car's cells at the reference size, every cell is drawn smaller by
 * the same factor until they do. The 800 x 286's 60 px foot is the case: one line, the cells a little
 * smaller, and every page of the catalogue still behind the band's button, each shedding its fields
 * from the tail in #155's order exactly where the house page sheds them at that band.
 */
import type { Item, Rect } from '../../generator.ts';
import { withMoreBindings, type Expr } from '../../bind.ts';
import { measureText } from '../../design/advances.ts';
import { rect } from '../../design/geometry.ts';
import { boxSlack, cells, monoWidth } from '../../design/metrics.ts';
import { band } from '../../elements/band.ts';
import { label } from '../../elements/label.ts';
import { numeral } from '../../elements/numeral.ts';
import { unit } from '../../elements/unit.ts';
import { ds, TRANSPARENT } from '../../tokens.ts';
import { walkItems } from '../../walk.ts';
import { BAND_PAGES, bandPageItems, bandPageRoom, relativeFields, type BandField } from '../../zones/bandPages.ts';
import { TELLTALE_PAGE } from '../../zones/telltales.ts';
import { carColour, centredY, CONTAINER_BORDER, INSET_RADIUS, RADIUS } from './register.ts';

/** The container `Brake Bias` is drawn in on the foot: 50 tall, 14 of padding, 23 px labels and 30 px values. */
const CELL = { height: 50, pad: 14, label: 23, value: 30, unit: 15, unitGap: 5, rowGap: 6, between: 10, gap: 14 };

type Cell = typeof CELL;

/** The cells at `k` of the reference, each figure rounded; one is the reference. */
const cellAt = (k: number): Cell => Object.fromEntries(Object.entries(CELL).map(([key, value]) => [key, Math.round(value * k)])) as Cell;

/** The smallest the cells are drawn, as a share of the reference, before a page sheds instead. */
const SMALLEST = 0.55;

const CELL_REFERENCE: Cell = CELL;

/** The value's box: its cells, or its measured advances where it is a word. */
function valueWidth(f: BandField, CELL: Cell): number {
  if (f.widest !== undefined) return Math.ceil(measureText('BarlowMedium', f.widest, CELL.value)) + boxSlack(CELL.value);
  const one = monoWidth(cells('SemiBold', CELL.value), f.chars) + boxSlack(CELL.value);
  return one + (f.row?.length ?? 0) * (one + CELL.between);
}

const unitWidth = (f: BandField, CELL: Cell): number =>
  f.after === undefined ? 0 : Math.ceil(Math.max(...[f.after, f.afterWidest ?? f.after].map((s) => measureText('BarlowMedium', s, CELL.unit)))) + 2;

const labelWidth = (f: BandField, CELL: Cell): number => Math.ceil(measureText('BarlowMedium', f.labelWidest ?? f.label, CELL.label)) + boxSlack(CELL.label);

/** How wide a field's container is: its title cell, and its value and unit with their padding. */
function cellWidth(f: BandField, CELL: Cell): number {
  const inner = valueWidth(f, CELL) + (f.after === undefined ? 0 : CELL.unitGap + unitWidth(f, CELL));
  return 2 * CONTAINER_BORDER + labelWidth(f, CELL) + 2 * CELL.pad + inner + 2 * CELL.pad;
}

function cell(f: BandField, prefix: string, at: Rect, CELL: Cell): Item {
  const name = `${prefix}${f.id}`;
  const units = f.after === undefined ? 0 : CELL.unitGap + unitWidth(f, CELL);
  const title = rect(at.left + CONTAINER_BORDER, at.top + CONTAINER_BORDER, labelWidth(f, CELL) + 2 * CELL.pad, at.height - 2 * CONTAINER_BORDER);
  const valueRight = at.left + at.width - CONTAINER_BORDER - CELL.pad - units;
  const valueTop = centredY(at, CELL.value);
  const one = f.widest === undefined ? monoWidth(cells('SemiBold', CELL.value), f.chars) + boxSlack(CELL.value) : valueWidth(f, CELL);
  const readings = [{ sample: f.sample, bind: f.bind }, ...(f.row ?? [])];
  const children: Item[] = [
    band(`${name}.cell`, at, TRANSPARENT, { border: { color: carColour('panel'), width: CONTAINER_BORDER }, radius: RADIUS }),
    band(`${name}.title`, title, carColour('panel'), { radius: INSET_RADIUS }),
    label(`${name}.label`, f.label, title.left, centredY(title, CELL.label), title.width, {
      size: CELL.label,
      color: f.labelColor ?? ds.color.text.label,
      hAlign: 'center',
      ...(f.labelBind ? { bind: f.labelBind, widest: f.labelWidest } : {}),
    }),
    ...readings.map((reading, i) => {
      const left = valueRight - (readings.length - i) * one - (readings.length - 1 - i) * CELL.between;
      const id = i === 0 ? `${name}.value` : `${name}.row${i}`;
      return f.widest !== undefined
        ? label(id, reading.sample, left, valueTop, one, { size: CELL.value, color: f.color ?? ds.color.text.primary, hAlign: 'right', bind: reading.bind, widest: f.widest })
        : numeral(id, reading.sample, left, valueTop, CELL.value, f.chars, {
            bind: reading.bind,
            widest: i === 0 ? f.numeralWidest : undefined,
            color: f.color,
            colorBind: f.colorBind,
            width: one,
            hAlign: 'right',
          });
    }),
  ];
  if (f.after !== undefined) {
    children.push(
      unit(`${name}.unit`, f.after, valueRight + CELL.unitGap, valueTop + CELL.value - CELL.unit - 2, unitWidth(f, CELL), {
        size: CELL.unit,
        ...(f.afterBind ? { bind: f.afterBind, widest: f.afterWidest ?? f.after } : {}),
        visibleBind: f.afterWhen,
      }),
    );
  }
  return withMoreBindings({ kind: 'layer', name, children }, { Visible: f.present });
}

interface Placed {
  f: BandField;
  line: number;
  x: number;
  width: number;
}

/** The fields in as many lines as the band holds, two at most, from the left of the room, a line full before the next is begun. */
function placeFields(fields: readonly BandField[], frame: Rect, CELL: Cell): Placed[] {
  const room = bandPageRoom(frame, false);
  const most = Math.max(1, Math.min(2, Math.floor((frame.height + CELL.rowGap) / (CELL.height + CELL.rowGap))));
  const placed: Placed[] = [];
  let line = 0;
  let x = room.left;
  for (const f of fields) {
    const width = cellWidth(f, CELL);
    if (x + width > room.left + room.width) {
      line += 1;
      x = room.left;
    }
    if (line >= most || width > room.width) break;
    placed.push({ f, line, x, width });
    x += width + CELL.gap;
  }
  return placed;
}

/**
 * The fields laid in lines centred on the band's height, at the largest size of the cells, from the
 * reference down, at which every field `wanted` names is placed: the fields the house page draws at
 * this band. A page that is short of them at the smallest size draws what that size holds.
 */
function cellLines(fields: readonly BandField[], frame: Rect, prefix: string, wanted: ReadonlySet<string>): Item[] {
  let CELL = CELL_REFERENCE;
  let placed = placeFields(fields, frame, CELL);
  for (let k = 1; k >= SMALLEST - 1e-9 && ![...wanted].every((id) => placed.some((p) => p.f.id === id)); k -= 0.05) {
    CELL = cellAt(k);
    placed = placeFields(fields, frame, CELL);
  }
  const lines = Math.max(1, ...placed.map((p) => p.line + 1));
  const top = frame.top + (frame.height - (lines * CELL.height + (lines - 1) * CELL.rowGap)) / 2;
  return placed.map(({ f, line: at, x: left, width }) => cell(f, prefix, rect(left, top + at * (CELL.height + CELL.rowGap), width, CELL.height), CELL));
}

/** One of band D's pages in the car's register; see the file comment. */
export function porscheBandPage(page: string, frame: Rect, prefix: string, classOnly?: Expr): Item[] {
  if (page === TELLTALE_PAGE) {
    const room = bandPageRoom(frame, false);
    const height = Math.min(2 * CELL.height, frame.height - 4);
    const panel = rect(room.left, frame.top + (frame.height - height) / 2, room.width, height);
    return [band(`${prefix}cell`, panel, carColour('panel'), { radius: RADIUS }), ...bandPageItems(page, frame, prefix, false)];
  }
  const fields = page === 'relative' && classOnly !== undefined ? relativeFields(classOnly) : BAND_PAGES[page];
  if (!fields) throw new RangeError(`band D has no page "${page}"`);
  // The fields the house page draws at this band, which the car's cells have to hold too.
  const house = [...walkItems(bandPageItems(page, frame, prefix, false, classOnly))].map((item) => item.name);
  const wanted = new Set(fields.map((f) => f.id).filter((id) => house.some((name) => name === `${prefix}${id}` || name.startsWith(`${prefix}${id}.`))));
  return cellLines(fields, frame, prefix, wanted);
}

