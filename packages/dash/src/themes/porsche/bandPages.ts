/**
 * Band D's own pages in the car's register, so that the foot reads as the same display as the
 * Porsche row beside them: every field the house page draws, each a grey cell with its label at the
 * left and its value right-aligned in a dark inset, as the car draws `Brake Bias`. The cells are laid
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
import { ds } from '../../tokens.ts';
import { BAND_PAGES, bandPageItems, bandPageRoom, relativeFields, type BandField } from '../../zones/bandPages.ts';
import { TELLTALE_PAGE } from '../../zones/telltales.ts';
import { carColour, centredY, INSET_RADIUS, RADIUS } from './register.ts';

/** The cell `Brake Bias` is drawn in on the foot: 50 tall, a 42 inset, 14 of padding, 23 px labels and 30 px values. */
const CELL = { height: 50, inset: 42, pad: 14, label: 23, value: 30, unit: 15, unitGap: 5, rowGap: 6, between: 10, gap: 14 };

/** The value's box: its cells, or its measured advances where it is a word. */
function valueWidth(f: BandField): number {
  if (f.widest !== undefined) return Math.ceil(measureText('BarlowMedium', f.widest, CELL.value)) + boxSlack(CELL.value);
  const one = monoWidth(cells('SemiBold', CELL.value), f.chars) + boxSlack(CELL.value);
  return one + (f.row?.length ?? 0) * (one + CELL.between);
}

const unitWidth = (f: BandField): number =>
  f.after === undefined ? 0 : Math.ceil(Math.max(...[f.after, f.afterWidest ?? f.after].map((s) => measureText('BarlowMedium', s, CELL.unit)))) + 2;

const labelWidth = (f: BandField): number => Math.ceil(measureText('BarlowMedium', f.labelWidest ?? f.label, CELL.label)) + boxSlack(CELL.label);

/** How wide a field's cell is: the label, the inset round its value and its unit. */
function cellWidth(f: BandField): number {
  const inner = valueWidth(f) + (f.after === undefined ? 0 : CELL.unitGap + unitWidth(f));
  return CELL.pad + labelWidth(f) + CELL.pad + (inner + 2 * CELL.pad) + 4;
}

function cell(f: BandField, prefix: string, at: Rect): Item {
  const name = `${prefix}${f.id}`;
  const units = f.after === undefined ? 0 : CELL.unitGap + unitWidth(f);
  const insetWidth = valueWidth(f) + units + 2 * CELL.pad;
  const inset = rect(at.left + at.width - 4 - insetWidth, at.top + (at.height - CELL.inset) / 2, insetWidth, CELL.inset);
  const valueRight = inset.left + inset.width - CELL.pad - units;
  const valueTop = centredY(inset, CELL.value);
  const one = f.widest === undefined ? monoWidth(cells('SemiBold', CELL.value), f.chars) + boxSlack(CELL.value) : valueWidth(f);
  const readings = [{ sample: f.sample, bind: f.bind }, ...(f.row ?? [])];
  const children: Item[] = [
    band(`${name}.cell`, at, carColour('panel'), { radius: RADIUS }),
    label(`${name}.label`, f.label, at.left + CELL.pad, centredY(at, CELL.label), labelWidth(f), {
      size: CELL.label,
      color: f.labelColor ?? ds.color.text.label,
      ...(f.labelBind ? { bind: f.labelBind, widest: f.labelWidest } : {}),
    }),
    band(`${name}.inset`, inset, carColour('inset'), { radius: INSET_RADIUS }),
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
      unit(`${name}.unit`, f.after, valueRight + CELL.unitGap, valueTop + CELL.value - CELL.unit - 2, unitWidth(f), {
        size: CELL.unit,
        ...(f.afterBind ? { bind: f.afterBind, widest: f.afterWidest ?? f.after } : {}),
        visibleBind: f.afterWhen,
      }),
    );
  }
  return withMoreBindings({ kind: 'layer', name, children }, { Visible: f.present });
}

/** The fields in up to two lines from the left of the room, a line full before the next is begun, centred on the band's height. */
function cellLines(fields: readonly BandField[], frame: Rect, prefix: string): Item[] {
  const room = bandPageRoom(frame, false);
  const most = 2;
  const placed: { f: BandField; line: number; x: number; width: number }[] = [];
  let line = 0;
  let x = room.left;
  for (const f of fields) {
    const width = cellWidth(f);
    if (x + width > room.left + room.width) {
      line += 1;
      x = room.left;
    }
    if (line >= most || width > room.width) break;
    placed.push({ f, line, x, width });
    x += width + CELL.gap;
  }
  const lines = Math.max(1, ...placed.map((p) => p.line + 1));
  const top = frame.top + (frame.height - (lines * CELL.height + (lines - 1) * CELL.rowGap)) / 2;
  return placed.map(({ f, line: at, x: left, width }) => cell(f, prefix, rect(left, top + at * (CELL.height + CELL.rowGap), width, CELL.height)));
}

/** One of band D's pages in the car's register; see the file comment. */
export function porscheBandPage(page: string, frame: Rect, prefix: string, classOnly?: Expr): Item[] {
  if (page === TELLTALE_PAGE) {
    const room = bandPageRoom(frame, false);
    const panel = rect(room.left, frame.top + (frame.height - 2 * CELL.height) / 2, room.width, 2 * CELL.height);
    return [band(`${prefix}cell`, panel, carColour('panel'), { radius: RADIUS }), ...bandPageItems(page, frame, prefix, false)];
  }
  const fields = page === 'relative' && classOnly !== undefined ? relativeFields(classOnly) : BAND_PAGES[page];
  if (!fields) throw new RangeError(`band D has no page "${page}"`);
  return cellLines(fields, frame, prefix);
}

