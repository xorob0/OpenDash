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
 * iRacing reports the pressure the car left the pit box with, not a live one; the temperature and
 * the tread are live.
 */
import type { Hex, Item, Rect } from '../generator.ts';
import { ncalc } from '../generator.ts';
import { withBindings, type Expr } from '../bind.ts';
import { measureText } from '../design/advances.ts';
import { WHEEL_CHANGE_TICK, assetBox, imageOf } from '../design/assets.ts';
import { rect } from '../design/geometry.ts';
import { canvasBaseline, canvasYForBaseline, cells, monoWidth, type Chars } from '../design/metrics.ts';
import { band } from '../elements/band.ts';
import { numeral } from '../elements/numeral.ts';
import { unit } from '../elements/unit.ts';
import { ds } from '../tokens.ts';
import { barGauge } from './gauge.ts';
import { densityOf, type Density, type DensitySpec } from './density.ts';
import { CHARS, pressureUnit, tyreChangeScheduled, tyrePressure, tyreTemperature, tyreWear, type Corner } from './values.ts';

const { iff, eq, lt, gt, str, num, fmt, concat, game } = ncalc;

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
 * The tyre drawing, as fractions of its own box, read off the canvas's SVG at every size it draws
 * one (34 by 62 on the components sheet, 37 by 58 on the pit wall, 84 by 132 on a companion page).
 * Every one of them is the same picture scaled, so the proportions are the drawing and the box is
 * rule 18's business.
 */
export const GLYPH = {
  /** 72 by 113 on the catalogue's `wide` sheet, and the same ratio at every other size. */
  aspect: 72 / 113,
  /** The body's outline, and the inset the change badge sits at. */
  stroke: 0.0385,
  radius: 0.2,
  /** A tread column and the gap between two of them, across the box. */
  column: 0.2033,
  columnGap: 0.065,
  /** The tread band down the box: where it starts and how tall it is. */
  treadTop: 0.07,
  treadHeight: 0.86,
  /** A groove cut across the tread, and the badge that marks a wheel down for a change. */
  groove: 0.0346,
  badge: 0.34,
  badgeRadius: 1.5,
} as const;

/** Below this the three columns stop reading as tread and the cell keeps its numbers instead. */
export const GLYPH_MIN_WIDTH = 20;

/** The share of a cell the drawing takes, which is what sizes it at every shape but the widest. */
export const GLYPH_SHARE = 0.35;

/** Three grooves where the canvas has the height for them, two where it does not. */
const GROOVES_FROM = 80;

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
const temperatureSize = (d: DensitySpec, density: Density): number => (density === 'zone' || density === 'wide' ? d.mid : d.big);

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
}

/** The unit's box: the canvas's 22 px, or what the widest unit the binding can produce really needs. */
const unitBox = (q: Quantity, d: DensitySpec): number =>
  q.unit === undefined ? 0 : Math.max(UNIT_CELL, Math.ceil(measureText('BarlowMedium', q.unit.widest ?? q.unit.text, d.labelSm)) + 1);

const valueWidth = (q: Quantity): number => monoWidth(cells('SemiBold', q.fs), q.chars);

const rowWidth = (q: Quantity, d: DensitySpec): number => valueWidth(q) + (q.unit === undefined ? 0 : UNIT_GAP + unitBox(q, d));

const blockHeight = (qs: readonly Quantity[]): number => qs.reduce((h, q) => h + q.fs, 0) + ROW_GAP * Math.max(0, qs.length - 1);

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

/** The readings this cell keeps: the longest run of them that fits, most important first. */
function keptQuantities(all: readonly Quantity[], frame: Rect, d: DensitySpec): Quantity[] {
  const kept = [...all];
  while (kept.length > 1 && (blockHeight(kept) > frame.height || Math.max(...kept.map((q) => rowWidth(q, d))) > frame.width)) kept.pop();
  return kept;
}

/** The drawing's box: its share of the cell, never taller than the cell nor wider than the numbers leave. */
function glyphSize(frame: Rect, numbers: number, gap: number): { width: number; height: number } {
  const width = Math.floor(Math.min(GLYPH_SHARE * frame.width, GLYPH.aspect * frame.height, frame.width - gap - numbers));
  if (width < GLYPH_MIN_WIDTH) return { width: 0, height: 0 };
  return { width, height: Math.floor(width / GLYPH.aspect) };
}

/**
 * The tyre: a body, three tread columns filling from the bottom, and the grooves cut across them.
 *
 * The columns take the tyre's colour -- its temperature band, with the caution amber of a worn
 * tread standing in for the nominal -- and the tread the corner has left.
 *
 * The canvas fills each column from its own third of the tyre, which `tyreWearMin`'s sections are
 * there for. Nothing reads them yet: the twelve `*wear[LMR]` properties are in no committed trace,
 * and a trace is recorded on the VM rather than written by hand, so the columns share the corner's
 * one figure until `bun run record` has been past them.
 */
function glyph(name: string, box: Rect, corner: Corner): Item[] {
  const stroke = Math.max(1, Math.round(GLYPH.stroke * box.width));
  const items: Item[] = [
    band(`${name}.body`, box, ds.purpose.block.well, {
      border: { color: ds.purpose.illustration.dim, width: stroke },
      radius: GLYPH.radius * box.width,
    }),
  ];
  const columnWidth = GLYPH.column * box.width;
  const treadTop = box.top + GLYPH.treadTop * box.height;
  const treadHeight = GLYPH.treadHeight * box.height;
  const left = box.left + (box.width - 3 * columnWidth - 2 * GLYPH.columnGap * box.width) / 2;
  const wear = tyreWear(corner);
  for (let i = 0; i < 3; i++) {
    const column = rect(left + i * (columnWidth + GLYPH.columnGap * box.width), treadTop, columnWidth, treadHeight);
    items.push(
      barGauge(`${name}.tread${i + 1}`, column, wear, {
        track: ds.color.surface.raised,
        fill: ds.color.text.primary,
        fillBind: temperatureColour(corner, cautionWhen(wear, ds.color.text.primary)),
        max: 100,
        value: Number(CORNER_SAMPLES[corner].wear),
      }),
    );
  }
  // The grooves are the division lines of the tread band, which is why there are three of them in a
  // tall drawing and two in a short one: a groove every quarter of a 113 px tyre is a groove every
  // 28 px, and the same every quarter of a 58 px one is a stripe.
  const grooves = box.height >= GROOVES_FROM ? 3 : 2;
  const thickness = Math.max(1, Math.round(GLYPH.groove * box.width));
  const span = 3 * columnWidth + 2 * GLYPH.columnGap * box.width;
  for (let i = 1; i <= grooves; i++) {
    const y = treadTop + (i * treadHeight) / (grooves + 1) - thickness / 2;
    items.push(band(`${name}.groove${i}`, rect(left, y, span, thickness), ds.purpose.block.well));
  }
  return items;
}

/**
 * The badge at the drawing's outer top corner, marking a wheel the pit box is set to change.
 *
 * The block is a rect so that its colour stays a token; the tick inside it is the part no rect can
 * draw and no font can measure, so it is the one picture this package ships.
 */
function changeBadge(name: string, box: Rect, corner: Corner, outer: 'left' | 'right'): Item[] {
  const size = GLYPH.badge * box.width;
  const inset = GLYPH.stroke * box.width;
  const x = outer === 'left' ? box.left + inset : box.left + box.width - inset - size;
  const badge = rect(x, box.top + inset, size, size);
  const visible = tyreChangeScheduled(corner);
  return [
    band(`${name}.change`, badge, ds.color.text.secondary, { visibleBind: visible, radius: GLYPH.badgeRadius }),
    {
      kind: 'image',
      name: `${name}.change.tick`,
      image: WHEEL_CHANGE_TICK.name,
      rect: assetBox(badge, imageOf(WHEEL_CHANGE_TICK)),
      ...withBindings({ Visible: visible }),
    },
  ];
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
  const drawing = glyphSize(frame, numbers, gap);
  const items: Item[] = [];
  if (drawing.width > 0) {
    const box = rect(
      opts.numbers === 'left' ? frame.left + frame.width - drawing.width : frame.left,
      frame.top + (frame.height - drawing.height) / 2,
      drawing.width,
      drawing.height,
    );
    // The badge sits on the side the readings are on, which is the outer one: the drawing is inboard.
    items.push(...glyph(name, box, corner), ...changeBadge(name, box, corner, opts.numbers));
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
    const x = opts.numbers === 'left' ? columnLeft + numbers - width : columnLeft;
    items.push(
      numeral(`${name}.${q.id}`, q.sample, x, top, q.fs, q.chars, {
        bind: q.bind,
        color: q.color,
        colorBind: q.colorBind,
        maxWidth: Math.max(0, columnEnd - x),
      }),
    );
    if (q.unit) {
      const box = unitBox(q, d);
      items.push(
        unit(`${name}.${q.id}.unit`, q.unit.text, x + valueWidth(q) + UNIT_GAP, canvasYForBaseline(canvasBaseline(top, q.fs), d.labelSm), box, {
          size: d.labelSm,
          bind: q.unit.bind,
          widest: q.unit.widest,
        }),
      );
    }
    top += q.fs + ROW_GAP;
  }
  return items;
}
