/**
 * Band D's eight pages, and the corner blocks that flank them.
 *
 * A band is a wide short box — 1920 by 60 at the reference size — which is a shape nothing else on
 * the face has, and that is why these are the band's own pages rather than the modules under the
 * same names. A module lays itself out in two dimensions and divides its height between rows; hand
 * one a 26 px body and it computes negative rectangles, which is exactly what happened the first
 * time this file reached for `pageBuilder`.
 *
 * So a band page is **one rank of fields**, read left to right, packed and centred between the
 * corner blocks, with nothing spread to fill. A page sheds its last field when the rank will not
 * fit, and the artboards draw that happening: the fuel page is seven fields at 1920, six at 1280,
 * five in the catalogue's 1200 and three at 600. A field the sim does not publish is a different
 * matter and closes the rank over its hole, which is `when: 'close'` in `second/rank.ts`.
 *
 * The fields are read off `design/canvas/ZoneCatalogue.dc.html`, page by page, and the metrics off
 * the band of each face's own artboard.
 */
import type { Item, Rect } from '../generator.ts';
import { ncalc } from '../generator.ts';
import { measureText } from '../design/advances.ts';
import { cells, monoWidth, textBox } from '../design/metrics.ts';
import { label } from '../elements/label.ts';
import { numeral } from '../elements/numeral.ts';
import { unit } from '../elements/unit.ts';
import { densityOf } from '../second/density.ts';
import { dimUnless, rank, type RankMember } from '../second/rank.ts';
import {
  CHARS,
  airTemperature,
  bestLap,
  clock,
  currentLap,
  carPosition,
  carRelativeGap,
  fuel as fuelLevel,
  fuelLapsLeft,
  fuelLastLap,
  fuelPerLap,
  fuelTimeLeft,
  incidents,
  lastLap,
  localClock,
  minutesClock,
  roadTemperature,
  simClock,
  windKmh,
} from '../second/values.ts';
import { ds } from '../tokens.ts';

const { fmt, isnull, num, str, iff, eq, gt, div, game, raw, concat, driver, playerPosition, aheadBehind, timespanToSeconds, toShortTime } = ncalc;

/** One field of a band page: a label above a value, with an optional unit after it. */
export interface BandField {
  id: string;
  label: string;
  sample: string;
  bind: string;
  chars: { digits: number; specials: number };
  /**
   * A label that reads the sim rather than naming the field: the relative's three rows are headed
   * by the position of the car they show, which is what the catalogue draws over each gap.
   * `labelWidest` is what the box is measured by, as `widest` is for a value.
   */
  labelBind?: string;
  labelWidest?: string;
  /**
   * Further numerals sharing this field's label, drawn after its own on the same line.
   *
   * The catalogue's tyre page is the case: four carcass temperatures under one "TYRES °C" rather
   * than four labels and four degree signs. They share the field's `chars`, so the row is a line of
   * equal cells and a temperature that gains a digit moves nothing.
   */
  row?: readonly { sample: string; bind: string }[];
  /** A small unit or denominator drawn after the value. */
  after?: string;
  color?: `#${string}`;
  colorBind?: string;
  /**
   * True while the game publishes the field. A field without one is always drawn; a field with one
   * is removed when the game has nothing for it and the rank closes over the hole, which is band
   * D's own rule and the reason the strip in the bar is narrower on a car that has fewer settings.
   */
  present?: string;
  /**
   * A word rather than a number, drawn as a proportional label.
   *
   * Rule 19: a value is laid in monospace cells cut for digits, and only what fits a cell may go in
   * one. "MEDIUM" and "GREEN" do not -- M, W and U all overrun -- so a compound or a track state is
   * a label that happens to be bound, not a value. `widest` is what it is measured by.
   */
  widest?: string;
}

const lapTime = (expr: string): string => iff(eq(timespanToSeconds(expr), num(0)), str('--:--.---'), toShortTime(expr, 3));

/** D1 Fuel: what a driver checks on a straight, which is why it is the default. */
const fuel: readonly BandField[] = [
  { id: 'fuel', label: 'Fuel', sample: '15.12', bind: fmt(fuelLevel(), '0.00'), chars: { digits: 5, specials: 1 }, after: 'L', color: ds.purpose.fuel.nominal },
  { id: 'time', label: 'Fuel time', sample: '08:46', bind: minutesClock(fuelTimeLeft()), chars: CHARS.minutesClock },
  { id: 'laps', label: 'Est. laps', sample: '13.1', bind: fmt(fuelLapsLeft(), '0.0'), chars: CHARS.consumption },
  { id: 'refuel', label: 'Refuel', sample: '32.67', bind: fmt(isnull(raw('PitSvFuel'), num(0)), '0.00'), chars: { digits: 5, specials: 1 }, color: ds.color.caution.primary },
  { id: 'perLap', label: 'Per lap', sample: '1.432', bind: fmt(fuelPerLap(), '0.000'), chars: { digits: 5, specials: 1 } },
  { id: 'lastLap', label: 'Last lap', sample: '1.321', bind: fmt(fuelLastLap(), '0.000'), chars: { digits: 5, specials: 1 } },
];

/** D2 Energy. Le Mans Ultimate publishes virtual energy; iRacing does not, so this reads `--`. */
const energy: readonly BandField[] = [
  { id: 'energy', label: 'Energy', sample: '68', bind: notAvailable(raw('VirtualEnergy')), chars: CHARS.temperature, after: '%' },
  { id: 'perLap', label: 'Per lap', sample: '5.6', bind: notAvailable(raw('VirtualEnergyPerLap')), chars: CHARS.consumption, after: '%' },
  { id: 'laps', label: 'Est. laps', sample: '12.1', bind: notAvailable(raw('VirtualEnergyLaps')), chars: CHARS.consumption },
  { id: 'refuel', label: 'Refuel', sample: '31', bind: notAvailable(raw('VirtualEnergyRefuel')), chars: CHARS.temperature, after: '%', color: ds.color.caution.primary },
  { id: 'ratio', label: 'Fuel to energy', sample: '1.04', bind: fuelToEnergy(), chars: CHARS.consumption },
];

/** D3 Stint. The pit window needs a strategy the plugin does not compute; see ADR 0009. */
const stint: readonly BandField[] = [
  { id: 'laps', label: 'Stint laps', sample: '12', bind: fmt(isnull(driver('lapsdonesincelastpitout', playerPosition()), num(0)), '0'), chars: CHARS.position },
  { id: 'time', label: 'Stint time', sample: '0:21:40', bind: clock(timespanToSeconds(isnull(driver('timesincelastpitout', playerPosition()), num(0)))), chars: CHARS.clock },
  { id: 'stops', label: 'Stops', sample: '1', bind: fmt(isnull(driver('pitcount', playerPosition()), num(0)), '0'), chars: CHARS.position },
  { id: 'lastStop', label: 'Last stop', sample: '24.3', bind: fmt(timespanToSeconds(isnull(driver('pitlastduration', playerPosition()), num(0))), '0.0'), chars: CHARS.consumption, after: 's' },
];

/**
 * D4 Tyres: the four carcass temperatures across the band, in car order, and the compound.
 *
 * One label over the four rather than four: the catalogue draws "Tyres °C" once and then 84 104 62
 * 88, which reads as a set and spends one degree sign instead of four on a band that has room for
 * neither.
 */
const tyres: readonly BandField[] = [
  {
    id: 'temps',
    label: 'Tyres °C',
    sample: '84',
    bind: fmt(isnull(raw('LFtempCM'), num(0)), '0'),
    chars: CHARS.temperature,
    row: [
      { sample: '104', bind: fmt(isnull(raw('RFtempCM'), num(0)), '0') },
      { sample: '62', bind: fmt(isnull(raw('LRtempCM'), num(0)), '0') },
      { sample: '88', bind: fmt(isnull(raw('RRtempCM'), num(0)), '0') },
    ],
  },
  { id: 'compound', label: 'Compound', sample: 'Medium', bind: isnull(driver('fronttyrecompound', playerPosition()), str('--')), chars: { digits: 7, specials: 0 }, widest: 'Medium' },
];

/** D5 Weather. There is no weather module: a companion page of it would be mostly empty, and a
 *  band is exactly the shape for it. */
const weather: readonly BandField[] = [
  { id: 'air', label: 'Air', sample: '21.5', bind: fmt(airTemperature(), '0.0'), chars: CHARS.pressure, after: '°' },
  { id: 'track', label: 'Track', sample: '27.6', bind: fmt(roadTemperature(), '0.0'), chars: CHARS.pressure, after: '°' },
  { id: 'wind', label: 'Wind', sample: '12', bind: fmt(windKmh(), '0'), chars: CHARS.temperature, after: 'KM/H' },
  { id: 'grip', label: 'Grip', sample: 'GREEN', bind: ncalc.ucase(isnull(game('TrackGripStatus'), str('--'))), chars: { digits: 7, specials: 0 }, widest: 'MODERATE' },
];

/** D6 Sectors: the three of the last lap, then the lap itself and the session best. */
const sectors: readonly BandField[] = [
  { id: 's1', label: 'S1', sample: '28.41', bind: fmt(timespanToSeconds(ncalc.driverSector('lastlap', playerPosition(), 1)), '0.00'), chars: CHARS.sector },
  { id: 's2', label: 'S2', sample: '41.07', bind: fmt(timespanToSeconds(ncalc.driverSector('lastlap', playerPosition(), 2)), '0.00'), chars: CHARS.sector },
  { id: 's3', label: 'S3', sample: '32.83', bind: fmt(timespanToSeconds(ncalc.driverSector('lastlap', playerPosition(), 3)), '0.00'), chars: CHARS.sector },
  { id: 'last', label: 'Last', sample: '1:42.905', bind: lapTime(lastLap()), chars: CHARS.lapTime },
  { id: 'best', label: 'Best', sample: '1:41.877', bind: lapTime(bestLap()), chars: CHARS.lapTime, color: ds.purpose.lap.sessionBest },
];

/**
 * D7 Relative: the car ahead, the driver, and the car behind.
 *
 * Each gap is headed by the position of the car it belongs to rather than by the word for where it
 * is, which is how the catalogue draws it: P3 above −1.342 says both which car and how far in the
 * room a label already takes. "P99" is the widest of them, and a field is measured by its widest.
 */
const relativePosition = (idx: string): string => concat(str('P'), fmt(carPosition(idx), '0'));

const relative: readonly BandField[] = [
  {
    id: 'ahead',
    label: 'P3',
    labelBind: relativePosition(aheadBehind(num(-1))),
    labelWidest: 'P99',
    sample: '-1.342',
    bind: fmt(carRelativeGap(aheadBehind(num(-1))), '0.000'),
    chars: CHARS.relativeGap,
    color: ds.color.text.secondary,
  },
  { id: 'you', label: 'P4', labelBind: relativePosition(playerPosition()), labelWidest: 'P99', sample: '0.000', bind: str('0.000'), chars: CHARS.relativeGap },
  {
    id: 'behind',
    label: 'P5',
    labelBind: relativePosition(aheadBehind(num(1))),
    labelWidest: 'P99',
    sample: '+0.722',
    bind: fmt(carRelativeGap(aheadBehind(num(1))), '+0.000;-0.000;0.000'),
    chars: CHARS.relativeGap,
    color: ds.color.text.secondary,
  },
];

/**
 * D8 Car: the telltale row belongs here, and the pictograms need the generator's image item
 * (XOR-115) before XOR-97 can draw them. Until then this is the readings the lamps would sit
 * beside, so the page is worth cycling to rather than an empty step in the ring.
 */
const car: readonly BandField[] = [
  { id: 'water', label: 'Water', sample: '92', bind: fmt(isnull(game('WaterTemperature'), num(0)), '0'), chars: CHARS.temperature, after: '°' },
  { id: 'oil', label: 'Oil', sample: '104', bind: fmt(isnull(game('OilTemperature'), num(0)), '0'), chars: CHARS.temperature, after: '°' },
  // The three gauges below the temperatures are the ones a sim either wires or does not. They are
  // removed rather than zeroed: 0.0 bar of oil pressure is a reading, and a wrong one.
  { id: 'oilPressure', label: 'Oil pressure', sample: '4.2', ...optional(game('OilPressure'), '0.0'), chars: CHARS.consumption },
  { id: 'fuelPressure', label: 'Fuel pressure', sample: '3.8', ...optional(raw('FuelPress'), '0.0'), chars: CHARS.consumption },
  { id: 'voltage', label: 'Voltage', sample: '13.8', ...optional(raw('Voltage'), '0.0'), chars: CHARS.consumption },
];

/**
 * A reading the sim may not publish at all: what it draws, and what says it is there.
 *
 * The text is guarded as well as hidden. A hidden item's bindings are still evaluated every frame,
 * so formatting a null would put an error in SimHub's log once per frame for a car that simply has
 * no such sensor.
 */
function optional(expr: string, pattern: string): { bind: string; present: string } {
  const there = ncalc.not(ncalc.isNull(expr));
  return { bind: iff(there, fmt(expr, pattern), str('')), present: there };
}

/** `--` when the sim publishes nothing, rather than a zero that reads as a reading. */
function notAvailable(expr: string): string {
  return iff(ncalc.isNull(expr), str('--'), fmt(expr, '0.0'));
}

/**
 * Litres per lap against energy per lap: how much fuel a per cent of the virtual tank is worth,
 * which is the number a strategist converts one budget into the other with.
 *
 * The denominator is defaulted rather than guarded, because NCalc's `if` evaluates the branch it
 * discards as well as the one it keeps, and a division by a null is an error in SimHub's log once
 * per frame on every car that has no virtual energy at all.
 */
function fuelToEnergy(): string {
  const perLap = isnull(raw('VirtualEnergyPerLap'), num(0));
  return iff(gt(perLap, num(0)), fmt(div(fuelPerLap(), isnull(raw('VirtualEnergyPerLap'), num(1))), '0.00'), str('--'));
}

/** The eight pages, by the id the contract gives them. */
export const BAND_PAGES: Record<string, readonly BandField[]> = {
  fuel,
  energy,
  stint,
  tyres,
  weather,
  sectors,
  relative,
  car,
};

/**
 * What the artboards draw band D to, per band rectangle.
 *
 * Keyed by the rectangle because that is all a band dashboard is ever given: its screens are built
 * at the band's size and nothing hands them the face they belong to. Every number here is read off
 * the band of that face's own artboard rather than taken from the zone density, which is what the
 * band used before and what put 20 px of padding under a drawing that says 16.
 *
 * The three gaps that do not vary -- 22 between the band's groups, 18 inside a corner block, 5
 * between a label and its value and between a value and its unit -- are the constants below.
 */
export interface BandMetrics {
  /** Side padding, inside which the zone letter and the corner blocks sit. */
  padX: number;
  /** Gap between the zone letter and the row beside it. */
  letterGap: number;
  /** Gap between the fields of a page's rank. */
  fieldGap: number;
  /** The value size the artboard draws, where the band's height allows it. */
  valueSize: number;
}

const BAND_METRICS: Record<string, BandMetrics> = {
  '1920x60': { padX: 16, letterGap: 16, fieldGap: 34, valueSize: 34 },
  '1280x60': { padX: 16, letterGap: 16, fieldGap: 34, valueSize: 34 },
  '1280x54': { padX: 16, letterGap: 16, fieldGap: 34, valueSize: 24 },
  '850x60': { padX: 16, letterGap: 16, fieldGap: 26, valueSize: 34 },
  '800x60': { padX: 16, letterGap: 16, fieldGap: 26, valueSize: 34 },
  '800x58': { padX: 16, letterGap: 16, fieldGap: 26, valueSize: 34 },
  '600x56': { padX: 12, letterGap: 14, fieldGap: 18, valueSize: 24 },
};

/** The reference band's, for a rectangle no artboard draws. */
const DEFAULT_BAND_METRICS: BandMetrics = { padX: 16, letterGap: 16, fieldGap: 26, valueSize: 34 };

export const bandMetrics = (frame: Rect): BandMetrics => BAND_METRICS[`${frame.width}x${frame.height}`] ?? DEFAULT_BAND_METRICS;

/** Gap between the band's three groups: the left corner, the page, the right corner. */
const BAND_GROUP_GAP = 22;
/** Gap between the fields of a corner block, and between the lamps and the clocks beside them. */
const CORNER_GAP = 18;
/** Gap between the lamp chips, which are 6 px apart because each carries its own outline. */
const LAMP_GAP = 6;
/** Gap between a field's label and its value, and between a value and the unit after it. */
const FIELD_GAP = 5;
/** Gap between the numerals a field draws under one label. */
const ROW_GAP = ds.space[2];

/**
 * The largest value size whose line box fits the band, given the label above it.
 *
 * The value takes the height the band gives it rather than the height the density prefers. A band
 * is 60 px at 1920, 58 on the nano and 54 at 1280 by 400, and a value sized for the tallest hangs
 * out of the shortest -- where WPF clips it and the row reads as a rendering fault. A rank of one
 * row has nothing to shed, so this is one of the two places a value shrinks instead.
 *
 * Solved by trying rather than by algebra, because the thing that has to fit is a WPF line box:
 * it starts a tenth of the size above the line it is given and runs about a fifth below the
 * baseline, and rounding at both ends is what the last pixel turns on.
 */
function valueSizeFor(height: number, preferred: number, labelFs: number, fieldGap: number): number {
  for (let fs = preferred; fs > 8; fs--) {
    const blockHeight = labelFs + fieldGap + fs;
    const top = Math.max(0, (height - blockHeight) / 2);
    const box = textBox(top + labelFs + fieldGap, fs);
    if (box.top >= 0 && box.top + box.height <= height) return fs;
  }
  return 8;
}

/** Width the unit after a value takes. Measured, not the remainder of the field: a field whose
 *  value fills its width left the unit a box narrower than its own glyph, and WPF clipped it. */
const unitWidth = (field: BandField, labelFs: number): number => (field.after ? Math.ceil(measureText('BarlowMedium', field.after, labelFs)) + 2 : 0);

/** Width the value of a field takes, the numerals sharing its label included. */
function valueWidthOf(field: BandField, valueFs: number): number {
  if (field.widest) return Math.ceil(measureText('BarlowMedium', field.widest, valueFs)) + 2;
  const cell = monoWidth(cells('SemiBold', valueFs), field.chars);
  return field.row ? cell * (field.row.length + 1) + ROW_GAP * field.row.length : cell;
}

/** Width a band field takes: its value with its unit, or its label, whichever is wider. */
function fieldWidth(field: BandField, valueFs: number, labelFs: number): number {
  const value = valueWidthOf(field, valueFs);
  const after = field.after ? FIELD_GAP + unitWidth(field, labelFs) : 0;
  const text = Math.ceil(measureText('BarlowMedium', field.labelWidest ?? field.label.toUpperCase(), labelFs)) + 2;
  return Math.ceil(Math.max(value + after, text));
}

/**
 * One field of a band page as a member of its rank: how wide it is, whether the game publishes it,
 * and how it draws itself wherever the rank puts it.
 */
function bandMember(field: BandField, prefix: string, geometry: { valueFs: number; labelFs: number; top: number; valueTop: number }): RankMember {
  const { valueFs, labelFs, top, valueTop } = geometry;
  const w = fieldWidth(field, valueFs, labelFs);
  const cell = field.widest ? w : monoWidth(cells('SemiBold', valueFs), field.chars);
  const valueWidth = valueWidthOf(field, valueFs);
  return {
    id: field.id,
    width: w,
    present: field.present,
    draw: (at) => {
      const items: Item[] = [
        label(`${prefix}${field.id}.label`, field.label.toUpperCase(), at.x, top, w, {
          size: labelFs,
          bind: field.labelBind,
          widest: field.labelWidest,
          leftBind: at.leftAt(),
          visibleBind: at.visibleBind,
        }),
        field.widest
          ? label(`${prefix}${field.id}.value`, field.sample, at.x, valueTop, w, {
              size: valueFs,
              color: field.color ?? ds.color.text.primary,
              bind: field.bind,
              widest: field.widest,
              leftBind: at.leftAt(),
              visibleBind: at.visibleBind,
            })
          : numeral(`${prefix}${field.id}.value`, field.sample, at.x, valueTop, valueFs, field.chars, {
              bind: field.bind,
              color: field.color,
              colorBind: field.colorBind,
              maxWidth: cell + 4,
              leftBind: at.leftAt(),
              visibleBind: at.visibleBind,
            }),
      ];
      // The numerals that share the label, each one cell further along, so a set reads as a set and
      // a reading that gains a digit moves none of the others.
      field.row?.forEach((more, i) => {
        const dx = (i + 1) * (cell + ROW_GAP);
        items.push(
          numeral(`${prefix}${field.id}.${i + 2}`, more.sample, at.x + dx, valueTop, valueFs, field.chars, {
            bind: more.bind,
            maxWidth: cell + 4,
            leftBind: at.leftAt(dx),
            visibleBind: at.visibleBind,
          }),
        );
      });
      if (field.after) {
        items.push(
          unit(`${prefix}${field.id}.unit`, field.after, at.x + valueWidth + FIELD_GAP, valueTop + (valueFs - labelFs), unitWidth(field, labelFs), {
            size: labelFs,
            leftBind: at.leftAt(valueWidth + FIELD_GAP),
            visibleBind: at.visibleBind,
          }),
        );
      }
      return items;
    },
  };
}

/**
 * One band page drawn in `frame`: its fields as one rank, packed and centred in what the corner
 * blocks leave.
 *
 * The rank sheds from the tail when the width is not there, because the fields are listed in
 * importance order, and it closes over any field the game does not publish. Nothing is spread to
 * fill: a band with three fields in it is three fields in the middle, not three fields stretched
 * across 1920 px.
 */
export function bandPageItems(id: string, frame: Rect, prefix: string, corners = false): Item[] {
  const fields = BAND_PAGES[id];
  if (!fields) throw new RangeError(`band D has no page "${id}"`);
  const m = bandMetrics(frame);
  const labelFs = densityOf('zone').labelSm;
  const valueFs = valueSizeFor(frame.height, m.valueSize, labelFs, FIELD_GAP);
  const geometry = blockGeometry(frame, valueFs, labelFs);

  // The rank gets what the letter and the corner blocks leave, not the whole band. Centring in the
  // whole band put the last field of D6 Sectors six pixels into the DRS lamp at 1280, where a page
  // and a corner block drew over each other and only a photograph would have shown it.
  const taken = corners ? bandCornerWidths(frame) : { left: m.padX + letterRoom(frame), right: m.padX };
  const apart = corners ? BAND_GROUP_GAP : 0;
  const usable = { left: frame.left + taken.left + apart, width: Math.max(0, frame.width - taken.left - taken.right - 2 * apart) };

  return rank(
    fields.map((field) => bandMember(field, prefix, geometry)),
    { left: usable.left, width: usable.width, gap: m.fieldGap, when: 'close' },
  ).items;
}

/** Where a block of labels over values sits in the band: vertically centred, its two rows 5 apart. */
function blockGeometry(frame: Rect, valueFs: number, labelFs: number): { valueFs: number; labelFs: number; top: number; valueTop: number } {
  const top = frame.top + Math.max(0, (frame.height - (labelFs + FIELD_GAP + valueFs)) / 2);
  return { valueFs, labelFs, top, valueTop: top + labelFs + FIELD_GAP };
}

/**
 * The room the zone letter takes at the left end, its gap included.
 *
 * The letter is drawn by the face rather than by the band -- a band dashboard is one file serving
 * one rectangle and knows no letter -- but the room is reserved here, because the corner block and
 * the rank are what would otherwise be drawn over it.
 */
const letterRoom = (frame: Rect): number => {
  const m = bandMetrics(frame);
  return Math.ceil(measureText('BarlowMedium', 'D', densityOf('zone').labelSm)) + 2 + m.letterGap;
};

// --- The corner blocks at each end of the band -------------------------------------------------
//
// Incidents against their limit and the track state on the left; DRS, push to pass, the spotter
// lamps and both clocks on the right.
//
// Drawn at 1920x480, 1280x480, 1280x400 and 1280x720 and absent at 850x480, 800x286 and 600x686.
// The threshold is those drawings rather than a round number, which is why the layout carries the
// answer rather than this file computing one.

/** The two fields in the left corner. */
const leftCornerFields = (): BandField[] => [
  {
    id: 'incidents',
    label: 'Incidents',
    sample: '3x',
    bind: concat(fmt(isnull(incidents(), num(0)), '0'), str('x')),
    chars: CHARS.count,
    color: ds.color.caution.primary,
  },
  // Mixed case rather than upper: "Dry" is a word the driver reads at a glance and "DRY" is one he
  // parses. It is a bound label and not a value for the reason rule 19 gives -- M and W overrun a
  // cell cut for digits -- and "Moderate" is the widest state iRacing reports.
  { id: 'trackState', label: 'Track', sample: 'Dry', bind: isnull(game('TrackGripStatus'), str('--')), chars: { digits: 5, specials: 0 }, widest: 'Moderate' },
];

/** The three lamps in the right corner. A lamp is a word, lit or dim; it never disappears. */
const cornerLamps = (): { id: string; text: string; on: string; colour: `#${string}` }[] => [
  { id: 'drs', text: 'DRS', on: eq(isnull(game('DRSAvailable'), num(0)), num(1)), colour: ds.purpose.flag.green },
  { id: 'p2p', text: 'P2P', on: eq(isnull(raw('PushToPass'), num(0)), num(1)), colour: ds.purpose.flag.blue },
  { id: 'spt', text: 'SPT', on: ncalc.ne(isnull(game('CarLeftRight'), num(0)), num(1)), colour: ds.purpose.flag.yellow },
];

/** The two clocks in the right corner. */
const cornerClocks = (): BandField[] => [
  { id: 'clock', label: 'Clock', sample: '13:11', bind: localClock(), chars: CHARS.clock },
  { id: 'sim', label: 'Sim', sample: '19:26', bind: simClock(), chars: CHARS.clock },
];

/**
 * How much of the band each corner block takes, padding included.
 *
 * Measured here rather than by the drawing code, because the page rank has to know it before it
 * lays anything out: a rank centred in the whole band draws its last field over the DRS lamp at
 * 1280, which is the same mistake the bar made at 850 before d839bd5, and the one thing a band
 * page must never do is hide the field beside it.
 */
export function bandCornerWidths(frame: Rect): { left: number; right: number } {
  const m = bandMetrics(frame);
  const labelFs = densityOf('zone').labelSm;
  const valueFs = cornerValueSize(frame, labelFs);
  const left = leftCornerFields().reduce((sum, f) => sum + fieldWidth(f, valueFs, labelFs), CORNER_GAP);
  const clockWidth = cornerClocks().reduce((sum, f) => sum + fieldWidth(f, valueFs, labelFs), CORNER_GAP);
  const lamps = cornerLamps().length * lampWidth(labelFs) + LAMP_GAP * (cornerLamps().length - 1);
  return { left: m.padX + letterRoom(frame) + left, right: m.padX + lamps + CORNER_GAP + clockWidth };
}

const cornerValueSize = (frame: Rect, labelFs: number): number => valueSizeFor(frame.height, densityOf('zone').small, labelFs, FIELD_GAP);

/**
 * The room a lamp takes: the word, and the padding of the chip the canvas draws around it.
 *
 * The chip itself -- 20 px tall with a 1 px outline in the lamp's colour -- waits on a bound
 * `BorderStyle.BorderColor`, which the format research says SimHub honours and `elements/band.ts`
 * does not yet offer. Until it does the word is drawn where the chip would put it, so that adding
 * the outline moves nothing.
 */
const LAMP_PAD_X = 7;
const lampWidth = (labelFs: number): number => Math.ceil(measureText('BarlowMedium', 'DRS', labelFs)) + 2 + 2 * (LAMP_PAD_X + 1);

export function bandCorners(frame: Rect, prefix: string): Item[] {
  const m = bandMetrics(frame);
  const labelFs = densityOf('zone').labelSm;
  const valueFs = cornerValueSize(frame, labelFs);
  const { top, valueTop } = blockGeometry(frame, valueFs, labelFs);
  const blockHeight = labelFs + FIELD_GAP + valueFs;
  const items: Item[] = [];

  const left = leftCornerFields();
  let x = frame.left + m.padX + letterRoom(frame);
  for (const field of left) {
    const w = fieldWidth(field, valueFs, labelFs);
    items.push(label(`${prefix}${field.id}.label`, field.label.toUpperCase(), x, top, w, { size: labelFs }));
    items.push(
      field.widest
        ? label(`${prefix}${field.id}.value`, field.sample, x, valueTop, w, {
            size: valueFs,
            color: field.color ?? ds.color.text.primary,
            bind: field.bind,
            widest: field.widest,
          })
        : numeral(`${prefix}${field.id}.value`, field.sample, x, valueTop, valueFs, field.chars, { bind: field.bind, color: field.color, maxWidth: w + 4 }),
    );
    x += w + CORNER_GAP;
  }

  // The right corner: three lamps, then the two clocks. A lamp is drawn dim rather than removed --
  // `when: 'dim'`, the other half of the rank's contract -- because a lamp that vanished would move
  // the two beside it at the moment they matter.
  const lamps = cornerLamps();
  const clocks = cornerClocks();
  const lamp = lampWidth(labelFs);

  // Laid from the right edge rather than from a computed start, so that Sim ends against the band's
  // own padding whatever the clocks measure, and each clock is right aligned inside its box for the
  // same reason: a field whose allowance is wider than its digits left them short of the edge.
  let right = frame.left + frame.width - m.padX;
  for (const field of [...clocks].reverse()) {
    const w = fieldWidth(field, valueFs, labelFs);
    right -= w;
    items.push(label(`${prefix}${field.id}.label`, field.label.toUpperCase(), right, top, w, { size: labelFs, hAlign: 'right' }));
    items.push(numeral(`${prefix}${field.id}.value`, field.sample, right, valueTop, valueFs, field.chars, { bind: field.bind, width: w, hAlign: 'right' }));
    right -= CORNER_GAP;
  }

  x = right - (lamps.length * lamp + LAMP_GAP * (lamps.length - 1));
  items.push(
    ...rank(
      lamps.map((each) => ({
        id: each.id,
        width: lamp,
        present: each.on,
        draw: (at) => [
          label(`${prefix}${each.id}`, each.text, at.x, top + (blockHeight - labelFs) / 2, lamp, {
            size: labelFs,
            hAlign: 'center',
            color: ds.color.text.dim,
            colorBind: dimUnless(at.litBind, each.colour),
          }),
        ],
      })),
      { left: x, width: lamps.length * lamp + LAMP_GAP * (lamps.length - 1), gap: LAMP_GAP, when: 'dim', align: 'left' },
    ).items,
  );
  return items;
}

/** Every page id band D can show, for a test that wants to walk them. */
export const BAND_PAGE_IDS: readonly string[] = Object.keys(BAND_PAGES);
