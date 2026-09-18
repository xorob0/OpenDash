/**
 * One corner of the tyre module: the tyre as a drawing, and beside it the three readings a driver
 * takes off it -- the temperature they act on mid-stint, the pressure they act on in the pits, and
 * the tread left they act on over a run.
 *
 * The cell is mirrored so that the four of them read as the car seen from above: the numbers sit
 * outboard and the drawing inboard, the left corners right-aligning their readings against the
 * drawing and the right corners left-aligning theirs.
 *
 * A cell too small for three readings drops one rather than shrinking all three, which is rule 17
 * and what `docs/design/readability-pass.md` §15 asks for; the drawing beside them is cut from
 * whatever is left, which is rule 18. Nothing here is ever scaled down.
 *
 * At `wide` the pressure is drawn twice, in the sim's unit and in the other one, which is what the
 * fifth wide zone page is named for. It is the only reading that takes a second line-mate, and the
 * cell drops that second reading before it drops the row it sits on.
 *
 * iRacing reports the pressure the car left the pit box with, not a live one; the temperature and
 * the tread are live.
 */
import type { Hex, Item, Rect } from '../generator.ts';
import { ncalc } from '../generator.ts';
import { type Expr } from '../bind.ts';
import { measureText } from '../design/advances.ts';
import { rect } from '../design/geometry.ts';
import { canvasBaseline, canvasYForBaseline, cells, monoWidth, type Chars } from '../design/metrics.ts';
import { numeral } from '../elements/numeral.ts';
import { unit } from '../elements/unit.ts';
import { ds } from '../tokens.ts';
import { tyreGlyph, tyreGlyphSize } from './tyreGlyph.ts';
import { densityOf, type Density, type DensitySpec } from './density.ts';
import { CHARS, pressureUnit, tyrePressure, tyreTemperature, tyreWear, type Corner } from './values.ts';

const { iff, eq, lt, gt, str, num, fmt, mul, concat, game } = ncalc;

/** Tread left below this percentage is drawn in caution. */
export const WEAR_CAUTION = 65;

/** Cold and hot thresholds in each unit SimHub can report, as on the dash's tyre card. */
export const TYRE_THRESHOLDS = {
  Celcius: { cold: 60, hot: 100 },
  Fahrenheit: { cold: 140, hot: 212 },
  Kelvin: { cold: 333, hot: 373 },
} as const;

const unitExpr = game('TemperatureUnit');
const perUnit = (f: string, k: string, c: string): string => iff(eq(unitExpr, str('Fahrenheit')), f, iff(eq(unitExpr, str('Kelvin')), k, c));

/**
 * The canvas's own spacings inside a corner, none of them on the `space` scale, so the literals
 * stay here with the drawing as their citation: three pixels between the readings, five between a
 * reading and its unit, and a 22 px box for the unit itself.
 */
export const ROW_GAP = 3;
export const UNIT_GAP = 5;
export const UNIT_CELL = 22;

/** Gap between the numbers and the drawing: ten pixels in the grid, seven in the four-column row. */
export const CORNER_GAP = 10;

/**
 * Between the two pressures of the wide page.
 *
 * The canvas draws no pair of readings on one line, so there is no measurement of its own to cite.
 * The cell's gap to its drawing stands in rather than a new number being invented: it is the one
 * distance inside a corner wide enough that the figures either side of it read as two readings and
 * not as one long value, five being what a value already leaves its own unit.
 */
export const READING_GAP = CORNER_GAP;

/**
 * The temperature's colour: blue when cold, red when hot, `nominal` in between, dim with no
 * reading. The tread columns pass their own nominal, which is the caution amber of a worn section.
 */
export function temperatureColour(corner: Corner, nominal: Expr = str(ds.purpose.tyre.nominal)): Expr {
  const t = tyreTemperature(corner);
  const cold = perUnit(num(TYRE_THRESHOLDS.Fahrenheit.cold), num(TYRE_THRESHOLDS.Kelvin.cold), num(TYRE_THRESHOLDS.Celcius.cold));
  const hot = perUnit(num(TYRE_THRESHOLDS.Fahrenheit.hot), num(TYRE_THRESHOLDS.Kelvin.hot), num(TYRE_THRESHOLDS.Celcius.hot));
  return iff(eq(t, num(0)), str(ds.color.text.dim), iff(lt(t, cold), str(ds.purpose.tyre.cold), iff(gt(t, hot), str(ds.purpose.tyre.hot), nominal)));
}

/** Tread left below the caution threshold is amber, which is what the wear numeral and its column take. */
const cautionWhen = (wear: Expr, nominal: Hex): Expr => iff(lt(wear, num(WEAR_CAUTION)), str(ds.color.caution.primary), str(nominal));

/** Design-time readings per corner, so the editor shows four wheels rather than the same one four times. */
export const CORNER_SAMPLES: Record<Corner, { temperature: string; pressure: string; wear: string }> = {
  FrontLeft: { temperature: '84', pressure: '27.8', wear: '79' },
  FrontRight: { temperature: '104', pressure: '28.6', wear: '61' },
  RearLeft: { temperature: '62', pressure: '27.1', wear: '86' },
  RearRight: { temperature: '88', pressure: '27.9', wear: '58' },
};

/**
 * The size the temperature is drawn at, which the canvas puts at 34 px in every zone and 64 on a
 * companion page. Each ramp names 34 differently -- it is the zone ramp's `mid` and the compact
 * one's `big` -- so the rung is named per density rather than the number repeated.
 *
 * Taken from the ramp and not from the cell's height, which is what used to make the same corner
 * 46 px in a tall zone and 34 in a short one with no decision behind either.
 */
const temperatureSize = (d: DensitySpec, density: Density): number => (density === 'zone' || density === 'wide' || density === 'panel' ? d.mid : d.big);

/** One reading of a corner: a value, and the small unit that follows it on its baseline. */
interface Quantity {
  id: string;
  fs: number;
  chars: Chars;
  sample: string;
  bind: Expr;
  color?: Hex;
  colorBind?: Expr;
  unit?: { text: string; widest?: string; bind?: Expr };
  /**
   * The same value read a second way, drawn after the first on the line they share. Only the wide
   * page carries one, and only its pressure: see {@link otherPressure}.
   */
  also?: Quantity;
}

/** A row's readings, left to right: the quantity itself, and the second reading where it has one. */
const readings = (q: Quantity): Quantity[] => (q.also ? [q, q.also] : [q]);

/**
 * The unit's box: the canvas's 22 px, or what the widest unit the binding can produce really needs.
 *
 * Measured upper-cased, which is how a unit is drawn: "kPa" fits the canvas's cell and "KPA" is a
 * twentieth of a pixel past it.
 */
const unitBox = (q: Quantity, d: DensitySpec): number =>
  q.unit === undefined ? 0 : Math.max(UNIT_CELL, Math.ceil(measureText('BarlowMedium', (q.unit.widest ?? q.unit.text).toUpperCase(), d.labelSm)) + 1);

const valueWidth = (q: Quantity): number => monoWidth(cells('SemiBold', q.fs), q.chars);

/** One reading: its cells, and the unit that follows it where it has one. */
const readingWidth = (q: Quantity, d: DensitySpec): number => valueWidth(q) + (q.unit === undefined ? 0 : UNIT_GAP + unitBox(q, d));

const rowWidth = (q: Quantity, d: DensitySpec): number => {
  const row = readings(q);
  return row.reduce((w, r) => w + readingWidth(r, d), 0) + READING_GAP * (row.length - 1);
};

const blockHeight = (qs: readonly Quantity[]): number => qs.reduce((h, q) => h + q.fs, 0) + ROW_GAP * Math.max(0, qs.length - 1);

/**
 * What a pressure reported in one unit is worth in the other. Physical constants, so they are
 * numbers here rather than tokens: `tokens.json` holds what the design decides, not what a psi is.
 */
const KPA_PER_PSI = 6.894757;
const PSI_PER_KPA = 0.1450377;
const PSI_PER_BAR = 14.503774;

/**
 * The pressure read a second way, which is what makes the wide page "tyres with both units": the
 * board is called in one unit and the setup screen is dialled in the other, and a driver on the pit
 * wall should not have to do the sum.
 *
 * SimHub reports one of Psi, Kpa and Bar, and the pair worth drawing is psi and kPa, so a psi car
 * is given kPa and both metric cars are given psi. Bar is kPa divided by a hundred, and printing
 * the same figure twice with the point moved would be width spent on nothing.
 *
 * The unit branched on is `pressureUnit`'s own spelling rather than SimHub's, so that the two
 * readings can never disagree about which unit the first of them is in.
 */
function otherPressure(pressure: Expr): { value: Expr; unit: Expr } {
  const shown = pressureUnit();
  const isPsi = eq(shown, str('psi'));
  return {
    // kPa is drawn whole and psi keeps its decimal, which is how each is read on a setup screen.
    value: iff(isPsi, fmt(mul(pressure, num(KPA_PER_PSI)), '0'), fmt(mul(pressure, iff(eq(shown, str('bar')), num(PSI_PER_BAR), num(PSI_PER_KPA))), '0.0')),
    unit: iff(isPsi, str('kPa'), str('psi')),
  };
}

/** The converted pressure as a reading of its own, drawn after the sim's on the same line. */
function otherReading(corner: Corner, d: DensitySpec): Quantity {
  const pressure = tyrePressure(corner);
  const other = otherPressure(pressure);
  return {
    id: 'pressure.alt',
    fs: d.small,
    // The same budget as the first reading, which is wider than either spelling of the second: a
    // kPa is three whole digits and a psi is `dd.d`, and `28.6` holds both.
    chars: CHARS.pressure,
    // The samples are the psi a stint starts on, so the second reading of one of them is kPa.
    sample: String(Math.round(Number(CORNER_SAMPLES[corner].pressure) * KPA_PER_PSI)),
    bind: iff(eq(pressure, num(0)), str('--'), other.value),
    color: ds.purpose.tyre.pressure,
    unit: { text: 'kPa', widest: 'kPa', bind: other.unit },
  };
}

/** The three readings in the order the drawing stacks them, which is also the order a cell sheds them. */
function quantities(corner: Corner, d: DensitySpec, density: Density): Quantity[] {
  const temp = tyreTemperature(corner);
  const pressure = tyrePressure(corner);
  const wear = tyreWear(corner);
  const sample = CORNER_SAMPLES[corner];
  return [
    {
      id: 'temp',
      fs: temperatureSize(d, density),
      // The degree sign takes a digit cell: 0.359 em in the condensed face against a 0.475 em cell.
      chars: { digits: CHARS.temperature.digits + 1, specials: CHARS.temperature.specials },
      sample: `${sample.temperature}°`,
      bind: iff(eq(temp, num(0)), str('--'), concat(fmt(temp, '0'), str('°'))),
      colorBind: temperatureColour(corner),
    },
    {
      id: 'pressure',
      fs: d.small,
      chars: CHARS.pressure,
      sample: sample.pressure,
      bind: iff(eq(pressure, num(0)), str('--'), fmt(pressure, '0.0')),
      color: ds.purpose.tyre.pressure,
      unit: { text: 'psi', widest: 'kPa', bind: pressureUnit() },
      // Wide zone page 5 is "Tyres with both pressure units", and it is the only box the catalogue
      // gives a corner enough width for a second reading; every other density draws the sim's own.
      ...(density === 'wide' ? { also: otherReading(corner, d) } : {}),
    },
    {
      // The corner's own figure rather than `tyreWearMin`'s worst section, for the reason the
      // drawing's columns share it: no committed trace carries the sections.
      id: 'wear',
      fs: d.small,
      chars: CHARS.percent,
      sample: sample.wear,
      bind: iff(eq(wear, num(0)), str('--'), fmt(wear, '0')),
      colorBind: cautionWhen(wear, ds.purpose.tyre.pressure),
      // Rule 19: the per-cent sign is one of the glyphs `font.cell.excluded` names, so it follows
      // the value as a label rather than going through a cell cut for digits.
      unit: { text: '%' },
    },
  ];
}

/**
 * The readings this cell keeps: the longest run of them that fits, most important first.
 *
 * A second reading goes before the reading that carries it, which is rule 17 applied inside a row
 * rather than down a column: a pressure the cell can draw once and not twice is still a pressure,
 * and shedding the whole row to keep the conversion beside it would cost the driver the figure in
 * order to keep the gloss on it. Only width is at stake, both readings sharing one line.
 */
function keptQuantities(all: readonly Quantity[], frame: Rect, d: DensitySpec): Quantity[] {
  const kept = all.map((q) => (q.also && rowWidth(q, d) > frame.width ? { ...q, also: undefined } : q));
  while (kept.length > 1 && (blockHeight(kept) > frame.height || Math.max(...kept.map((q) => rowWidth(q, d))) > frame.width)) kept.pop();
  return kept;
}

export interface WheelOptions {
  /**
   * Which side of the cell the numbers take. The grid mirrors its corners so that the readings sit
   * outboard; the four-column row does not, and puts the drawing first in every cell.
   */
  numbers: 'left' | 'right';
  /** Gap between the numbers and the drawing; the canvas's ten in the grid and seven in the row. */
  gap?: number;
}

/** One wheel drawn in `frame`. */
export function wheel(name: string, frame: Rect, corner: Corner, density: Density, opts: WheelOptions): Item[] {
  const d = densityOf(density);
  const gap = opts.gap ?? CORNER_GAP;
  const kept = keptQuantities(quantities(corner, d, density), frame, d);
  const numbers = Math.max(...kept.map((q) => rowWidth(q, d)));
  const drawing = tyreGlyphSize(frame, frame.width - gap - numbers);
  const items: Item[] = [];
  if (drawing.width > 0) {
    const box = rect(
      opts.numbers === 'left' ? frame.left + frame.width - drawing.width : frame.left,
      frame.top + (frame.height - drawing.height) / 2,
      drawing.width,
      drawing.height,
    );
    const wear = tyreWear(corner);
    items.push(
      ...tyreGlyph(name, box, corner, {
        fillBind: temperatureColour(corner, cautionWhen(wear, ds.color.text.primary)),
        sample: Number(CORNER_SAMPLES[corner].wear),
        // The badge sits on the side the readings are on, which is the outer one: the drawing is inboard.
        badge: opts.numbers,
      }),
    );
  }
  // The column is flush with the cell's outer edge and its rows are set against that same edge, so
  // a left tyre's readings end where the drawing begins and a right tyre's begin where it ends.
  const columnLeft = opts.numbers === 'left' ? frame.left : frame.left + frame.width - numbers;
  // A value's box is a little wider than its cells, because WPF clips a box sized to the exact
  // text. That slack has to come out of the gap before the drawing, never out of the cell's edge.
  const columnEnd = opts.numbers === 'left' ? columnLeft + numbers + gap : frame.left + frame.width;
  let top = frame.top + (frame.height - blockHeight(kept)) / 2;
  for (const q of kept) {
    const width = rowWidth(q, d);
    // The row is set as one piece, so a second reading extends it inwards from the outer edge and
    // the reading nearest the drawing is the last one written rather than the first.
    let x = opts.numbers === 'left' ? columnLeft + numbers - width : columnLeft;
    for (const r of readings(q)) {
      items.push(
        numeral(`${name}.${r.id}`, r.sample, x, top, r.fs, r.chars, {
          bind: r.bind,
          color: r.color,
          colorBind: r.colorBind,
          maxWidth: Math.max(0, columnEnd - x),
        }),
      );
      if (r.unit) {
        const box = unitBox(r, d);
        items.push(
          unit(`${name}.${r.id}.unit`, r.unit.text, x + valueWidth(r) + UNIT_GAP, canvasYForBaseline(canvasBaseline(top, r.fs), d.labelSm), box, {
            size: d.labelSm,
            bind: r.unit.bind,
            widest: r.unit.widest,
          }),
        );
      }
      x += readingWidth(r, d) + READING_GAP;
    }
    top += q.fs + ROW_GAP;
  }
  return items;
}
