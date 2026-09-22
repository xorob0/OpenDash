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
 *
 * Seven of the eight pages are that. The eighth, D8 Car, is the twelve-lamp telltale rank, which is
 * a rank of boxes rather than of fields and lives in `telltales.ts`; this file hands it the same
 * room it gives a page of fields and otherwise leaves it alone.
 */
import type { Item, Rect } from '../generator.ts';
import { ncalc } from '../generator.ts';
import { measureText } from '../design/advances.ts';
import { cells, monoWidth, textBox } from '../design/metrics.ts';
import { band } from '../elements/band.ts';
import { label } from '../elements/label.ts';
import { numeral } from '../elements/numeral.ts';
import { unit } from '../elements/unit.ts';
import { densityOf } from '../second/density.ts';
import { dimUnless, rank, type RankMember } from '../second/rank.ts';
import { TELLTALE_PAGE, telltaleItems } from './telltales.ts';
import {
  airTemperature,
  bestLap,
  carPosition,
  positionLabelled,
  carRelativeGap,
  CHARS,
  clock,
  currentLap,
  fuel as fuelLevel,
  fuelLapsLeft,
  fuelLastLap,
  fuelIsSettled,
  fuelPerLap,
  fuelTimeLeft,
  fuelUnit,
  incidents,
  lastLap,
  listNeighbour,
  localClock,
  minutesClock,
  NO_TIME,
  NO_VALUE,
  roadTemperature,
  sectorTime,
  simClock,
  windKmh,
} from '../second/values.ts';
import { ds, TRANSPARENT } from '../tokens.ts';

const { fmt, isnull, num, str, iff, eq, gt, div, game, raw, concat, driver, playerPosition, timespanToSeconds, toShortTime } = ncalc;

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
  /**
   * The unit as the sim names it, with `after` as the design-time text.
   *
   * Fuel is the case: SimHub publishes the tank in the unit `FuelUnit` names, so a fixed "L" over a
   * value in gallons is the same fault as a fixed speed unit, and the box is measured by whichever
   * of the two spellings is wider.
   */
  afterBind?: string;
  /** The longest spelling `afterBind` can produce, which the box is measured by. */
  afterWidest?: string;
  color?: `#${string}`;
  /** The label's own colour, where it is not the label grey: D7 writes the driver's own position
   *  in the primary text the way it writes his own gap. */
  labelColor?: `#${string}`;
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

const lapTime = (expr: string): string => iff(eq(timespanToSeconds(expr), num(0)), str(NO_TIME), toShortTime(expr, 3));

/** D1 Fuel: what a driver checks on a straight, which is why it is the default. */
const fuel: readonly BandField[] = [
  { id: 'fuel', label: 'Fuel', sample: '15.12', bind: fmt(fuelLevel(), '0.00'), chars: { digits: 5, specials: 1 }, after: 'L', afterBind: fuelUnit(), afterWidest: 'GAL', color: ds.purpose.fuel.nominal },
  { id: 'time', label: 'Fuel time', sample: '08:46', bind: minutesClock(fuelTimeLeft()), chars: CHARS.minutesClock },
  // Behind the consumption gate, which is the same one the fuel module draws its own est. laps
  // behind. SimHub publishes the remaining laps as zero before it has a per-lap figure, so an idle
  // screen said "0.0" with the confidence of a reading rather than saying it had none.
  { id: 'laps', label: 'Est. laps', sample: '13.1', bind: iff(fuelIsSettled(), fmt(fuelLapsLeft(), '0.0'), str(NO_VALUE)), chars: CHARS.consumption },
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

/**
 * D6 Sectors: the three of the last lap, then the lap itself and the session best.
 *
 * Through `sectorTime` rather than a bare format, so a sector nobody has set yet reads the same
 * placeholder the two lap times on this page already read. Formatted raw it came out as 0.00, which
 * on the one page a driver reads sector by sector is a time rather than an absence.
 */
const sectors: readonly BandField[] = [
  { id: 's1', label: 'S1', sample: '28.41', bind: sectorTime(ncalc.driverSector('lastlap', playerPosition(), 1)), chars: CHARS.sector },
  { id: 's2', label: 'S2', sample: '41.07', bind: sectorTime(ncalc.driverSector('lastlap', playerPosition(), 2)), chars: CHARS.sector },
  { id: 's3', label: 'S3', sample: '32.83', bind: sectorTime(ncalc.driverSector('lastlap', playerPosition(), 3)), chars: CHARS.sector },
  { id: 'last', label: 'Last', sample: '1:42.905', bind: lapTime(lastLap()), chars: CHARS.lapTime },
  { id: 'best', label: 'Best', sample: '1:41.877', bind: lapTime(bestLap()), chars: CHARS.lapTime, color: ds.purpose.lap.sessionBest },
];

/**
 * D7 Relative: the car ahead, the driver, and the car behind.
 *
 * Each gap is headed by the position of the car it belongs to rather than by the word for where it
 * is, and the catalogue sets the two side by side at the same size rather than one over the other:
 * P3 −1.342 is read in one movement of the eye, where a 15 px P3 over a 34 px gap would read as the
 * heading of a column one number long. That is {@link INLINE_PAGES}. "P99" is the widest position,
 * and a field is measured by its widest.
 *
 * The catalogue draws a 16 by 11 country flag between the two, which nothing publishes a country
 * for; it is the same missing source as the licence badge on the opponents page.
 *
 * The two neighbours are the player's own class wherever the positions heading them are, which is
 * `PositionMode` and nothing else here: a band screen is built at its rectangle and is never told
 * which face it belongs to, so there is no zone filter for it to ask. {@link listNeighbour} takes one as
 * its second argument for the day band D carries a filter of its own.
 */
const relativePosition = (idx: string): string => positionLabelled(idx);

const relative: readonly BandField[] = [
  {
    id: 'ahead',
    label: 'P3',
    labelBind: relativePosition(listNeighbour(-1)),
    labelWidest: 'P99',
    sample: '-1.342',
    bind: carRelativeGap(listNeighbour(-1)),
    chars: CHARS.relativeGap,
    color: ds.color.text.secondary,
  },
  {
    id: 'you',
    label: 'P4',
    labelBind: relativePosition(playerPosition()),
    labelWidest: 'P99',
    labelColor: ds.color.text.primary,
    sample: '0.000',
    bind: str('0.000'),
    chars: CHARS.relativeGap,
  },
  {
    id: 'behind',
    label: 'P5',
    labelBind: relativePosition(listNeighbour(1)),
    labelWidest: 'P99',
    sample: '+0.722',
    bind: carRelativeGap(listNeighbour(1)),
    chars: CHARS.relativeGap,
    color: ds.color.text.secondary,
  },
];

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

/**
 * Seven of the eight pages, by the id the contract gives them.
 *
 * D8 Car is the eighth and is not here, because it is not a rank of fields at all: it is the
 * twelve-lamp telltale rank of `telltales.ts`, which is a rank of boxes and pictograms and shares
 * only the centring. {@link BAND_PAGE_IDS} is the whole cycle.
 */
export const BAND_PAGES: Record<string, readonly BandField[]> = {
  fuel,
  energy,
  stint,
  tyres,
  weather,
  sectors,
  relative,
};

/**
 * The pages the catalogue draws as one line rather than as labels over values, and the word each is
 * headed with.
 *
 * D7 is the only one, and it needs the word because P3, P4 and P5 name the cars rather than the
 * page. The gap between its groups is the catalogue's own 20 and not the band's field gap, which is
 * the pitch of two rows and too wide for three pairs on one line.
 */
interface InlinePage {
  word: string;
  gap: number;
}

const INLINE_PAGES: Record<string, InlinePage> = { relative: { word: 'Relative', gap: 20 } };

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
/**
 * The row a field's label sits in, which is shorter than the label itself.
 *
 * Every band on every artboard puts a 15 px label in a 13 px row and centres it there, the same
 * arrangement the bar above uses. Drawing the label at the row's height instead cost two pixels of
 * a size the whole face is set in, and a 13 px label beside the bar's 15 px one reads as a
 * rendering fault rather than a hierarchy.
 */
const LABEL_ROW = 13;
/** Gap between the numerals a field draws under one label. */
const ROW_GAP = ds.space[2];
/** Gap between the position and the gap of a field the catalogue draws on one line. */
const INLINE_GAP = 8;

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
function valueSizeFor(height: number, preferred: number, labelFs: number, labelRow: number, fieldGap: number): number {
  for (let fs = preferred; fs > 8; fs--) {
    const top = blockTop(height, fs, labelFs, labelRow, fieldGap);
    const value = textBox(top + labelRow + fieldGap, fs);
    const labelTop = textBox(top + (labelRow - labelFs) / 2, labelFs).top;
    if (labelTop >= 0 && value.top >= 0 && value.top + value.height <= height) return fs;
  }
  return 8;
}

/**
 * How far down the block must start for the label's own box to be inside the band.
 *
 * A 15 px label centred in a 13 px row already begins a pixel above the row, and the box around it
 * begins a tenth of the size above that again. The block therefore cannot be pushed flat against
 * the top of the band, which is what bounds the ride-up below.
 */
const minBlockTop = (labelFs: number, labelRow: number): number => Math.max(0, -textBox((labelRow - labelFs) / 2, labelFs).top);

/**
 * Where the two rows sit in a band of `height`: centred, then ridden up until the line box fits.
 *
 * What WPF clips is the line box and not the glyphs, and the box hangs about a fifth of the size
 * below the row the value is given. A block centred on its two rows therefore hangs out of a band
 * that has the room for it: 13 over 5 over 34 is 52 px of rows in a 60 px band, and the box that
 * holds the 34 ends six tenths of a pixel past the bottom. Shrinking the value was the old answer
 * and it cost the artboards' 34 at every 60 px band and at the nano's 58. Moving the block up by
 * those six tenths costs nothing a driver can see and keeps the size the drawing asks for, so the
 * value only shrinks once the band is genuinely too short, which is the 54 px band at 1280 by 400.
 */
function blockTop(height: number, valueFs: number, labelFs: number, labelRow: number, fieldGap: number): number {
  const centred = Math.max(0, (height - (labelRow + fieldGap + valueFs)) / 2);
  const box = textBox(centred + labelRow + fieldGap, valueFs);
  const over = box.top + box.height - height;
  return over > 0 ? Math.max(minBlockTop(labelFs, labelRow), centred - over) : centred;
}

/** Width the unit after a value takes. Measured, not the remainder of the field: a field whose
 *  value fills its width left the unit a box narrower than its own glyph, and WPF clipped it. */
const unitWidth = (field: BandField): number => {
  if (!field.after) return 0;
  // Measured by the wider of the two spellings where the sim names the unit, since either may be
  // the one drawn and a box cut for the shorter clips the longer.
  const drawn = [field.after, ...(field.afterWidest ? [field.afterWidest] : [])].map((s) => measureText('BarlowMedium', s.toUpperCase(), ds.size.labelSm));
  return Math.ceil(Math.max(...drawn)) + 2;
};

/** Width the value of a field takes, the numerals sharing its label included. */
function valueWidthOf(field: BandField, valueFs: number): number {
  if (field.widest) return Math.ceil(measureText('BarlowMedium', field.widest, valueFs)) + 2;
  const cell = monoWidth(cells('SemiBold', valueFs), field.chars);
  return field.row ? cell * (field.row.length + 1) + ROW_GAP * field.row.length : cell;
}

/** Width a band field takes: its value with its unit, or its label, whichever is wider. */
function fieldWidth(field: BandField, valueFs: number, labelFs: number): number {
  const value = valueWidthOf(field, valueFs);
  const after = field.after ? FIELD_GAP + unitWidth(field) : 0;
  const text = Math.ceil(measureText('BarlowMedium', field.labelWidest ?? field.label.toUpperCase(), labelFs)) + 2;
  return Math.ceil(Math.max(value + after, text));
}

/**
 * One field of a band page as a member of its rank: how wide it is, whether the game publishes it,
 * and how it draws itself wherever the rank puts it.
 */
function bandMember(field: BandField, prefix: string, geometry: BlockGeometry): RankMember {
  const { valueFs, labelFs, labelTop, valueTop } = geometry;
  const w = fieldWidth(field, valueFs, labelFs);
  const cell = field.widest ? w : monoWidth(cells('SemiBold', valueFs), field.chars);
  const valueWidth = valueWidthOf(field, valueFs);
  return {
    id: field.id,
    width: w,
    present: field.present,
    draw: (at) => {
      const items: Item[] = [
        label(`${prefix}${field.id}.label`, field.label.toUpperCase(), at.x, labelTop, w, {
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
          unit(`${prefix}${field.id}.unit`, field.after, at.x + valueWidth + FIELD_GAP, valueTop + (valueFs - ds.size.labelSm), unitWidth(field), {
            size: ds.size.labelSm,
            ...(field.afterBind ? { bind: field.afterBind, widest: field.afterWidest ?? field.after } : {}),
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
  const usable = bandPageRoom(frame, corners);
  if (id === TELLTALE_PAGE) return telltaleItems(frame, prefix, usable);

  const fields = BAND_PAGES[id];
  if (!fields) throw new RangeError(`band D has no page "${id}"`);
  const m = bandMetrics(frame);
  const labelFs = ds.size.label;
  const inline = INLINE_PAGES[id];
  const valueFs = inline ? inlineValueSize(frame.height, m.valueSize) : valueSizeFor(frame.height, m.valueSize, labelFs, LABEL_ROW, FIELD_GAP);

  const geometry = blockGeometry(frame, valueFs, labelFs);
  const members = inline ? inlineMembers(fields, inline, prefix, frame, valueFs, labelFs) : fields.map((field) => bandMember(field, prefix, geometry));

  return rank(members, { left: usable.left, width: usable.width, gap: inline ? inline.gap : m.fieldGap, when: 'close' }).items;
}

/**
 * The room a page has: what the side padding, the zone letter and the corner blocks leave.
 *
 * Centring in the whole band put the last field of D6 Sectors six pixels into the DRS lamp at 1280,
 * where a page and a corner block drew over each other and only a photograph would have shown it.
 * The telltale rank is centred in the same room, which is what puts twelve lamps between the
 * corners rather than across them.
 */
export function bandPageRoom(frame: Rect, corners: boolean): { left: number; width: number } {
  const m = bandMetrics(frame);
  const taken = corners ? bandCornerWidths(frame) : { left: m.padX + letterRoom(frame), right: m.padX };
  const apart = corners ? BAND_GROUP_GAP : 0;
  return { left: frame.left + taken.left + apart, width: Math.max(0, frame.width - taken.left - taken.right - 2 * apart) };
}

/**
 * The largest value whose box fits a band that draws one line rather than two rows.
 *
 * An inline page has no label row above its values, so the whole band is the line's to use and the
 * artboards' size fits with room to spare; this exists so that a band shorter than any drawn still
 * shrinks rather than clips.
 */
function inlineValueSize(height: number, preferred: number): number {
  for (let fs = preferred; fs > 8; fs--) {
    const box = textBox((height - fs) / 2, fs);
    if (box.top >= 0 && box.top + box.height <= height) return fs;
  }
  return 8;
}

/** The page word and then one member per field, each of them a position and a gap on one line. */
function inlineMembers(fields: readonly BandField[], page: InlinePage, prefix: string, frame: Rect, valueFs: number, labelFs: number): RankMember[] {
  const valueTop = frame.top + (frame.height - valueFs) / 2;
  const wordTop = frame.top + (frame.height - labelFs) / 2;
  const wordWidth = Math.ceil(measureText('BarlowMedium', page.word.toUpperCase(), labelFs)) + 2;
  const word: RankMember = {
    id: 'word',
    width: wordWidth,
    draw: (at) => [
      label(`${prefix}word`, page.word.toUpperCase(), at.x, wordTop, wordWidth, { size: labelFs, leftBind: at.leftAt(), visibleBind: at.visibleBind }),
    ],
  };

  return [
    word,
    ...fields.map((field): RankMember => {
      // The position is set in the value's face at the value's size, so it is measured there and
      // not in the label's: a position is proportional rather than cellular, because "P" is wider
      // than any digit and a cell cut for digits would clip it.
      const head = Math.ceil(measureText('BarlowCondensedSemiBold', field.labelWidest ?? field.label, valueFs)) + 2;
      const value = valueWidthOf(field, valueFs);
      return {
        id: field.id,
        width: head + INLINE_GAP + value,
        present: field.present,
        draw: (at) => [
          numeral(`${prefix}${field.id}.position`, field.label, at.x, valueTop, valueFs, field.chars, {
            bind: field.labelBind,
            proportional: true,
            widest: field.labelWidest,
            color: field.labelColor ?? ds.color.text.label,
            leftBind: at.leftAt(),
            visibleBind: at.visibleBind,
          }),
          numeral(`${prefix}${field.id}.value`, field.sample, at.x + head + INLINE_GAP, valueTop, valueFs, field.chars, {
            bind: field.bind,
            color: field.color,
            maxWidth: value + 4,
            leftBind: at.leftAt(head + INLINE_GAP),
            visibleBind: at.visibleBind,
          }),
        ],
      };
    }),
  ];
}

/** Where a block of a label over a value sits in the band, its two rows 5 apart and the label
 *  centred in a row shorter than itself. */
function blockGeometry(frame: Rect, valueFs: number, labelFs: number): BlockGeometry {
  const top = frame.top + blockTop(frame.height, valueFs, labelFs, LABEL_ROW, FIELD_GAP);
  return { valueFs, labelFs, top, labelTop: top + (LABEL_ROW - labelFs) / 2, valueTop: top + LABEL_ROW + FIELD_GAP };
}

interface BlockGeometry {
  valueFs: number;
  labelFs: number;
  /** The top of the block, which is the top of the label's row rather than of the label. */
  top: number;
  labelTop: number;
  valueTop: number;
}

/**
 * The room the zone letter takes at the left end, its gap included.
 *
 * The letter is drawn by the face rather than by the band -- a band dashboard is one file serving
 * one rectangle and knows no letter -- but the room is reserved here, because the corner block and
 * the rank are what would otherwise be drawn over it. Measured at `ds.size.label`, which is the
 * size the face draws it at: a room measured against one size and filled at another is the mistake
 * this file exists to avoid.
 */
const letterRoom = (frame: Rect): number => {
  const m = bandMetrics(frame);
  return Math.ceil(measureText('BarlowMedium', 'D', ds.size.label)) + 2 + m.letterGap;
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
  // The spotter is caution amber and not the flag's yellow, which every sheet that lights it draws:
  // a car beside you is a thing to be careful of rather than a yellow flag.
  // The two properties SimHub actually publishes, rather than iRacing's own enum: every recorded
  // trace has `CarLeftRight` at null, so the old read defaulted to 0, and 0 is not 1, which lit the
  // lamp on every face at all times. The strip reads the same pair, so the band and the lights say
  // one thing about a car alongside.
  {
    id: 'spt',
    text: 'SPT',
    on: ncalc.or(gt(isnull(game('SpotterCarLeft'), num(0)), num(0)), gt(isnull(game('SpotterCarRight'), num(0)), num(0))),
    colour: ds.color.caution.primary,
  },
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
  const labelFs = ds.size.label;
  const valueFs = cornerValueSize(frame);
  const left = leftCornerFields().reduce((sum, f) => sum + fieldWidth(f, valueFs, labelFs), CORNER_GAP);
  const clockWidth = cornerClocks().reduce((sum, f) => sum + fieldWidth(f, valueFs, labelFs), CORNER_GAP);
  const lamps = cornerLamps().length * lampWidth() + LAMP_GAP * (cornerLamps().length - 1);
  return { left: m.padX + letterRoom(frame) + left, right: m.padX + lamps + CORNER_GAP + clockWidth };
}

const cornerValueSize = (frame: Rect): number => valueSizeFor(frame.height, densityOf('zone').small, ds.size.label, LABEL_ROW, FIELD_GAP);

/**
 * The room a lamp takes: the word, the 7 px either side of it, and the chip's own outline.
 *
 * The chip is 20 px tall with a 1 px border, and the border and the word carry one colour between
 * them: a lamp is lit or dim as a whole, and an outline left bright around a dim word would read
 * as a lamp half on.
 */
const LAMP_PAD_X = 7;
const LAMP_HEIGHT = 20;
const LAMP_BORDER = 1;
const lampWidth = (): number => Math.ceil(measureText('BarlowMedium', 'DRS', ds.size.labelSm)) + 2 + 2 * (LAMP_PAD_X + 1);

export function bandCorners(frame: Rect, prefix: string): Item[] {
  const m = bandMetrics(frame);
  const labelFs = ds.size.label;
  const valueFs = cornerValueSize(frame);
  const { top, labelTop, valueTop } = blockGeometry(frame, valueFs, labelFs);
  const blockHeight = LABEL_ROW + FIELD_GAP + valueFs;
  const items: Item[] = [];

  const left = leftCornerFields();
  let x = frame.left + m.padX + letterRoom(frame);
  for (const field of left) {
    const w = fieldWidth(field, valueFs, labelFs);
    items.push(label(`${prefix}${field.id}.label`, field.label.toUpperCase(), x, labelTop, w, { size: labelFs }));
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
  const lamp = lampWidth();

  // Laid from the right edge rather than from a computed start, so that Sim ends against the band's
  // own padding whatever the clocks measure, and each clock is right aligned inside its box for the
  // same reason: a field whose allowance is wider than its digits left them short of the edge.
  let right = frame.left + frame.width - m.padX;
  for (const field of [...clocks].reverse()) {
    const w = fieldWidth(field, valueFs, labelFs);
    right -= w;
    items.push(label(`${prefix}${field.id}.label`, field.label.toUpperCase(), right, labelTop, w, { size: labelFs, hAlign: 'right' }));
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
        draw: (at) => {
          const lit = dimUnless(at.litBind, each.colour);
          const chip = { left: at.x, top: top + (blockHeight - LAMP_HEIGHT) / 2, width: lamp, height: LAMP_HEIGHT };
          return [
            band(`${prefix}${each.id}.chip`, chip, TRANSPARENT, { border: { color: ds.color.text.dim, width: LAMP_BORDER, colorBind: lit } }),
            label(`${prefix}${each.id}`, each.text, at.x, top + (blockHeight - ds.size.labelSm) / 2, lamp, {
              size: ds.size.labelSm,
              hAlign: 'center',
              color: ds.color.text.dim,
              colorBind: lit,
            }),
          ];
        },
      })),
      { left: x, width: lamps.length * lamp + LAMP_GAP * (lamps.length - 1), gap: LAMP_GAP, when: 'dim', align: 'left' },
    ).items,
  );
  return items;
}

/** Every page id band D can show, in cycle order, for a test that wants to walk them. */
export const BAND_PAGE_IDS: readonly string[] = [...Object.keys(BAND_PAGES), TELLTALE_PAGE];
