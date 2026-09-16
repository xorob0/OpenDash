/**
 * listRow: a position, who it is, and the gap to them, on one line over a filled rectangle.
 *
 * It is one of the three parts a face is allowed to fill at all -- the input and the pop-up are
 * the others -- and it fills in `purpose.block.fill`, which is the ground a group of values the
 * eye takes in as one instrument sits on. A row is that group: the three readings are about one
 * car and are read together, so they share a ground where a card holding one value keeps none.
 *
 * The two numerals are measured in their cells and the name takes what they leave. A name that
 * does not fit the remainder is not drawn at all rather than drawn over the gap: SimHub hands the
 * box to WPF as `MaxTextWidth` and WPF clips in silence, so a row that draws a cut name looks like
 * a row about a driver called "Tomasz Kowalc".
 */
import type { Hex, Item, Rect } from '../generator.ts';
import type { Expr } from '../bind.ts';
import { measureText } from '../design/advances.ts';
import { boxSlack, canvasBaseline, canvasYForBaseline, cells, monoWidth, type Chars } from '../design/metrics.ts';
import { band } from '../elements/band.ts';
import { label } from '../elements/label.ts';
import { numeral } from '../elements/numeral.ts';
import { ds } from '../tokens.ts';

/**
 * What the canvas draws the row to: `padding: 8px 12px`, a 10 px flex gap, the numerals at rung S
 * and the name in the label face. Ten is off the `space` scale, which goes 8 then 12, so it stays
 * here with the canvas as its citation.
 */
export const LIST_ROW = { padX: 12, padY: 8, gap: 10, size: ds.size.valueSm, nameSize: ds.size.label } as const;

/** The height a row of numerals at `fs` takes, padding included. */
export const listRowHeight = (fs: number = LIST_ROW.size): number => fs + 2 * LIST_ROW.padY;

/** One of the row's two monospaced readings: the design-time text, its binding and its cell budget. */
export interface ListRowValue {
  sample: string;
  bind?: Expr;
  chars: Chars;
  color?: Hex;
  colorBind?: Expr;
}

export interface ListRowSpec {
  /** The position, as the canvas writes it: "P4". */
  position: ListRowValue;
  /** Whose row it is. Flexes over whatever the two numerals leave. */
  name: { text: string; bind?: Expr; widest?: string };
  /** The gap to the player, as "0.000". */
  gap: ListRowValue;
}

/** A filled row: position at the left, gap at the right, the name between them. */
export function listRow(name: string, frame: Rect, spec: ListRowSpec, fs: number = LIST_ROW.size): Item[] {
  const { padX, padY, gap: innerGap, nameSize } = LIST_ROW;
  const mono = cells('SemiBold', fs);
  const slack = boxSlack(fs);
  const positionBox = monoWidth(mono, spec.position.chars) + slack;
  const gapBox = monoWidth(mono, spec.gap.chars) + slack;

  // The canvas centres the line in the row rather than hanging it off the padding, so a caller may
  // give the row more height than `listRowHeight` and still have the three readings centred in it.
  const top = frame.top + (frame.height - fs) / 2;
  const left = frame.left + padX;
  const gapX = frame.left + frame.width - padX - gapBox;

  const items: Item[] = [
    band(`${name}.fill`, frame, ds.purpose.block.fill),
    numeral(`${name}.position`, spec.position.sample, left, top, fs, spec.position.chars, {
      bind: spec.position.bind,
      color: spec.position.color,
      colorBind: spec.position.colorBind,
      width: positionBox,
    }),
    numeral(`${name}.gap`, spec.gap.sample, gapX, top, fs, spec.gap.chars, {
      bind: spec.gap.bind,
      color: spec.gap.color,
      colorBind: spec.gap.colorBind,
      hAlign: 'right',
      width: gapBox,
    }),
  ];

  const nameX = left + positionBox + innerGap;
  const room = gapX - innerGap - nameX;
  const drawn = spec.name.widest ?? spec.name.text;
  if (measureText('BarlowMedium', drawn, nameSize) < room) {
    items.push(
      label(`${name}.name`, spec.name.text, nameX, canvasYForBaseline(canvasBaseline(top, fs), nameSize), room, {
        size: nameSize,
        color: ds.color.text.primary,
        bind: spec.name.bind,
        widest: spec.name.widest,
      }),
    );
  }
  return items;
}
