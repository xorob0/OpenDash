/**
 * The Porsche register, as the ticket's table of constants writes it: the car's greys, its border
 * and radii, and the one element every coloured box on the display is drawn from.
 *
 * The colours the car adds are `palette.porsche` in this theme's overlay and are read at draw time
 * rather than when the module loads, because `themes/drawings.ts` imports this file in every process
 * and only the Porsche's has those tokens. Everything else is a house token the overlay leaves alone
 * or points somewhere new: the setting boxes wear `color.good`, `color.info`, `color.caution`,
 * `color.danger` and `color.text.primary` by what they are, which is the exception #194 records.
 */
import type { Hex, Item, Rect } from '../../generator.ts';
import { withMoreBindings, type Expr } from '../../bind.ts';
import { measureText } from '../../design/advances.ts';
import { rect } from '../../design/geometry.ts';
import { boxSlack } from '../../design/metrics.ts';
import { band } from '../../elements/band.ts';
import { label } from '../../elements/label.ts';
import { ncalc } from '../../generator.ts';
import { inTheCar } from '../../second/values.ts';
import { ds, resolveToken, TRANSPARENT } from '../../tokens.ts';

/** Every coloured box, the track state, the speed box and the gear tile. */
export const BORDER = 3;
/** Panels, tiles and outlined boxes. */
export const RADIUS = 7;
/** The value cells inside a panel. */
export const INSET_RADIUS = 5;
/** Labels, in the case the car writes them. */
export const LABEL_SIZE = 23;
/** Inside a box, between its border and its name or its value. */
export const BOX_PAD = 14;

/** A colour this theme adds under `palette.porsche`, refused unless it resolves to `#RRGGBB`. */
export function carColour(name: 'panel' | 'tile' | 'inset' | 'edge' | 'divider' | 'badge' | 'compound'): Hex {
  const value = resolveToken(`palette.porsche.${name}`);
  if (typeof value !== 'string' || !/^#[0-9A-F]{6}$/.test(value)) throw new Error(`porsche: palette.porsche.${name} is not a #RRGGBB colour (${String(value)})`);
  return value as Hex;
}

/** The width a run of Barlow takes, with the slack WPF needs to draw its last glyph whole. */
export const runWidth = (text: string, size: number, bold = false): number => Math.ceil(measureText(bold ? 'BarlowBold' : 'BarlowMedium', text, size)) + boxSlack(size);

/** A line of `size` centred on the height of `box`, as the canvas centres a flex row. */
export const centredY = (box: Rect, size: number): number => box.top + (box.height - size) / 2;

/** One reading a box shows: the design-time sample, its binding and the widest the binding can draw. */
export interface Reading {
  sample: string;
  bind: Expr;
  widest: string;
}

/**
 * The car's setting box: a rounded outline in the box's own colour, the short name in Barlow Bold
 * at the left and the value at the right, both at the label size. Hidden while `present` is false,
 * which is how a box whose setting the sim does not publish disappears.
 */
export function settingBox(name: string, frame: Rect, colour: Hex, title: string, reading: Reading, present?: Expr): Item {
  const y = centredY(frame, LABEL_SIZE);
  const inner = frame.width - 2 * (BORDER + BOX_PAD);
  const valueWidth = Math.min(inner, runWidth(reading.widest, LABEL_SIZE));
  const children: Item[] = [
    band(`${name}.box`, frame, TRANSPARENT, { border: { color: colour, width: BORDER }, radius: RADIUS }),
    label(`${name}.name`, title, frame.left + BORDER + BOX_PAD, y, runWidth(title, LABEL_SIZE, true), { size: LABEL_SIZE, weight: 'Bold', color: ds.color.text.primary }),
    label(`${name}.value`, reading.sample, frame.left + frame.width - BORDER - BOX_PAD - valueWidth, y, valueWidth, {
      size: LABEL_SIZE,
      color: ds.color.text.primary,
      hAlign: 'right',
      bind: reading.bind,
      widest: reading.widest,
    }),
  ];
  return withMoreBindings({ kind: 'layer', name, children }, { Visible: present });
}

/** The border of a container, which is all a container has: 2 px of the panel grey. */
export const CONTAINER_BORDER = 2;

/**
 * A container with a name and a value, as the car draws `Lap` and `Brake Bias`: the container is a
 * grey border and nothing else, the name is in a grey title cell at its left, as tall as the container
 * holds with no padding, and the value is bare on the black ground in `valueCell`, centred.
 */
export function namedCell(name: string, frame: Rect, title: string, valueCell: Rect, valueSize: number, reading: Reading): Item[] {
  const titleCell = rect(frame.left + CONTAINER_BORDER, frame.top + CONTAINER_BORDER, valueCell.left - frame.left - 2 * CONTAINER_BORDER, frame.height - 2 * CONTAINER_BORDER);
  return [
    band(`${name}.cell`, frame, TRANSPARENT, { border: { color: carColour('panel'), width: CONTAINER_BORDER }, radius: RADIUS }),
    band(`${name}.title`, titleCell, carColour('panel'), { radius: INSET_RADIUS }),
    label(`${name}.name`, title, titleCell.left, centredY(titleCell, LABEL_SIZE), titleCell.width, { size: LABEL_SIZE, color: ds.color.text.primary, hAlign: 'center' }),
    label(`${name}.value`, reading.sample, valueCell.left, centredY(valueCell, valueSize), valueCell.width, {
      size: valueSize,
      color: ds.color.text.primary,
      hAlign: 'center',
      bind: reading.bind,
      widest: reading.widest,
    }),
  ];
}

/** The tank the car calls low, in litres, which iRacing publishes in litres whatever the display unit. */
const LOW_FUEL_LITRES = 10;

/** The car's fuel alarm: in the car, with `FuelLevel` published and under ten litres. */
export const lowFuelAlarm = (): Expr => {
  const fuel = ncalc.raw('FuelLevel');
  return ncalc.and(inTheCar(), ncalc.not(ncalc.isNull(fuel)), ncalc.lt(fuel, ncalc.num(LOW_FUEL_LITRES)));
};
